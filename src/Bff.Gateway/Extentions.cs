using Microsoft.AspNetCore.Mvc;

namespace Bff.Gateway;

public static class Extentions
{
    public static IServiceCollection GatewayService(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddReverseProxy()
    .LoadFromConfig(configuration.GetSection("ReverseProxy"));

        services.AddHttpClient("ShoppingCart", c => c.BaseAddress = new Uri("http://localhost:5252"));
        services.AddHttpClient("Discount", c => c.BaseAddress = new Uri("http://localhost:5282"));
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Title = "Gateway Service API",
                Version = "v1",
                Description = "Microservice API Gateway "
            });
        });

        return services;
    }
    public  static IEndpointRouteBuilder UseGatewayEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/gateway/carts/{userId}/with-discount", async (
        Guid userId,
        [FromQuery] string? couponCode,
        IHttpClientFactory clientFactory) =>
        {
            var cartClient = clientFactory.CreateClient("ShoppingCart");
            var discountClient = clientFactory.CreateClient("Discount");

            var cartResponse = await cartClient.GetAsync($"/api/carts/{userId}");
            if (!cartResponse.IsSuccessStatusCode) return Results.NotFound();

            var cartString = await cartResponse.Content.ReadAsStringAsync();
            var cartObj = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.Nodes.JsonObject>(cartString, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (cartObj == null || !cartObj.ContainsKey("totalAmount")) return Results.NotFound();

            decimal originalTotal = cartObj["totalAmount"]?.GetValue<decimal>() ?? 0m;
            decimal finalPrice = originalTotal;
            decimal discountAmount = 0;

            if (!string.IsNullOrEmpty(couponCode))
            {
                var discountReq = new { CouponCode = couponCode, TotalAmount = originalTotal };
                var discountRes = await discountClient.PostAsJsonAsync("/api/discounts/calculate", discountReq);

                if (discountRes.IsSuccessStatusCode)
                {
                    var discountResultString = await discountRes.Content.ReadAsStringAsync();
                    var discountResultObj = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.Nodes.JsonObject>(discountResultString, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (discountResultObj != null)
                    {
                        if (discountResultObj.ContainsKey("discountedAmount"))
                            finalPrice = discountResultObj["discountedAmount"]?.GetValue<decimal>() ?? finalPrice;

                        if (discountResultObj.ContainsKey("discountAmount"))
                            discountAmount = discountResultObj["discountAmount"]?.GetValue<decimal>() ?? discountAmount;
                    }
                }
            }

            return Results.Ok(new
            {
                Cart = cartObj,
                OriginalTotal = originalTotal,
                DiscountApplied = discountAmount,
                FinalAmountToPay = finalPrice,
                AppliedCoupon = couponCode
            });
        });

        return app;

    }
    
    public static WebApplication GatewayApp(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Gateway Service API v1");
            options.RoutePrefix = "swagger";
        });
        return app;
    }

}
