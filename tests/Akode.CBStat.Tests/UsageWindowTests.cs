using Akode.CBStat.Models;
using FluentAssertions;

namespace Akode.CBStat.Tests;

[TestClass]
public class UsageWindowTests
{
    #region ComputeDailyBudget - Basic

    [TestMethod]
    public void ComputeDailyBudget_WhenNoResetAt_ReturnsNull()
    {
        var window = new UsageWindow { Used = 10, Limit = 100, ResetAt = null };

        window.ComputeDailyBudget().Should().BeNull();
    }

    [TestMethod]
    public void ComputeDailyBudget_WhenResetInPast_ReturnsNull()
    {
        var now = new DateTime(2025, 2, 10, 14, 0, 0);
        var window = new UsageWindow
        {
            Used = 10,
            Limit = 100,
            ResetAt = now.AddHours(-1).ToUniversalTime()
        };

        window.ComputeDailyBudget(workDayStartHour: 1, nowLocal: now).Should().BeNull();
    }

    #endregion

    #region ComputeDailyBudget - WorkDayStartHour

    [TestMethod]
    public void ComputeDailyBudget_WithDefaultWorkDayStart_Uses1AM()
    {
        // Reset in 7 days, 0% used, window = 7 days
        // Work days: Feb 10 01:00, Feb 11 01:00, ..., Feb 16 01:00 (7 work days until reset)
        var now = new DateTime(2025, 2, 10, 12, 0, 0); // Monday noon
        var resetAt = new DateTime(2025, 2, 17, 0, 30, 0); // Next Monday 00:30 (before 01:00, so day 7 ends)
        var window = new UsageWindow
        {
            Used = 0,
            Limit = 100,
            WindowMinutes = 7 * 24 * 60 - 30, // ~7 days
            ResetAt = resetAt.ToUniversalTime()
        };

        var budget = window.ComputeDailyBudget(workDayStartHour: 1, nowLocal: now);

        // Day 1 of 7, should get ~14.3% (100/7)
        budget.Should().BeApproximately(14.3, 0.1);
    }

    [TestMethod]
    public void ComputeDailyBudget_WithWorkDayStart0_UsesMidnight()
    {
        // At 00:30, with workDayStart=0, we're in the new day
        // At 00:30, with workDayStart=1, we're still in the previous day
        var now = new DateTime(2025, 2, 11, 0, 30, 0); // Tuesday 00:30
        var resetAt = new DateTime(2025, 2, 17, 12, 0, 0); // Next Monday noon
        var window = new UsageWindow
        {
            Used = 0,
            Limit = 100,
            WindowMinutes = 7 * 24 * 60,
            ResetAt = resetAt.ToUniversalTime()
        };

        var budgetAt0 = window.ComputeDailyBudget(workDayStartHour: 0, nowLocal: now);
        var budgetAt1 = window.ComputeDailyBudget(workDayStartHour: 1, nowLocal: now);

        // With workDayStart=0, we're in day 2 (Tuesday started at midnight)
        // With workDayStart=1, we're still in day 1 (Tuesday starts at 1:00)
        budgetAt0.Should().BeGreaterThan(budgetAt1!.Value);
    }

    [TestMethod]
    public void ComputeDailyBudget_WithWorkDayStart6_Uses6AM()
    {
        // At 05:00, with workDayStart=6, we're still in the previous day
        // At 07:00, with workDayStart=6, we're in the new day
        var nowBefore = new DateTime(2025, 2, 11, 5, 0, 0); // Tuesday 05:00
        var nowAfter = new DateTime(2025, 2, 11, 7, 0, 0);  // Tuesday 07:00
        var resetAt = new DateTime(2025, 2, 17, 12, 0, 0);

        var window = new UsageWindow
        {
            Used = 0,
            Limit = 100,
            WindowMinutes = 7 * 24 * 60,
            ResetAt = resetAt.ToUniversalTime()
        };

        var budgetBefore = window.ComputeDailyBudget(workDayStartHour: 6, nowLocal: nowBefore);
        var budgetAfter = window.ComputeDailyBudget(workDayStartHour: 6, nowLocal: nowAfter);

        // After 6:00, new day started, so cumulative allowed is higher
        budgetAfter.Should().BeGreaterThan(budgetBefore!.Value);
    }

