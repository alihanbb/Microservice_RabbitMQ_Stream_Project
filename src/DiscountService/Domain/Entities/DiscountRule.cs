namespace DiscountService.Domain.Entities;

public class DiscountRule : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public decimal DiscountPercentage { get; private set; }
    public decimal MinDiscountAmount { get; private set; }
    public bool IsActive { get; private set; }
    public Priority Priority { get; private set; }

    private DiscountRule() { }

    public static DiscountRule Create(
        string name,
        string description,
        decimal discountPercentage,
        decimal minDiscountAmount,
        Priority priority)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Name cannot be empty", nameof(name));

        if (discountPercentage <= 0 || discountPercentage > 100)
            throw new DomainException("Discount percentage must be between 0 and 100", nameof(discountPercentage));

        if (minDiscountAmount < 0)
            throw new DomainException("Minimum discount amount cannot be negative", nameof(minDiscountAmount));

        return new DiscountRule
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description ?? string.Empty,
            DiscountPercentage = discountPercentage,
            MinDiscountAmount = minDiscountAmount,
            IsActive = true,
            Priority = priority,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };
    }

    public static DiscountRule Load(
        Guid id,
        string name,
        string description,
        decimal discountPercentage,
        decimal minDiscountAmount,
        bool isActive,
        Priority priority,
        DateTime createdDate,
        DateTime updatedDate)
    {
        return new DiscountRule
        {
            Id = id,
            Name = name,
            Description = description,
            DiscountPercentage = discountPercentage,
            MinDiscountAmount = minDiscountAmount,
            IsActive = isActive,
            Priority = priority,
            CreatedDate = createdDate,
            UpdatedDate = updatedDate
        };
    }

    public void Update(string name, string description, decimal discountPercentage, decimal minDiscountAmount, Priority priority)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Name cannot be empty", nameof(name));

        if (discountPercentage <= 0 || discountPercentage > 100)
            throw new DomainException("Discount percentage must be between 0 and 100", nameof(discountPercentage));

        Name = name;
        Description = description ?? string.Empty;
        DiscountPercentage = discountPercentage;
        MinDiscountAmount = minDiscountAmount;
        Priority = priority;
        UpdatedDate = DateTime.UtcNow;
    }

    public void Activate()
    {
        if (IsActive)
            throw new DomainException("Discount rule is already active");

        IsActive = true;
        UpdatedDate = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        if (!IsActive)
            throw new DomainException("Discount rule is already inactive");

        IsActive = false;
        UpdatedDate = DateTime.UtcNow;
    }

    public decimal CalculateDiscount(decimal amount)
    {
        if (!IsActive)
            return 0;

        if (amount < MinDiscountAmount)
            return 0;

        return amount * (DiscountPercentage / 100);
    }
}
