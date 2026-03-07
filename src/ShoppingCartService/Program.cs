using ShoppingCartService.API.Endpoints;
using ShoppingCartService.API.Middleware;
using ShoppingCartService.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddApiVersioningServices();
builder.Services.AddSwaggerServices();
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// Validate configuration at startup
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
        var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
        foreach (var description in provider.ApiVersionDescriptions)
        {
            options.SwaggerEndpoint(
                $"/swagger/{description.GroupName}/swagger.json",
                $"Shopping Cart API {description.GroupName.ToUpperInvariant()}");
        }
        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();

app.MapCartEndpoints();

await SeedDataAsync(app);

app.Run();

static async Task SeedDataAsync(WebApplication app)
{
    await Task.CompletedTask;
}
