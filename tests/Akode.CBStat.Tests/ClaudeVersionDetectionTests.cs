using Akode.CBStat.Services.Providers;
using FluentAssertions;

namespace Akode.CBStat.Tests;

[TestClass]
public class ClaudeVersionDetectionTests
{
    [TestMethod]
    [DataRow("2.1.74 (Claude Code)", "2.1.74")]
    [DataRow("2.1.74", "2.1.74")]
    [DataRow("3.0.0 (Claude Code)", "3.0.0")]
    [DataRow("2.1 (Beta)", "2.1")]
    [DataRow("10.20.30", "10.20.30")]
    public void ParseClaudeVersionOutput_WithValidOutput_ReturnsVersion(string input, string expected)
    {
        var result = ClaudeUsageProvider.ParseClaudeVersionOutput(input);
        result.Should().Be(expected);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow(null)]
    [DataRow("invalid")]
    [DataRow("not a version")]
    [DataRow("v2.1.74")]  // Doesn't match ^\d+\.\d+ pattern
    public void ParseClaudeVersionOutput_WithInvalidOutput_ReturnsDefault(string? input)
    {
        var result = ClaudeUsageProvider.ParseClaudeVersionOutput(input!);
        result.Should().Be("2.1.0");  // DefaultVersion
    }

    [TestMethod]
    public void ParseClaudeVersionOutput_WithActualClaudeOutput_ParsesCorrectly()
    {
        // This is what `claude --version` actually returns
        var actualOutput = "2.1.74 (Claude Code)";
        var result = ClaudeUsageProvider.ParseClaudeVersionOutput(actualOutput);
        result.Should().Be("2.1.74");
    }
}
