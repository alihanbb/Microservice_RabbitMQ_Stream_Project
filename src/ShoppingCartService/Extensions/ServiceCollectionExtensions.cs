using ShoppingCartService.API.Configuration;
using ShoppingCartService.Application.Commands.AddItem;
using ShoppingCartService.Application.Commands.ConfirmCart;
using ShoppingCartService.Application.Commands.RemoveItem;
using ShoppingCartService.Application.Commands.UpdateItemQuantity;
using ShoppingCartService.Application.Interfaces;
using ShoppingCartService.Application.Queries.GetCart;
using ShoppingCartService.Infrastructure.EventStore;
using ShoppingCartService.Infrastructure.Messaging;
using ShoppingCartService.Infrastructure.Repositories;
using ShoppingCartService.Infrastructure.Seed;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ShoppingCartService.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<AddItemCommandHandler>();
        services.AddScoped<RemoveItemCommandHandler>();
        services.AddScoped<ConfirmCartCommandHandler>();
        services.AddScoped<UpdateItemQuantityCommandHandler>();
        services.AddScoped<GetCartQueryHandler>();

        services.AddValidatorsFromAssemblyContaining<Program>();
        services.AddMemoryCache();

        return services;
    }

    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var redisConnectionString = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Redis connection string is not configured. Set ConnectionStrings__Redis environment variable or appsettings.Development.json.");

        services.AddSingleton(new RedisConnectionFactory(redisConnectionString));

        services.AddOptions<EventStoreOptions>()
            .Bind(configuration.GetSection(EventStoreOptions.SectionName));

        services.AddScoped<RedisEventStore>();
        services.AddScoped<IEventStore>(sp => sp.GetRequiredService<RedisEventStore>());
        services.AddScoped<ICartAggregateRepository, CartAggregateRepository>();

        services.AddSingleton<IRabbitMQStreamPublisher, RabbitMQStreamPublisher>();
        services.AddHostedService<RabbitMQInitializationService>();
        services.AddScoped<CartSeedData>();

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
