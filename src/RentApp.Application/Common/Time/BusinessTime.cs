namespace RentApp.Application.Common.Time;

/// <summary>
/// The clock that decides business dates (what "today" is for rent and tenancies). Registered as a keyed
/// <see cref="TimeProvider"/> that defaults to the system clock, so tests can fix the business date
/// without affecting token lifetimes or audit timestamps.
/// </summary>
public static class BusinessTime
{
    public const string Key = "business";
}
