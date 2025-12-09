namespace DiscountService.Infrastructure.Repositories;

public sealed class DiscountRuleRepository(DiscountDbContext context) : IDiscountRuleRepository
{
    public async Task<DiscountRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await context.DiscountRules.FindAsync([id], cancellationToken);

    public async Task<IEnumerable<DiscountRule>> GetAllAsync(CancellationToken cancellationToken = default)
        => await context.DiscountRules
            .OrderByDescending(r => r.Priority)
            .ToListAsync(cancellationToken);

    public async Task<IEnumerable<DiscountRule>> GetActiveRulesAsync(CancellationToken cancellationToken = default)
        => await context.DiscountRules
            .Where(r => r.IsActive)
            .OrderByDescending(r => r.Priority)
            .ToListAsync(cancellationToken);

    public async Task<IEnumerable<DiscountRule>> GetByPriorityAsync(Priority priority, CancellationToken cancellationToken = default)
        => await context.DiscountRules
            .Where(r => r.Priority == priority)
            .ToListAsync(cancellationToken);

    public async Task SaveAsync(DiscountRule rule, CancellationToken cancellationToken = default)
    {
        var exists = await context.DiscountRules.AnyAsync(r => r.Id == rule.Id, cancellationToken);

        if (exists)
            context.DiscountRules.Update(rule);
        else
            await context.DiscountRules.AddAsync(rule, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rule = await context.DiscountRules.FindAsync([id], cancellationToken);
        
        if (rule is not null)
        {
            context.DiscountRules.Remove(rule);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
        => await context.DiscountRules.AnyAsync(r => r.Id == id, cancellationToken);
}
