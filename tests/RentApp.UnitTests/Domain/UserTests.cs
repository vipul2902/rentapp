using RentApp.Domain.Users;

namespace RentApp.UnitTests.Domain;

public class UserTests
{
    private static readonly Guid Org = Guid.NewGuid();

    [Fact]
    public void EmailIsTrimmedForDisplayAndNormalizedForLookup()
    {
        var user = User.CreateStaff(Org, "  Ravi  ", "  Ravi.K@Example.com ", " ", "hash", StaffPermissions.None);

        Assert.Equal("Ravi", user.Name);
        Assert.Equal("Ravi.K@Example.com", user.Email);
        Assert.Equal("RAVI.K@EXAMPLE.COM", user.NormalizedEmail);
        Assert.Null(user.Phone);
    }

    [Fact]
    public void OwnerHasEveryPermissionWithoutStoringAny()
    {
        var owner = User.CreateOwner(Org, "Asha", "asha@example.com", null, "hash");

        Assert.Equal(StaffPermissions.None, owner.Permissions);
        Assert.Equal(StaffPermissions.All, owner.EffectivePermissions);
    }

    [Fact]
    public void OwnerCannotBeDisabledOrRestricted()
    {
        var owner = User.CreateOwner(Org, "Asha", "asha@example.com", null, "hash");

        Assert.Throws<InvalidOperationException>(owner.Disable);
        Assert.Throws<InvalidOperationException>(() => owner.SetPermissions(StaffPermissions.ViewTenants));
    }

    [Fact]
    public void UndefinedPermissionBitsAreDropped()
    {
        var staff = User.CreateStaff(Org, "Ravi", "r@example.com", null, "hash", (StaffPermissions)0xFF);

        Assert.Equal(StaffPermissions.All, staff.Permissions);
    }

    [Fact]
    public void StaffCanBeDisabledAndEnabled()
    {
        var staff = User.CreateStaff(Org, "Ravi", "r@example.com", null, "hash", StaffPermissions.RecordPayments);

        staff.Disable();
        Assert.False(staff.IsActive);
        staff.Enable();
        Assert.True(staff.IsActive);
    }
}
