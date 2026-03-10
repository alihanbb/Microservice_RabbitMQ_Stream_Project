using NotificationService.Application.Interfaces;
using NotificationService.Models;

namespace NotificationService.Application.Services;
public sealed class LogNotificationSender : INotificationSender
{
    private readonly ILogger<LogNotificationSender> _logger;

    public LogNotificationSender(ILogger<LogNotificationSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(CartConfirmedEvent cartEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[NOTIFICATION] Cart confirmed — UserId: {UserId}, CartId: {CartId}, " +
            "TotalAmount: {TotalAmount:C}, Items: {TotalItems}, ConfirmedAt: {ConfirmedAt}",
            cartEvent.UserId,
            cartEvent.CartId,
            cartEvent.TotalAmount,
            cartEvent.TotalItems,
            cartEvent.ConfirmedAt);

        if (cartEvent.Items?.Count > 0)
        {
            foreach (var item in cartEvent.Items)
            {
                _logger.LogDebug(
                    "[NOTIFICATION] Item — {ProductName} (ProductId: {ProductId}), " +
                    "Category: {Category}, Qty: {Quantity}, Price: {Price:C}",
                    item.ProductName,
                    item.ProductId,
                    item.Category,
                    item.Quantity,
                    item.Price);
            }
        }

        return Task.CompletedTask;
    }
}
