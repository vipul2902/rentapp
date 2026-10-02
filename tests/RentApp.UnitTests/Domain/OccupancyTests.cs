using RentApp.Domain.Properties;

namespace RentApp.UnitTests.Domain;

public class OccupancyTests
{
    [Theory]
    [InlineData(BedStatus.Available, RoomStatus.Active, false, BedOccupancy.Vacant)]
    [InlineData(BedStatus.Reserved, RoomStatus.Active, false, BedOccupancy.Reserved)]
    [InlineData(BedStatus.Unavailable, RoomStatus.Active, false, BedOccupancy.Unavailable)]
    [InlineData(BedStatus.Available, RoomStatus.Unavailable, false, BedOccupancy.Unavailable)]
    [InlineData(BedStatus.Reserved, RoomStatus.Unavailable, false, BedOccupancy.Unavailable)]
    [InlineData(BedStatus.Available, RoomStatus.Active, true, BedOccupancy.Occupied)]
    [InlineData(BedStatus.Unavailable, RoomStatus.Unavailable, true, BedOccupancy.Occupied)]
    public void DeriveAppliesTheSingleOccupancyRule(BedStatus bed, RoomStatus room, bool tenancy, BedOccupancy expected)
    {
        Assert.Equal(expected, Occupancy.Derive(bed, room, tenancy));
    }

    [Fact]
    public void SummaryCountsEachState()
    {
        var summary = OccupancySummary.From(
            [BedOccupancy.Vacant, BedOccupancy.Vacant, BedOccupancy.Occupied, BedOccupancy.Reserved, BedOccupancy.Unavailable]);

        Assert.Equal(new OccupancySummary(TotalBeds: 5, Occupied: 1, Vacant: 2, Reserved: 1, Unavailable: 1), summary);
    }

    [Theory]
    [InlineData(0, "A")]
    [InlineData(2, "C")]
    [InlineData(25, "Z")]
    [InlineData(26, "27")]
    public void DefaultLabelsAreLettersThenNumbers(int index, string expected)
    {
        Assert.Equal(expected, Occupancy.DefaultBedLabel(index));
    }

    [Fact]
    public void NextLabelFillsTheFirstGap()
    {
        Assert.Equal("B", Occupancy.NextBedLabel(["A", "c"]));
        Assert.Equal("A", Occupancy.NextBedLabel([]));
        Assert.Equal("A", Occupancy.NextBedLabel(["WINDOW"]));
    }
}

public class RoomAndBedTests
{
    private static readonly Guid Org = Guid.NewGuid();

    [Fact]
    public void RoomCapacityRules()
    {
        var room = Room.Create(Org, Guid.NewGuid(), " 201 ", " AC ", 2);

        Assert.Equal("201", room.RoomNumber);
        Assert.Equal("AC", room.RoomType);
        Assert.True(room.CanAddBed(activeBedCount: 1));
        Assert.False(room.CanAddBed(activeBedCount: 2));
        Assert.Throws<InvalidOperationException>(() => room.Update("201", null, 1, RoomStatus.Active, activeBedCount: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => room.Update("201", null, 0, RoomStatus.Active, activeBedCount: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => room.Update("201", null, 51, RoomStatus.Active, activeBedCount: 0));

        room.Archive();
        Assert.False(room.CanAddBed(activeBedCount: 0));
    }

    [Fact]
    public void BedLabelIsNormalizedAndRentValidated()
    {
        var bed = Bed.Create(Org, Guid.NewGuid(), " window ", 8500.50m);

        Assert.Equal("WINDOW", bed.Label);
        Assert.Equal(8500.50m, bed.DefaultMonthlyRent);
        Assert.Throws<ArgumentOutOfRangeException>(() => bed.Update("A", BedStatus.Available, 0m));
        Assert.Throws<ArgumentException>(() => bed.Update("A", BedStatus.Available, 10.555m));
        Assert.Throws<InvalidOperationException>(() => bed.Update("A", BedStatus.Archived, null));
    }
}
