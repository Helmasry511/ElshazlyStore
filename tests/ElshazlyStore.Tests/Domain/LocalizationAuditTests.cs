using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ElshazlyStore.Tests.Domain;

/// <summary>
/// Localization audit: prevents English regressions in the default (Arabic) RESX.
/// The default resource file (Strings.resx) MUST contain Arabic values for all
/// user-visible keys. This test enforces the Arabic-first UI policy.
/// </summary>
public partial class LocalizationAuditTests
{
    /// <summary>
    /// All navigation, settings, and state keys that MUST have Arabic values
    /// in the default RESX file. Brand names and technical IDs are excluded.
    /// </summary>
    private static readonly string[] ArabicRequiredKeys =
    [
        // Navigation sections
        "Section_Main", "Section_Commerce", "Section_Inventory",
        "Section_Sales", "Section_Accounting", "Section_Admin",

        // Navigation items
        "Nav_Home", "Nav_Dashboard", "Nav_Products", "Nav_Customers",
        "Nav_Suppliers", "Nav_Warehouses", "Nav_Stock", "Nav_Purchases",
        "Nav_Production", "Nav_Sales", "Nav_SalesReturns", "Nav_PurchaseReturns",
        "Nav_Balances", "Nav_Payments", "Nav_Users", "Nav_Roles",
        "Nav_Import", "Nav_ReasonCodes", "Nav_PrintConfig", "Nav_Settings",

        // Login
        "Login_Title", "Login_Subtitle", "Login_Username", "Login_Password",
        "Login_SignIn", "Login_SigningIn", "Login_PasswordRequired",

        // Home
        "Home_Welcome", "Home_Hint",

        // Settings
        "Settings_Title", "Settings_Appearance", "Settings_DarkMode",
        "Settings_About", "Settings_AboutLine1", "Settings_AboutLine2",

        // Actions
        "Action_SignOut", "Action_ThemeToggle", "Action_Retry",
        "Action_Save", "Action_Cancel", "Action_Delete", "Action_Edit",
        "Action_Create", "Action_Search", "Action_Confirm", "Action_Close",
        "Action_Yes", "Action_No",

        // States
        "State_Loading", "State_Empty", "State_Error",
        "State_UnexpectedError", "State_ConnectionError", "State_TimeoutError",

        // Paging
        "Paging_Page", "Paging_Of", "Paging_Items",
        "Paging_First", "Paging_Previous", "Paging_Next", "Paging_Last",

        // Dialogs
        "Dialog_ConfirmDelete", "Dialog_ConfirmTitle",
        "Dialog_ErrorTitle", "Dialog_InfoTitle",

        // Status badges
        "Status_Draft", "Status_Posted", "Status_Voided",
        "Status_Active", "Status_Inactive", "Status_Approved",
    ];

    [GeneratedRegex(@"[\u0600-\u06FF]")]
    private static partial Regex ArabicCharPattern();

    private static string FindResxFile()
    {
        // Walk up from test bin directory to find the workspace root, then
        // navigate to the Desktop project's Strings.resx.
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(dir!, "src", "ElshazlyStore.Desktop",
                "Localization", "Strings.resx");
            if (File.Exists(candidate))
                return candidate;
            dir = Path.GetDirectoryName(dir);
        }

        throw new FileNotFoundException(
            "Could not locate Strings.resx. Run tests from the workspace root.");
    }

    private static Dictionary<string, string> LoadResxValues(string path)
    {
        var doc = XDocument.Load(path);
        return doc.Descendants("data")
            .Where(d => d.Attribute("name") != null && d.Element("value") != null)
            .ToDictionary(
                d => d.Attribute("name")!.Value,
                d => d.Element("value")!.Value);
    }

    [Fact]
    public void DefaultResx_ContainsAllRequiredKeys()
    {
        var resxPath = FindResxFile();
        var values = LoadResxValues(resxPath);

        var missing = ArabicRequiredKeys
            .Where(k => !values.ContainsKey(k))
            .ToList();

        Assert.True(missing.Count == 0,
            $"Missing keys in Strings.resx: {string.Join(", ", missing)}");
    }

    [Fact]
    public void DefaultResx_AllRequiredKeysHaveArabicValues()
    {
        var resxPath = FindResxFile();
        var values = LoadResxValues(resxPath);
        var regex = ArabicCharPattern();

        var englishKeys = ArabicRequiredKeys
            .Where(k => values.TryGetValue(k, out var v) && !regex.IsMatch(v))
            .ToList();

        Assert.True(englishKeys.Count == 0,
            $"Keys with non-Arabic values in Strings.resx (violates Arabic-first policy): " +
            $"{string.Join(", ", englishKeys.Select(k => $"{k}='{values[k]}'"))}");
    }

    [Fact]
    public void DefaultResx_NoEmptyValues()
    {
        var resxPath = FindResxFile();
        var values = LoadResxValues(resxPath);

        var empty = ArabicRequiredKeys
            .Where(k => values.TryGetValue(k, out var v) && string.IsNullOrWhiteSpace(v))
            .ToList();

        Assert.True(empty.Count == 0,
            $"Keys with empty values in Strings.resx: {string.Join(", ", empty)}");
    }
}
