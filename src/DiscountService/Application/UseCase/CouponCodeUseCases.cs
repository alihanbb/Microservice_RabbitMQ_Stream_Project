using DiscountService.API.Contracts;
using DiscountService.Application.Response;
using DiscountService.Domain.Entities;
using DiscountService.Domain.Exceptions;
using DiscountService.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace DiscountService.Application.UseCase;

public interface ICouponCodeUseCases
{
    Task<IEnumerable<CouponCodeResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<CouponCodeResponse>> GetValidAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<CouponCodeResponse>> GetExpiredAsync(CancellationToken cancellationToken = default);
    Task<Result<CouponCodeResponse>> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<Result<CouponCodeResponse>> CreateAsync(CreateCouponRequest request, CancellationToken cancellationToken = default);
    Task<Result<ValidateCouponResponse>> ValidateAsync(ValidateCouponRequest request, CancellationToken cancellationToken = default);
    Task<Result<CouponCodeResponse>> UseAsync(string code, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(string code, CancellationToken cancellationToken = default);
}

public class CouponCodeUseCases(
    ICouponCodeRepository couponRepository,
    ILogger<CouponCodeUseCases> logger) : ICouponCodeUseCases
{
    public async Task<IEnumerable<CouponCodeResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var coupons = await couponRepository.GetAllAsync(cancellationToken);
        return coupons.Select(MapToResponse);
    }

    public async Task<IEnumerable<CouponCodeResponse>> GetValidAsync(CancellationToken cancellationToken = default)
    {
        var coupons = await couponRepository.GetValidCouponsAsync(cancellationToken);
        return coupons.Select(MapToResponse);
    }

    public async Task<IEnumerable<CouponCodeResponse>> GetExpiredAsync(CancellationToken cancellationToken = default)
    {
        var coupons = await couponRepository.GetExpiredCouponsAsync(cancellationToken);
        return coupons.Select(MapToResponse);
    }

    public async Task<Result<CouponCodeResponse>> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var coupon = await couponRepository.GetByCodeAsync(code.ToUpperInvariant(), cancellationToken);
        if (coupon == null)
            return Result<CouponCodeResponse>.Failure($"Coupon code '{code}' not found");

        return Result<CouponCodeResponse>.Success(MapToResponse(coupon));
    }

    public async Task<Result<CouponCodeResponse>> CreateAsync(CreateCouponRequest request, CancellationToken cancellationToken = default)
    {
        if (await couponRepository.ExistsAsync(request.Code.ToUpperInvariant(), cancellationToken))
            return Result<CouponCodeResponse>.Failure($"Coupon code '{request.Code}' already exists");

        try
        {
            var coupon = CouponCode.Create(
                request.Code,
                request.Description,
                request.ExpiresAt,
                request.MaxUsageCount);

            await couponRepository.SaveAsync(coupon, cancellationToken);
            logger.LogInformation("Created coupon code {Code}", coupon.Code);
            
            return Result<CouponCodeResponse>.Success(MapToResponse(coupon));
        }
        catch (DomainException ex)
        {
            return Result<CouponCodeResponse>.Failure(ex.Message);
        }
    }

    public async Task<Result<ValidateCouponResponse>> ValidateAsync(ValidateCouponRequest request, CancellationToken cancellationToken = default)
    {
        var coupon = await couponRepository.GetByCodeAsync(request.Code.ToUpperInvariant(), cancellationToken);
        if (coupon == null)
            return Result<ValidateCouponResponse>.Failure($"Coupon code '{request.Code}' not found");

        if (!coupon.IsValid())
        {
            var reason = coupon.IsExpired() ? "Coupon has expired" :
                         coupon.IsUsed ? "Coupon usage limit reached" :
                         "Coupon has no active discount rules";

            return Result<ValidateCouponResponse>.Success(new ValidateCouponResponse(
                IsValid: false,
                Code: coupon.Code,
                OriginalAmount: request.Amount,
                DiscountAmount: 0,
                FinalAmount: request.Amount,
                Message: reason));
        }

        var discount = coupon.CalculateTotalDiscount(request.Amount);
        return Result<ValidateCouponResponse>.Success(new ValidateCouponResponse(
            IsValid: true,
            Code: coupon.Code,
            OriginalAmount: request.Amount,
            DiscountAmount: discount,
            FinalAmount: request.Amount - discount,
            Message: $"Coupon valid. {coupon.DiscountRules.Count(r => r.IsActive)} active rules applied."));
    }

    public async Task<Result<CouponCodeResponse>> UseAsync(string code, CancellationToken cancellationToken = default)
    {
        var coupon = await couponRepository.GetByCodeAsync(code.ToUpperInvariant(), cancellationToken);
        if (coupon == null)
            return Result<CouponCodeResponse>.Failure($"Coupon code '{code}' not found");

        try
        {
            coupon.Use();
            await couponRepository.SaveAsync(coupon, cancellationToken);
            logger.LogInformation("Coupon {Code} used. Usage: {Current}/{Max}", coupon.Code, coupon.CurrentUsageCount, coupon.MaxUsageCount);
            
            return Result<CouponCodeResponse>.Success(MapToResponse(coupon));
        }
        catch (DomainException ex)
        {
            return Result<CouponCodeResponse>.Failure(ex.Message);
        }
    }

    public async Task<Result> DeleteAsync(string code, CancellationToken cancellationToken = default)
    {
        var coupon = await couponRepository.GetByCodeAsync(code.ToUpperInvariant(), cancellationToken);
        if (coupon == null)
            return Result.Failure($"Coupon code '{code}' not found");

        await couponRepository.DeleteAsync(coupon.Id, cancellationToken);
        logger.LogInformation("Deleted coupon code {Code}", code);
        return Result.Success();
    }

    private static CouponCodeResponse MapToResponse(CouponCode coupon) => new(
        Id: coupon.Id,
        Code: coupon.Code,
        Description: coupon.Description,
        IsUsed: coupon.IsUsed,
        UsedAt: coupon.UsedAt,
        ExpiresAt: coupon.ExpiresAt,
        MaxUsageCount: coupon.MaxUsageCount,
        CurrentUsageCount: coupon.CurrentUsageCount,
        IsExpired: coupon.IsExpired(),
        IsValid: coupon.IsValid(),
        CreatedDate: coupon.CreatedDate,
        UpdatedDate: coupon.UpdatedDate,
        DiscountRules: coupon.DiscountRules.Select(r => new DiscountRuleResponse(
            Id: r.Id,
            Name: r.Name,
            Description: r.Description,
            DiscountPercentage: r.DiscountPercentage,
            MinDiscountAmount: r.MinDiscountAmount,
            IsActive: r.IsActive,
            Priority: r.Priority.ToString(),
            CreatedDate: r.CreatedDate,
            UpdatedDate: r.UpdatedDate)));
}
