using Microsoft.EntityFrameworkCore;
using RentApp.Application.Common.Abstractions;
using RentApp.Application.Common.Security;
using RentApp.Domain.Payments;
using RentApp.Domain.Users;

namespace RentApp.Application.Payments;

/// <summary>Renders a receipt as a PDF. Implemented in Infrastructure.</summary>
public interface IReceiptPdfRenderer
{
    byte[] Render(ReceiptDto receipt);
}

public sealed record ReceiptPdf(string FileName, byte[] Content);

/// <summary>Viewing and downloading receipts. Allowed for staff who can record payments or generate receipts.</summary>
public sealed class ReceiptService(IAppDbContext db, ICurrentUser currentUser, IReceiptPdfRenderer renderer)
{
    public const StaffPermissions Access = StaffPermissions.RecordPayments | StaffPermissions.GenerateReceipts;

    public async Task<ReceiptDto> GetAsync(Guid receiptId, CancellationToken cancellationToken)
    {
        currentUser.EnsureAnyPermission(Access);
        return await Project(db.Receipts.AsNoTracking().Where(r => r.Id == receiptId), db).SingleOrDefaultAsync(cancellationToken)
               ?? throw PaymentErrors.ReceiptNotFound();
    }

    public async Task<ReceiptPdf> GetPdfAsync(Guid receiptId, CancellationToken cancellationToken)
    {
        var receipt = await GetAsync(receiptId, cancellationToken);
        return new ReceiptPdf($"{receipt.ReceiptNumber}.pdf", renderer.Render(receipt));
    }

    internal static IQueryable<ReceiptDto> Project(IQueryable<Receipt> receipts, IAppDbContext db) =>
        from r in receipts
        join p in db.Payments on r.PaymentId equals p.Id
        select new ReceiptDto(
            r.Id, r.PaymentId, r.ReceiptNumber, r.GeneratedAt, r.IssuedOn, r.OrganizationName, r.PropertyName, r.PropertyAddress,
            r.PropertyContactPhone, r.TenantName, r.TenantPhone, r.RoomNumber, r.BedLabel, r.PeriodLabel, r.Amount,
            r.PaymentDate, r.Method, r.ReferenceNumber, p.Status == PaymentStatus.Voided);
}
