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

        public string BrowserExecutablePath { get => browserExecutablePath; set => SetValue(ref browserExecutablePath, value); }
        public ObservableCollection<BrowserGame> BrowserGames { get => browserGames; set => SetValue(ref browserGames, value); }
        public bool UseSharedProfile { get => useSharedProfile; set => SetValue(ref useSharedProfile, value); }
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