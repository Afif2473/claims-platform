namespace Claims.Api.Models;

/// <summary>
/// The six APAC markets the platform serves, and the currency each one settles in.
/// The currency is derived from the market rather than supplied by the claimant,
/// so a claim can never carry a mismatched market/currency pair.
/// </summary>
public static class Markets
{
    public static readonly IReadOnlyDictionary<string, string> CurrencyByMarket = new Dictionary<string, string>
    {
        ["SG"] = "SGD",
        ["MY"] = "MYR",
        ["HK"] = "HKD",
        ["TH"] = "THB",
        ["ID"] = "IDR",
        ["AU"] = "AUD"
    };

    public static bool IsSupported(string? marketCode) =>
        marketCode is not null && CurrencyByMarket.ContainsKey(marketCode.ToUpperInvariant());
}
