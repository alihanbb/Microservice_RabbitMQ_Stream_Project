namespace DiscountService.Domain.Entities;

public abstract class BaseEntity
{
    public Guid Id { get; set; } 
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedDate { get; set; }   
}
