using DiscountService.API.Contracts;
using DiscountService.Application.Response;
using DiscountService.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace DiscountService.Application.UseCase;

public interface ICalculateDiscountUseCase
{
    Task<Result<(decimal Discount, IEnumerable<AppliedRuleInfo> AppliedRules)>> ExecuteAsync(
        string couponCode,
        decimal cartTotal,
        CancellationToken cancellationToken = default);
}

public class CalculateDiscountUseCase(
    ICouponCodeRepository couponRepository,
    ILogger<CalculateDiscountUseCase> logger) : ICalculateDiscountUseCase
{
    public async Task<Result<(decimal Discount, IEnumerable<AppliedRuleInfo> AppliedRules)>> ExecuteAsync(
        string couponCode,
        decimal cartTotal,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(couponCode))
            return Result<(decimal, IEnumerable<AppliedRuleInfo>)>.Failure("Coupon code is required");

        if (cartTotal <= 0)
            return Result<(decimal, IEnumerable<AppliedRuleInfo>)>.Failure("Cart total must be positive");

        var coupon = await couponRepository.GetByCodeAsync(couponCode.ToUpperInvariant(), cancellationToken);
        if (coupon == null)
            return Result<(decimal, IEnumerable<AppliedRuleInfo>)>.Failure($"Coupon code '{couponCode}' not found");

        if (!coupon.IsValid())
        {
            var reason = coupon.IsExpired() ? "Coupon has expired" :
                         coupon.IsUsed ? "Coupon usage limit reached" :
                         "Coupon has no active discount rules";

            logger.LogWarning("Invalid coupon {Code} used: {Reason}", couponCode, reason);
            return Result<(decimal, IEnumerable<AppliedRuleInfo>)>.Failure(reason);
        }

        var appliedRules = new List<AppliedRuleInfo>();
        decimal totalDiscount = 0;

        var activeRules = coupon.DiscountRules
            .Where(r => r.IsActive)
            .OrderByDescending(r => r.Priority);

        foreach (var rule in activeRules)
        {
            var ruleDiscount = rule.CalculateDiscount(cartTotal);
            if (ruleDiscount > 0)
            {
                totalDiscount += ruleDiscount;
                appliedRules.Add(new AppliedRuleInfo(
                    RuleName: rule.Name,
                    DiscountPercentage: rule.DiscountPercentage,
                    DiscountAmount: ruleDiscount,
                    Priority: rule.Priority.ToString()));
            }
        }

        if (totalDiscount > cartTotal)
            totalDiscount = cartTotal;

        logger.LogInformation("Calculated discount for coupon {Code}: {Discount:C} from {CartTotal:C} ({RuleCount} rules applied)",
            couponCode, totalDiscount, cartTotal, appliedRules.Count);

        return Result<(decimal, IEnumerable<AppliedRuleInfo>)>.Success((totalDiscount, appliedRules));
    }
}
