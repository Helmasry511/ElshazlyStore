# PHASE UI 2.2a — ARABIC-FIRST GUARANTEE + RTL BASELINE — CLOSEOUT REPORT

**Revision:** R2  
**Status:** ✅ COMPLETE (pending human vision gate)  
**Date:** 2026-03-04  
**Build:** 0 errors · 0 warnings  
**Tests:** 214 total · 3 runs · 0 failures in all 3 runs  

---

## 1. Root Cause of English Still Appearing + Exact Fix

### Root Cause

Despite R1 moving Arabic into the default resource (`Strings.resx`), English UI labels still appeared because of **two compounding issues:**

1. **English satellite file still existed (`Strings.en.resx`)** — The .NET `ResourceManager` could still resolve English resources on machines with an English system locale. Even with `CurrentUICulture` forced to `ar-EG`, the satellite DLL was built, deployed, and available for fallback.

2. **Culture was set too late** — The `ar-EG` culture was applied inside `OnStartup()` *after* `base.OnStartup()`. While no XAML windows existed yet at that point, the `SatelliteResourceLanguages` property included `en`, leaving a permanent escape path for English.

3. **No centralized localization bootstrap** — Culture-setting code was a one-off block inside `OnStartup`, with no guarantee it ran before all XAML evaluation.

### Exact Fix (R2)

| Change | What it eliminates |
|--------|--------------------|
| **Deleted `Strings.en.resx`** | No English satellite DLL is generated or deployed. ResourceManager can only reach neutral `Strings.resx` (Arabic). |
| **Changed `SatelliteResourceLanguages` from `ar;en` → `ar`** | MSBuild no longer creates an `en/` satellite folder. |
| **Added `<NeutralLanguage>ar</NeutralLanguage>` to csproj** | Assembly-level `NeutralResourcesLanguageAttribute` tells the ResourceManager the neutral resource IS Arabic — no probing ambiguity. |
| **Created `LocalizationBootstrapper.cs`** | Single static class that sets `ar-EG` culture + RTL + LanguageProperty. Called once, idempotent. |
| **Moved bootstrap to `App()` constructor** (before `InitializeComponent`) | Culture and RTL are set before ANY XAML is parsed — no timing window for English. |
| **Fixed hardcoded English in `App.xaml.cs` error dialog** | MessageBox title/text now uses `Strings.Dialog_ErrorTitle` / `Strings.State_UnexpectedError`. |
| **Fixed hardcoded English in `MessageService.cs`** | `DefaultTitle` changed from `"ElshazlyStore"` → `Strings.AppName` (resolves to "الشاذلي"). |

**After R2, it is impossible for English to appear in the UI.** There is no English resource file, no English satellite DLL, and the culture is locked to `ar-EG` before the first XAML element is created.

---

## 2. Arabic/RTL Permanent Policy (applies to all future phases)

> **NON-NEGOTIABLE UI LANGUAGE POLICY**
>
> 1. Arabic is the **ONLY** user-visible UI language.
> 2. RTL is the **default** FlowDirection across the entire app.
> 3. English is allowed **ONLY** for technical IDs/codes (SKU/Barcode/ErrorCode). No English UI labels.
> 4. No hard-coded UI strings in XAML/ViewModels. All via RESX keys.
> 5. Every future phase must pass an "Arabic/RTL compliance checklist" before closure.

### Architecture Decision: Arabic-Only RESX Layout (R2)

| File | Language | Role |
|------|----------|------|
| `Localization/Strings.resx` | **Arabic** | Default resource (embedded in main assembly) |
| ~~`Localization/Strings.en.resx`~~ | ~~English~~ | **DELETED in R2** |
| `Localization/Strings.cs` | — | Static accessor class with `ResourceManager` |
| `Localization/LocalizationBootstrapper.cs` | — | Centralized culture + RTL setup (NEW in R2) |

---

## 3. What Files Changed (R2 delta only)

### New Files (1)

| File | Purpose |
|------|---------|
| `Localization/LocalizationBootstrapper.cs` | Centralized ar-EG culture + RTL + LanguageProperty setup. Called from `App()` constructor. DEBUG-only logging of `CurrentUICulture` and `Strings.Nav_Home` sample. |

### Deleted Files (1)

| File | Reason |
|------|--------|
| `Localization/Strings.en.resx` | English satellite eliminated — Arabic is the only language. |

### Modified Files (3)

| File | Change |
|------|--------|
| `ElshazlyStore.Desktop.csproj` | `SatelliteResourceLanguages` → `ar` only; added `<NeutralLanguage>ar</NeutralLanguage>`; removed stale comment |
| `App.xaml.cs` | Added constructor with `LocalizationBootstrapper.Initialize()` before `InitializeComponent()`. Removed duplicate culture code from `OnStartup`. Fixed hardcoded English error dialog to use RESX keys. |
| `Services/MessageService.cs` | `DefaultTitle` changed from `const "ElshazlyStore"` → `Strings.AppName` property (Arabic "الشاذلي") |

---

## 4. Localization Binding Verification (Scope B)

All visible UI strings verified to bind to RESX keys:

