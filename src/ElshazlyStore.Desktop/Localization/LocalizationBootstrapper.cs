using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Windows;

namespace ElshazlyStore.Desktop.Localization;

/// <summary>
/// Centralizes Arabic culture and RTL setup for the entire application.
/// Must be called once, as early as possible (before any XAML is parsed).
/// Idempotent — safe to call more than once.
/// </summary>
public static class LocalizationBootstrapper
{
    private static bool _initialized;

    /// <summary>
    /// Sets ar-EG culture on all threads and configures WPF for RTL layout.
    /// Call this BEFORE <c>InitializeComponent()</c> in <c>App</c>.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        // ── Force Arabic culture on every thread ──
        var arCulture = new CultureInfo("ar-EG");
        Thread.CurrentThread.CurrentCulture = arCulture;
        Thread.CurrentThread.CurrentUICulture = arCulture;
        CultureInfo.DefaultThreadCurrentCulture = arCulture;
        CultureInfo.DefaultThreadCurrentUICulture = arCulture;

        // ── RTL as default FlowDirection for all FrameworkElements ──
        FrameworkElement.FlowDirectionProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(FlowDirection.RightToLeft));

        // ── Arabic formatting (dates, numbers) for WPF ──
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(
                System.Windows.Markup.XmlLanguage.GetLanguage(arCulture.IetfLanguageTag)));

#if DEBUG
        Debug.WriteLine($"[LocalizationBootstrapper] CurrentUICulture = {CultureInfo.CurrentUICulture.Name}");
        Debug.WriteLine($"[LocalizationBootstrapper] Strings.Nav_Home  = {Strings.Nav_Home}");
#endif
    }
}
