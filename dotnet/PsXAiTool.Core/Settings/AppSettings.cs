namespace PsXAiTool.Core.Settings;

public class AppSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public string AnthropicApiKey { get; set; } = string.Empty;
    public string VapidPublicKey { get; set; } = string.Empty;
    public string VapidPrivateKey { get; set; } = string.Empty;
    public string VapidSubject { get; set; } = "mailto:admin@example.com";
    public string HaikuModel { get; set; } = "claude-haiku-4-5-20251001";
    public string SonnetModel { get; set; } = "claude-sonnet-5";
    public int NewsFetchIntervalMinutes { get; set; } = 30;
    public int PriceFetchIntervalMinutes { get; set; } = 60;
    public string DailyBriefingCron { get; set; } = "0 8 * * *";
    public string ValidationCron { get; set; } = "0 18 * * *";
}
