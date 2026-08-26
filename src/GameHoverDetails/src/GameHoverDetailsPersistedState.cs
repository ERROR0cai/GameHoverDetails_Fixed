using System.Collections.Generic;

namespace GameHoverDetails
{
    /// <summary>
    /// JSON shape for Playnite plugin settings save/load.
    /// Only canonical fields — no inverse UI bindings — so legacy JSON with duplicate keys cannot corrupt booleans.
    /// </summary>
    public sealed class GameHoverDetailsPersistedState
    {
        public int HoverWidth { get; set; }
        public int ShowDelayMs { get; set; }
        public int HoverFieldBlockSpacingDip { get; set; }
        public bool HoverDisabled { get; set; }
        /// <summary>Null in legacy JSON — treated as true (off in Fullscreen).</summary>
        public bool? HoverDisabledInFullscreen { get; set; }
        public bool HideFieldTitlesInHover { get; set; }
        public bool ShowFieldInlineIconsInHover { get; set; }
        /// <summary>Null in legacy JSON — treated as true (Playnite theme chrome).</summary>
        public bool? UseThemeChrome { get; set; }
        public string HoverChromeBackgroundHex { get; set; }
        public string HoverChromeBorderHex { get; set; }
        public string HoverChromeIconHex { get; set; }
        public string HoverChromeIconBackgroundHex { get; set; }
        public string HoverChromeTextHex { get; set; }
        /// <summary>Null in legacy JSON — treated as 100. Zero is a valid saved value.</summary>
        public int? HoverChromeBackgroundOpacity { get; set; }
        public List<string> SelectedFieldKeys { get; set; }
        public List<string> DisabledFieldKeysOrder { get; set; }
    }
}
