using DiscountService.API.Endpoint;
using DiscountService.API.MiddleWare;
using DiscountService.Application.UseCase;
using DiscountService.Infrastructure.Configuration;
using DiscountService.Infrastructure.Messaging;
using DiscountService.Infrastructure.Repositories;
using DiscountService.Infrastructure.Seed;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Discount Service API",
        Version = "v1",
        Description = "Microservice for managing discount rules, coupon codes, and discount calculations"
    });
});

builder.Services.AddDbContext<DiscountDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SqlConnection")));

builder.Services.AddOptions<DatabaseConfiguration>()
    .Bind(builder.Configuration.GetSection("Database"))
    .ValidateOnStart();

builder.Services.AddScoped<ICouponCodeRepository, CouponCodeRepository>();
builder.Services.AddScoped<IDiscountRuleRepository, DiscountRuleRepository>();

builder.Services.AddScoped<ICalculateDiscountUseCase, CalculateDiscountUseCase>();
builder.Services.AddScoped<ICouponCodeUseCases, CouponCodeUseCases>();
builder.Services.AddScoped<IDiscountRuleUseCases, DiscountRuleUseCases>();

builder.Services.AddScoped<DiscountRuleSeedData>();
builder.Services.AddScoped<CouponCodeSeedData>();

builder.Services.AddHostedService<CartEventConsumer>();
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Discount Service API v1");
    options.RoutePrefix = "swagger";
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapDiscountEndpoints();

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
