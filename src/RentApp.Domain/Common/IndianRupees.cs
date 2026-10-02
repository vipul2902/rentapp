using System.Globalization;
using System.Text;

namespace RentApp.Domain.Common;

/// <summary>
/// Rupee amounts the way Indian receipts print them: lakh/crore grouping ("₹1,25,000.00") and words
/// ("Rupees One Lakh Twenty-Five Thousand Only"). Independent of the server's culture settings.
/// </summary>
public static class IndianRupees
{
    private static readonly string[] Ones =
    [
        "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten",
        "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen",
    ];

    private static readonly string[] Tens = ["", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety"];

    /// <summary>"₹8,500.00"; with <paramref name="alwaysShowPaise"/> false, whole rupees print as "₹8,500".</summary>
    public static string Format(decimal amount, bool alwaysShowPaise = true)
    {
        var rounded = decimal.Round(Math.Abs(amount), 2, MidpointRounding.AwayFromZero);
        var rupees = decimal.Truncate(rounded);
        var paise = (int)((rounded - rupees) * 100);
        var digits = rupees.ToString("0", CultureInfo.InvariantCulture);

        // The last three digits form one group; everything before is grouped in twos.
        var grouped = new StringBuilder();
        if (digits.Length <= 3)
        {
            grouped.Append(digits);
        }
        else
        {
            var head = digits[..^3];
            for (var i = 0; i < head.Length; i++)
            {
                if (i > 0 && (head.Length - i) % 2 == 0)
                {
                    grouped.Append(',');
                }

                grouped.Append(head[i]);
            }

            grouped.Append(',').Append(digits[^3..]);
        }

        var sign = amount < 0 ? "-" : string.Empty;
        return paise == 0 && !alwaysShowPaise
            ? $"{sign}₹{grouped}"
            : string.Create(CultureInfo.InvariantCulture, $"{sign}₹{grouped}.{paise:D2}");
    }

    public static string InWords(decimal amount)
    {
        var rounded = decimal.Round(Math.Abs(amount), 2, MidpointRounding.AwayFromZero);
        var rupees = (long)decimal.Truncate(rounded);
        var paise = (int)((rounded - rupees) * 100);

        var words = $"Rupees {Words(rupees)}";
        if (paise > 0)
        {
            words += $" and {Words(paise)} Paise";
        }

        return words + " Only";
    }

    private static string Words(long number)
    {
        if (number < 20)
        {
            return Ones[number];
        }

        var parts = new List<string>();
        void Take(long unit, string name)
        {
            if (number >= unit)
            {
                parts.Add($"{Words(number / unit)} {name}");
                number %= unit;
            }
        }

        Take(10_000_000, "Crore");
        Take(100_000, "Lakh");
        Take(1_000, "Thousand");
        Take(100, "Hundred");
        if (number > 0)
        {
            parts.Add(number < 20 ? Ones[number] : Tens[number / 10] + (number % 10 > 0 ? "-" + Ones[number % 10] : string.Empty));
        }

        return string.Join(' ', parts);
    }
}
