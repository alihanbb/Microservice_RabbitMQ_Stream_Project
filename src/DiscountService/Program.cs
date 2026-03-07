using DiscountService.Domain.Repositories;
using DiscountService.Infrastructure.Persistence;
using DiscountService.Infrastructure.Repositories;
using DiscountService.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Database
builder.Services.AddDbContext<DiscountDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SqlConnection")));

// Validate database configuration at startup
builder.Services.AddOptions<DatabaseConfiguration>()
    .Bind(builder.Configuration.GetSection("Database"))
    .ValidateOnStart();

// Repositories
builder.Services.AddScoped<ICouponCodeRepository, CouponCodeRepository>();
builder.Services.AddScoped<IDiscountRuleRepository, DiscountRuleRepository>();

// Seed Data
builder.Services.AddScoped<DiscountRuleSeedData>();
builder.Services.AddScoped<CouponCodeSeedData>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

await SeedDataAsync(app);

app.Run();

static async Task SeedDataAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;

    try
    {
        var dbContext = services.GetRequiredService<DiscountDbContext>();
        
        // Only run migrations in development environment
        if (app.Environment.IsDevelopment())
        {
            await dbContext.Database.MigrateAsync();
        }

        var ruleSeed = services.GetRequiredService<DiscountRuleSeedData>();
        await ruleSeed.SeedAsync();

        var couponSeed = services.GetRequiredService<CouponCodeSeedData>();
        await couponSeed.SeedAsync();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "An error occurred while seeding the database. This is expected if the database is not available.");
    }
}
