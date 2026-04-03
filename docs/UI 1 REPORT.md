# UI 1 REPORT — WPF Foundation

**Phase:** UI 1 — WPF Foundation  
**Status:** ✅ COMPLETE  
**Date:** 2026-03-02  
**Build:** 0 Errors, 0 Warnings (full solution)

---

## What Was Implemented

### 1. WPF Project Structure
- New `ElshazlyStore.Desktop` project (`net8.0-windows`, WPF)
- Added to solution under `src/` folder
- Separate from backend — no project references to Domain/Infrastructure/Api
- UI communicates only via typed API client (HTTP)

### 2. App Shell
- **MainWindow** with three-region layout:
  - **TopBar** (48px): App title + theme toggle button (sun/moon vector icons)
  - **Sidebar** (220px): Scrollable nav with section headers (MAIN / COMMERCE placeholder), bottom Settings button
  - **Content Region**: DataTemplate-driven, swaps ViewModels automatically
- Minimum size 900×600, default 1200×780, centered on screen

### 3. DPI / Scaling
- `app.manifest` with **Per-Monitor V2 DPI awareness**
- `UseLayoutRounding="True"` + `SnapsToDevicePixels="True"` on Window and all pages  
- No fixed pixel sizes that break layout — all sizing is flexible (MinWidth/MaxWidth, margins/padding)
- Ready for 100% / 125% / 150% scaling

### 4. Dark Mode + Light Mode
- Two `ResourceDictionary` files: `DarkTheme.xaml`, `LightTheme.xaml`
- All brushes use `DynamicResource` for runtime swapping
- `SharedStyles.xaml` — shared styles, fonts, spacing tokens, button styles
- `ThemeService` swaps the theme dictionary at index 0 at runtime
- **User preference persisted** to `%LOCALAPPDATA%\ElshazlyStore\preferences.json`
- Toggle via ToggleButton in TopBar or CheckBox in Settings page

### 5. MVVM Architecture
- `CommunityToolkit.Mvvm` (source generators) for `ObservableProperty`, `RelayCommand`
- `ViewModelBase` → `MainViewModel`, `HomeViewModel`, `SettingsViewModel`
- DataTemplate mapping in `MainWindow.Resources` (implicit DataTemplates)
- No code-behind logic — all in ViewModels and services

### 6. Dependency Injection
- `Microsoft.Extensions.DependencyInjection` configured in `App.xaml.cs`
- Registered services:
  - `INavigationService` → `NavigationService`
  - `IThemeService` → `ThemeService`
  - `IUserPreferencesService` → `UserPreferencesService`
  - `IMessageService` → `MessageService`
  - `ITokenStore` → `InMemoryTokenStore` (stub)
  - `ApiClient` (typed HttpClient)
  - All ViewModels (transient)
  - `MainWindow` (transient)

### 7. Navigation Service
- `INavigationService` with generic `NavigateTo<TViewModel>()`
- Resolves ViewModels from DI container
- `CurrentPageChanged` event drives the `ContentControl` binding in shell
- Easy to extend — just register new ViewModel + add DataTemplate + nav button

### 8. API Client Infrastructure
- `ApiClient` — typed `HttpClient` with `Get/Post/Put/Delete` async methods
- Base URL from `appsettings.json` (`Api:BaseUrl`)
- `AuthHeaderHandler` — adds `Bearer` token when available (DelegatingHandler)
- `CorrelationIdHandler` — adds `X-Correlation-Id` header to every request
- `ProblemDetails` model with `ToUserMessage()` for RFC 7807 parsing
- `ApiResult<T>` generic wrapper for success/failure responses
- `IMessageService` for standardized user-facing error/info/confirm dialogs

### 9. Logging
- **Serilog** with Console + rolling File sinks
- Log path from config: `logs/elshazly-desktop-{Date}.log`
- 14-day retention
- `CorrelationIdHandler` logs outgoing requests with correlation IDs
- Startup/shutdown logged

### 10. Publish Profiles
- `win-x64.pubxml` and `win-x86.pubxml` under `Properties/PublishProfiles/`
- Self-contained, single-file publish
- No trimming (WPF not trim-compatible)

---

## Project Structure

