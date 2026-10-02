using System.ComponentModel.DataAnnotations;

namespace RentApp.Application.Common.Validation;

/// <summary>
/// A positive rupee amount with at most two decimal places that fits numeric(12,2). Null is allowed;
/// combine with [Required] when the amount is mandatory.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class MoneyAttribute : ValidationAttribute
{
    public const decimal Max = 9_999_999_999.99m;

    public MoneyAttribute()
        : base("Enter an amount greater than zero, with at most 2 decimal places.")
    {
    }

    /// <summary>Allow 0 (e.g. no security deposit).</summary>
    public bool AllowZero { get; init; }

    public override bool IsValid(object? value) =>
        value is null
        || (value is decimal amount
            && (AllowZero ? amount >= 0 : amount > 0)
            && amount <= Max
            && decimal.Round(amount, 2) == amount);

    public override string FormatErrorMessage(string name) =>
        AllowZero ? "Enter zero or a positive amount, with at most 2 decimal places." : base.FormatErrorMessage(name);
}
