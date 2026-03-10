using System.Net;
using System.Text;
using RabbitMQ.Stream.Client;
using RabbitMQ.Stream.Client.AMQP;
using RabbitMQ.Stream.Client.Reliable;

namespace ShoppingCartService.Infrastructure.Messaging;

public sealed class RabbitMQStreamPublisher : IRabbitMQStreamPublisher
{
    private readonly ILogger<RabbitMQStreamPublisher> _logger;
    private readonly string _host;
    private readonly int _port;
    private readonly string _username;
    private readonly string _password;
    private readonly string _streamName;
    private readonly int _partitions;

    private StreamSystem? _streamSystem;
    private Producer? _producer;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public RabbitMQStreamPublisher(IConfiguration configuration, ILogger<RabbitMQStreamPublisher> logger)
    {
        _logger = logger;
        _host = configuration["RabbitMQ:Host"] ?? "localhost";
        _port = int.TryParse(configuration["RabbitMQ:Port"], out var port) ? port : 5552;
        _username = configuration["RabbitMQ:Username"] ?? "guest";
        _password = configuration["RabbitMQ:Password"] ?? "guest";
        _streamName = configuration["RabbitMQ:StreamName"] ?? "shopping-cart-events";
        _partitions = int.TryParse(configuration["RabbitMQ:Partitions"], out var parts) ? parts : 3;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;

        try
        {
            var config = new StreamSystemConfig
            {
                UserName = _username,
                Password = _password,
                Endpoints = [new DnsEndPoint(_host, _port)],
                RpcTimeOut = TimeSpan.FromSeconds(30),
                Heartbeat = TimeSpan.FromSeconds(60)
            };

            _streamSystem = await StreamSystem.Create(config).ConfigureAwait(false);

            // Create super stream if it doesn't exist
            try
            {
                await _streamSystem.CreateSuperStream(new PartitionsSuperStreamSpec(_streamName, _partitions)).ConfigureAwait(false);
                _logger.LogInformation("Super stream '{StreamName}' created with {Partitions} partitions", _streamName, _partitions);
            }
            catch (CreateStreamException)
            {
                // Super stream already exists — not an error
                _logger.LogDebug("Super stream '{StreamName}' already exists", _streamName);
            }

            var producerConfig = new ProducerConfig(_streamSystem, _streamName)
            {
                // Route messages to partitions based on MessageId (CartId)
                SuperStreamConfig = new SuperStreamConfig
                {
                    Routing = message => message.Properties.MessageId?.ToString() ?? Guid.NewGuid().ToString()
                },
                ConfirmationHandler = async confirmation =>
                {
                    if (confirmation.Status == ConfirmationStatus.Confirmed)
                    {
                        _logger.LogDebug("Message confirmed on super stream '{StreamName}'", _streamName);
                    }
                    else
                    {
                        _logger.LogWarning("Message not confirmed on super stream '{StreamName}': {Status}",
                            _streamName, confirmation.Status);
                    }

                    await Task.CompletedTask;
                }
            };

            _producer = await Producer.Create(producerConfig).ConfigureAwait(false);
            _initialized = true;

            _logger.LogInformation("RabbitMQ Super Stream publisher initialized for '{StreamName}' ({Partitions} partitions)", _streamName, _partitions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize RabbitMQ Super Stream publisher");
            throw;
        }
    }

    public async Task PublishAsync<T>(T @event, string? routingKey = null, CancellationToken cancellationToken = default) where T : class
    {
        if (!_initialized || _producer is null)
        {
            await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Double-check after acquiring lock
                if (!_initialized || _producer is null)
                {
                    _logger.LogWarning("RabbitMQ Super Stream publisher not initialized. Attempting to initialize...");
                    await InitializeAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                _initLock.Release();
            }
        }

        try
        {
            var json = JsonSerializer.Serialize(@event);
            var body = Encoding.UTF8.GetBytes(json);

            // Set MessageId as routing key for consistent partition routing
            var message = new Message(body)
            {
                Properties = new Properties
                {
                    MessageId = routingKey ?? Guid.NewGuid().ToString()
                }
            };

            await _producer!.Send(message).ConfigureAwait(false);

            _logger.LogInformation(
                "Successfully published event {EventType} to super stream '{StreamName}' (RoutingKey: {RoutingKey}, Size: {Size} bytes)",
                typeof(T).Name, _streamName, message.Properties.MessageId, body.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event {EventType} to super stream '{StreamName}'", typeof(T).Name, _streamName);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_producer is not null)
            {
                await _producer.Close().ConfigureAwait(false);
                _producer = null;
            }

            if (_streamSystem is not null)
            {
                await _streamSystem.Close().ConfigureAwait(false);
                _streamSystem = null;
            }

            _initialized = false;
            _logger.LogInformation("RabbitMQ Super Stream publisher disposed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while disposing RabbitMQ Super Stream publisher");
        }
    }
}
