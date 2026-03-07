#region
using ShoppingCartService.API.Configuration;
using ShoppingCartService.Application.Commands.AddItem;
using ShoppingCartService.Application.Commands.ConfirmCart;
using ShoppingCartService.Application.Commands.RemoveItem;
using ShoppingCartService.Application.Interfaces;
using ShoppingCartService.Application.Queries.GetCart;
using ShoppingCartService.Infrastructure.EventStore;
using ShoppingCartService.Infrastructure.Messaging;
using ShoppingCartService.Infrastructure.Repositories;
using Swashbuckle.AspNetCore.SwaggerGen;
#endregion
namespace ShoppingCartService.Extensions;
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Legacy handlers (state-based)
        services.AddScoped<AddItemCommandHandler>();
        services.AddScoped<RemoveItemCommandHandler>();
        services.AddScoped<ConfirmCartCommandHandler>();
        services.AddScoped<GetCartQueryHandler>();

        // Event-sourced handlers (V2)
        services.AddScoped<AddItemCommandHandlerV2>();
        services.AddScoped<RemoveItemCommandHandlerV2>();
        services.AddScoped<ConfirmCartCommandHandlerV2>();
        services.AddScoped<GetCartQueryHandlerV2>();

        services.AddValidatorsFromAssemblyContaining<Program>();
        services.AddMemoryCache();

        return services;
    }

    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var redisConnectionString = configuration.GetConnectionString("Redis") 
            ?? "localhost:6379,password=RedisPass123";

        services.AddSingleton(new RedisConnectionFactory(redisConnectionString));

        // Legacy repository (state-based)
        services.AddScoped<ICartRepository, RedisCartRepository>();

        // Event Sourcing infrastructure
        services.AddScoped<RedisEventStore>();
        services.AddScoped<IEventStore>(sp => sp.GetRequiredService<RedisEventStore>());
        services.AddScoped<ICartAggregateRepository, CartAggregateRepository>();

        // RabbitMQ Stream
        services.AddSingleton<IRabbitMQStreamPublisher, RabbitMQStreamPublisher>();
        
        // Initialize RabbitMQ Stream Publisher on startup
        services.AddHostedService<RabbitMQInitializationService>();

        return services;
    }

    public static IServiceCollection AddApiVersioningServices(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = ApiVersionReader.Combine(
                new UrlSegmentApiVersionReader(),
                new HeaderApiVersionReader("X-Api-Version"),
                new QueryStringApiVersionReader("api-version")
            );
        })
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

        return services;
    }

    public static IServiceCollection AddSwaggerServices(this IServiceCollection services)
    {
        services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
        
        services.AddSwaggerGen(options =>
        {
            options.OperationFilter<SwaggerDefaultValues>();
            
            var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }
        });

        return services;
    }
}
