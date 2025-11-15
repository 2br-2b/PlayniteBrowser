using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PlayniteBrowser
{
    public enum BrowserType
    {
        Chromium,
        Firefox
    }

    public class BrowserGame : ObservableObject
    {
        private string name = string.Empty;
        private string url = string.Empty;

        public string Name { get => name; set => SetValue(ref name, value); }
        public string Url { get => url; set => SetValue(ref url, value); }

        [DontSerialize]
        public string GameId
        {
            get
            {
                if (string.IsNullOrEmpty(url))
                    return string.Empty;

                using (var sha256 = SHA256.Create())
                {
                    var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(url));
                    return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                }
            }
        }
    }

    public class PlayniteBrowserSettings : ObservableObject
    {
        private string browserExecutablePath = @"C:\Program Files\BraveSoftware\Brave-Browser\Application\brave.exe";
        private ObservableCollection<BrowserGame> browserGames = new ObservableCollection<BrowserGame>();
        private bool useSharedProfile = false;
        private BrowserType browserType = BrowserType.Chromium;

        public string BrowserExecutablePath { get => browserExecutablePath; set => SetValue(ref browserExecutablePath, value); }
        public ObservableCollection<BrowserGame> BrowserGames { get => browserGames; set => SetValue(ref browserGames, value); }
        public bool UseSharedProfile { get => useSharedProfile; set => SetValue(ref useSharedProfile, value); }
        public BrowserType BrowserType { get => browserType; set => SetValue(ref browserType, value); }

        public static string GetProfilePath(string extensionsDataPath, BrowserType browserType, bool useSharedProfile, BrowserGame game)
        {
            var profilePath = System.IO.Path.Combine(extensionsDataPath, "Browser");
            profilePath = System.IO.Path.Combine(profilePath, "Profiles");
            profilePath = System.IO.Path.Combine(profilePath, browserType.ToString());

            if (useSharedProfile)
            {
                profilePath = System.IO.Path.Combine(profilePath, "Shared");
            }
            else
            {
                if (game == null)
                {
                    return null;
                }

                var folderName =
                    new string(game.Name
                        .Where(c => char.IsLetterOrDigit(c))
                        .Take(10)
                        .ToArray())
                    + "-" + game.GameId.Substring(0, 5);

                if (string.IsNullOrEmpty(folderName))
                {
                    folderName = "game";
                }

                profilePath = System.IO.Path.Combine(profilePath, folderName.ToString());
            }

            return profilePath;
        }
    }

    public class PlayniteBrowserSettingsViewModel : ObservableObject, ISettings
    {
        private readonly PlayniteBrowser plugin;
        private PlayniteBrowserSettings editingClone { get; set; }

        private PlayniteBrowserSettings settings;
        public PlayniteBrowserSettings Settings
        {
            get => settings;
            set
            {
                settings = value;
                OnPropertyChanged();
            }
        }

        private BrowserGame selectedGame;
        public BrowserGame SelectedGame
        {
            get => selectedGame;
            set
            {
                selectedGame = value;
                OnPropertyChanged();
            }
        }

        public RelayCommand<object> BrowseCommand
        {
            get => new RelayCommand<object>((a) =>
            {
                var selectedPath = plugin.PlayniteApi.Dialogs.SelectFile("Executable files|*.exe");
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    Settings.BrowserExecutablePath = selectedPath;
                }
            });
        }

        public RelayCommand<object> AddGameCommand
        {
            get => new RelayCommand<object>((a) =>
            {
                var newGame = new BrowserGame
                {
                    Name = "New Game",
                    Url = "https://"
                };
                Settings.BrowserGames.Add(newGame);
                SelectedGame = newGame;
            });
        }

        public RelayCommand<object> RemoveGameCommand
        {
            get => new RelayCommand<object>((a) =>
            {
                if (SelectedGame != null)
                {
                    Settings.BrowserGames.Remove(SelectedGame);
                    SelectedGame = null;
                }
            }, (a) => SelectedGame != null);
        }

        public RelayCommand<object> OpenProfileCommand
        {
            get => new RelayCommand<object>((a) =>
            {
                if (SelectedGame == null)
                    return;

                // Open the selected game's profile (always use individual profile, not shared)
                var profilePath = PlayniteBrowserSettings.GetProfilePath(
                    plugin.PlayniteApi.Paths.ExtensionsDataPath,
                    Settings.BrowserType,
                    false, // Always use individual profile for selected game
                    SelectedGame);

                if (!string.IsNullOrEmpty(profilePath))
                {
                    LaunchBrowserWithProfile(profilePath);
                }
            }, (a) => SelectedGame != null && !Settings.UseSharedProfile);
        }

        public RelayCommand<object> OpenSharedProfileCommand
        {
            get => new RelayCommand<object>((a) =>
            {
                // Open the shared profile
                var profilePath = PlayniteBrowserSettings.GetProfilePath(
                    plugin.PlayniteApi.Paths.ExtensionsDataPath,
                    Settings.BrowserType,
                    true, // Use shared profile
                    null);

                if (!string.IsNullOrEmpty(profilePath))
                {
                    LaunchBrowserWithProfile(profilePath);
                }
            }, (a) => Settings.UseSharedProfile);
        }

        private void LaunchBrowserWithProfile(string profilePath)
        {
            // Ensure the profile directory exists before launching browser
            if (!System.IO.Directory.Exists(profilePath))
            {
                System.IO.Directory.CreateDirectory(profilePath);
            }

            // Build browser arguments based on browser type
            string arguments;
            if (Settings.BrowserType == BrowserType.Chromium)
            {
                arguments = $"--user-data-dir=\"{profilePath}\"";
            }
            else // Firefox
            {
                arguments = $"-new-instance -profile \"{profilePath}\"";
            }

            // Launch the browser with the profile
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = Settings.BrowserExecutablePath,
                Arguments = arguments,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(startInfo);
        }

        public PlayniteBrowserSettingsViewModel(PlayniteBrowser plugin)
        {
            // Injecting your plugin instance is required for Save/Load method because Playnite saves data to a location based on what plugin requested the operation.
            this.plugin = plugin;

            // Load saved settings.
            var savedSettings = plugin.LoadPluginSettings<PlayniteBrowserSettings>();

            // LoadPluginSettings returns null if no saved data is available.
            if (savedSettings != null)
            {
                Settings = savedSettings;
            }
            else
            {
                Settings = new PlayniteBrowserSettings();
            }
        }

        public void BeginEdit()
        {
            // Code executed when settings view is opened and user starts editing values.
            editingClone = Serialization.GetClone(Settings);
        }

        public void CancelEdit()
        {
            // Code executed when user decides to cancel any changes made since BeginEdit was called.
            // This method should revert any changes made to Option1 and Option2.
            Settings = editingClone;
        }

        public void EndEdit()
        {
            // Code executed when user decides to confirm changes made since BeginEdit was called.
            // This method should save settings made to Option1 and Option2.
            plugin.SavePluginSettings(Settings);
        }

        public bool VerifySettings(out List<string> errors)
        {
            // Code execute when user decides to confirm changes made since BeginEdit was called.
            // Executed before EndEdit is called and EndEdit is not called if false is returned.
            // List of errors is presented to user if verification fails.
            errors = new List<string>();

            // Verify browser executable exists
            if (string.IsNullOrWhiteSpace(Settings.BrowserExecutablePath))
            {
                errors.Add("Browser executable path cannot be empty.");
            }
            else if (!System.IO.File.Exists(Settings.BrowserExecutablePath))
            {
                errors.Add($"Browser executable not found at: {Settings.BrowserExecutablePath}");
            }

            return errors.Count == 0;
        }
    }
}