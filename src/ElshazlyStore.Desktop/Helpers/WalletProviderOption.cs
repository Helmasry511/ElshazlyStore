namespace ElshazlyStore.Desktop.Helpers;

/// <summary>
/// E-wallet provider definitions for the Egyptian market.
/// </summary>
public sealed class WalletProviderOption
{
    public WalletProviderOption(string value, string displayName, bool requiresBankName = false)
    {
        Value = value;
        DisplayName = displayName;
        RequiresBankName = requiresBankName;
    }

    public string Value { get; }
    public string DisplayName { get; }
    public bool RequiresBankName { get; }

    public static IReadOnlyList<WalletProviderOption> All { get; } =
    [
        new("Vodafone Cash", "فودافون كاش"),
        new("Orange Cash", "أورانج كاش"),
        new("Etisalat Cash", "اتصالات كاش"),
        new("WE Pay", "وي باي / WE Pay"),
        new("Bank Wallet", "محفظة بنكية", requiresBankName: true),
    ];
}
