

namespace DiscountService.Domain.Repositories;

public interface ICouponCodeRepository
{
    Task<CouponCode?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CouponCode?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IEnumerable<CouponCode>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<CouponCode>> GetValidCouponsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<CouponCode>> GetExpiredCouponsAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(CouponCode coupon, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string code, CancellationToken cancellationToken = default);
}
