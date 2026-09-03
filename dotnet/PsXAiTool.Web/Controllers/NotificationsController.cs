using Microsoft.AspNetCore.Mvc;
using PsXAiTool.Core.DTOs;
using PsXAiTool.Core.Interfaces;

namespace PsXAiTool.Web.Controllers;

[ApiController]
[Route("api/notifications")]
public class NotificationsController(INotificationService notifications) : ControllerBase
{
    [HttpGet("vapid-public-key")]
    public IActionResult GetVapidPublicKey() =>
        Ok(new VapidPublicKeyDto(notifications.GetVapidPublicKey()));

    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] PushSubscribeRequest request)
    {
        await notifications.SubscribeAsync(request);
        return Ok();
    }

    [HttpPost("unsubscribe")]
    public async Task<IActionResult> Unsubscribe([FromBody] string endpoint)
    {
        await notifications.UnsubscribeAsync(endpoint);
        return Ok();
    }

    [HttpGet("subscribers/count")]
    public async Task<IActionResult> GetSubscriberCount() =>
        Ok(new SubscriberCountDto(await notifications.GetSubscriberCountAsync()));
}