    #endregion

    #region ComputeDailyBudget - Usage Scenarios

    [TestMethod]
    public void ComputeDailyBudget_WhenFullyUsed_ReturnsZero()
    {
        var now = new DateTime(2025, 2, 10, 12, 0, 0);
        var window = new UsageWindow
        {
            Used = 100,
            Limit = 100,
            WindowMinutes = 7 * 24 * 60,
            ResetAt = now.AddDays(3).ToUniversalTime()
        };

        var budget = window.ComputeDailyBudget(workDayStartHour: 1, nowLocal: now);

        budget.Should().Be(0);
    }

    [TestMethod]
    public void ComputeDailyBudget_WhenOverused_ReturnsZero()
    {
        var now = new DateTime(2025, 2, 10, 12, 0, 0);
        var window = new UsageWindow
        {
            Used = 120, // Over limit
            Limit = 100,
            WindowMinutes = 7 * 24 * 60,
            ResetAt = now.AddDays(3).ToUniversalTime()
        };

        var budget = window.ComputeDailyBudget(workDayStartHour: 1, nowLocal: now);

        budget.Should().Be(0);
    }

    [TestMethod]
    public void ComputeDailyBudget_WhenAheadOfPace_ReturnsZero()
    {
        // Day 1 of 7, but already used 50% (way ahead of 14% pace)
        var now = new DateTime(2025, 2, 10, 12, 0, 0);
        var resetAt = new DateTime(2025, 2, 17, 12, 0, 0);
        var window = new UsageWindow
        {
            Used = 50,
            Limit = 100,
            WindowMinutes = 7 * 24 * 60,
            ResetAt = resetAt.ToUniversalTime()
        };

        var budget = window.ComputeDailyBudget(workDayStartHour: 1, nowLocal: now);

        // Already used more than allowed for day 1, budget is 0
        budget.Should().Be(0);
    }

    [TestMethod]
    public void ComputeDailyBudget_LastDay_ReturnsRemaining()
    {
        // Last day of cycle, 80% used, should get remaining 20%
        var now = new DateTime(2025, 2, 17, 10, 0, 0);
        var resetAt = new DateTime(2025, 2, 17, 12, 0, 0);
        var window = new UsageWindow
        {
            Used = 80,
            Limit = 100,
            WindowMinutes = 7 * 24 * 60,
            ResetAt = resetAt.ToUniversalTime()
        };

        var budget = window.ComputeDailyBudget(workDayStartHour: 1, nowLocal: now);

        // Last day, cumulative allowed = 100%, remaining = 20%
        budget.Should().BeApproximately(20, 0.1);
    }

    #endregion

    #region ComputeDailyBudget - Fallback (no WindowMinutes)

    [TestMethod]
    public void ComputeDailyBudget_WithoutWindowMinutes_DividesEvenlyByDays()
    {
        var now = new DateTime(2025, 2, 10, 12, 0, 0);
        var resetAt = new DateTime(2025, 2, 14, 12, 0, 0); // 4 days away
        var window = new UsageWindow
        {
            Used = 0,
            Limit = 100,
            WindowMinutes = 0, // Unknown window
            ResetAt = resetAt.ToUniversalTime()
        };

        var budget = window.ComputeDailyBudget(workDayStartHour: 1, nowLocal: now);

        // 100% remaining / 4 days = 25%
        budget.Should().BeApproximately(25, 0.1);
    }

    #endregion

    #region GetDailyBudgetText

    [TestMethod]
    public void GetDailyBudgetText_FormatsCorrectly()
    {
        // Use UTC directly to avoid timezone issues
        var resetAtUtc = DateTime.UtcNow.AddDays(7);
        var window = new UsageWindow
        {
            Used = 0,
            Limit = 100,
            WindowMinutes = 7 * 24 * 60,
            ResetAt = resetAtUtc
        };

        var text = window.GetDailyBudgetText(workDayStartHour: 1);

        text.Should().MatchRegex(@"\(\d+\.\d%\)");
    }

    [TestMethod]
    public void GetDailyBudgetText_WhenNoBudget_ReturnsEmpty()
    {
        var window = new UsageWindow { Used = 0, Limit = 100, ResetAt = null };

        window.GetDailyBudgetText().Should().BeEmpty();
    }

    #endregion
}
