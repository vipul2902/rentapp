namespace RentApp.Domain.Common;

/// <summary>Turns an instant into the calendar date in an organization's time zone.</summary>
public static class OrganizationCalendar
{
    /// <summary>
    /// "Today" for an organization. Rent due dates and move-in/out are calendar dates in the owner's
    /// time zone, so 00:30 IST on the 5th is already the 5th even though it is the 4th in UTC.
    /// Unknown time zone ids fall back to UTC rather than failing.
    /// </summary>
    public static DateOnly Today(DateTimeOffset now, string timeZoneId)
    {
        var zone = TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var found) ? found : TimeZoneInfo.Utc;
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
    }
}
