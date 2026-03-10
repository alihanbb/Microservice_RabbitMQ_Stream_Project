namespace DiscountService.API.Endpoint;

public static class EndpointExtensions
{
    public static IEndpointRouteBuilder MapDiscountEndpoints(this IEndpointRouteBuilder builder)
    {
        var api = builder.MapGroup("api");
        
        api.MapCouponCodesEndpoints();
        api.MapDiscountRulesEndpoints();
        api.MapDiscountCalculationEndpoints();
        
        return builder;
    }
}