```
src/ElshazlyStore.Desktop/
├── ElshazlyStore.Desktop.csproj
├── App.xaml                        # Resource dictionaries (theme + styles)
├── App.xaml.cs                     # DI setup, Serilog, startup
├── app.manifest                    # Per-Monitor V2 DPI awareness
├── appsettings.json                # API base URL, logging config
│
├── Helpers/
│   ├── NavItemActiveConverter.cs
│   ├── PageDataTemplateSelector.cs
│   └── StringEqualityToVisibilityConverter.cs
│
├── Models/
│   ├── ApiResult.cs                # Generic API response wrapper
│   └── ProblemDetails.cs           # RFC 7807 error model
│
├── Resources/
│   └── Themes/
│       ├── DarkTheme.xaml          # Dark color palette + brushes
│       ├── LightTheme.xaml         # Light color palette + brushes
│       └── SharedStyles.xaml       # Fonts, spacing, button styles
│
├── Services/
│   ├── IMessageService.cs
│   ├── MessageService.cs
│   ├── INavigationService.cs
│   ├── NavigationService.cs
│   ├── IThemeService.cs
│   ├── ThemeService.cs
│   ├── IUserPreferencesService.cs
│   ├── UserPreferencesService.cs
│   └── Api/
│       ├── ApiClient.cs            # Typed HttpClient for backend
│       ├── AuthHeaderHandler.cs    # Bearer token injection
│       ├── CorrelationIdHandler.cs # X-Correlation-Id header
│       ├── ITokenStore.cs
│       └── InMemoryTokenStore.cs   # Stub — no real auth yet
│
├── ViewModels/
│   ├── ViewModelBase.cs
│   ├── MainViewModel.cs           # Shell: nav + theme commands
│   ├── HomeViewModel.cs
│   └── SettingsViewModel.cs
│
├── Views/
│   ├── MainWindow.xaml             # Shell: TopBar + Sidebar + Content
│   ├── MainWindow.xaml.cs
│   └── Pages/
│       ├── HomePage.xaml
│       ├── HomePage.xaml.cs
│       ├── SettingsPage.xaml
│       └── SettingsPage.xaml.cs
│
└── Properties/
    └── PublishProfiles/
        ├── win-x64.pubxml
        └── win-x86.pubxml
```

---

## How to Run

```bash
# From solution root:
dotnet run --project src/ElshazlyStore.Desktop
```

Or open `ElshazlyStore.sln` in Visual Studio and set `ElshazlyStore.Desktop` as startup project.

---

## How to Publish

### x64 (recommended)
```bash
dotnet publish src/ElshazlyStore.Desktop/ElshazlyStore.Desktop.csproj \
  -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true \
  -o artifacts/win-x64
```

### x86
```bash
dotnet publish src/ElshazlyStore.Desktop/ElshazlyStore.Desktop.csproj \
  -c Release -r win-x86 --self-contained true -p:PublishSingleFile=true \
  -o artifacts/win-x86
```

### Using publish profiles
```bash
dotnet publish src/ElshazlyStore.Desktop/ElshazlyStore.Desktop.csproj \
  /p:PublishProfile=win-x64

dotnet publish src/ElshazlyStore.Desktop/ElshazlyStore.Desktop.csproj \
  /p:PublishProfile=win-x86
```

Output: single self-contained `.exe` + `appsettings.json` in the output folder.

---

## DPI Implementation

| Technique | Where |
|-----------|-------|
| Per-Monitor V2 awareness | `app.manifest` |
| `UseLayoutRounding="True"` | MainWindow, all Pages |
| `SnapsToDevicePixels="True"` | MainWindow, all Pages, all Borders |
| No hardcoded sizes that break | All layouts use Min/Max + relative sizing |
| Vector icons | All nav icons are `Path` geometry (resolution-independent) |

---

## Theme Implementation

| Aspect | Detail |
|--------|--------|
| Theme files | `DarkTheme.xaml`, `LightTheme.xaml` (index 0 in MergedDictionaries) |
| Shared styles | `SharedStyles.xaml` (index 1, always loaded) |
| Runtime switch | `ThemeService.ApplyTheme()` replaces dictionary at index 0 |
| Binding | All colors via `DynamicResource` (updates instantly) |
| Persistence | `%LOCALAPPDATA%\ElshazlyStore\preferences.json` |
| UI controls | ToggleButton (TopBar) + CheckBox (Settings page) |

---

## Notes & Constraints

1. **No app icon (`.ico`)** included yet — removed from csproj to avoid build error. Add one later.
2. **No Login UI** — only the API client infrastructure is wired. `InMemoryTokenStore` is a stub.
3. **No business screens** — only Home (welcome card) and Settings pages exist.
4. **x86 publish** works but is not tested at runtime — WPF on x86 is fully supported by .NET 8.
5. **PublishTrimmed is disabled** — WPF is not trim-safe; self-contained publish without trimming produces a larger exe but is reliable.
6. **Backend must be running** for API calls to succeed (not exercised in UI 1 — no business screens make API calls yet).

---

**STOP — UI 1 Foundation is complete. Do not proceed to UI 2 without explicit approval.**
