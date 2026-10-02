namespace RentApp.Domain.Organizations;

public enum OrganizationStatus
{
    Active = 1,
    Suspended = 2,

    /// <summary>The owner deleted their account. Nobody can sign in; records are kept (financial history is permanent).</summary>
    Closed = 3,
}
