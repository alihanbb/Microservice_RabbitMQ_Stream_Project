using Bff.Gateway;

var builder = WebApplication.CreateBuilder(args);

builder.Services.GatewayService(builder.Configuration);

var app = builder.Build();

app.UseGatewayEndpoints();
app.MapReverseProxy();
app.UseRouting();
app.GatewayApp();
app.Run();
