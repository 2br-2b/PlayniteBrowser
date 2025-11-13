# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

PlayniteBrowser is a Playnite library plugin that allows users to add browser-based games to their Playnite game library. The plugin enables launching web games/applications through a configured Chromium-based browser (e.g., Brave, Chrome, Edge).

## Build Commands

This is a .NET Framework 4.6.2 project using MSBuild:

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
   - Implements `GetPlayActions()` to create AutomaticPlayController instances that launch games
   - Downloads favicons using Google's favicon service and caches them in `ExtensionsDataPath/PlayniteBrowser/Icons`
   - Scrapes website metadata for descriptions and og:image background images
   - Each game gets its own isolated browser profile in `ExtensionsDataPath/PlayniteBrowser/Profiles/{GameId}`

2. **PlayniteBrowserSettings.cs** - Contains three key classes:
   - `BrowserGame`: Model for individual browser games (Name, URL, GameId)
     - GameId is computed as SHA256 hash of the URL (not serialized)
     - Inherits from ObservableObject for property change notifications
   - `PlayniteBrowserSettings`: Settings storage (BrowserExecutablePath, BrowserGames list)
     - Default browser path: `C:\Program Files\BraveSoftware\Brave-Browser\Application\brave.exe`
   - `PlayniteBrowserSettingsViewModel`: ViewModel implementing ISettings pattern with BeginEdit/EndEdit/CancelEdit lifecycle
     - Provides RelayCommands: BrowseCommand, AddGameCommand, RemoveGameCommand
     - Validates browser executable existence in VerifySettings()

3. **PlayniteBrowserClient.cs** - LibraryClient implementation (currently minimal, just returns IsInstalled=true)

4. **PlayniteBrowserSettingsView.xaml** - WPF settings UI
   - DataGrid bound to BrowserGames collection with Name and URL columns
   - Browse button for selecting browser executable
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
  2. Returns AutomaticPlayController with:
     - Path: Browser executable
     - Arguments: `--user-data-dir="{profile_path}" --app={url}`
     - TrackingMode: Process
- Game installation status is determined by checking if the browser executable exists

### Browser Launch Configuration

Each game launches with Chromium flags:
- `--user-data-dir`: Isolates each game's cookies, cache, and settings
- `--app`: Launches in PWA (Progressive Web App) mode without browser chrome

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
