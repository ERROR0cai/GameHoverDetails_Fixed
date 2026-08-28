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

        /// <summary>Cover / background / small tile art — no inline catalog glyph beside content.</summary>
        public static bool IsGameArtImageField(string key)
        {
            return key == "Icon" || key == "CoverImage" || key == "BackgroundImage";
        }

        private static FontFamily phosphorFontFamily;
        private static FontFamily uniconsFontFamily;
        private static FontFamily hugeIconsFontFamily;

        /// <summary>Default catalog glyph family (Unicons). TTFs are WPF resources in the DLL and also copied next to it.</summary>
        public static FontFamily GlyphFontFamily => GetGlyphFontFamily(GameHoverDetailsSettings.IconStyleUnicons);

        public static FontFamily GetGlyphFontFamily(string style)
        {
            var norm = GameHoverDetailsSettings.NormalizeIconStyle(style);
            if (string.Equals(norm, GameHoverDetailsSettings.IconStyleUnicons, StringComparison.Ordinal))
            {
                if (uniconsFontFamily == null)
                {
                    uniconsFontFamily = LoadPackagedFont("Unicons-Line.ttf", "unicons-line");
                }

                return uniconsFontFamily;
            }

            if (string.Equals(norm, GameHoverDetailsSettings.IconStyleHugeIcons, StringComparison.Ordinal))
            {
                if (hugeIconsFontFamily == null)
                {
                    hugeIconsFontFamily = LoadPackagedFont("HugeIcons-StrokeRounded.ttf", "hgi-stroke-rounded");
                }

                return hugeIconsFontFamily;
            }

            if (phosphorFontFamily == null)
            {
                phosphorFontFamily = LoadPackagedFont("Phosphor.ttf", "Phosphor");
            }

            return phosphorFontFamily;
        }

        private static FontFamily LoadPackagedFont(string fileName, string familyName)
        {
            // Pin the TTF file in the face name. `./#FamilyName` against a folder of
            // several icon fonts lets WPF pick the wrong face, so PUA glyphs (Phosphor /
            // Unicons) render as missing while Huge Icons BMP codepoints still "show".
            var face = "./" + fileName + "#" + familyName;
            var baseDir = GetPluginDirectory();
            if (!string.IsNullOrEmpty(baseDir))
            {
                var fontsDir = Path.Combine(baseDir, "fonts") + Path.DirectorySeparatorChar;
                if (File.Exists(Path.Combine(fontsDir, fileName)))
                {
                    return new FontFamily(new Uri(fontsDir, UriKind.Absolute), face);
                }
            }

            return new FontFamily(
                "pack://application:,,,/GameHoverDetails;component/fonts/" + fileName + "#" + familyName);
        }

        private static string GetPluginDirectory()
        {
            var assembly = typeof(HoverFieldCatalog).Assembly;
            if (!string.IsNullOrEmpty(assembly.Location))
            {
                return Path.GetDirectoryName(assembly.Location);
            }

            try
            {
                var codeBase = assembly.CodeBase;
                if (!string.IsNullOrEmpty(codeBase))
                {
                    return Path.GetDirectoryName(new Uri(codeBase).LocalPath);
                }
            }
            catch
            {
            }

            return null;
        }

        public static string GetDisplayName(string key)
        {
            var d = All.FirstOrDefault(x => x.Key == key);
            var fallback = d?.DisplayName ?? key ?? string.Empty;
            if (string.IsNullOrEmpty(key))
            {
                return fallback;
            }

            return HoverLoc.Get("LOCGameHoverDetails_Field_" + key, fallback);
        }

        /// <summary>Glyph for the current icon style (settings list, Add field, hover chips).</summary>
        public static string GetGlyph(string key, string style)
        {
            var norm = GameHoverDetailsSettings.NormalizeIconStyle(style);
            if (string.Equals(norm, GameHoverDetailsSettings.IconStyleUnicons, StringComparison.Ordinal))
            {
                return GetUniconsGlyph(key);
            }

            if (string.Equals(norm, GameHoverDetailsSettings.IconStyleHugeIcons, StringComparison.Ordinal))
            {
                return GetHugeIconsGlyph(key);
            }

            return GetPhosphorGlyph(key);
        }

        /// <summary>Phosphor Regular PUA glyph (@phosphor-icons/web 2.1.2).</summary>
        public static string GetSettingsGlyph(string key)
        {
            return GetPhosphorGlyph(key);
        }

        private static string GetPhosphorGlyph(string key)
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

        /// <summary>Unicons Line PUA glyph (IconScout Simple License, catalog subset).</summary>
        private static string GetUniconsGlyph(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return "\uE859";
            }

            switch (key)
            {
                case "Icon": return "\uEA54";
                case "CoverImage": return "\uEA55";
                case "BackgroundImage": return "\uEA57";
                case "Name": return "\uEBE4";
                case "Description": return "\uE94B";
                case "Platform": return "\uEAF9";
                case "Genre": return "\uE890";
                case "Developer": return "\uEC50";
                case "Publisher": return "\uEB1D";
                case "Category": return "\uEABF";
                case "Tags": return "\uE893";
                case "Features": return "\uE84E";
                case "Series": return "\uE99F";
                case "Region": return "\uE9AA";
                case "AgeRating": return "\uEBFA";
                case "Version": return "\uEC7F";
                case "Notes": return "\uE8FC";
                case "InstallationFolder": return "\uEC51";
                case "InstallSize": return "\uEACB";
                case "ReleaseDate": return "\uE8DC";
                case "DateAdded": return "\uE8DB";
                case "TimePlayed": return "\uE920";
                case "RecentActivity": return "\uEAD8";
                case "LastPlayed": return "\uE92C";
                case "CompletionStatus": return "\uE9C2";
                case "UserScore": return "\uE9AB";
                case "CriticScore": return "\uE901";
                case "CommunityScore": return "\uEA11";
                case "Source": return "\uE97D";
                case "Library": return "\uE92E";
                case "Links": return "\uEBB8";
                default: return "\uE859";
            }
        }

        /// <summary>Huge Icons Stroke Rounded (MIT, catalog subset).</summary>
        private static string GetHugeIconsGlyph(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return "\u4197";
            }

            switch (key)
            {
                case "Icon": return "\u4197";
                case "CoverImage": return "\u4198";
                case "BackgroundImage": return "\u419C";
                case "Name": return "\u48D5";
                case "Description": return "\u3FF9";
                case "Platform": return "\u40A4";
                case "Genre": return "\u488C";
                case "Developer": return "\u4789";
                case "Publisher": return "\u3CCA";
                case "Category": return "\u4061";
                case "Tags": return "\u488E";
                case "Features": return "\u478E";
                case "Series": return "\u424F";
                case "Region": return "\u40CA";
                case "AgeRating": return "\u46E0";
                case "Version": return "\u40B6";
                case "Notes": return "\u447F";
                case "InstallationFolder": return "\u4062";
                case "InstallSize": return "\u4123";
                case "ReleaseDate": return "\u3CE4";
                case "DateAdded": return "\u3CE8";
                case "TimePlayed": return "\u3E05";
                case "RecentActivity": return "\u4918";
                case "LastPlayed": return "\u3E06";
                case "CompletionStatus": return "\u3DA6";
                case "UserScore": return "\u47E3";
                case "CriticScore": return "\u3BBA";
                case "CommunityScore": return "\u49CD";
                case "Source": return "\u4289";
                case "Library": return "\u3C5B";
                case "Links": return "\u4293";
                default: return "\u4197";
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
