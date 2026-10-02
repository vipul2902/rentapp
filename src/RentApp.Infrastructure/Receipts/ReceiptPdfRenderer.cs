using System.Globalization;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using RentApp.Application.Payments;
using RentApp.Domain.Payments;

namespace RentApp.Infrastructure.Receipts;

/// <summary>
/// Draws a one-page A4 receipt with PDFsharp. Everything shown comes from the receipt's stored snapshot,
/// so the PDF for a receipt is the same whenever it is downloaded (apart from the VOID mark).
/// </summary>
internal sealed class ReceiptPdfRenderer : IReceiptPdfRenderer
{
    private const double Margin = 40;
    private const string ProductName = "RentApp";

    private static readonly XColor Brand = XColor.FromArgb(0x5B, 0x3D, 0xF5);
    private static readonly XColor BrandTint = XColor.FromArgb(0xF1, 0xEE, 0xFF);
    private static readonly XColor Ink = XColor.FromArgb(0x1A, 0x16, 0x2E);
    private static readonly XColor Muted = XColor.FromArgb(0x6B, 0x67, 0x80);
    private static readonly XColor Rule = XColor.FromArgb(0xE4, 0xE1, 0xEE);
    private static readonly XColor Danger = XColor.FromArgb(0xD9, 0x2D, 0x20);

    static ReceiptPdfRenderer()
    {
        // PDFsharp needs fonts supplied explicitly (Linux containers have none it can find). This is a
        // process-wide setting that can only be made once, hence the static constructor.
        GlobalFontSettings.FontResolver ??= new EmbeddedFontResolver();
    }

    public byte[] Render(ReceiptDto receipt)
    {
        using var document = new PdfDocument();
        document.Info.Title = $"Receipt {receipt.ReceiptNumber}";
        document.Info.Author = receipt.OrganizationName;
        document.Info.Creator = ProductName;

        var page = document.AddPage();
        page.Size = PageSize.A4;
        using (var gfx = XGraphics.FromPdfPage(page))
        {
            Draw(gfx, page.Width.Point, page.Height.Point, receipt);
        }

        using var stream = new MemoryStream();
        document.Save(stream, false);
        return stream.ToArray();
    }

