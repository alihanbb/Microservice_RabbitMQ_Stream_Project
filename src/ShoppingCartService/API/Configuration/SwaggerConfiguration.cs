using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ShoppingCartService.API.Configuration;

public class ConfigureSwaggerOptions : IConfigureOptions<SwaggerGenOptions>
{
    private readonly IApiVersionDescriptionProvider _provider;

    public ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider)
    {
        _provider = provider;
    }

    public void Configure(SwaggerGenOptions options)
    {
        foreach (var description in _provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, CreateInfoForApiVersion(description));
        }
    }

    private static OpenApiInfo CreateInfoForApiVersion(ApiVersionDescription description)
    {
        var info = new OpenApiInfo
        {
            Title = "Shopping Cart Service API",
            Version = description.ApiVersion.ToString(),
            Description = "A microservice for managing shopping carts with Redis storage.",
            Contact = new OpenApiContact
            {
                Name = "Development Team",
                Email = "dev@example.com"
            },
            License = new OpenApiLicense
            {
                Name = "MIT",
                Url = new Uri("https://opensource.org/licenses/MIT")
            }
        };

        if (description.IsDeprecated)
        {
            info.Description += " **This API version has been deprecated.**";
        }

        return info;
    }
}

public class SwaggerDefaultValues : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var apiDescription = context.ApiDescription;

        if (operation.Parameters == null)
        {
            return;
        }

        foreach (var parameter in operation.Parameters)
        {
            var description = apiDescription.ParameterDescriptions
                .FirstOrDefault(p => p.Name == parameter.Name);

            if (description != null)
            {
                parameter.Description ??= description.ModelMetadata?.Description;

                if (parameter.Schema.Default == null && description.DefaultValue != null)
                {
                    parameter.Schema.Default = new Microsoft.OpenApi.Any.OpenApiString(
                        description.DefaultValue.ToString());
                }

                parameter.Required |= description.IsRequired;
            }
        }
    }
}
