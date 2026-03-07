using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using NotificationService.Models;

namespace NotificationService.Functions;

/// <summary>
/// Azure Function that processes cart confirmation events from RabbitMQ Stream.
/// Triggered when a CartConfirmedEvent is published to the shopping-cart-events queue.
/// </summary>
public class ProcessCartConfirmedFunction
{
    private readonly ILogger<ProcessCartConfirmedFunction> _logger;

    public ProcessCartConfirmedFunction(ILogger<ProcessCartConfirmedFunction> logger)
    {
        _logger = logger;
    }

    [Function("ProcessCartConfirmed")]
    public async Task Run(
        [RabbitMQTrigger("shopping-cart-events", ConnectionStringSetting = "RabbitMQConnection")]
        CartConfirmedEvent cartEvent)
    {
        try
        {
            _logger.LogInformation(
                "Processing cart confirmation for user {UserId}. Event timestamp: {ConfirmedAt}, Item count: {ItemCount}, Total amount: {TotalAmount}",
                cartEvent.UserId,
                cartEvent.ConfirmedAt,
                cartEvent.ItemCount,
                cartEvent.TotalAmount);

            // Log cart items for audit trail
            if (cartEvent.Items?.Count > 0)
            {
                foreach (var item in cartEvent.Items)
                {
                    _logger.LogInformation(
                        "Cart item - Product: {ProductName} ({ProductId}), Category: {Category}, Quantity: {Quantity}, Price: {Price}",
                        item.ProductName,
                        item.ProductId,
                        item.Category,
                        item.Quantity,
                        item.Price);
                }
            }

            // Send notification (for now just logging, can be extended to email/SMS/push notifications)
            await SendNotificationAsync(cartEvent);

            _logger.LogInformation(
                "Successfully processed and sent notification for cart confirmation (User: {UserId})",
                cartEvent.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing cart confirmation event for user {UserId}",
                cartEvent.UserId);
            throw;
        }
    }

    private async Task SendNotificationAsync(CartConfirmedEvent cartEvent)
    {
        // TODO: Implement actual notification sending
        // This could be:
        // - Email notification via SendGrid
        // - SMS via Twilio
        // - Push notification via Firebase
        // - Log to database
        
        _logger.LogInformation(
            "Notification: Cart confirmed for user {UserId} with {ItemCount} items. Total: ${TotalAmount}",
            cartEvent.UserId,
            cartEvent.ItemCount,
            cartEvent.TotalAmount);

        await Task.CompletedTask;
    }
}
