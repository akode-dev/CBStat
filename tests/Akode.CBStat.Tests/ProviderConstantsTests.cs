using Akode.CBStat.Models;
using FluentAssertions;

namespace Akode.CBStat.Tests;

[TestClass]
public class ProviderConstantsTests
{
    [TestMethod]
    [DataRow("claude")]
    [DataRow("codex")]
    [DataRow("gemini")]
    public void IsValidProvider_WithValidProvider_ReturnsTrue(string provider)
    {
        ProviderConstants.IsValidProvider(provider).Should().BeTrue();
    }

    [TestMethod]
    [DataRow("invalid")]
    [DataRow("")]
    [DataRow(null)]
    public void IsValidProvider_WithInvalidProvider_ReturnsFalse(string? provider)
    {
        ProviderConstants.IsValidProvider(provider!).Should().BeFalse();
    }

    [TestMethod]
    public void ValidateAndNormalize_WithValidProvider_ReturnsNormalized()
    {
        ProviderConstants.ValidateAndNormalize("CLAUDE").Should().Be("claude");
        ProviderConstants.ValidateAndNormalize(" Codex ").Should().Be("codex");
    }

    [TestMethod]
    public void ValidateAndNormalize_WithInvalidProvider_ThrowsArgumentException()
    {
        var act = () => ProviderConstants.ValidateAndNormalize("invalid");
        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void GetSource_ReturnsCorrectSource()
    {
        ProviderConstants.GetSource("claude").Should().Be("oauth");
        ProviderConstants.GetSource("codex").Should().Be("cli");
        ProviderConstants.GetSource("gemini").Should().Be("cli");
    }
}
