using FluentAssertions;
using PsXAiTool.Core.Entities;
using PsXAiTool.Infrastructure.Services;

namespace PsXAiTool.Tests.Services;

public class PrefilterServiceTests
{
    private readonly PrefilterService _sut = new();

    [Fact]
    public void IsRelevant_WithPsxKeyword_ReturnsTrue()
    {
        var article = new NewsArticle { Title = "PSX records new high", Description = "" };
        _sut.IsRelevant(article).Should().BeTrue();
    }

    [Fact]
    public void IsRelevant_WithDividendKeyword_ReturnsTrue()
    {
        var article = new NewsArticle { Title = "Engro announces dividends", Description = "" };
        _sut.IsRelevant(article).Should().BeTrue();
    }

    [Fact]
    public void IsRelevant_WithTwoWeakKeywords_ReturnsTrue()
    {
        var article = new NewsArticle
        {
            Title = "Pakistan economy",
            Description = "market performance stocks mentioned"
        };
        _sut.IsRelevant(article).Should().BeTrue();
    }

    [Fact]
    public void IsRelevant_WithUnrelatedNews_ReturnsFalse()
    {
        var article = new NewsArticle { Title = "Hollywood actor wins award at ceremony", Description = "Red carpet event held last night." };
        _sut.IsRelevant(article).Should().BeFalse();
    }

    [Fact]
    public void IsRelevant_WithKseKeyword_ReturnsTrue()
    {
        var article = new NewsArticle { Title = "KSE-100 closes flat", Description = "Trading volume low" };
        _sut.IsRelevant(article).Should().BeTrue();
    }

    [Fact]
    public void IsRelevant_WithEarningsKeyword_ReturnsTrue()
    {
        var article = new NewsArticle { Title = "Hub Power Company quarterly earnings beat estimates", Description = "" };
        _sut.IsRelevant(article).Should().BeTrue();
    }
}
