using DiscountService.Domain.Entities;

namespace DiscountService.Domain.Repositories;

public interface IDiscountRuleRepository
{
    Task<DiscountRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<DiscountRule>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<DiscountRule>> GetActiveRulesAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<DiscountRule>> GetByPriorityAsync(Priority priority, CancellationToken cancellationToken = default);
    Task SaveAsync(DiscountRule rule, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}
