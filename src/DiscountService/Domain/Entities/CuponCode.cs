namespace DiscountService.Domain.Entities;

public class CouponCode : BaseEntity
{
    public string Code { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public bool IsUsed { get; private set; }
    public DateTime? UsedAt { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public int MaxUsageCount { get; private set; }
    public int CurrentUsageCount { get; private set; }

    private List<DiscountRule> _discountRules = [];
    public IReadOnlyCollection<DiscountRule> DiscountRules => _discountRules.AsReadOnly();

    private CouponCode() { }

    public static CouponCode Create(
        string code,
        string description,
        DateTime? expiresAt = null,
        int maxUsageCount = 1)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainException("Code cannot be empty", nameof(code));

        if (maxUsageCount <= 0)
            throw new DomainException("Max usage count must be greater than 0", nameof(maxUsageCount));

        if (expiresAt.HasValue && expiresAt.Value <= DateTime.UtcNow)
            throw new DomainException("Expiration date must be in the future", nameof(expiresAt));

        return new CouponCode
        {
            Id = Guid.NewGuid(),
            Code = code.ToUpperInvariant().Trim(),
            Description = description ?? string.Empty,
            IsUsed = false,
            UsedAt = null,
            ExpiresAt = expiresAt,
            MaxUsageCount = maxUsageCount,
            CurrentUsageCount = 0,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };
    }

    public static CouponCode Load(
        Guid id,
        string code,
        string description,
        bool isUsed,
        DateTime? usedAt,
        DateTime? expiresAt,
        int maxUsageCount,
        int currentUsageCount,
        DateTime createdDate,
        DateTime updatedDate,
        List<DiscountRule>? discountRules = null)
    {
        var coupon = new CouponCode
        {
            Id = id,
            Code = code,
            Description = description,
            IsUsed = isUsed,
            UsedAt = usedAt,
            ExpiresAt = expiresAt,
            MaxUsageCount = maxUsageCount,
            CurrentUsageCount = currentUsageCount,
            CreatedDate = createdDate,
            UpdatedDate = updatedDate
        };

        if (discountRules != null)
            coupon._discountRules.AddRange(discountRules);

        return coupon;
    }

    public void AddDiscountRule(DiscountRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        if (_discountRules.Any(r => r.Id == rule.Id))
            throw new DomainException("Discount rule already exists in this coupon");

        _discountRules.Add(rule);
        UpdatedDate = DateTime.UtcNow;
    }

    public void RemoveDiscountRule(Guid ruleId)
    {
        var rule = _discountRules.FirstOrDefault(r => r.Id == ruleId)
            ?? throw new DomainException($"Discount rule with Id {ruleId} not found");

        _discountRules.Remove(rule);
        UpdatedDate = DateTime.UtcNow;
    }

    public void Use()
    {
        if (IsUsed)
            throw new DomainException("Coupon has already been fully used");

        if (IsExpired())
            throw new DomainException("Coupon has expired");

        CurrentUsageCount++;

        if (CurrentUsageCount >= MaxUsageCount)
        {
            IsUsed = true;
            UsedAt = DateTime.UtcNow;
        }

        UpdatedDate = DateTime.UtcNow;
    }

    public void Reset()
    {
        IsUsed = false;
        UsedAt = null;
        CurrentUsageCount = 0;
        UpdatedDate = DateTime.UtcNow;
    }

    public bool IsExpired() => ExpiresAt.HasValue && ExpiresAt.Value <= DateTime.UtcNow;

    public bool IsValid() => !IsUsed && !IsExpired() && _discountRules.Any(r => r.IsActive);

    public decimal CalculateTotalDiscount(decimal amount)
    {
        if (!IsValid())
            return 0;

        return _discountRules
            .Where(r => r.IsActive)
            .OrderByDescending(r => r.Priority)
            .Sum(r => r.CalculateDiscount(amount));
    }
}
