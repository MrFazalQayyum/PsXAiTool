namespace PsXAiTool.Core.DTOs;

public record PushSubscribeRequest(string Endpoint, string P256dh, string Auth);

public record VapidPublicKeyDto(string PublicKey);

public record SubscriberCountDto(int Count);
