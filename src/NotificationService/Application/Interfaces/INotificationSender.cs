using NotificationService.Models;

namespace NotificationService.Application.Interfaces;

public interface INotificationSender
{
    Task SendAsync(CartConfirmedEvent cartEvent, CancellationToken cancellationToken = default);
}
