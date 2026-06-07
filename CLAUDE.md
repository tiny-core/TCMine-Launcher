# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

TCMine-Launcher is a desktop Minecraft launcher targeting **NeoForge** modloader builds. It is built with **Avalonia 12** (cross-platform XAML UI) on **.NET 10**, using the **CommunityToolkit.Mvvm** source generators for MVVM. Game launching, file installation, and authentication are provided by the **CmlLib.Core** family of packages (`CmlLib.Core`, `CmlLib.Core.Installer.NeoForge`, `CmlLib.Core.Auth.Microsoft` + `XboxAuthNet.Game.Msal`).

> Note: backend logic is being wired up incrementally. **Microsoft login is real** — `MainWindowViewModel` delegates to `Services/AuthService` (CmlLib `JELoginHandler` via `JELoginHandlerBuilder.BuildDefault()`), which does interactive system-browser auth, silent re-login from cache on startup, and real sign-out. The resulting `MSession` is held in the shell for launching. **Still fake:** `HomePageViewModel.PlayAsync` runs a simulated progress sequence (no CmlLib download/launch yet). The `Models/Minecraft.cs` and `Models/Auth.cs` classes are empty placeholders.

### Services

`Services/AuthService.cs` is the first of a planned services layer. It wraps the CmlLib auth handler and exposes `LoginAsync` (silent→browser), `LoginSilentAsync` (startup), and `SignOutAsync`, all taking a `CancellationToken` (the login command wires a Cancel button + 5-min timeout). On Linux the MSAL default WebUI doesn't exist, so it builds the auth pipeline manually (`CreateAuthenticator… + AddMsalOAuth(app, msal => msal.SystemBrowser()/.Silent())`) — see the `msal-linux-auth` memory. The VM maps the returned `MSession` (`CmlLib.Core.Auth`, from CmlLib.Core.Commons) onto the pure `PlayerProfile` — Models never reference CmlLib. Auth tokens persist via MSAL's cross-platform cache, so login survives restarts.

`Services/AppConfig.cs` reads the Azure **client ID** (a public-client ID, not a secret) solely from a value baked into the assembly at build time — no env vars, no external config files. The value comes from a gitignored `TCMine-Launcher/Client.props` (`<MicrosoftClientId>`) that the `.csproj` imports and emits as an `AssemblyMetadataAttribute` (also overridable via `dotnet build/publish -p:MicrosoftClientId=…`). This keeps the ID out of git while embedding it in both dev and production builds. See `Client.props.example`; a missing/empty value surfaces a clear "not configured" error at login.

## Commands

```bash
# Build (solution at repo root)
dotnet build

# Run the app (project lives in the TCMine-Launcher/ subfolder)
dotnet run --project TCMine-Launcher

# Release build
dotnet build -c Release
```

There is **no test project** in the repo yet — the Models are deliberately UI-free so they can be unit-tested once a test project is added.

## Architecture

Strict **MVVM** with a clean three-layer separation that is enforced by convention and documented heavily in the source comments (the comments are in Portuguese). When editing, respect these layer boundaries — they are the central design rule of this codebase:

- **`Models/`** — pure domain data and business logic. **Zero** dependencies on Avalonia or UI. These are plain serializable objects (`PlayerProfile`, `GameProfile`, `LaunchProgress`/`LaunchState`). Domain-derived values (e.g. `GameProfile.JvmMemoryArgs`, `PlayerProfile.ComputeInitials()`) live here, not in the ViewModel. Decisions about *what* gets launched belong here.

- **`ViewModels/`** — owns Model instances, exposes them to the UI via `[ObservableProperty]`/`[RelayCommand]` (CommunityToolkit source generators), holds **UI-only state** that must not leak into Models (e.g. `IsLaunching`, `LaunchProgress`). On changes from the UI it syncs back to the Model via the generated `partial void OnXxxChanged(...)` hooks. ViewModels never reference Avalonia controls. All ViewModels derive from `ViewModelBase : ObservableObject`.

- **`Views/`** — AXAML + minimal code-behind. Code-behind may contain **only** OS-window concerns (custom title-bar drag, minimize, close — see `MainWindow.axaml.cs`). No launch logic, version logic, or profile state. The View communicates with the ViewModel exclusively through `{Binding ...}` and `DataContext`.

### Navigation & view resolution

`MainWindowViewModel` is the **shell**: it owns the shared `PlayerProfile`/`GameProfile`, an `IsLoggedIn` flag (Login screen vs. app), and a `SelectedTab` (`AppTab` enum) that drives `CurrentPage`. The window swaps screens with a single `<ContentControl Content="{Binding CurrentPage}"/>`; `LoginView` is an overlay toggled by `IsVisible="{Binding !IsLoggedIn}"`. Sidebar buttons call `NavigateCommand` with an `{x:Static vm:AppTab.X}` parameter and highlight the active tab via `Classes.active="{Binding IsXSelected}"` bindings.

`ViewLocator` (`IDataTemplate`, registered in `App.axaml`) resolves each page VM to its View by **string convention**: it replaces `"ViewModel"` → `"View"` in the full type name, so `ViewModels.HomePageViewModel` → `Views.HomePageView`. Every page View is a `UserControl` and its VM derives from `ViewModelBase` (required for `ViewLocator.Match`). New screens must follow the `XxxPageViewModel` → `XxxPageView` naming and be registered as a tab/page in `MainWindowViewModel`. The shell window itself (`MainWindow`) is instantiated directly in `App.OnFrameworkInitializationCompleted`, not via the locator.

Page VMs receive the shared models through their constructors. When login mutates the player, the shell calls each page's `NotifyPlayerChanged()` so computed bindings (avatar initials, name, account label) refresh — there is no global property-change propagation.

### Bindings

`AvaloniaUseCompiledBindingsByDefault` is **enabled** in the `.csproj`. Bindings are compiled and type-checked against the `DataContext` type, so set `x:DataType` in AXAML and use strongly-typed binding paths.

### Conventions

- The app uses a custom (chromeless) window with a hand-built title bar; window controls are wired in `MainWindow.axaml.cs`.
- Source comments and user-facing strings are in **Portuguese** — match this when adding to existing files.
- `Program.cs` / `BuildAvaloniaApp` enables `WithDeveloperTools()` only under `#if DEBUG` (Avalonia dev tools / DiagnosticsSupport).
