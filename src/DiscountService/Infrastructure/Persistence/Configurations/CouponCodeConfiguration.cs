using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscountService.Infrastructure.Persistence.Configurations;

public class CouponCodeConfiguration : IEntityTypeConfiguration<CouponCode>
{
    public void Configure(EntityTypeBuilder<CouponCode> builder)
    {
        builder.ToTable("CouponCodes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.Property(x => x.IsUsed)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.UsedAt);

        builder.Property(x => x.ExpiresAt);

        builder.Property(x => x.MaxUsageCount)
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(x => x.CurrentUsageCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.CreatedDate)
            .IsRequired();

        builder.Property(x => x.UpdatedDate)
            .IsRequired();

        // Indexes
        builder.HasIndex(x => x.Code)
            .IsUnique();

        builder.HasIndex(x => x.IsUsed);
        builder.HasIndex(x => x.ExpiresAt);

        // Relationships - Many-to-Many with DiscountRules
        builder.HasMany(x => x.DiscountRules)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "CouponDiscountRules",
                j => j.HasOne<DiscountRule>()
                    .WithMany()
                    .HasForeignKey("DiscountRuleId")
                    .OnDelete(DeleteBehavior.Cascade),
                j => j.HasOne<CouponCode>()
                    .WithMany()
                    .HasForeignKey("CouponCodeId")
                    .OnDelete(DeleteBehavior.Cascade),
                j =>
                {
                    j.HasKey("CouponCodeId", "DiscountRuleId");
                    j.ToTable("CouponDiscountRules");
                });

        // Configure backing field for DiscountRules collection
        builder.Navigation(x => x.DiscountRules)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
