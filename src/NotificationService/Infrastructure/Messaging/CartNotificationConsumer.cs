#region usings
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using NotificationService.Application.Interfaces;
using NotificationService.Models;
using RabbitMQ.Stream.Client;
using RabbitMQ.Stream.Client.AMQP;
using RabbitMQ.Stream.Client.Reliable;
#endregion
namespace NotificationService.Infrastructure.Messaging;
public sealed class CartNotificationConsumer(
        ILogger<CartNotificationConsumer> logger,
        IConfiguration configuration,
        INotificationSender notificationSender,
        IMemoryCache cache) : BackgroundService
{

    private StreamSystem? _streamSystem;
    private Consumer? _consumer;
    private Producer? _dlqProducer;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var host = configuration["RabbitMQ:Host"] ?? "localhost";
        var port = int.TryParse(configuration["RabbitMQ:Port"], out var p) ? p : 5552;
        var username = configuration["RabbitMQ:Username"] ?? "guest";
        var password = configuration["RabbitMQ:Password"] ?? "guest";
        var streamName = configuration["RabbitMQ:SuperStreamName"] ?? "shopping-cart-events";
        var partitions = int.TryParse(configuration["RabbitMQ:Partitions"], out var parts) ? parts : 3;

        var maxRetries = 10;
        var retryCount = 0;

        while (!stoppingToken.IsCancellationRequested && retryCount < maxRetries)
        {
            try
            {
                var config = new StreamSystemConfig
                {
                    UserName = username,
                    Password = password,
                    VirtualHost = "/",
                    Endpoints = [new DnsEndPoint(host, port)],
                    RpcTimeOut = TimeSpan.FromSeconds(30)
                };

                _streamSystem = await StreamSystem.Create(config);
                logger.LogInformation("Connected to RabbitMQ Stream system");

                try
                {
                    await _streamSystem.CreateSuperStream(new PartitionsSuperStreamSpec(streamName, partitions));
                    logger.LogInformation("Super stream '{StreamName}' created with {Partitions} partitions", streamName, partitions);
                }
                catch (CreateStreamException)
                {
                    logger.LogDebug("Super stream '{StreamName}' already exists", streamName);
                }

                var dlqStreamName = "shopping-cart-dlq";
                try
                {
                    await _streamSystem.CreateStream(new StreamSpec(dlqStreamName) { MaxLengthBytes = 5L * 1024 * 1024 * 1024 });
                    logger.LogInformation("DLQ stream '{StreamName}' created", dlqStreamName);
                }
                catch (CreateStreamException)
                {
                    logger.LogDebug("DLQ stream '{StreamName}' already exists", dlqStreamName);
                }

                _dlqProducer = await Producer.Create(new ProducerConfig(_streamSystem, dlqStreamName));

                _consumer = await Consumer.Create(new ConsumerConfig(_streamSystem, streamName)
                {
                    IsSuperStream = true,
                    OffsetSpec = new OffsetTypeNext(),
                    MessageHandler = async (stream, _, _, message) =>
                    {
                        await ProcessMessageAsync(message, stream, stoppingToken);
                    }
                });

                logger.LogInformation(
                    "NotificationService consumer started on '{StreamName}' ({Partitions} partitions)",
                    streamName, partitions);

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("NotificationService consumer stopping due to cancellation");
                break;
            }
            catch (Exception ex)
            {
                retryCount++;
                var delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, retryCount)));

                logger.LogWarning(ex,
                    "Failed to connect to RabbitMQ Super Stream (attempt {Attempt}/{MaxRetries}). Retrying in {Delay}s...",
                    retryCount, maxRetries, delay.TotalSeconds);

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        if (retryCount >= maxRetries)
        {
            logger.LogError(
                "Failed to connect to RabbitMQ Super Stream after {MaxRetries} attempts. Consumer will not start.",
                maxRetries);
        }
    }

    private async Task ProcessMessageAsync(Message message, string partitionName, CancellationToken ct)
    {
        int retryCount = 0;
        Exception? lastException = null;

        while (retryCount < 3)
        {
            try
            {
                var contents = message.Data.Contents;
                var json = Encoding.UTF8.GetString(
                    contents.IsSingleSegment
                        ? contents.First.Span
                        : System.Buffers.BuffersExtensions.ToArray(contents));

                var cartEvent = JsonSerializer.Deserialize<CartConfirmedEvent>(json);

                if (cartEvent == null)
                {
                    logger.LogWarning("Received null cart event from partition '{Partition}'", partitionName);
                    return;
                }

                var cacheKey = $"notified:cart:{cartEvent.CartId}";
                if (cache.TryGetValue(cacheKey, out _))
                {
                    logger.LogDebug(
                        "Duplicate message skipped — CartId: {CartId} already processed",
                        cartEvent.CartId);
                    return;
                }

                var cacheTtlMinutes = int.Parse(configuration["Notification:IdempotencyCacheMinutes"] ?? "1440");
                cache.Set(cacheKey, true, TimeSpan.FromMinutes(cacheTtlMinutes));

                await notificationSender.SendAsync(cartEvent, ct);
                
                return;
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "Failed to deserialize cart event from partition '{Partition}'", partitionName);
                lastException = ex;
                break; 
            }
            catch (Exception ex)
            {
                lastException = ex;
                retryCount++;
                logger.LogWarning(ex, "Error processing message from partition '{Partition}'. Retry {RetryCount}/3", partitionName, retryCount);
                if (retryCount < 3)
                {
                    await Task.Delay(1000 * retryCount, ct);
                }
            }
        }

        logger.LogError("Message {MessageId} failed processing. Moving to DLQ. Reason: {Error}", message.Properties?.MessageId, lastException?.Message);

        if (_dlqProducer != null)
        {
            try
            {
                var dlqBytes = message.Data.Contents.IsSingleSegment
                    ? message.Data.Contents.First.Span.ToArray()
                    : System.Buffers.BuffersExtensions.ToArray(message.Data.Contents);

                var dlqMessage = new Message(dlqBytes)
                {
                    Properties = new Properties { MessageId = message.Properties?.MessageId ?? Guid.NewGuid().ToString() },
                    ApplicationProperties = new ApplicationProperties
                    {
                        { "ErrorReason", lastException?.Message ?? "Unknown Error" },
                        { "FailedAt", DateTime.UtcNow.ToString("O") }
                    }
                };
                
                await _dlqProducer.Send(dlqMessage);
                logger.LogInformation("Message {MessageId} successfully moved to DLQ", dlqMessage.Properties.MessageId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to publish message {MessageId} to DLQ!", message.Properties?.MessageId);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        if (_consumer is not null)
            await _consumer.Close();
        if (_dlqProducer is not null)
            await _dlqProducer.Close();
        if (_streamSystem is not null)
            await _streamSystem.Close();
    }
}
