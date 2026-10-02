namespace RentApp.Application.Common.Security;

/// <summary>JWT claim names issued by the API. Short names keep tokens small.</summary>
public static class AppClaimTypes
{
    public const string Subject = "sub";
    public const string Organization = "org";
    public const string Role = "role";

    /// <summary>One claim per granted staff permission (e.g. "RecordPayments"). Owners carry none.</summary>
    public const string Permission = "perm";
}