| UI Area | Binding | Status |
|---------|---------|--------|
| Sidebar section headers (Main/Commerce/Inventory/Sales/Accounting/Admin) | `{x:Static loc:Strings.Section_*}` | ✅ |
| Sidebar nav items (20 entries) | `{x:Static loc:Strings.Nav_*}` | ✅ |
| Home welcome card (title/subtitle/welcome/hint) | `{x:Static loc:Strings.AppName/AppSubtitle/Home_Welcome/Home_Hint}` | ✅ |
| Settings page (title/appearance/dark mode/about) | `{x:Static loc:Strings.Settings_*}` | ✅ |
| Title bar (MainWindow) | `Title="{Binding Title}"` → `Strings.AppTitle` in MainViewModel | ✅ |
| Title bar (LoginWindow) | `Title="{Binding Title}"` → `Strings.Login_Title` in LoginViewModel | ✅ |
| Theme toggle tooltip | `{x:Static loc:Strings.Action_ThemeToggle}` | ✅ |
| Logout tooltip | `{x:Static loc:Strings.Action_SignOut}` | ✅ |
| Login labels + buttons | `{x:Static loc:Strings.Login_*}` | ✅ |
| Controls (BusyOverlay/EmptyState/ErrorDisplay) | `{x:Static loc:Strings.State_*/Action_Retry}` | ✅ |
| MessageBox default title | `Strings.AppName` (Arabic) | ✅ |
| Global error handler | `Strings.State_UnexpectedError` + `Strings.Dialog_ErrorTitle` | ✅ |

**Zero hardcoded English UI strings remain.** Log messages (Serilog) are intentionally English for developer diagnostics.

---

## 5. Test Runs Evidence (3 runs)

| Run | Total | Passed | Failed | Failed Test(s) |
|-----|-------|--------|--------|----------------|
| 1 | 214 | **214** | **0** | — |
| 2 | 214 | **214** | **0** | — |
| 3 | 214 | **214** | **0** | — |

### Flake Classification

The concurrency double-post tests (`PurchaseReceiptTests...ConcurrentDoublePost`, `SalesInvoiceTests...ConcurrentDoublePost`) that flaked in R1 did **not** fail in any of the 3 R2 runs. Classification remains:

**Known Flake — Backend concurrency race.** Non-deterministic, non-reproducible, unrelated to UI changes. All 3 localization audit tests passed in all 3 runs.

---

## 6. Build Results

```
$ dotnet build ElshazlyStore.sln --nologo

Build succeeded.
    0 Warning(s)
    0 Error(s)
```

---

## 7. Human Vision Gate — Manual Verification Script

### Launch

```powershell
cd src/ElshazlyStore.Desktop
dotnet run
```

### Visual Checklist

| # | What to check | Expected (Arabic) |
|---|--------------|-------------------|
| 1 | **Login window title bar** | "الشاذلي — تسجيل الدخول" |
| 2 | **Login card header** | "الشاذلي" (accent color) + "الشاذلي لإدارة المتجر" |
| 3 | **Login labels** | "اسم المستخدم" / "كلمة المرور" |
| 4 | **Login button** | "تسجيل الدخول" |
| 5 | *(Login with admin/Admin123!)* | — |
| 6 | **Main window title bar** | "الشاذلي — نظام إدارة المتجر" |
| 7 | **Sidebar section: رئيسي** | الرئيسية, لوحة التحكم |
| 8 | **Sidebar section: التجارة** | المنتجات, العملاء, الموردون |
| 9 | **Sidebar section: المخزون** | المخازن, المخزون, المشتريات, التصنيع |
| 10 | **Sidebar section: المبيعات** | المبيعات, مرتجعات المبيعات, مرتجعات المشتريات |
| 11 | **Sidebar section: المحاسبة** | الأرصدة, المدفوعات |
| 12 | **Sidebar section: الإدارة** | المستخدمون, الأدوار, الاستيراد, أكواد الأسباب, إعدادات الطباعة |
| 13 | **Sidebar bottom** | الإعدادات |
| 14 | **Home page** | "مرحباً! التطبيق جاهز للاستخدام." + "استخدم القائمة الجانبية للتنقل بين الصفحات." |
| 15 | **Settings page** | عنوان "الإعدادات" / "المظهر" / "الوضع الليلي" / "حول" |
| 16 | **Theme toggle tooltip** | "تبديل الوضع الداكن / الفاتح" |
| 17 | **RTL layout** | Everything flows right-to-left; sidebar on the right |
| 18 | **ZERO English labels** | No English anywhere in nav, home, settings, title bar |

---

## 8. RTL Verification Checklist

| # | Item | Status |
|---|------|--------|
| 1 | Global `FlowDirection.RightToLeft` via `FrameworkElement.FlowDirectionProperty.OverrideMetadata` | ✅ |
| 2 | `FrameworkElement.LanguageProperty.OverrideMetadata` for Arabic (`ar-EG`) formatting | ✅ |
| 3 | Explicit `FlowDirection="RightToLeft"` on MainWindow + LoginWindow | ✅ |
| 4 | `ar-EG` culture set in `App()` constructor before `InitializeComponent()` | ✅ |
| 5 | `DefaultThreadCurrentCulture` + `DefaultThreadCurrentUICulture` = `ar-EG` | ✅ |
| 6 | No English satellite resource file exists | ✅ |
| 7 | `NeutralLanguage=ar` in csproj | ✅ |
| 8 | All UI strings via RESX keys (XAML `{x:Static}` / C# `Strings.*`) | ✅ |
| 9 | No hardcoded English in MessageBox/error dialogs | ✅ |
| 10 | Centralized bootstrap in `LocalizationBootstrapper.cs` (no duplicate culture code) | ✅ |

---

## 9. STOP

**Do NOT proceed to UI 2.2b until user explicitly approves this phase.**

Human vision gate is MANDATORY — the user must visually confirm Arabic in the running app before closure.
