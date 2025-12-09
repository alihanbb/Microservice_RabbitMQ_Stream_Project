
namespace DiscountService.Infrastructure.Repositories;

public sealed class CouponCodeRepository(DiscountDbContext context) : ICouponCodeRepository
{
    public async Task<CouponCode?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await context.CouponCodes
            .Include(c => c.DiscountRules)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<CouponCode?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        => await context.CouponCodes
            .Include(c => c.DiscountRules)
            .FirstOrDefaultAsync(c => c.Code == code.ToUpperInvariant(), cancellationToken);

    public async Task<IEnumerable<CouponCode>> GetAllAsync(CancellationToken cancellationToken = default)
        => await context.CouponCodes
            .Include(c => c.DiscountRules)
            .OrderByDescending(c => c.CreatedDate)
            .ToListAsync(cancellationToken);

    public async Task<IEnumerable<CouponCode>> GetValidCouponsAsync(CancellationToken cancellationToken = default)
        => await context.CouponCodes
            .Include(c => c.DiscountRules)
            .Where(c => !c.IsUsed && (c.ExpiresAt == null || c.ExpiresAt > DateTime.UtcNow))
            .OrderByDescending(c => c.CreatedDate)
            .ToListAsync(cancellationToken);

    public async Task<IEnumerable<CouponCode>> GetExpiredCouponsAsync(CancellationToken cancellationToken = default)
        => await context.CouponCodes
            .Include(c => c.DiscountRules)
            .Where(c => c.ExpiresAt != null && c.ExpiresAt <= DateTime.UtcNow)
            .ToListAsync(cancellationToken);

    public async Task SaveAsync(CouponCode coupon, CancellationToken cancellationToken = default)
    {
        var exists = await context.CouponCodes.AnyAsync(c => c.Id == coupon.Id, cancellationToken);

        if (exists)
            context.CouponCodes.Update(coupon);
        else
            await context.CouponCodes.AddAsync(coupon, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var coupon = await context.CouponCodes.FindAsync([id], cancellationToken);

        if (coupon is not null)
        {
            context.CouponCodes.Remove(coupon);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> ExistsAsync(string code, CancellationToken cancellationToken = default)
        => await context.CouponCodes.AnyAsync(c => c.Code == code.ToUpperInvariant(), cancellationToken);
}
