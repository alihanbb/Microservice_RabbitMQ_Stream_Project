using ShoppingCartService.API.Configuration;
using ShoppingCartService.API.Endpoints;
using ShoppingCartService.API.Middleware;
using ShoppingCartService.Extensions;
using ShoppingCartService.Infrastructure.Seed;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddSwaggerServices();
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

builder.Services.AddOptions<RabbitMQConfiguration>()
    .Bind(builder.Configuration.GetSection("RabbitMQ"))
    .ValidateOnStart();

var app = builder.Build();
app.UseRouting();
app.UseCorrelationId();
app.UseRequestLogging();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Shopping Cart API V1");
        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();

app.MapCartEndpoints();

await SeedDataAsync(app);

app.Run();

static async Task SeedDataAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var seedData = scope.ServiceProvider.GetRequiredService<CartSeedData>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        await seedData.SeedAsync();
        logger.LogInformation("Seed data completed successfully");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while seeding the database");
    }
}
