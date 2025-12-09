namespace DiscountService.Infrastructure.Seed;

public class DiscountRuleSeedData(IDiscountRuleRepository repository)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        foreach (var rule in GetSampleRules())
        {
            if (!await repository.ExistsAsync(rule.Id, cancellationToken))
            {
                await repository.SaveAsync(rule, cancellationToken);
            }
        }
    }

    private static List<DiscountRule> GetSampleRules() =>
    [
        // Seasonal Discounts
        CreateRule("SUMMER-SALE", "Summer Season 15% Discount", 15, 50, Priority.Medium),
        CreateRule("WINTER-SALE", "Winter Season 20% Discount", 20, 75, Priority.Medium),
        CreateRule("SPRING-SALE", "Spring Collection 12% Off", 12, 40, Priority.Low),
        CreateRule("AUTUMN-SALE", "Autumn Special 18% Discount", 18, 60, Priority.Medium),

        // Holiday Discounts
        CreateRule("BLACK-FRIDAY", "Black Friday 30% Off Everything", 30, 100, Priority.Critical),
        CreateRule("CYBER-MONDAY", "Cyber Monday Tech Deals 25%", 25, 150, Priority.Critical),
        CreateRule("CHRISTMAS-DEAL", "Christmas Special 22% Off", 22, 80, Priority.High),
        CreateRule("NEW-YEAR", "New Year Celebration 20% Off", 20, 50, Priority.High),
        CreateRule("VALENTINES", "Valentine's Day 14% Off", 14, 30, Priority.Medium),
        CreateRule("EASTER-SPECIAL", "Easter Holiday 16% Discount", 16, 45, Priority.Medium),

        // Membership Discounts
        CreateRule("VIP-MEMBER", "VIP Member Exclusive 25% Off", 25, 0, Priority.High),
        CreateRule("GOLD-MEMBER", "Gold Member 20% Discount", 20, 0, Priority.High),
        CreateRule("SILVER-MEMBER", "Silver Member 15% Discount", 15, 0, Priority.Medium),
        CreateRule("BRONZE-MEMBER", "Bronze Member 10% Discount", 10, 0, Priority.Low),
        CreateRule("NEW-MEMBER", "New Member Welcome 12% Off", 12, 25, Priority.Medium),

        // Category Discounts
        CreateRule("ELECTRONICS-DEAL", "Electronics Category 10% Off", 10, 200, Priority.Medium),
        CreateRule("FASHION-WEEK", "Fashion Week 35% Discount", 35, 100, Priority.High),
        CreateRule("HOME-GARDEN", "Home & Garden 18% Off", 18, 150, Priority.Medium),
        CreateRule("SPORTS-FITNESS", "Sports & Fitness 20% Off", 20, 75, Priority.Medium),
        CreateRule("BOOKS-MEDIA", "Books & Media 15% Discount", 15, 30, Priority.Low),
        CreateRule("BEAUTY-CARE", "Beauty & Care 22% Off", 22, 50, Priority.Medium),
        CreateRule("TOYS-GAMES", "Toys & Games 25% Discount", 25, 40, Priority.Medium),

        // Cart Value Discounts
        CreateRule("CART-500", "Orders over $500 get 8% Off", 8, 500, Priority.Low),
        CreateRule("CART-1000", "Orders over $1000 get 12% Off", 12, 1000, Priority.Medium),
        CreateRule("CART-2500", "Orders over $2500 get 18% Off", 18, 2500, Priority.High),
        CreateRule("CART-5000", "Orders over $5000 get 25% Off", 25, 5000, Priority.Critical),

        // Flash Sale Discounts
        CreateRule("FLASH-SALE-1H", "1-Hour Flash Sale 40% Off", 40, 50, Priority.Critical),
        CreateRule("FLASH-SALE-3H", "3-Hour Flash Sale 30% Off", 30, 40, Priority.High),
        CreateRule("DAILY-DEAL", "Daily Deal 15% Discount", 15, 25, Priority.Medium),
        CreateRule("WEEKEND-SPECIAL", "Weekend Special 20% Off", 20, 60, Priority.High),

        // Loyalty Discounts
        CreateRule("LOYALTY-1YEAR", "1 Year Customer 10% Off", 10, 0, Priority.Low),
        CreateRule("LOYALTY-3YEAR", "3 Year Customer 15% Off", 15, 0, Priority.Medium),
        CreateRule("LOYALTY-5YEAR", "5 Year Customer 20% Off", 20, 0, Priority.High),

        // First Purchase Discounts
        CreateRule("FIRST-ORDER", "First Order 20% Discount", 20, 30, Priority.High),
        CreateRule("FIRST-APP", "First App Purchase 25% Off", 25, 20, Priority.High),

        // Referral Discounts
        CreateRule("REFERRAL-BONUS", "Referral Bonus 15% Off", 15, 0, Priority.Medium),
        CreateRule("FRIEND-INVITE", "Friend Invite 12% Discount", 12, 25, Priority.Medium),

        // Special Events
        CreateRule("ANNIVERSARY", "Store Anniversary 30% Off", 30, 100, Priority.Critical),
        CreateRule("CLEARANCE", "Clearance Sale 50% Off", 50, 0, Priority.Critical),
        CreateRule("STUDENT-DISC", "Student Discount 15% Off", 15, 0, Priority.Medium),
        CreateRule("SENIOR-DISC", "Senior Citizen 12% Off", 12, 0, Priority.Medium),
        CreateRule("MILITARY-DISC", "Military Appreciation 20% Off", 20, 0, Priority.High)
    ];

    private static DiscountRule CreateRule(string name, string description, decimal percentage, decimal minAmount, Priority priority)
        => DiscountRule.Create(name, description, percentage, minAmount, priority);
}
