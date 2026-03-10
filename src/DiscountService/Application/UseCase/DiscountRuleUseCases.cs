using DiscountService.API.Contracts;
using DiscountService.Application.Response;
using DiscountService.Domain.Entities;
using DiscountService.Domain.Exceptions;
using DiscountService.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace DiscountService.Application.UseCase;

public interface IDiscountRuleUseCases
{
    Task<IEnumerable<DiscountRuleResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<DiscountRuleResponse>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<Result<IEnumerable<DiscountRuleResponse>>> GetByPriorityAsync(string priority, CancellationToken cancellationToken = default);
    Task<Result<DiscountRuleResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<DiscountRuleResponse>> CreateAsync(CreateDiscountRuleRequest request, CancellationToken cancellationToken = default);
    Task<Result<DiscountRuleResponse>> UpdateAsync(Guid id, UpdateDiscountRuleRequest request, CancellationToken cancellationToken = default);
    Task<Result<DiscountRuleResponse>> ActivateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<DiscountRuleResponse>> DeactivateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public class DiscountRuleUseCases(
    IDiscountRuleRepository ruleRepository,
    ILogger<DiscountRuleUseCases> logger) : IDiscountRuleUseCases
{
    public async Task<IEnumerable<DiscountRuleResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var rules = await ruleRepository.GetAllAsync(cancellationToken);
        return rules.Select(MapToResponse);
    }

    public async Task<IEnumerable<DiscountRuleResponse>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var rules = await ruleRepository.GetActiveRulesAsync(cancellationToken);
        return rules.Select(MapToResponse);
    }

    public async Task<Result<IEnumerable<DiscountRuleResponse>>> GetByPriorityAsync(string priority, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<Priority>(priority, ignoreCase: true, out var parsedPriority))
            return Result<IEnumerable<DiscountRuleResponse>>.Failure($"Invalid priority value. Valid values: {string.Join(", ", Enum.GetNames<Priority>())}");

        var rules = await ruleRepository.GetByPriorityAsync(parsedPriority, cancellationToken);
        return Result<IEnumerable<DiscountRuleResponse>>.Success(rules.Select(MapToResponse));
    }

    public async Task<Result<DiscountRuleResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rule = await ruleRepository.GetByIdAsync(id, cancellationToken);
        if (rule == null)
            return Result<DiscountRuleResponse>.Failure($"Discount rule with ID {id} not found");

        return Result<DiscountRuleResponse>.Success(MapToResponse(rule));
    }

    public async Task<Result<DiscountRuleResponse>> CreateAsync(CreateDiscountRuleRequest request, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<Priority>(request.Priority, ignoreCase: true, out var priority))
            return Result<DiscountRuleResponse>.Failure($"Invalid priority value. Valid values: {string.Join(", ", Enum.GetNames<Priority>())}");

        try
        {
            var rule = DiscountRule.Create(
                request.Name,
                request.Description,
                request.DiscountPercentage,
                request.MinDiscountAmount,
                priority);

            await ruleRepository.SaveAsync(rule, cancellationToken);
            logger.LogInformation("Created discount rule {Name} with {Percentage}% discount", rule.Name, rule.DiscountPercentage);
            
            return Result<DiscountRuleResponse>.Success(MapToResponse(rule));
        }
        catch (DomainException ex)
        {
            return Result<DiscountRuleResponse>.Failure(ex.Message);
        }
    }

    public async Task<Result<DiscountRuleResponse>> UpdateAsync(Guid id, UpdateDiscountRuleRequest request, CancellationToken cancellationToken = default)
    {
        var rule = await ruleRepository.GetByIdAsync(id, cancellationToken);
        if (rule == null)
            return Result<DiscountRuleResponse>.Failure($"Discount rule with ID {id} not found");

        if (!Enum.TryParse<Priority>(request.Priority, ignoreCase: true, out var priority))
            return Result<DiscountRuleResponse>.Failure($"Invalid priority value. Valid values: {string.Join(", ", Enum.GetNames<Priority>())}");

        try
        {
            rule.Update(
                request.Name,
                request.Description,
                request.DiscountPercentage,
                request.MinDiscountAmount,
                priority);

            await ruleRepository.SaveAsync(rule, cancellationToken);
            logger.LogInformation("Updated discount rule {Id} - {Name}", id, rule.Name);
            
            return Result<DiscountRuleResponse>.Success(MapToResponse(rule));
        }
        catch (DomainException ex)
        {
            return Result<DiscountRuleResponse>.Failure(ex.Message);
        }
    }

    public async Task<Result<DiscountRuleResponse>> ActivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rule = await ruleRepository.GetByIdAsync(id, cancellationToken);
        if (rule == null)
            return Result<DiscountRuleResponse>.Failure($"Discount rule with ID {id} not found");

        try
        {
            rule.Activate();
            await ruleRepository.SaveAsync(rule, cancellationToken);
            logger.LogInformation("Activated discount rule {Id} - {Name}", id, rule.Name);
            
            return Result<DiscountRuleResponse>.Success(MapToResponse(rule));
        }
        catch (DomainException ex)
        {
            return Result<DiscountRuleResponse>.Failure(ex.Message);
        }
    }

    public async Task<Result<DiscountRuleResponse>> DeactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rule = await ruleRepository.GetByIdAsync(id, cancellationToken);
        if (rule == null)
            return Result<DiscountRuleResponse>.Failure($"Discount rule with ID {id} not found");

        try
        {
            rule.Deactivate();
            await ruleRepository.SaveAsync(rule, cancellationToken);
            logger.LogInformation("Deactivated discount rule {Id} - {Name}", id, rule.Name);
            
            return Result<DiscountRuleResponse>.Success(MapToResponse(rule));
        }
        catch (DomainException ex)
        {
            return Result<DiscountRuleResponse>.Failure(ex.Message);
        }
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await ruleRepository.ExistsAsync(id, cancellationToken))
            return Result.Failure($"Discount rule with ID {id} not found");

        await ruleRepository.DeleteAsync(id, cancellationToken);
        logger.LogInformation("Deleted discount rule {Id}", id);
        return Result.Success();
    }

    private static DiscountRuleResponse MapToResponse(DiscountRule rule) => new(
        Id: rule.Id,
        Name: rule.Name,
        Description: rule.Description,
        DiscountPercentage: rule.DiscountPercentage,
        MinDiscountAmount: rule.MinDiscountAmount,
        IsActive: rule.IsActive,
        Priority: rule.Priority.ToString(),
        CreatedDate: rule.CreatedDate,
        UpdatedDate: rule.UpdatedDate);
}
