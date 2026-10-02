namespace RentApp.Domain.Properties;

/// <summary>What the owner sees for a bed: the spec's Vacant / Occupied / Reserved / Unavailable.</summary>
public enum BedOccupancy
{
    Vacant = 1,
    Occupied = 2,
    Reserved = 3,
    Unavailable = 4,
}

public static class Occupancy
{
    /// <summary>
    /// The single rule for a bed's occupancy. An active tenancy always wins (a tenant living there is
    /// occupied, whatever the flag says); otherwise an unavailable room makes every bed unavailable.
    /// Archived beds and rooms are excluded before this is called.
    /// </summary>
    public static BedOccupancy Derive(BedStatus bed, RoomStatus room, bool hasActiveTenancy)
    {
        if (hasActiveTenancy)
        {
            return BedOccupancy.Occupied;
        }

        if (room == RoomStatus.Unavailable || bed == BedStatus.Unavailable)
        {
            return BedOccupancy.Unavailable;
        }

        return bed == BedStatus.Reserved ? BedOccupancy.Reserved : BedOccupancy.Vacant;
    }

    /// <summary>Default labels for a new room's beds: A, B, C … Z, then 27, 28 … for very large dorms.</summary>
    public static string DefaultBedLabel(int index) =>
        index < 26 ? ((char)('A' + index)).ToString() : (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The first default label not already used in the room.</summary>
    public static string NextBedLabel(IReadOnlyCollection<string> usedLabels)
    {
        var used = new HashSet<string>(usedLabels, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; ; i++)
        {
            var label = DefaultBedLabel(i);
            if (!used.Contains(label))
            {
                return label;
            }
        }
    }
}

/// <summary>Bed counts for a room, a property or the whole organization.</summary>
public sealed record OccupancySummary(int TotalBeds, int Occupied, int Vacant, int Reserved, int Unavailable)
{
    public static readonly OccupancySummary Empty = new(0, 0, 0, 0, 0);

    public static OccupancySummary From(IEnumerable<BedOccupancy> beds)
    {
        int occupied = 0, vacant = 0, reserved = 0, unavailable = 0;
        foreach (var bed in beds)
        {
            switch (bed)
            {
                case BedOccupancy.Occupied: occupied++; break;
                case BedOccupancy.Vacant: vacant++; break;
                case BedOccupancy.Reserved: reserved++; break;
                case BedOccupancy.Unavailable: unavailable++; break;
            }
        }

        return new OccupancySummary(occupied + vacant + reserved + unavailable, occupied, vacant, reserved, unavailable);
    }
}
