namespace ShoppingCartService.Application.Interfaces;

public interface IRabbitMQStreamPublisher : IAsyncDisposable
{
    Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : class;
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
