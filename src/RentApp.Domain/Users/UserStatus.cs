namespace RentApp.Domain.Users;

public enum UserStatus
{
    Active = 1,
    Disabled = 2,

    /// <summary>The person deleted their account: personal details are erased and it can never sign in again.</summary>
    Deleted = 3,
}
