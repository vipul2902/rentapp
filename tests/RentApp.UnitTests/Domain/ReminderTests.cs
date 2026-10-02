using RentApp.Domain.Reminders;

namespace RentApp.UnitTests.Domain;

public class ReminderTests
{
    private static readonly DateOnly Today = new(2026, 10, 12);

    [Theory]
    [InlineData("2026-10-16", null)] // 4 days ahead: too early
    [InlineData("2026-10-15", ReminderType.Upcoming)]
    [InlineData("2026-10-13", ReminderType.Upcoming)]
    [InlineData("2026-10-12", ReminderType.DueToday)]
    [InlineData("2026-10-11", ReminderType.Overdue)]
    [InlineData("2026-10-06", ReminderType.Overdue)] // 6 days late
    [InlineData("2026-10-05", ReminderType.LongOverdue)] // 7 days late
    [InlineData("2026-08-05", ReminderType.LongOverdue)]
    public void TheStageFollowsTheDueDate(string due, ReminderType? expected)
    {
        Assert.Equal(expected, ReminderRules.TypeFor(DateOnly.Parse(due, System.Globalization.CultureInfo.InvariantCulture), Today));
    }

    [Fact]
    public void EachStageIsSuggestedOnceAndOverdueWaitsForTheGracePeriod()
    {
        (ReminderType, DateOnly)[] none = [];
        (ReminderType, DateOnly)[] sentUpcoming = [(ReminderType.Upcoming, Today.AddDays(-2))];

        Assert.True(ReminderRules.IsSuggested(ReminderType.Upcoming, 0, none, Today));
        Assert.False(ReminderRules.IsSuggested(ReminderType.Upcoming, 0, sentUpcoming, Today));
        // An upcoming reminder does not stop the due-today one.
        Assert.True(ReminderRules.IsSuggested(ReminderType.DueToday, 0, sentUpcoming, Today));
        Assert.False(ReminderRules.IsSuggested(ReminderType.Overdue, 2, none, Today));
        Assert.True(ReminderRules.IsSuggested(ReminderType.Overdue, 3, none, Today));
    }

    [Fact]
    public void LongOverdueComesBackEveryWeek()
    {
        (ReminderType, DateOnly)[] sixDaysAgo = [(ReminderType.LongOverdue, Today.AddDays(-6))];
        (ReminderType, DateOnly)[] weekAgo = [(ReminderType.LongOverdue, Today.AddDays(-7))];

        Assert.False(ReminderRules.IsSuggested(ReminderType.LongOverdue, 14, sixDaysAgo, Today));
        Assert.True(ReminderRules.IsSuggested(ReminderType.LongOverdue, 14, weekAgo, Today));
    }

    [Fact]
    public void MessagesAreReadyToSend()
    {
        var input = new ReminderRules.MessageInput("Rahul Sharma", 8500m, false, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 5), 7, "Sunrise PG");

        Assert.Equal(
            "Hi Rahul, your rent of ₹8,500 for October 2026 is due today. Please make the payment at your earliest convenience. Thank you! – Sunrise PG",
            ReminderRules.Message(ReminderType.DueToday, input));
        Assert.Equal(
            "Hi Rahul, a friendly reminder that your rent of ₹8,500 for October 2026 is due on 5 Oct 2026. Thank you! – Sunrise PG",
            ReminderRules.Message(ReminderType.Upcoming, input));
        Assert.Contains("is 7 days overdue (due on 5 Oct 2026)", ReminderRules.Message(ReminderType.LongOverdue, input), StringComparison.Ordinal);
        Assert.Contains("the remaining rent of ₹3,500.50 for October 2026 was due on 5 Oct 2026 and is now 1 day overdue",
            ReminderRules.Message(ReminderType.Overdue, input with { Balance = 3500.50m, PartlyPaid = true, DaysOverdue = 1 }), StringComparison.Ordinal);
    }

    [Fact]
    public void SharingCountsAsSentButCopyingNeedsConfirmation()
    {
        var at = DateTimeOffset.UtcNow;
        var shared = Reminder.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ReminderType.DueToday, " Hi ", ReminderChannel.WhatsApp, null, Today, at);
        var copied = Reminder.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ReminderType.DueToday, "Hi", ReminderChannel.Copy, null, Today, at);

        Assert.Equal((ReminderStatus.Sent, at, "Hi"), (shared.Status, shared.SentAt!.Value, shared.Message));
        Assert.Equal((ReminderStatus.Prepared, (DateTimeOffset?)null), (copied.Status, copied.SentAt));
        Assert.True(copied.MarkSent(at));
        Assert.False(copied.MarkSent(at));
        Assert.Equal(ReminderStatus.Sent, copied.Status);
    }
}
