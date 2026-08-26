using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;

namespace GameHoverDetails
{
    internal sealed class HoverFieldDefinition
    {
        public HoverFieldDefinition(string key, string displayName)
        {
            Key = key;
            DisplayName = displayName;
        }

        public string Key { get; }
        public string DisplayName { get; }
    }

    internal static class HoverFieldCatalog
    {
        /// <summary>Stable order matching Playnite details-panel columns (left to right).</summary>
        public static readonly IReadOnlyList<HoverFieldDefinition> All = new List<HoverFieldDefinition>
        {
            new HoverFieldDefinition("Icon", "Icon"),
            new HoverFieldDefinition("TimePlayed", "Time Played"),
            new HoverFieldDefinition("RecentActivity", "Recent Activity"),
            new HoverFieldDefinition("Library", "Library"),
            new HoverFieldDefinition("Publisher", "Publisher"),
            new HoverFieldDefinition("Features", "Features"),
            new HoverFieldDefinition("Series", "Series"),
            new HoverFieldDefinition("Version", "Version"),
            new HoverFieldDefinition("UserScore", "User Score"),
            new HoverFieldDefinition("Notes", "Notes"),
            new HoverFieldDefinition("InstallationFolder", "Installation Folder"),
            new HoverFieldDefinition("CoverImage", "Cover Image"),
            new HoverFieldDefinition("LastPlayed", "Last Played"),
            new HoverFieldDefinition("CompletionStatus", "Completion Status"),
            new HoverFieldDefinition("Genre", "Genre"),
            new HoverFieldDefinition("ReleaseDate", "Release Date"),
            new HoverFieldDefinition("Tags", "Tags"),
            new HoverFieldDefinition("Region", "Region"),
            new HoverFieldDefinition("CommunityScore", "Community Score"),
            new HoverFieldDefinition("Links", "Links"),
            new HoverFieldDefinition("Name", "Name"),
            new HoverFieldDefinition("BackgroundImage", "Background Image"),
            new HoverFieldDefinition("DateAdded", "Date Added"),
            new HoverFieldDefinition("Platform", "Platform"),
            new HoverFieldDefinition("Developer", "Developer"),
            new HoverFieldDefinition("Category", "Category"),
            new HoverFieldDefinition("AgeRating", "Age Rating"),
            new HoverFieldDefinition("Source", "Source"),
            new HoverFieldDefinition("CriticScore", "Critic Score"),
            new HoverFieldDefinition("Description", "Description"),
            new HoverFieldDefinition("InstallSize", "Install Size")
        };

        private static readonly HashSet<string> ValidKeys = new HashSet<string>();
        private static readonly Dictionary<string, int> KeyOrder = new Dictionary<string, int>();

        static HoverFieldCatalog()
        {
            for (var i = 0; i < All.Count; i++)
            {
                var k = All[i].Key;
                ValidKeys.Add(k);
                KeyOrder[k] = i;
            }
        }

        public static bool IsKnownKey(string key)
        {
            return !string.IsNullOrEmpty(key) && ValidKeys.Contains(key);
        }

        /// <summary>Cover / background / small tile art — no inline Phosphor glyph beside content.</summary>
        public static bool IsGameArtImageField(string key)
        {
            return key == "Icon" || key == "CoverImage" || key == "BackgroundImage";
        }

        private static FontFamily glyphFontFamily;

        /// <summary>Phosphor Regular (MIT) shipped next to the plugin DLL for settings list, Add field menu, and hover chips. Lazy so Playnite start does not parse the TTF on the UI thread.</summary>
        public static FontFamily GlyphFontFamily
        {
            get
            {
                if (glyphFontFamily == null)
                {
                    glyphFontFamily = CreateGlyphFontFamily();
                }

                return glyphFontFamily;
            }
        }

        private static FontFamily CreateGlyphFontFamily()
        {
            var baseDir = Path.GetDirectoryName(typeof(HoverFieldCatalog).Assembly.Location);
            if (!string.IsNullOrEmpty(baseDir))
            {
                var fontsDir = Path.Combine(baseDir, "fonts") + Path.DirectorySeparatorChar;
                if (File.Exists(Path.Combine(fontsDir, "Phosphor.ttf")))
                {
                    return new FontFamily(new Uri(fontsDir, UriKind.Absolute), "./#Phosphor");
                }
            }

            return new FontFamily(
                new Uri("pack://application:,,,/GameHoverDetails;component/fonts/"),
                "./#Phosphor");
        }

        public static string GetDisplayName(string key)
        {
            var d = All.FirstOrDefault(x => x.Key == key);
            return d?.DisplayName ?? key ?? string.Empty;
        }

        /// <summary>Single Phosphor Regular PUA glyph (@phosphor-icons/web 2.1.2) for settings list / Add field menu / hover chips.</summary>
        public static string GetSettingsGlyph(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return "\uE2CE";
            }

            switch (key)
            {
                case "Icon": return "\uE5DA";
                case "CoverImage": return "\uE2CA";
                case "BackgroundImage": return "\uEAA2";
                case "Name": return "\uE48A";
                case "Description": return "\uE0A8";
                case "Platform": return "\uE26E";
                case "Genre": return "\uE2F4";
                case "Developer": return "\uE1BC";
                case "Publisher": return "\uE102";
                case "Category": return "\uE260";
                case "Tags": return "\uE478";
                case "Features": return "\uE6A2";
                case "Series": return "\uE466";
                case "Region": return "\uE288";
                case "AgeRating": return "\uE40C";
                case "Version": return "\uE278";
                case "Notes": return "\uE348";
                case "InstallationFolder": return "\uE24A";
                case "InstallSize": return "\uE2A0";
                case "ReleaseDate": return "\uE108";
                case "DateAdded": return "\uE714";
                case "TimePlayed": return "\uE19A";
                case "RecentActivity": return "\uE1A0";
                case "LastPlayed": return "\uE19E";
                case "CompletionStatus": return "\uE184";
                case "UserScore": return "\uE46A";
                case "CriticScore": return "\uE320";
                case "CommunityScore": return "\uE68E";
                case "Source": return "\uE470";
                case "Library": return "\uE758";
                case "Links": return "\uE2E2";
                default: return "\uE2CE";
            }
        }

        public static int CompareKeys(string a, string b)
        {
            var ia = GetOrder(a);
            var ib = GetOrder(b);
            return ia.CompareTo(ib);
        }

        public static int GetOrder(string key)
        {
            return KeyOrder.TryGetValue(key, out var o) ? o : int.MaxValue;
        }

        public static List<string> GetAllKeysInCatalogOrder()
        {
            return All.Select(d => d.Key).ToList();
        }
    }
}
