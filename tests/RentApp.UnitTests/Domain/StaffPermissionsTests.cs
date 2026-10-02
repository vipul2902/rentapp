using RentApp.Domain.Users;

namespace RentApp.UnitTests.Domain;

public class StaffPermissionsTests
{
    [Fact]
    public void ToListReturnsIndividualFlagsInStableOrder()
    {
        var list = (StaffPermissions.SendReminders | StaffPermissions.ViewProperties).ToList();

        Assert.Equal([StaffPermissions.ViewProperties, StaffPermissions.SendReminders], list);
    }

    [Fact]
    public void AllExpandsToEveryIndividualPermissionAndNoneToNothing()
    {
        Assert.Equal(5, StaffPermissions.All.ToList().Count);
        Assert.Empty(StaffPermissions.None.ToList());
    }

    [Fact]
    public void CombineRoundTripsAndIgnoresUndefinedBits()
    {
        var combined = StaffPermissionsExtensions.Combine([StaffPermissions.RecordPayments, StaffPermissions.GenerateReceipts, (StaffPermissions)1024]);

        Assert.Equal(StaffPermissions.RecordPayments | StaffPermissions.GenerateReceipts, combined);
        Assert.Equal(combined, StaffPermissionsExtensions.Combine(combined.ToList()));
    }

    [Fact]
    public void PermissionValuesNeverChange()
    {
        // Stored as integers in users.permissions: renumbering would silently change people's access.
        Assert.Equal(1, (int)StaffPermissions.ViewProperties);
        Assert.Equal(2, (int)StaffPermissions.ViewTenants);
        Assert.Equal(4, (int)StaffPermissions.RecordPayments);
        Assert.Equal(8, (int)StaffPermissions.GenerateReceipts);
        Assert.Equal(16, (int)StaffPermissions.SendReminders);
    }
}
