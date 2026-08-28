using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;
using Playnite.SDK;

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

        /// <summary>Default catalog glyph family (Unicons). TTFs are copied next to the DLL under fonts\.</summary>
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

        private static readonly HashSet<string> LoggedMissingFonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

            if (LoggedMissingFonts.Add(fileName))
            {
                LogManager.GetLogger().Warn("GameHoverDetails icon font missing: " + fileName);
            }

            return new FontFamily("Segoe UI Symbol");
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

        private sealed class GlyphFaces
        {
            public GlyphFaces(string phosphor, string unicons, string hugeIcons)
            {
                Phosphor = phosphor;
                Unicons = unicons;
                HugeIcons = hugeIcons;
            }

            public string Phosphor { get; }
            public string Unicons { get; }
            public string HugeIcons { get; }
        }

        /// <summary>Phosphor, Unicons Line, Huge Icons Stroke Rounded (catalog subset).</summary>
        private static readonly Dictionary<string, GlyphFaces> Glyphs = new Dictionary<string, GlyphFaces>
        {
            { "Icon", new GlyphFaces("\uE5DA", "\uEA54", "\u4197") },
            { "CoverImage", new GlyphFaces("\uE2CA", "\uEA55", "\u4198") },
            { "BackgroundImage", new GlyphFaces("\uEAA2", "\uEA57", "\u419C") },
            { "Name", new GlyphFaces("\uE48A", "\uEBE4", "\u48D5") },
            { "Description", new GlyphFaces("\uE0A8", "\uE94B", "\u3FF9") },
            { "Platform", new GlyphFaces("\uE26E", "\uEAF9", "\u40A4") },
            { "Genre", new GlyphFaces("\uE2F4", "\uE890", "\u488C") },
            { "Developer", new GlyphFaces("\uE1BC", "\uEC50", "\u4789") },
            { "Publisher", new GlyphFaces("\uE102", "\uEB1D", "\u3CCA") },
            { "Category", new GlyphFaces("\uE260", "\uEABF", "\u4061") },
            { "Tags", new GlyphFaces("\uE478", "\uE893", "\u488E") },
            { "Features", new GlyphFaces("\uE6A2", "\uE84E", "\u478E") },
            { "Series", new GlyphFaces("\uE466", "\uE99F", "\u424F") },
            { "Region", new GlyphFaces("\uE288", "\uE9AA", "\u40CA") },
            { "AgeRating", new GlyphFaces("\uE40C", "\uEBFA", "\u46E0") },
            { "Version", new GlyphFaces("\uE278", "\uEC7F", "\u40B6") },
            { "Notes", new GlyphFaces("\uE348", "\uE8FC", "\u447F") },
            { "InstallationFolder", new GlyphFaces("\uE24A", "\uEC51", "\u4062") },
            { "InstallSize", new GlyphFaces("\uE2A0", "\uEACB", "\u4123") },
            { "ReleaseDate", new GlyphFaces("\uE108", "\uE8DC", "\u3CE4") },
            { "DateAdded", new GlyphFaces("\uE714", "\uE8DB", "\u3CE8") },
            { "TimePlayed", new GlyphFaces("\uE19A", "\uE920", "\u3E05") },
            { "RecentActivity", new GlyphFaces("\uE1A0", "\uEAD8", "\u4918") },
            { "LastPlayed", new GlyphFaces("\uE19E", "\uE92C", "\u3E06") },
            { "CompletionStatus", new GlyphFaces("\uE184", "\uE9C2", "\u3DA6") },
            { "UserScore", new GlyphFaces("\uE46A", "\uE9AB", "\u47E3") },
            { "CriticScore", new GlyphFaces("\uE320", "\uE901", "\u3BBA") },
            { "CommunityScore", new GlyphFaces("\uE68E", "\uEA11", "\u49CD") },
            { "Source", new GlyphFaces("\uE470", "\uE97D", "\u4289") },
            { "Library", new GlyphFaces("\uE758", "\uE92E", "\u3C5B") },
            { "Links", new GlyphFaces("\uE2E2", "\uEBB8", "\u4293") }
        };

        private static readonly GlyphFaces DefaultGlyphs = new GlyphFaces("\uE2CE", "\uE859", "\u4197");

        /// <summary>Glyph for the current icon style (settings list, Add field, hover chips).</summary>
        public static string GetGlyph(string key, string style)
        {
            var faces = Glyphs.TryGetValue(key ?? string.Empty, out var mapped) ? mapped : DefaultGlyphs;
            var norm = GameHoverDetailsSettings.NormalizeIconStyle(style);
            if (string.Equals(norm, GameHoverDetailsSettings.IconStyleUnicons, StringComparison.Ordinal))
            {
                return faces.Unicons;
            }

            if (string.Equals(norm, GameHoverDetailsSettings.IconStyleHugeIcons, StringComparison.Ordinal))
            {
                return faces.HugeIcons;
            }

            return faces.Phosphor;
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
