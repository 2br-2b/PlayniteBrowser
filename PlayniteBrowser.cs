using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace PlayniteBrowser
{
    public class PlayniteBrowser : LibraryPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private PlayniteBrowserSettingsViewModel settings { get; set; }

        public override Guid Id { get; } = Guid.Parse("3c58c307-b26e-4569-855a-e19afa4a3b2f");

        // Change to something more appropriate
        public override string Name => "Browser";

        // Implementing Client adds ability to open it via special menu in playnite.
        public override LibraryClient Client { get; } = new PlayniteBrowserClient();

        public PlayniteBrowser(IPlayniteAPI api) : base(api)
        {
            settings = new PlayniteBrowserSettingsViewModel(this);
            Properties = new LibraryPluginProperties
            {
                HasSettings = true
            };
        }

        private string GetProfilePath(PlayniteBrowserSettings settings, string gameId)
        {
            var dataBasePath = System.IO.Path.Combine(PlayniteApi.Paths.ExtensionsDataPath, "PlayniteBrowser");
            var profilesBasePath = System.IO.Path.Combine(dataBasePath, "Profiles");

            var profilePath = settings.UseSharedProfile
                ? System.IO.Path.Combine(profilesBasePath, "Shared")
                : System.IO.Path.Combine(profilesBasePath, gameId);

            // Ensure the profile directory exists
            if (!System.IO.Directory.Exists(profilePath))
            {
                System.IO.Directory.CreateDirectory(profilePath);
            }

            return profilePath;
        }

        private string GetFaviconPath(string url, string gameId, string iconsPath)
        {
            try
            {
                var uri = new Uri(url);
                var domain = uri.Host;
                var iconFileName = $"{gameId}.png";
                var iconPath = System.IO.Path.Combine(iconsPath, iconFileName);

                // If icon already exists, return it
                if (System.IO.File.Exists(iconPath))
                {
                    return iconPath;
                }

                // Download favicon using Google's favicon service
                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(10);
                    var faviconUrl = $"https://www.google.com/s2/favicons?domain={domain}&sz=128";
                    var imageBytes = httpClient.GetByteArrayAsync(faviconUrl).Result;

                    // Save the favicon
                    System.IO.File.WriteAllBytes(iconPath, imageBytes);
                    return iconPath;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to download favicon for {url}");
                return null;
            }
        }

        private string GetOgImageUrl(string html)
        {
            try
            {
                // Look for og:image meta tag
                var imageMatch = System.Text.RegularExpressions.Regex.Match(
                    html,
                    @"<meta\s+property=[""']og:image[""']\s+content=[""']([^""']*)[""']",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (imageMatch.Success)
                {
                    return System.Net.WebUtility.HtmlDecode(imageMatch.Groups[1].Value);
                }

                // Try alternative meta tag format
                imageMatch = System.Text.RegularExpressions.Regex.Match(
                    html,
                    @"<meta\s+content=[""']([^""']*)[""']\s+property=[""']og:image[""']",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (imageMatch.Success)
                {
                    return System.Net.WebUtility.HtmlDecode(imageMatch.Groups[1].Value);
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to parse og:image from HTML");
                return string.Empty;
            }
        }

        private string GetWebsiteMetadata(string url, out string description, out string ogImageUrl)
        {
            description = string.Empty;
            ogImageUrl = string.Empty;

            try
            {
                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(10);
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                    var html = httpClient.GetStringAsync(url).Result;

                    // Get description
                    var descriptionMatch = System.Text.RegularExpressions.Regex.Match(
                        html,
                        @"<meta\s+(?:name|property)=[""'](?:description|og:description)[""']\s+content=[""']([^""']*)[""']",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                    if (descriptionMatch.Success)
                    {
                        description = System.Net.WebUtility.HtmlDecode(descriptionMatch.Groups[1].Value);
                    }
                    else
                    {
                        // Try alternative meta tag format
                        descriptionMatch = System.Text.RegularExpressions.Regex.Match(
                            html,
                            @"<meta\s+content=[""']([^""']*)[""']\s+(?:name|property)=[""'](?:description|og:description)[""']",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                        if (descriptionMatch.Success)
                        {
                            description = System.Net.WebUtility.HtmlDecode(descriptionMatch.Groups[1].Value);
                        }
                    }

                    // Get og:image URL
                    ogImageUrl = GetOgImageUrl(html);

                    return html;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to fetch metadata for {url}");
                return string.Empty;
            }
        }

        private string DownloadBackgroundImage(string imageUrl, string baseUrl, string gameId, string backgroundsPath)
        {
            try
            {
                if (string.IsNullOrEmpty(imageUrl))
                {
                    return null;
                }

                // Make relative URLs absolute
                if (!imageUrl.StartsWith("http://") && !imageUrl.StartsWith("https://"))
                {
                    var baseUri = new Uri(baseUrl);
                    if (imageUrl.StartsWith("//"))
                    {
                        imageUrl = baseUri.Scheme + ":" + imageUrl;
                    }
                    else if (imageUrl.StartsWith("/"))
                    {
                        imageUrl = $"{baseUri.Scheme}://{baseUri.Host}{imageUrl}";
                    }
                    else
                    {
                        imageUrl = new Uri(baseUri, imageUrl).ToString();
                    }
                }

                var imageFileName = $"{gameId}_bg.jpg";
                var imagePath = System.IO.Path.Combine(backgroundsPath, imageFileName);

                // If image already exists, return it
                if (System.IO.File.Exists(imagePath))
                {
                    return imagePath;
                }

                // Download the background image
                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(15);
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                    var imageBytes = httpClient.GetByteArrayAsync(imageUrl).Result;

                    // Only save if we got actual image data
                    if (imageBytes != null && imageBytes.Length > 0)
                    {
                        System.IO.File.WriteAllBytes(imagePath, imageBytes);
                        return imagePath;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to download background image from {imageUrl}");
                return null;
            }
        }
        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            // Load saved browser games
            var loadedSettings = LoadPluginSettings<PlayniteBrowserSettings>();
            if (loadedSettings == null || loadedSettings.BrowserGames == null || loadedSettings.BrowserGames.Count == 0)
            {
                return new List<GameMetadata>();
            }

            var games = new List<GameMetadata>();

            // Get Playnite's application data path for storing browser profiles and icons
            var dataBasePath = System.IO.Path.Combine(PlayniteApi.Paths.ExtensionsDataPath, "PlayniteBrowser");
            var profilesBasePath = System.IO.Path.Combine(dataBasePath, "Profiles");
            var iconsPath = System.IO.Path.Combine(dataBasePath, "Icons");
            var backgroundsPath = System.IO.Path.Combine(dataBasePath, "Backgrounds");

            // Ensure the directories exist
            if (!System.IO.Directory.Exists(iconsPath))
            {
                System.IO.Directory.CreateDirectory(iconsPath);
            }
            if (!System.IO.Directory.Exists(backgroundsPath))
            {
                System.IO.Directory.CreateDirectory(backgroundsPath);
            }

            foreach (var browserGame in loadedSettings.BrowserGames)
            {
                // Get the profile path for this game (shared or individual)
                var profilePath = GetProfilePath(loadedSettings, browserGame.GameId);

                // Get the favicon for this game
                var faviconPath = GetFaviconPath(browserGame.Url, browserGame.GameId, iconsPath);

                // Get the website metadata (description and og:image)
                string description, ogImageUrl;
                GetWebsiteMetadata(browserGame.Url, out description, out ogImageUrl);

                // Download the background image
                var backgroundPath = DownloadBackgroundImage(ogImageUrl, browserGame.Url, browserGame.GameId, backgroundsPath);

                var gameMetadata = new GameMetadata()
                {
                    Name = browserGame.Name,
                    GameId = browserGame.GameId,
                    Description = description,
                    IsInstalled = System.IO.File.Exists(loadedSettings.BrowserExecutablePath),
                    Platforms = new HashSet<MetadataProperty>
                    {
                        new MetadataNameProperty("PC")
                    },
                    Links = new List<Link>
                    {
                        new Link("Website", browserGame.Url)
                    }
                };

                // Add icon if favicon was successfully downloaded
                if (!string.IsNullOrEmpty(faviconPath))
                {
                    gameMetadata.Icon = new MetadataFile(faviconPath);
                }

                // Add background image if og:image was successfully downloaded
                if (!string.IsNullOrEmpty(backgroundPath))
                {
                    gameMetadata.BackgroundImage = new MetadataFile(backgroundPath);
                }

                games.Add(gameMetadata);
            }

            return games;
        }

        public override IEnumerable<PlayController> GetPlayActions(GetPlayActionsArgs args)
        {
            var loadedSettings = LoadPluginSettings<PlayniteBrowserSettings>();
            if (loadedSettings == null)
            {
                yield break;
            }

            // Find the browser game by GameId
            var browserGame = loadedSettings.BrowserGames.FirstOrDefault(g => g.GameId == args.Game.GameId);
            if (browserGame == null)
            {
                yield break;
            }

            // Get the profile path for this game (shared or individual)
            var profilePath = GetProfilePath(loadedSettings, browserGame.GameId);

            yield return new AutomaticPlayController(args.Game)
            {
                Type = AutomaticPlayActionType.File,
                TrackingMode = TrackingMode.Process,
                Name = "Play in Browser",
                Path = loadedSettings.BrowserExecutablePath,
                Arguments = $"--user-data-dir=\"{profilePath}\" --app={browserGame.Url}"
            };
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new PlayniteBrowserSettingsView();
        }
    }
}