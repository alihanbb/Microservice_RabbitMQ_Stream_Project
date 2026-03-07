using DiscountService.Domain.Entities;
using DiscountService.Domain.Repositories;

namespace DiscountService.Infrastructure.Seed;

public class CouponCodeSeedData(ICouponCodeRepository couponRepository, IDiscountRuleRepository ruleRepository)
{
    private static readonly object RandomLock = new();

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var rules = (await ruleRepository.GetAllAsync(cancellationToken)).ToList();
        
        if (rules.Count == 0) return;

        foreach (var couponData in GetSampleCoupons())
        {
            if (await couponRepository.ExistsAsync(couponData.Code, cancellationToken))
                continue;

            var coupon = CouponCode.Create(
                couponData.Code,
                couponData.Description,
                couponData.ExpiresAt,
                couponData.MaxUsageCount);

            // Assign random rules to coupons
            var assignedRules = GetRandomRules(rules, couponData.RuleCount);
            foreach (var rule in assignedRules)
            {
                coupon.AddDiscountRule(rule);
            }

            await couponRepository.SaveAsync(coupon, cancellationToken);
        }
    }

    private static List<DiscountRule> GetRandomRules(List<DiscountRule> allRules, int count)
    {
        lock (RandomLock)
        {
            return allRules.OrderBy(_ => Random.Shared.Next()).Take(Math.Min(count, allRules.Count)).ToList();
        }
    }

    private static List<CouponData> GetSampleCoupons() =>
    [
        // Seasonal Coupons
        new("SUMMER2024", "Summer 2024 Exclusive Coupon", DateTime.UtcNow.AddMonths(3), 1000, 2),
        new("WINTER2024", "Winter 2024 Special Offer", DateTime.UtcNow.AddMonths(6), 500, 2),
        new("SPRING2025", "Spring 2025 Collection", DateTime.UtcNow.AddMonths(9), 750, 1),
        new("FALL2024", "Fall 2024 Promotion", DateTime.UtcNow.AddMonths(2), 600, 2),

        // Holiday Coupons
        new("BLACKFRI24", "Black Friday 2024 Mega Sale", DateTime.UtcNow.AddDays(30), 5000, 3),
        new("CYBERMON24", "Cyber Monday 2024 Tech Deals", DateTime.UtcNow.AddDays(30), 3000, 2),
        new("XMAS2024", "Christmas 2024 Gift Coupon", DateTime.UtcNow.AddMonths(2), 2000, 2),
        new("NEWYEAR25", "New Year 2025 Celebration", DateTime.UtcNow.AddMonths(3), 1500, 2),
        new("VALENTINE25", "Valentine's Day 2025", DateTime.UtcNow.AddMonths(4), 800, 1),
        new("EASTER2025", "Easter 2025 Special", DateTime.UtcNow.AddMonths(5), 600, 1),

        // VIP & Membership Coupons
        new("VIP-ELITE", "VIP Elite Member Exclusive", DateTime.UtcNow.AddYears(1), 100, 3),
        new("VIP-GOLD", "Gold Membership Reward", DateTime.UtcNow.AddMonths(6), 200, 2),
        new("VIP-SILVER", "Silver Member Benefit", DateTime.UtcNow.AddMonths(6), 300, 2),
        new("VIP-BRONZE", "Bronze Member Perk", DateTime.UtcNow.AddMonths(3), 500, 1),
        new("NEWMEMBER", "New Member Welcome Gift", DateTime.UtcNow.AddMonths(1), 10000, 1),

        // First Purchase Coupons
        new("WELCOME10", "Welcome 10% First Order", DateTime.UtcNow.AddMonths(1), 50000, 1),
        new("FIRSTBUY", "First Purchase Special", DateTime.UtcNow.AddMonths(2), 25000, 1),
        new("STARTHERE", "Start Shopping Discount", DateTime.UtcNow.AddMonths(1), 30000, 1),

        // Referral Coupons
        new("REFER2024", "Referral Program 2024", DateTime.UtcNow.AddYears(1), 10000, 1),
        new("FRIENDCODE", "Friend Referral Code", DateTime.UtcNow.AddMonths(6), 5000, 1),
        new("SHAREWIN", "Share & Win Coupon", DateTime.UtcNow.AddMonths(3), 3000, 1),

        // Flash Sale Coupons
        new("FLASH50", "Flash Sale 50% Limited", DateTime.UtcNow.AddDays(1), 100, 2),
        new("FLASH40", "Flash Sale 40% Quick Deal", DateTime.UtcNow.AddDays(2), 200, 2),
        new("FLASH30", "Flash Sale 30% Today Only", DateTime.UtcNow.AddDays(1), 500, 1),
        new("HOURLY25", "Hourly Deal 25% Off", DateTime.UtcNow.AddHours(6), 50, 1),

        // Category Specific Coupons
        new("TECHSAVE", "Tech Category Savings", DateTime.UtcNow.AddMonths(2), 1000, 1),
        new("FASHIONISTA", "Fashion Lover Discount", DateTime.UtcNow.AddMonths(2), 800, 1),
        new("HOMESTYLE", "Home & Living Special", DateTime.UtcNow.AddMonths(3), 600, 1),
        new("SPORTSFAN", "Sports Enthusiast Deal", DateTime.UtcNow.AddMonths(2), 400, 1),
        new("BOOKWORM", "Book Lover Coupon", DateTime.UtcNow.AddMonths(4), 1200, 1),
        new("BEAUTYBOSS", "Beauty Products Discount", DateTime.UtcNow.AddMonths(2), 700, 1),

        // Loyalty Coupons
        new("LOYAL1YR", "1 Year Loyalty Reward", DateTime.UtcNow.AddMonths(12), 100, 2),
        new("LOYAL3YR", "3 Year Loyalty Bonus", DateTime.UtcNow.AddMonths(12), 50, 3),
        new("LOYAL5YR", "5 Year VIP Loyalty", DateTime.UtcNow.AddMonths(12), 25, 4),
        new("THANKYOU", "Customer Appreciation", DateTime.UtcNow.AddMonths(1), 5000, 1),

        // Cart Value Coupons
        new("BIGSPEND", "Big Spender Reward", DateTime.UtcNow.AddMonths(3), 500, 2),
        new("BULK2024", "Bulk Order Discount", DateTime.UtcNow.AddMonths(6), 300, 2),
        new("MEGA500", "Orders $500+ Coupon", DateTime.UtcNow.AddMonths(2), 1000, 1),
        new("SUPER1000", "Orders $1000+ Super Deal", DateTime.UtcNow.AddMonths(2), 500, 2),

        // Special Event Coupons
        new("ANNIV2024", "Store Anniversary 2024", DateTime.UtcNow.AddDays(7), 2000, 3),
        new("CLEARANCE", "Clearance Event Code", DateTime.UtcNow.AddDays(14), 3000, 2),
        new("STUDENT24", "Student Discount 2024", DateTime.UtcNow.AddMonths(12), 10000, 1),
        new("SENIOR24", "Senior Citizen 2024", DateTime.UtcNow.AddMonths(12), 10000, 1),
        new("MILITARY", "Military Appreciation", DateTime.UtcNow.AddMonths(12), 10000, 1),

        // Weekend & Daily Coupons
        new("WEEKEND20", "Weekend Special 20%", DateTime.UtcNow.AddDays(3), 2000, 1),
        new("MONDAY15", "Monday Motivation 15%", DateTime.UtcNow.AddDays(1), 1500, 1),
        new("FRIDAY25", "Friday Frenzy 25%", DateTime.UtcNow.AddDays(1), 1000, 2),
        new("DAILYDEAL", "Daily Deal Coupon", DateTime.UtcNow.AddDays(1), 500, 1),

        // App Exclusive Coupons
        new("APPONLY", "App Exclusive Offer", DateTime.UtcNow.AddMonths(6), 5000, 2),
        new("MOBILEAPP", "Mobile App Special", DateTime.UtcNow.AddMonths(3), 3000, 1),
        new("DOWNLOAD", "Download App Reward", DateTime.UtcNow.AddMonths(1), 20000, 1),

        // Newsletter Coupons
        new("NEWSLETTER", "Newsletter Subscriber", DateTime.UtcNow.AddMonths(2), 8000, 1),
        new("SUBSCRIBE", "Email Subscription Bonus", DateTime.UtcNow.AddMonths(1), 10000, 1),
        new("INBOX10", "Inbox Exclusive 10% Off", DateTime.UtcNow.AddMonths(1), 5000, 1),

        // Limited Edition Coupons
        new("LIMITED50", "Limited Edition 50 Uses", DateTime.UtcNow.AddDays(7), 50, 3),
        new("EXCLUSIVE", "Exclusive Access Code", DateTime.UtcNow.AddDays(14), 100, 2),
        new("RARECODE", "Rare Coupon Code", DateTime.UtcNow.AddDays(30), 25, 4)
    ];

    private sealed record CouponData(string Code, string Description, DateTime ExpiresAt, int MaxUsageCount, int RuleCount);
}
