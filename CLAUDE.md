# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

PlayniteBrowser is a Playnite library plugin that allows users to add browser-based games to their Playnite game library. The plugin enables launching web games/applications through a configured browser. The plugin supports both Chromium-based browsers (e.g., Brave, Chrome, Edge) and Firefox.

## Build Commands

This is a .NET Framework 4.6.2 project using MSBuild.

**IMPORTANT: DO NOT build the project. The user will handle all build operations.**

Build commands for reference only:

```bash
# Build in Debug mode (default)
msbuild PlayniteBrowser.sln /p:Configuration=Debug

# Build in Release mode
msbuild PlayniteBrowser.sln /p:Configuration=Release

# Clean and rebuild
msbuild PlayniteBrowser.sln /t:Clean,Build

# Restore NuGet packages
nuget restore PlayniteBrowser.sln
```

Output directory:
- Debug: `bin\Debug\`
- Release: `bin\Release\`

## Architecture

### Plugin Structure

This is a Playnite LibraryPlugin that follows the Playnite SDK plugin architecture:

1. **PlayniteBrowser.cs** - Main plugin class implementing `LibraryPlugin`
   - ID: `3c58c307-b26e-4569-855a-e19afa4a3b2f` (must match extension.yaml)
   - Implements `GetGames()` to convert browser game entries into Playnite game metadata
   - Implements `GetPlayActions()` to create AutomaticPlayController instances that launch games with browser-specific arguments
   - Downloads favicons using Google's favicon service and caches them in `ExtensionsDataPath/Browser/Icons`
   - Scrapes website metadata for descriptions and og:image background images
   - Each game gets its own isolated browser profile in `ExtensionsDataPath/Browser/Profiles/{BrowserType}/{GameName-GameIdPrefix}`
   - `GetProfilePath(settings, game)` creates browser-type-specific profile directories with sanitized folder names

2. **PlayniteBrowserSettings.cs** - Contains four key classes/enums:
   - `BrowserType`: Enum defining supported browser types (Chromium, Firefox)
   - `BrowserGame`: Model for individual browser games (Name, URL, GameId)
     - GameId is computed as SHA256 hash of the URL (not serialized)
     - Inherits from ObservableObject for property change notifications
   - `PlayniteBrowserSettings`: Settings storage
     - `BrowserExecutablePath`: Path to browser executable (default: Brave)
     - `BrowserType`: Selected browser type (default: Chromium)
     - `BrowserGames`: ObservableCollection of browser games
     - `UseSharedProfile`: Whether to use a shared profile for all games
   - `PlayniteBrowserSettingsViewModel`: ViewModel implementing ISettings pattern with BeginEdit/EndEdit/CancelEdit lifecycle
     - Provides RelayCommands: BrowseCommand, AddGameCommand, RemoveGameCommand
     - Validates browser executable existence in VerifySettings()

3. **PlayniteBrowserClient.cs** - LibraryClient implementation (currently minimal, just returns IsInstalled=true)

4. **PlayniteBrowserSettingsView.xaml** - WPF settings UI
   - TextBox and Browse button for selecting browser executable path
   - ComboBox dropdown for selecting BrowserType (Chromium or Firefox)
   - CheckBox for enabling shared browser profile
   - DataGrid bound to BrowserGames collection with Name and URL columns
   - Add/Remove buttons for managing games

### Data Flow

- Settings are persisted via Playnite's `LoadPluginSettings<T>()` and `SavePluginSettings<T>()` methods
- When `GetGames()` is called:
  1. Plugin loads saved BrowserGame entries
  2. Creates unique profile directories for each game
  3. Downloads favicons (cached) and scrapes website descriptions
  4. Converts to GameMetadata with icon, description, links, and platform metadata
- When launching a game via `GetPlayActions()`:
  1. Finds the BrowserGame by GameId
  2. Gets the profile path for the game (browser-type-specific)
  3. Constructs browser-specific launch arguments
  4. Returns AutomaticPlayController with:
     - Path: Browser executable
     - Arguments: Browser-specific (see below)
     - TrackingMode: Process
- Game installation status is determined by checking if the browser executable exists

### Browser Launch Configuration

The plugin supports two browser types with different launch arguments:

**Chromium-based browsers** (Chrome, Brave, Edge, etc.):
- `--user-data-dir="{profile_path}"`: Isolates each game's cookies, cache, and settings
- `--app={url}`: Launches in PWA (Progressive Web App) mode without browser chrome
- Full arguments: `--user-data-dir="{profile_path}" --app={url}`

**Firefox**:
- `-new-instance`: Starts a new Firefox instance
- `-profile "{profile_path}"`: Uses a custom profile directory for isolation
- `-kiosk {url}`: Launches in kiosk/fullscreen mode
- Full arguments: `-new-instance -profile "{profile_path}" -kiosk {url}`

Profile paths are structured as: `ExtensionsDataPath/Browser/Profiles/{BrowserType}/{GameName-GameIdPrefix}` or `ExtensionsDataPath/Browser/Profiles/{BrowserType}/Shared` when UseSharedProfile is enabled.

### Key Design Patterns

- **MVVM**: Settings UI uses ViewModel pattern with ObservableObject base
- **Plugin Lifecycle**: Implements ISettings interface for edit/save/cancel/verify workflow
- **Playnite SDK Integration**: Uses MetadataProperty, GameAction, and GameMetadata from SDK

### Extension Configuration

The `extension.yaml` file defines plugin metadata:
- Type: GameLibrary (not GenericPlugin)
- Module: ChromiumBrowser.dll
- Icon and localization resources are copied to output directory

### Localization

Localization resources are stored in `Localization\*.xaml` as WPF ResourceDictionary files. The project currently has an empty `en_US.xaml` file ready for string resources.