    private static void Draw(XGraphics gfx, double width, double height, ReceiptDto r)
    {
        var contentWidth = width - (2 * Margin);
        var white = XBrushes.White;
        var ink = new XSolidBrush(Ink);
        var muted = new XSolidBrush(Muted);

        // ---- Header band ----
        const double headerHeight = 118;
        gfx.DrawRectangle(new XSolidBrush(Brand), 0, 0, width, headerHeight);
        gfx.DrawString(r.OrganizationName, Font(20, bold: true), white, new XRect(Margin, 30, contentWidth * 0.62, 26), XStringFormats.TopLeft);
        var formatter = new XTextFormatter(gfx);
        formatter.DrawString($"{r.PropertyName}\n{r.PropertyAddress}", Font(9.5), white, new XRect(Margin, 60, contentWidth * 0.6, 44), XStringFormats.TopLeft);

        var right = new XRect(Margin, 30, contentWidth, 20);
        gfx.DrawString("RENT RECEIPT", Font(13, bold: true), white, right, XStringFormats.TopRight);
        right.Offset(0, 22);
        gfx.DrawString(r.ReceiptNumber, Font(11), white, right, XStringFormats.TopRight);
        right.Offset(0, 17);
        gfx.DrawString($"Date: {Date(r.IssuedOn)}", Font(10), white, right, XStringFormats.TopRight);

        var y = headerHeight + 28;
        if (r.IsVoid)
        {
            var banner = new XRect(Margin, y, contentWidth, 30);
            gfx.DrawRoundedRectangle(new XPen(Danger, 1), new XSolidBrush(XColor.FromArgb(0xFD, 0xEC, 0xEA)), banner, new XSize(8, 8));
            gfx.DrawString("This payment was voided. This receipt is no longer valid.", Font(10.5, bold: true), new XSolidBrush(Danger), banner, XStringFormats.Center);
            y += 46;
        }

        // ---- Received from / Stay ----
        gfx.DrawString("RECEIVED FROM", Font(8.5, bold: true), muted, Margin, y);
        gfx.DrawString("ROOM / BED", Font(8.5, bold: true), muted, Margin + (contentWidth * 0.6), y);
        y += 18;
        gfx.DrawString(r.TenantName, Font(15, bold: true), ink, Margin, y);
        gfx.DrawString($"Room {r.RoomNumber} · Bed {r.BedLabel}", Font(12, bold: true), ink, Margin + (contentWidth * 0.6), y);
        y += 16;
        gfx.DrawString(r.TenantPhone, Font(10), muted, Margin, y);
        y += 26;

        // ---- Details table ----
        var rows = new List<(string Label, string Value)>
        {
            ("Rent period", r.PeriodLabel),
            ("Payment date", Date(r.PaymentDate)),
            ("Payment method", MethodLabel(r.Method)),
        };
        if (!string.IsNullOrWhiteSpace(r.ReferenceNumber))
        {
            rows.Add(("Reference", r.ReferenceNumber));
        }

        const double rowHeight = 30;
        var rulePen = new XPen(Rule, 0.8);
        gfx.DrawLine(rulePen, Margin, y, Margin + contentWidth, y);
        foreach (var (label, value) in rows)
        {
            var cell = new XRect(Margin, y, contentWidth, rowHeight);
            gfx.DrawString(label, Font(10.5), muted, cell, XStringFormats.CenterLeft);
            gfx.DrawString(value, Font(10.5, bold: true), ink, cell, XStringFormats.CenterRight);
            y += rowHeight;
            gfx.DrawLine(rulePen, Margin, y, Margin + contentWidth, y);
        }

        // ---- Amount ----
        y += 22;
        var amountBox = new XRect(Margin, y, contentWidth, 96);
        gfx.DrawRoundedRectangle(new XSolidBrush(BrandTint), amountBox, new XSize(14, 14));
        gfx.DrawString("AMOUNT RECEIVED", Font(9, bold: true), new XSolidBrush(Brand), Margin + 20, y + 26);
        gfx.DrawString(IndianRupees.Format(r.Amount), Font(28, bold: true), ink, Margin + 20, y + 62);
        gfx.DrawString(IndianRupees.InWords(r.Amount), Font(9.5), muted, Margin + 20, y + 82);

        // ---- Footer ----
        var footerTop = height - 92;
        gfx.DrawLine(rulePen, Margin, footerTop, Margin + contentWidth, footerTop);
        gfx.DrawString("This is a computer-generated receipt and does not need a signature.", Font(9), muted, Margin, footerTop + 20);
        if (!string.IsNullOrWhiteSpace(r.PropertyContactPhone))
        {
            gfx.DrawString($"Questions about this receipt? Call {r.PropertyContactPhone}.", Font(9), muted, Margin, footerTop + 34);
        }

        gfx.DrawString($"Generated with {ProductName}", Font(8.5, bold: true), new XSolidBrush(Brand), new XRect(Margin, footerTop + 12, contentWidth, 12), XStringFormats.TopRight);

        if (r.IsVoid)
        {
            var state = gfx.Save();
            gfx.RotateAtTransform(-32, new XPoint(width / 2, height / 2));
            gfx.DrawString("VOID", Font(150, bold: true), new XSolidBrush(XColor.FromArgb(56, Danger.R, Danger.G, Danger.B)),
                new XRect(0, (height / 2) - 90, width, 180), XStringFormats.Center);
            gfx.Restore(state);
        }
    }

    private static XFont Font(double size, bool bold = false) =>
        new(EmbeddedFontResolver.Family, size, bold ? XFontStyleEx.Bold : XFontStyleEx.Regular, new XPdfFontOptions(PdfFontEncoding.Unicode));

    private static string Date(DateOnly date) => date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

    internal static string MethodLabel(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Cash",
        PaymentMethod.Upi => "UPI",
        PaymentMethod.BankTransfer => "Bank transfer",
        PaymentMethod.Card => "Card",
        _ => "Other",
    };
}

/// <summary>Serves the Noto Sans fonts embedded in this assembly for every font request.</summary>
internal sealed class EmbeddedFontResolver : IFontResolver
{
    public const string Family = "Noto Sans";
    private const string Regular = "NotoSans-Regular";
    private const string Bold = "NotoSans-Bold";

    private static readonly Lazy<byte[]> RegularBytes = new(() => Load(Regular));
    private static readonly Lazy<byte[]> BoldBytes = new(() => Load(Bold));

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic) =>
        new(bold ? Bold : Regular, mustSimulateBold: false, mustSimulateItalic: italic);

    public byte[]? GetFont(string faceName) => faceName switch
    {
        Regular => RegularBytes.Value,
        Bold => BoldBytes.Value,
        _ => null,
    };

    private static byte[] Load(string faceName)
    {
        using var stream = typeof(EmbeddedFontResolver).Assembly.GetManifestResourceStream($"RentApp.Fonts.{faceName}.ttf")
                           ?? throw new InvalidOperationException($"Embedded font {faceName} is missing.");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }
}
