using PsXAiTool.Core.Entities;

namespace PsXAiTool.Core.Interfaces;

public interface IPrefilterService
{
    bool IsRelevant(NewsArticle article);
}
