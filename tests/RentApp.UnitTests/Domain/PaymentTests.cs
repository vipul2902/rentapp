using RentApp.Domain.Payments;

namespace RentApp.UnitTests.Domain;

public class PaymentTests
{
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly Guid C = Guid.NewGuid();

    private static PaymentAllocator.Outstanding O(Guid id, decimal balance) => new(id, balance);

    [Fact]
    public void FullPaymentSettlesTheOldestChargeFirst()
    {
        var result = PaymentAllocator.Allocate(8500m, [O(A, 8500m), O(B, 8500m)]);

        Assert.Equal([new PaymentAllocator.Allocation(A, 8500m)], result);
    }

    [Fact]
    public void PartialPaymentLeavesABalance()
    {
        // Spec §9: charge 8,500, payment 5,000 → outstanding 3,500.
        var result = PaymentAllocator.Allocate(5000m, [O(A, 8500m)]);

        Assert.Equal(5000m, Assert.Single(result).Amount);
    }

    [Fact]
    public void ALargePaymentSpillsOverIntoLaterCharges()
    {
        var result = PaymentAllocator.Allocate(12000m, [O(A, 8500m), O(B, 8500m), O(C, 8500m)]);

        Assert.Equal([new PaymentAllocator.Allocation(A, 8500m), new PaymentAllocator.Allocation(B, 3500m)], result);
    }

    [Fact]
    public void PaisaAmountsAreAllocatedExactly()
    {
        var result = PaymentAllocator.Allocate(100.10m, [O(A, 33.33m), O(B, 33.33m), O(C, 33.44m)]);

        Assert.Equal([33.33m, 33.33m, 33.44m], result.Select(a => a.Amount));
        Assert.Equal(100.10m, result.Sum(a => a.Amount));
    }

    [Fact]
    public void SettledChargesAreSkipped()
    {
        var result = PaymentAllocator.Allocate(1000m, [O(A, 0m), O(B, 1000m)]);

        Assert.Equal(B, Assert.Single(result).ChargeId);
    }

    [Fact]
    public void AllocationCanNeverExceedThePaymentOrTheBalance()
    {
        Assert.Throws<InvalidOperationException>(() => PaymentAllocator.Allocate(9000m, [O(A, 8500m)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => PaymentAllocator.Allocate(0m, [O(A, 8500m)]));
    }

    [Theory]
    [InlineData(2026, 1, "REC-2026-000001")]
    [InlineData(2026, 123, "REC-2026-000123")]
    [InlineData(2027, 1234567, "REC-2027-1234567")]
    public void ReceiptNumbersAreZeroPadded(int year, long sequence, string expected)
    {
        Assert.Equal(expected, Receipt.FormatNumber(year, sequence));
    }

    [Fact]
    public void PeriodLabelNamesOneMonthOrARange()
    {
        Assert.Equal("October 2026", Receipt.PeriodLabelFor([new DateOnly(2026, 10, 1)]));
        Assert.Equal("August 2026 – October 2026", Receipt.PeriodLabelFor([new DateOnly(2026, 10, 1), new DateOnly(2026, 8, 1), new DateOnly(2026, 9, 1)]));
    }

    [Fact]
    public void VoidingRecordsWhoAndWhyAndCannotRepeat()
    {
        var payment = Payment.Record(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 10, 2), 8500m, PaymentMethod.Upi, " UPI123 ", " ", null, null, DateTimeOffset.UtcNow);
        Assert.Equal(("UPI123", (string?)null), (payment.ReferenceNumber, payment.Notes));

        var by = Guid.NewGuid();
        payment.Void("Recorded twice", by, DateTimeOffset.UtcNow);

        Assert.Equal((PaymentStatus.Voided, "Recorded twice", by), (payment.Status, payment.VoidReason!, payment.VoidedByUserId!.Value));
        Assert.Throws<InvalidOperationException>(() => payment.Void("Again", by, DateTimeOffset.UtcNow));
    }
}
