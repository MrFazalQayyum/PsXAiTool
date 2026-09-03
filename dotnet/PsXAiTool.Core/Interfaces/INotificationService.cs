using PsXAiTool.Core.DTOs;

namespace PsXAiTool.Core.Interfaces;

public interface INotificationService
{
    string GetVapidPublicKey();
    Task SubscribeAsync(PushSubscribeRequest request);
    Task UnsubscribeAsync(string endpoint);
    Task<int> GetSubscriberCountAsync();
    Task SendNotificationAsync(string title, string body, string? url = null);
}
