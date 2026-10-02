using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentApp.Api.Auth;
using RentApp.Api.Errors;
using RentApp.Application.Common.Paging;
using RentApp.Application.Payments;
using RentApp.Domain.Users;

namespace RentApp.Api.Controllers;

/// <summary>Payments. Recording needs RecordPayments, reading needs ViewTenants, voiding is owner-only.</summary>
[ApiController]
[Route("api/v1/payments")]
[Produces("application/json")]
[ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
public sealed class PaymentsController(PaymentService payments) : ControllerBase
{
    public const string IdempotencyHeader = "Idempotency-Key";

    /// <summary>
    /// Records a payment and issues its receipt. The amount settles the tenant's oldest dues first and
    /// cannot exceed what they owe. Send a unique Idempotency-Key per payment: a retry with the same key
    /// returns the original payment instead of recording it again.
    /// </summary>
    [HttpPost]
    [RequirePermission(StaffPermissions.RecordPayments)]
    [ProducesResponseType<RecordPaymentResult>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RecordPaymentResult>> Record(
        RecordPaymentRequest request, [FromHeader(Name = IdempotencyHeader)] string? idempotencyKey, CancellationToken cancellationToken)
    {
        var result = await payments.RecordAsync(request, idempotencyKey, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Payment.Id }, result);
    }

    [HttpGet]
    [RequirePermission(StaffPermissions.ViewTenants)]
    [ProducesResponseType<PagedResult<PaymentSummary>>(StatusCodes.Status200OK)]
    public Task<PagedResult<PaymentSummary>> List([FromQuery] PaymentListQuery query, CancellationToken cancellationToken) =>
        payments.ListAsync(query, cancellationToken);

    [HttpGet("{id:guid}")]
    [RequirePermission(StaffPermissions.ViewTenants)]
    [ProducesResponseType<PaymentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<PaymentDto> Get(Guid id, CancellationToken cancellationToken) => payments.GetAsync(id, cancellationToken);

    /// <summary>Reverses a payment (owner only, with a reason). The record and its receipt are kept, marked VOID.</summary>
    [HttpPost("{id:guid}/void")]
    [Authorize(Policy = Policies.OwnerOnly)]
    [ProducesResponseType<PaymentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public Task<PaymentDto> Void(Guid id, VoidPaymentRequest request, CancellationToken cancellationToken) =>
        payments.VoidAsync(id, request, cancellationToken);
}

/// <summary>Receipts. Available to staff who can record payments or generate receipts.</summary>
[ApiController]
[Route("api/v1/receipts")]
[Produces("application/json")]
[ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
[RequirePermission(ReceiptService.Access)]
public sealed class ReceiptsController(ReceiptService receipts) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ReceiptDto>(StatusCodes.Status200OK)]
    public Task<ReceiptDto> Get(Guid id, CancellationToken cancellationToken) => receipts.GetAsync(id, cancellationToken);

    /// <summary>The receipt as a PDF, ready to share with the tenant.</summary>
    [HttpGet("{id:guid}/pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK, "application/pdf")]
    public async Task<IActionResult> Pdf(Guid id, CancellationToken cancellationToken)
    {
        var pdf = await receipts.GetPdfAsync(id, cancellationToken);
        // Receipts hold personal details: never let a shared cache keep them.
        Response.Headers.CacheControl = "private, no-store";
        return File(pdf.Content, "application/pdf", pdf.FileName);
    }
}
