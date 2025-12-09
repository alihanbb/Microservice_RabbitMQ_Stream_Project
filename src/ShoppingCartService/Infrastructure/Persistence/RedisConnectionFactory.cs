using StackExchange.Redis;

namespace ShoppingCartService.Infrastructure.Persistence;

public class RedisConnectionFactory : IDisposable
{
    private readonly Lazy<ConnectionMultiplexer> _connection;
    private bool _disposed;

    public RedisConnectionFactory(string connectionString)
    {
        _connection = new Lazy<ConnectionMultiplexer>(() =>
        {
            var options = ConfigurationOptions.Parse(connectionString);
            options.AbortOnConnectFail = false;
            options.ConnectRetry = 3;
            options.ConnectTimeout = 5000;
            return ConnectionMultiplexer.Connect(options);
        });
    }

    public IDatabase GetDatabase(int db = -1)
    {
        return _connection.Value.GetDatabase(db);
    }

    public IServer GetServer()
    {
        var endpoints = _connection.Value.GetEndPoints();
        return _connection.Value.GetServer(endpoints.First());
    }

    public bool IsConnected => _connection.Value.IsConnected;

    public void Dispose()
    {
        if (_disposed) return;

        if (_connection.IsValueCreated)
        {
            _connection.Value.Dispose();
        }

        _disposed = true;
    }
}
