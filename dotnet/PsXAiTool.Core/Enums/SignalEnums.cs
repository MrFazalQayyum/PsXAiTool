namespace PsXAiTool.Core.Enums;

public enum SignalDirection { Bullish, Bearish, Neutral }

public enum ValidationVerdict { Correct, Wrong, Neutral }

public enum SignalType
{
    EarningsReport,
    DividendAnnouncement,
    ManagementChange,
    RegulatoryAction,
    MergersAcquisitions,
    ProductLaunch,
    MacroeconomicFactor,
    SectorTrend,
    TechnicalBreakout,
    Other
}
