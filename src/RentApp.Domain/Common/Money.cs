namespace RentApp.Domain.Common;

/// <summary>Rupee amount rules shared by every entity that stores money (numeric(12,2)).</summary>
public static class Money
{
    public const decimal Max = 9_999_999_999.99m;

    public static bool HasValidScale(decimal amount) => decimal.Round(amount, 2) == amount;

    public static decimal EnsurePositive(decimal amount, string paramName)
    {
        if (amount is <= 0 or > Max || !HasValidScale(amount))
        {
            throw new ArgumentOutOfRangeException(paramName, amount, "Amount must be positive with at most 2 decimal places.");
        }

        return amount;
    }

    public static decimal EnsureNonNegative(decimal amount, string paramName)
    {
        if (amount is < 0 or > Max || !HasValidScale(amount))
        {
            throw new ArgumentOutOfRangeException(paramName, amount, "Amount must be zero or positive with at most 2 decimal places.");
        }

        return amount;
    }
}
