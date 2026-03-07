using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Stream.Client;
using RabbitMQ.Stream.Client.Reliable;
using ShoppingCartService.Application.Interfaces;

namespace ShoppingCartService.Infrastructure.Messaging;

public sealed class RabbitMQStreamPublisher : IRabbitMQStreamPublisher
{
    private readonly ILogger<RabbitMQStreamPublisher> _logger;
    private readonly string _host;
    private readonly int _port;
    private readonly string _username;
    private readonly string _password;
    private readonly string _streamName;

    private StreamSystem? _streamSystem;
    private Producer? _producer;
    private bool _initialized;

    public RabbitMQStreamPublisher(IConfiguration configuration, ILogger<RabbitMQStreamPublisher> logger)
    {
        _logger = logger;
        _host = configuration["RabbitMQ:Host"] ?? "localhost";
        _port = int.TryParse(configuration["RabbitMQ:Port"], out var port) ? port : 5552;
        _username = configuration["RabbitMQ:Username"] ?? "guest";
        _password = configuration["RabbitMQ:Password"] ?? "guest";
        _streamName = configuration["RabbitMQ:StreamName"] ?? "shopping-cart-events";
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
                RequestTimeout = TimeSpan.FromSeconds(30),
                Heartbeat = TimeSpan.FromSeconds(60),
                MaxFrameSize = 1_000_000,
                ClientProperties = new Dictionary<string, string>
                {
                    { "connection_name", "ShoppingCartService_RabbitMQStreamPublisher" },
                    { "client_version", "1.0.0" }
                }
            };

            _streamSystem = await StreamSystem.Create(config).ConfigureAwait(false);

            // Create stream if it doesn't exist
            try
            {
                await _streamSystem.CreateStream(new StreamSpec(_streamName)
                {
                    MaxLengthBytes = 200_000_000 // 200MB
                }).ConfigureAwait(false);
                _logger.LogInformation("Stream '{StreamName}' created", _streamName);
            }
            catch (CreateStreamException)
            {
                // Stream already exists — not an error
                _logger.LogDebug("Stream '{StreamName}' already exists", _streamName);
            }

            var producerConfig = new ProducerConfig(_streamSystem, _streamName)
            {
                ConfirmationHandler = async confirmation =>
                {
                    if (confirmation.Status == ConfirmationStatus.Confirmed)
                    {
                        _logger.LogDebug("Message confirmed on stream '{StreamName}'", _streamName);
                    }
                    else
                    {
                        _logger.LogWarning("Message not confirmed on stream '{StreamName}': {Status}",
                            _streamName, confirmation.Status);
                    }

                    await Task.CompletedTask;
                }
            };

            _producer = await Producer.Create(producerConfig).ConfigureAwait(false);
            _initialized = true;

            _logger.LogInformation("RabbitMQ Stream publisher initialized for stream '{StreamName}'", _streamName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize RabbitMQ Stream publisher");
            throw;
        }
    }

    public async Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : class
    {
        if (!_initialized || _producer is null)
        {
            _logger.LogWarning("RabbitMQ Stream publisher not initialized. Attempting to initialize...");
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            var json = JsonSerializer.Serialize(@event);
            var body = Encoding.UTF8.GetBytes(json);
            var message = new Message(body);

            await _producer!.Send(message).ConfigureAwait(false);

            _logger.LogDebug("Published event {EventType} to stream '{StreamName}'", typeof(T).Name, _streamName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event {EventType} to stream '{StreamName}'", typeof(T).Name, _streamName);
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
            _logger.LogInformation("RabbitMQ Stream publisher disposed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while disposing RabbitMQ Stream publisher");
        }
    }
}
