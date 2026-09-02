using Microsoft.Extensions.Logging;
using PsXAiTool.Core.Interfaces;

namespace PsXAiTool.Infrastructure.Jobs;

public class BackgroundJobs(
    IMarketService marketService,
    ISignalService signalService,
    ILogger<BackgroundJobs> logger)
{
    public async Task FetchPricesJob()
    {
        logger.LogInformation("Job: FetchPrices started.");
        await marketService.FetchAndStorePricesAsync();
        logger.LogInformation("Job: FetchPrices complete.");
    }

    public async Task ProcessNewsJob()
    {
        logger.LogInformation("Job: ProcessNews started.");
        await signalService.TriggerNewsProcessingAsync();
        logger.LogInformation("Job: ProcessNews complete.");
    }

    public async Task GenerateBriefingJob()
    {
        logger.LogInformation("Job: GenerateBriefing started.");
        await signalService.TriggerBriefingAsync();
        logger.LogInformation("Job: GenerateBriefing complete.");
    }

    public async Task ValidateSignalsJob()
    {
        logger.LogInformation("Job: ValidateSignals started.");
        await signalService.TriggerValidationAsync();
        logger.LogInformation("Job: ValidateSignals complete.");
    }
}
