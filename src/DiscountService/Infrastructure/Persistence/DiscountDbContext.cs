namespace DiscountService.Infrastructure.Persistence
{
    public class DiscountDbContext : DbContext
    {
        public DiscountDbContext(DbContextOptions<DiscountDbContext> options) : base(options) { }

        public DbSet<CouponCode> CouponCodes => Set<CouponCode>();
        public DbSet<DiscountRule> DiscountRules => Set<DiscountRule>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(DiscountDbContext).Assembly);
        }
    }
}
