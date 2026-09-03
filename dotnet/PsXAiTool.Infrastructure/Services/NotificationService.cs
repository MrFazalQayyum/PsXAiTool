using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PsXAiTool.Core.DTOs;
using PsXAiTool.Core.Interfaces;
using PsXAiTool.Core.Settings;
using PsXAiTool.Infrastructure.Data;
using WebPush;
using DbPushSubscription = PsXAiTool.Core.Entities.PushSubscription;

namespace PsXAiTool.Infrastructure.Services;

public class NotificationService(
    AppDbContext db,
    IOptions<AppSettings> settings,
    ILogger<NotificationService> logger) : INotificationService
{
    private readonly AppSettings _settings = settings.Value;

    public string GetVapidPublicKey() => _settings.VapidPublicKey;

    public async Task SubscribeAsync(PushSubscribeRequest request)
    {
        var exists = await db.PushSubscriptions.AnyAsync(s => s.Endpoint == request.Endpoint);
        if (exists) return;

        db.PushSubscriptions.Add(new DbPushSubscription
        {
            Endpoint = request.Endpoint,
            P256dh = request.P256dh,
            Auth = request.Auth
        });
        await db.SaveChangesAsync();
    }

    public async Task UnsubscribeAsync(string endpoint)
    {
        var sub = await db.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == endpoint);
        if (sub is null) return;

        db.PushSubscriptions.Remove(sub);
        await db.SaveChangesAsync();
    }

    public async Task<int> GetSubscriberCountAsync() =>
        await db.PushSubscriptions.CountAsync();

    public async Task SendNotificationAsync(string title, string body, string? url = null)
    {
        if (string.IsNullOrEmpty(_settings.VapidPublicKey) || string.IsNullOrEmpty(_settings.VapidPrivateKey))
        {
            logger.LogWarning("VAPID keys not configured. Skipping push notification.");
            return;
        }

        var subscribers = await db.PushSubscriptions.ToListAsync();
        if (subscribers.Count == 0) return;

        var vapidDetails = new VapidDetails(_settings.VapidSubject, _settings.VapidPublicKey, _settings.VapidPrivateKey);
        var client = new WebPushClient();
        var payload = JsonSerializer.Serialize(new { title, body, url });

        var stale = new List<DbPushSubscription>();
        foreach (var sub in subscribers)
        {
            try
            {
                var webPushSub = new PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                await client.SendNotificationAsync(webPushSub, payload, vapidDetails);
            }
            catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone)
            {
                stale.Add(sub);
            }
            catch (Exception ex)
            {
                logger.LogWarning("Push notification failed for {Endpoint}: {Error}", sub.Endpoint, ex.Message);
            }
        }

        if (stale.Count > 0)
        {
            db.PushSubscriptions.RemoveRange(stale);
            await db.SaveChangesAsync();
        }
    }
}
