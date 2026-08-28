using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Playnite.SDK;
using Playnite.SDK.Data;

namespace GameHoverDetails
{
    public class GameHoverDetailsSettings : ObservableObject, ISettings
    {
        private const int MinWidth = 120;
        private const int MaxWidth = 500;
        private const int MinShowDelayMs = 0;
        private const int MaxShowDelayMs = 500;
        private const int MinFieldBlockSpacingDip = 4;
        private const int MaxFieldBlockSpacingDip = 36;
        private const int DefaultFieldBlockSpacingDip = 11;
        internal const int MinContentPaddingDip = 4;
        internal const int MaxContentPaddingDip = 32;
        internal const int DefaultContentPaddingDip = 14;
        private const int MinChromeOpacity = 0;
        private const int MaxChromeOpacity = 100;
        private const int DefaultChromeOpacity = 100;
        internal const double DefaultBodyFontSize = 13;
        internal const double MinBodyFontSize = 9;
        internal const double MaxBodyFontSize = 20;
        internal const double DefaultTitleFontSize = 10.5;
        internal const double MinTitleFontSize = 8;
        internal const double MaxTitleFontSize = 16;
        internal const int DefaultIconChipSizeDip = 32;
        internal const int MinIconChipSizeDip = 24;
        internal const int MaxIconChipSizeDip = 48;
        internal const int DefaultIconChipPaddingDip = 8;
        internal const int MinIconChipPaddingDip = 0;
        internal const int MaxIconChipPaddingDip = 16;
        public const string IconStylePhosphor = "Phosphor";
        public const string IconStyleUnicons = "Unicons";
        public const string IconStyleHugeIcons = "HugeIcons";
        public const string IconChipShapeCircle = "Circle";
        public const string IconChipShapeRectangle = "Rectangle";
        public const string IconChipShapeRounded = "Rounded";
        public const string IconChipShapeSoftRounded = "SoftRounded";
        public const string IconChipShapeSquircle = "Squircle";
        public const string IconChipShapeArch = "Arch";
        public const string IconChipShapeTile = "Tile";
        public const string IconChipShapeLeaf = "Leaf";

        private static readonly string[] FactoryDefaultSelectedKeys = { "Icon", "Name", "LastPlayed" };

        public const string BackgroundStyleRegular = "Regular";
        public const string BackgroundStyleGameCover = "GameCover";

        /// <summary>Landscape fanart panel: wider than the Regular width slider.</summary>
        internal const double ArtBackgroundWidthScale = 1.38;

        /// <summary>Factory hover uses game background at this opacity.</summary>
        internal const int FanartFactoryOpacity = 75;

        /// <summary>Turning <c>Use game background</c> on always snaps opacity here.</summary>
        internal const int FanartDefaultOpacity = 50;

        [DontSerialize]
        private GameHoverDetailsPlugin plugin;

        private int hoverWidth = 360;
        private int showDelayMs;
        private int hoverFieldBlockSpacingDip = DefaultFieldBlockSpacingDip;
        private int hoverContentPaddingDip = DefaultContentPaddingDip;
        private bool hoverDisabled;
        private bool hoverDisabledInFullscreen = true;
        private bool hideFieldTitlesInHover;
        private bool showFieldInlineIconsInHover;
        private bool hideIconChipBackground;
        private bool hideFieldDividers = true;
        private bool hidePanelBorder;
        private double hoverBodyFontSize = DefaultBodyFontSize;
        private double hoverTitleFontSize = DefaultTitleFontSize;
        private string hoverIconStyle = IconStyleUnicons;
        private int hoverIconChipSizeDip = DefaultIconChipSizeDip;
        private int hoverIconChipPaddingDip = DefaultIconChipPaddingDip;
        private string hoverIconChipShape = IconChipShapeCircle;
        private bool useThemeChrome = true;
        private string hoverBackgroundStyle = BackgroundStyleGameCover;
        private string hoverChromeBackgroundHex = HoverChromePalette.DefaultFillHex;
        private string hoverChromeBorderHex = HoverChromePalette.DefaultBorderHex;
        private string hoverChromeDividerHex = HoverChromePalette.DefaultDividerHex;
        private string hoverChromeIconHex = HoverChromePalette.DefaultIconHex;
        private string hoverChromeIconBackgroundHex = HoverChromePalette.DefaultIconBackgroundHex;
        private string hoverChromeTextHex = HoverChromePalette.DefaultTextHex;
        private int hoverChromeBackgroundOpacity = FanartFactoryOpacity;
        private List<string> selectedFieldKeys = new List<string>(FactoryDefaultSelectedKeys);
        private List<string> disabledFieldKeysOrder = new List<string>();

        private int hoverWidthOriginal;
        private int showDelayMsOriginal;
        private int hoverFieldBlockSpacingDipOriginal;
        private int hoverContentPaddingDipOriginal;
        private bool hoverDisabledOriginal;
        private bool hoverDisabledInFullscreenOriginal;
        private bool hideFieldTitlesInHoverOriginal;
        private bool showFieldInlineIconsInHoverOriginal;
        private bool hideIconChipBackgroundOriginal;
        private bool hideFieldDividersOriginal;
        private bool hidePanelBorderOriginal;
        private double hoverBodyFontSizeOriginal;
        private double hoverTitleFontSizeOriginal;
        private string hoverIconStyleOriginal;
        private int hoverIconChipSizeDipOriginal;
        private int hoverIconChipPaddingDipOriginal;
        private string hoverIconChipShapeOriginal;
        private bool useThemeChromeOriginal;
        private string hoverBackgroundStyleOriginal;
        private string hoverChromeBackgroundHexOriginal;
        private string hoverChromeBorderHexOriginal;
        private string hoverChromeDividerHexOriginal;
        private string hoverChromeIconHexOriginal;
        private string hoverChromeIconBackgroundHexOriginal;
        private string hoverChromeTextHexOriginal;
        private int hoverChromeBackgroundOpacityOriginal;
        private List<string> selectedFieldKeysOriginal;
        private List<string> disabledFieldKeysOrderOriginal;

        public int HoverWidth
        {
            get => hoverWidth;
            set => SetValue(ref hoverWidth, ClampWidth(value));
        }

        /// <summary>Milliseconds to wait after the pointer rests on a game tile before opening the hover (0 = immediate).</summary>
        public int ShowDelayMs
        {
            get => showDelayMs;
            set => SetValue(ref showDelayMs, ClampShowDelayMs(value));
        }

        /// <summary>Vertical gap between field blocks in the hover panel (device-independent pixels).</summary>
        public int HoverFieldBlockSpacingDip
        {
            get => hoverFieldBlockSpacingDip;
            set => SetValue(ref hoverFieldBlockSpacingDip, ClampFieldBlockSpacingDip(value));
        }

        /// <summary>Inset around the field/icon list inside the hover panel (device-independent pixels).</summary>
        public int HoverContentPaddingDip
        {
            get => hoverContentPaddingDip;
            set => SetValue(ref hoverContentPaddingDip, ClampContentPaddingDip(value));
        }

        /// <summary>When true, hover popups are turned off (persisted; default false for existing installs).</summary>
        public bool HoverDisabled
        {
            get => hoverDisabled;
            set => SetValue(ref hoverDisabled, value, nameof(HoverDisabled), nameof(HoverDetailsEnabled));
        }

        /// <summary>UI binding for "Enable hover details" (inverse of <see cref="HoverDisabled"/>).</summary>
        /// <remarks>Not serialized — persists via <see cref="HoverDisabled"/> only. Dual properties deserialize in arbitrary order and could corrupt the backing field.</remarks>
        [DontSerialize]
        public bool HoverDetailsEnabled
        {
            get => !hoverDisabled;
            set => HoverDisabled = !value;
        }

        /// <summary>When true, hover is off in Playnite Fullscreen (persisted; default true).</summary>
        public bool HoverDisabledInFullscreen
        {
            get => hoverDisabledInFullscreen;
            set => SetValue(ref hoverDisabledInFullscreen, value, nameof(HoverDisabledInFullscreen), nameof(HoverDetailsEnabledInFullscreen));
        }

        /// <summary>UI binding for "Show hover in Fullscreen mode".</summary>
        [DontSerialize]
        public bool HoverDetailsEnabledInFullscreen
        {
            get => !hoverDisabledInFullscreen;
            set => HoverDisabledInFullscreen = !value;
        }

        /// <summary>When true, field labels (e.g. Publisher) are hidden in the hover panel.</summary>
        public bool HideFieldTitlesInHover
        {
            get => hideFieldTitlesInHover;
            set => SetValue(ref hideFieldTitlesInHover, value, nameof(HideFieldTitlesInHover), nameof(HoverTitlesInHover));
        }

        /// <summary>UI binding for "Show field titles in hover".</summary>
        /// <remarks>Not serialized — persists via <see cref="HideFieldTitlesInHover"/> only.</remarks>
        [DontSerialize]
        public bool HoverTitlesInHover
        {
            get => !hideFieldTitlesInHover;
            set => HideFieldTitlesInHover = !value;
        }

        /// <summary>When true, show a catalog icon beside text values (not used for cover/icon/background rows or platform icon strip).</summary>
        public bool ShowFieldInlineIconsInHover
        {
            get => showFieldInlineIconsInHover;
            set => SetValue(
                ref showFieldInlineIconsInHover,
                value,
                nameof(ShowFieldInlineIconsInHover),
                nameof(ShowIconBackgroundColorControls));
        }

        /// <summary>When true, inline icon chips have no fill.</summary>
        public bool HideIconChipBackground
        {
            get => hideIconChipBackground;
            set => SetValue(
                ref hideIconChipBackground,
                value,
                nameof(HideIconChipBackground),
                nameof(ShowIconChipBackground),
                nameof(ShowIconBackgroundColorControls));
        }

        /// <summary>UI binding for "Show icon background".</summary>
        [DontSerialize]
        public bool ShowIconChipBackground
        {
            get => !hideIconChipBackground;
            set => HideIconChipBackground = !value;
        }

        /// <summary>Icon-background color picker is only useful when icons and chip fill are on.</summary>
        [DontSerialize]
        public bool ShowIconBackgroundColorControls => showFieldInlineIconsInHover && !hideIconChipBackground;

        /// <summary>When true, no 1px divider between field blocks.</summary>
        public bool HideFieldDividers
        {
            get => hideFieldDividers;
            set => SetValue(
                ref hideFieldDividers,
                value,
                nameof(HideFieldDividers),
                nameof(ShowFieldDividers),
                nameof(ShowDividerColorControls));
        }

        /// <summary>UI binding for "Show dividers". Default off.</summary>
        [DontSerialize]
        public bool ShowFieldDividers
        {
            get => !hideFieldDividers;
            set => HideFieldDividers = !value;
        }

        /// <summary>Divider color picker is only useful when field dividers are shown.</summary>
        [DontSerialize]
        public bool ShowDividerColorControls => !hideFieldDividers;

        /// <summary>When true, the hover panel has no 1px outline.</summary>
        public bool HidePanelBorder
        {
            get => hidePanelBorder;
            set => SetValue(
                ref hidePanelBorder,
                value,
                nameof(HidePanelBorder),
                nameof(ShowPanelBorder),
                nameof(ShowBorderColorControls));
        }

        /// <summary>UI binding for "Show border". Default on.</summary>
        [DontSerialize]
        public bool ShowPanelBorder
        {
            get => !hidePanelBorder;
            set => HidePanelBorder = !value;
        }

        /// <summary>Border color picker is only useful when the panel outline is shown.</summary>
        [DontSerialize]
        public bool ShowBorderColorControls => !hidePanelBorder;

        /// <summary>Body / value text size in the hover panel (device-independent pixels).</summary>
        public double HoverBodyFontSize
        {
            get => hoverBodyFontSize;
            set => SetValue(ref hoverBodyFontSize, ClampBodyFontSize(value), nameof(HoverBodyFontSize), nameof(HoverBodyLineHeight));
        }

        /// <summary>Field-title text size when titles are shown.</summary>
        public double HoverTitleFontSize
        {
            get => hoverTitleFontSize;
            set => SetValue(ref hoverTitleFontSize, ClampTitleFontSize(value), nameof(HoverTitleFontSize), nameof(HoverTitleLineHeight));
        }

        [DontSerialize]
        public double HoverBodyLineHeight => HoverBodyFontSize * (18.0 / DefaultBodyFontSize);

        [DontSerialize]
        public double HoverTitleLineHeight => HoverTitleFontSize * (14.0 / DefaultTitleFontSize);

        /// <summary>Catalog glyph family: Unicons (default), Phosphor, or HugeIcons.</summary>
        public string HoverIconStyle
        {
            get => hoverIconStyle;
            set
            {
                var norm = NormalizeIconStyle(value);
                if (string.Equals(hoverIconStyle, norm, System.StringComparison.Ordinal))
                {
                    return;
                }

                SetValue(
                    ref hoverIconStyle,
                    norm,
                    nameof(HoverIconStyle),
                    nameof(HoverIconFontFamily));
            }
        }

        [DontSerialize]
        public FontFamily HoverIconFontFamily => HoverFieldCatalog.GetGlyphFontFamily(hoverIconStyle);

        /// <summary>Glyph size for inline field icons (device-independent pixels). Padding grows the chip around this.</summary>
        public int HoverIconChipSizeDip
        {
            get => hoverIconChipSizeDip;
            set => SetValue(
                ref hoverIconChipSizeDip,
                ClampIconChipSizeDip(value),
                nameof(HoverIconChipSizeDip),
                nameof(HoverIconGlyphFontSize),
                nameof(HoverIconChipOuterSizeDip));
        }

        /// <summary>Space between the glyph and the chip edge. Does not change <see cref="HoverIconChipSizeDip"/>.</summary>
        public int HoverIconChipPaddingDip
        {
            get => hoverIconChipPaddingDip;
            set => SetValue(
                ref hoverIconChipPaddingDip,
                ClampIconChipPaddingDip(value),
                nameof(HoverIconChipPaddingDip),
                nameof(HoverIconChipOuterSizeDip));
        }

        /// <summary>Fill shape when icon background is on. Circle, Rectangle, Rounded, SoftRounded, Squircle, Arch, Tile, Leaf.</summary>
        public string HoverIconChipShape
        {
            get => hoverIconChipShape;
            set
            {
                var norm = NormalizeIconChipShape(value);
                if (string.Equals(hoverIconChipShape, norm, System.StringComparison.Ordinal))
                {
                    return;
                }

                SetValue(ref hoverIconChipShape, norm, nameof(HoverIconChipShape));
            }
        }

        [DontSerialize]
        public double HoverIconGlyphFontSize => HoverIconChipSizeDip;

        /// <summary>Chip box: icon size plus padding on each side.</summary>
        [DontSerialize]
        public int HoverIconChipOuterSizeDip => HoverIconChipSizeDip + (2 * HoverIconChipPaddingDip);

        internal CornerRadius ResolveIconChipCornerRadius()
        {
            return ResolveIconChipCornerRadius(HoverIconChipShape, HoverIconChipOuterSizeDip);
        }

        [DontSerialize]
        private bool syncingThemeIntoHex;

        /// <summary>True between <see cref="BeginEdit"/> and <see cref="EndEdit"/> / <see cref="CancelEdit"/>.</summary>
        [DontSerialize]
        internal bool SuppressHoverLiveUpdates { get; private set; }

        /// <summary>True while restoring snapshot values so the settings preview does not rebuild per field.</summary>
        [DontSerialize]
        internal bool SuppressSettingsViewRebuilds { get; private set; }

        /// <summary>Regular solid fill, or the hovered game's cover as the panel background.</summary>
        public string HoverBackgroundStyle
        {
            get => hoverBackgroundStyle;
            set
            {
                var norm = NormalizeBackgroundStyle(value);
                if (string.Equals(hoverBackgroundStyle, norm, System.StringComparison.Ordinal))
                {
                    return;
                }

                var turningFanartOn = !IsGameCoverBackgroundStyle
                    && string.Equals(norm, BackgroundStyleGameCover, System.StringComparison.Ordinal);
                SetValue(ref hoverBackgroundStyle, norm, nameof(HoverBackgroundStyle), nameof(HoverBackgroundStyleIndex));
                OnPropertyChanged(nameof(IsGameCoverBackgroundStyle));
                OnPropertyChanged(nameof(UseGameBackground));
                OnPropertyChanged(nameof(ShowRegularBackgroundColorControls));
                OnPropertyChanged(nameof(PreviewChromeMaxWidth));
                if (turningFanartOn)
                {
                    HoverChromeBackgroundOpacity = FanartDefaultOpacity;
                }
            }
        }

        /// <summary>ComboBox index: 0 Regular, 1 Game cover. Kept for older bindings.</summary>
        [DontSerialize]
        public int HoverBackgroundStyleIndex
        {
            get => IsGameCoverBackgroundStyle ? 1 : 0;
            set => HoverBackgroundStyle = value == 1 ? BackgroundStyleGameCover : BackgroundStyleRegular;
        }

        /// <summary>UI binding for "Use game background".</summary>
        [DontSerialize]
        public bool UseGameBackground
        {
            get => IsGameCoverBackgroundStyle;
            set => HoverBackgroundStyle = value ? BackgroundStyleGameCover : BackgroundStyleRegular;
        }

        [DontSerialize]
        public bool IsGameCoverBackgroundStyle =>
            string.Equals(hoverBackgroundStyle, BackgroundStyleGameCover, System.StringComparison.OrdinalIgnoreCase);

        /// <summary>Background color picker applies only to Regular fill.</summary>
        [DontSerialize]
        public bool ShowRegularBackgroundColorControls => !IsGameCoverBackgroundStyle;

        /// <summary>Settings preview is wider in fanart mode so the landscape image is not cropped into a poster.</summary>
        [DontSerialize]
        public double PreviewChromeMaxWidth => IsGameCoverBackgroundStyle ? 280 : 216;

        /// <summary>Hover panel width: Regular uses the slider; fanart is ~22% wider (capped at the slider max).</summary>
        internal int ResolveHoverPanelWidth()
        {
            var w = HoverWidth;
            if (!IsGameCoverBackgroundStyle)
            {
                return w;
            }

            var wide = (int)System.Math.Round(w * ArtBackgroundWidthScale);
            if (wide < 200)
            {
                wide = System.Math.Max(w, 200);
            }

            return wide > MaxWidth ? MaxWidth : wide;
        }

        /// <summary>When true, popup chrome follows Playnite theme (accent-dark fill); pickers stay visible as a live mirror.</summary>
        public bool UseThemeChrome
        {
            get => useThemeChrome;
            set => SetValue(ref useThemeChrome, value, nameof(UseThemeChrome));
        }

        public string HoverChromeBackgroundHex
        {
            get => hoverChromeBackgroundHex;
            set
            {
                if (!HoverChromePalette.TryNormalizeHex(value, out var hex))
                {
                    return;
                }

                if (string.Equals(hoverChromeBackgroundHex, hex, System.StringComparison.Ordinal))
                {
                    return;
                }

                SetValue(ref hoverChromeBackgroundHex, hex, nameof(HoverChromeBackgroundHex), nameof(ChromeBackgroundSwatchBrush));
                UncheckThemeChromeIfUserEdited();
            }
        }

        public string HoverChromeBorderHex
        {
            get => hoverChromeBorderHex;
            set
            {
                if (!HoverChromePalette.TryNormalizeHex(value, out var hex))
                {
                    return;
                }

                if (string.Equals(hoverChromeBorderHex, hex, System.StringComparison.Ordinal))
                {
                    return;
                }

                SetValue(ref hoverChromeBorderHex, hex, nameof(HoverChromeBorderHex), nameof(ChromeBorderSwatchBrush));
                UncheckThemeChromeIfUserEdited();
            }
        }

        public string HoverChromeDividerHex
        {
            get => hoverChromeDividerHex;
            set
            {
                if (!HoverChromePalette.TryNormalizeHex(value, out var hex))
                {
                    return;
                }

                if (string.Equals(hoverChromeDividerHex, hex, System.StringComparison.Ordinal))
                {
                    return;
                }

                SetValue(ref hoverChromeDividerHex, hex, nameof(HoverChromeDividerHex), nameof(ChromeDividerSwatchBrush));
                UncheckThemeChromeIfUserEdited();
            }
        }

        public string HoverChromeIconHex
        {
            get => hoverChromeIconHex;
            set
            {
                if (!HoverChromePalette.TryNormalizeHex(value, out var hex))
                {
                    return;
                }

                if (string.Equals(hoverChromeIconHex, hex, System.StringComparison.Ordinal))
                {
                    return;
                }

                SetValue(ref hoverChromeIconHex, hex, nameof(HoverChromeIconHex), nameof(ChromeIconSwatchBrush));
                UncheckThemeChromeIfUserEdited();
            }
        }

        public string HoverChromeIconBackgroundHex
        {
            get => hoverChromeIconBackgroundHex;
            set
            {
                if (!HoverChromePalette.TryNormalizeHex(value, out var hex))
                {
                    return;
                }

                if (string.Equals(hoverChromeIconBackgroundHex, hex, System.StringComparison.Ordinal))
                {
                    return;
                }

                SetValue(ref hoverChromeIconBackgroundHex, hex, nameof(HoverChromeIconBackgroundHex), nameof(ChromeIconBackgroundSwatchBrush));
                UncheckThemeChromeIfUserEdited();
            }
        }

        public string HoverChromeTextHex
        {
            get => hoverChromeTextHex;
            set
            {
                if (!HoverChromePalette.TryNormalizeHex(value, out var hex))
                {
                    return;
                }

                if (string.Equals(hoverChromeTextHex, hex, System.StringComparison.Ordinal))
                {
                    return;
                }

                SetValue(ref hoverChromeTextHex, hex, nameof(HoverChromeTextHex), nameof(ChromeTextSwatchBrush));
                UncheckThemeChromeIfUserEdited();
            }
        }

        /// <summary>Fill opacity (0–100) for theme and custom chrome.</summary>
        public int HoverChromeBackgroundOpacity
        {
            get => hoverChromeBackgroundOpacity;
            set => SetValue(ref hoverChromeBackgroundOpacity, ClampChromeOpacity(value));
        }

        [DontSerialize]
        public Brush ChromeBackgroundSwatchBrush => HoverChromePalette.SwatchFromHex(hoverChromeBackgroundHex);

        [DontSerialize]
        public Brush ChromeBorderSwatchBrush => HoverChromePalette.SwatchFromHex(hoverChromeBorderHex);

        [DontSerialize]
        public Brush ChromeDividerSwatchBrush => HoverChromePalette.SwatchFromHex(hoverChromeDividerHex);

        [DontSerialize]
        public Brush ChromeIconSwatchBrush => HoverChromePalette.SwatchFromHex(hoverChromeIconHex);

        [DontSerialize]
        public Brush ChromeIconBackgroundSwatchBrush => HoverChromePalette.SwatchFromHex(hoverChromeIconBackgroundHex);

        [DontSerialize]
        public Brush ChromeTextSwatchBrush => HoverChromePalette.SwatchFromHex(hoverChromeTextHex);

        /// <summary>Copy live accent-dark theme colors into the pickers without turning off theme-sync.</summary>
        public void ApplyThemeColorsToPickers()
        {
            if (!HoverChromePalette.TryComputeThemeChromeHexes(out var hexes))
            {
                return;
            }

            RunWithThemeHexSync(() =>
            {
                HoverChromeBackgroundHex = hexes.Fill;
                HoverChromeBorderHex = hexes.Border;
                HoverChromeDividerHex = hexes.Divider;
                HoverChromeIconBackgroundHex = hexes.IconBackground;
            });
        }

        /// <summary>Restore factory colors and turn off theme-sync.</summary>
        public void ResetCustomChromeColors()
        {
            UseThemeChrome = false;
            HoverChromeBackgroundHex = HoverChromePalette.DefaultFillHex;
            HoverChromeBorderHex = HoverChromePalette.DefaultBorderHex;
            HoverChromeDividerHex = HoverChromePalette.DefaultDividerHex;
            HoverChromeIconHex = HoverChromePalette.DefaultIconHex;
            HoverChromeIconBackgroundHex = HoverChromePalette.DefaultIconBackgroundHex;
            HoverChromeTextHex = HoverChromePalette.DefaultTextHex;
            HoverChromeBackgroundOpacity = DefaultChromeOpacity;
        }

        private void UncheckThemeChromeIfUserEdited()
        {
            if (!syncingThemeIntoHex && useThemeChrome)
            {
                UseThemeChrome = false;
            }
        }

        internal void RunWithThemeHexSync(System.Action action)
        {
            syncingThemeIntoHex = true;
            try
            {
                action();
            }
            finally
            {
                syncingThemeIntoHex = false;
            }
        }

        public List<string> SelectedFieldKeys
        {
            get => selectedFieldKeys;
            set
            {
                var norm = NormalizeKeys(value ?? new List<string>());
                if (ListsEqual(selectedFieldKeys, norm))
                {
                    CoalesceDisabledOrder();
                    return;
                }

                SetValue(ref selectedFieldKeys, norm, nameof(SelectedFieldKeys), nameof(SelectedFieldCount));
                CoalesceDisabledOrder();
            }
        }

        /// <summary>All non-enabled catalog keys, in UI order (for disabled list).</summary>
        public List<string> DisabledFieldKeysOrder
        {
            get => disabledFieldKeysOrder;
            set
            {
                disabledFieldKeysOrder = value ?? new List<string>();
                CoalesceDisabledOrder();
            }
        }

        [DontSerialize]
        public int SelectedFieldCount => selectedFieldKeys.Count;

        internal IPlayniteAPI TryGetPlayniteApi() => plugin?.GetPlayniteApi();

        /// <summary>True when hover must not show (globally off, or Fullscreen with that option on).</summary>
        internal bool IsHoverSuppressed()
        {
            if (hoverDisabled)
            {
                return true;
            }

            if (!hoverDisabledInFullscreen)
            {
                return false;
            }

            return IsFullscreenApplicationMode(TryGetPlayniteApi());
        }

        internal static bool IsFullscreenApplicationMode(IPlayniteAPI api)
        {
            try
            {
                return api?.ApplicationInfo != null && api.ApplicationInfo.Mode == ApplicationMode.Fullscreen;
            }
            catch
            {
                return false;
            }
        }

        public GameHoverDetailsSettings()
        {
        }

        public GameHoverDetailsSettings(GameHoverDetailsPlugin plugin)
            : this()
        {
            this.plugin = plugin ?? throw new System.ArgumentNullException(nameof(plugin));
            var saved = plugin.LoadPluginSettings<GameHoverDetailsPersistedState>();
            if (saved != null)
            {
                hoverWidth = ClampWidth(saved.HoverWidth);
                showDelayMs = ClampShowDelayMs(saved.ShowDelayMs);
                hoverFieldBlockSpacingDip = saved.HoverFieldBlockSpacingDip <= 0
                    ? DefaultFieldBlockSpacingDip
                    : ClampFieldBlockSpacingDip(saved.HoverFieldBlockSpacingDip);
                hoverContentPaddingDip = saved.HoverContentPaddingDip == null
                    ? DefaultContentPaddingDip
                    : ClampContentPaddingDip(saved.HoverContentPaddingDip.Value);
                hoverDisabled = saved.HoverDisabled;
                hoverDisabledInFullscreen = saved.HoverDisabledInFullscreen ?? true;
                hideFieldTitlesInHover = saved.HideFieldTitlesInHover;
                showFieldInlineIconsInHover = saved.ShowFieldInlineIconsInHover;
                hideIconChipBackground = saved.HideIconChipBackground;
                hideFieldDividers = saved.HideFieldDividers ?? true;
                hidePanelBorder = saved.HidePanelBorder ?? false;
                hoverBodyFontSize = saved.HoverBodyFontSize == null || saved.HoverBodyFontSize.Value <= 0
                    ? DefaultBodyFontSize
                    : ClampBodyFontSize(saved.HoverBodyFontSize.Value);
                hoverTitleFontSize = saved.HoverTitleFontSize == null || saved.HoverTitleFontSize.Value <= 0
                    ? DefaultTitleFontSize
                    : ClampTitleFontSize(saved.HoverTitleFontSize.Value);
                hoverIconStyle = NormalizeIconStyle(saved.HoverIconStyle);
                hoverIconChipSizeDip = saved.HoverIconChipSizeDip == null || saved.HoverIconChipSizeDip.Value <= 0
                    ? DefaultIconChipSizeDip
                    : ClampIconChipSizeDip(saved.HoverIconChipSizeDip.Value);
                hoverIconChipPaddingDip = saved.HoverIconChipPaddingDip == null
                    ? DefaultIconChipPaddingDip
                    : ClampIconChipPaddingDip(saved.HoverIconChipPaddingDip.Value);
                hoverIconChipShape = NormalizeIconChipShape(saved.HoverIconChipShape);
                useThemeChrome = saved.UseThemeChrome ?? true;
                hoverBackgroundStyle = NormalizeBackgroundStyle(saved.HoverBackgroundStyle);
                hoverChromeBackgroundHex = HoverChromePalette.NormalizeHexOrDefault(
                    saved.HoverChromeBackgroundHex,
                    HoverChromePalette.DefaultFillHex);
                hoverChromeBorderHex = HoverChromePalette.NormalizeHexOrDefault(
                    saved.HoverChromeBorderHex,
                    HoverChromePalette.DefaultBorderHex);
                hoverChromeDividerHex = HoverChromePalette.NormalizeHexOrDefault(
                    string.IsNullOrWhiteSpace(saved.HoverChromeDividerHex)
                        ? saved.HoverChromeBorderHex
                        : saved.HoverChromeDividerHex,
                    HoverChromePalette.DefaultDividerHex);
                hoverChromeIconHex = HoverChromePalette.NormalizeHexOrDefault(
                    saved.HoverChromeIconHex,
                    HoverChromePalette.DefaultIconHex);
                hoverChromeIconBackgroundHex = HoverChromePalette.NormalizeHexOrDefault(
                    saved.HoverChromeIconBackgroundHex,
                    HoverChromePalette.DefaultIconBackgroundHex);
                hoverChromeTextHex = HoverChromePalette.NormalizeHexOrDefault(
                    saved.HoverChromeTextHex,
                    HoverChromePalette.DefaultTextHex);
                hoverChromeBackgroundOpacity = saved.HoverChromeBackgroundOpacity == null
                    ? DefaultChromeOpacity
                    : ClampChromeOpacity(saved.HoverChromeBackgroundOpacity.Value);
                selectedFieldKeys = NormalizeKeys(saved.SelectedFieldKeys ?? new List<string>());
                disabledFieldKeysOrder = saved.DisabledFieldKeysOrder != null
                    ? new List<string>(saved.DisabledFieldKeysOrder)
                    : new List<string>();
            }

            CoalesceDisabledOrder();
        }

        public IReadOnlyList<string> GetOrderedSelectedKeys()
        {
            return selectedFieldKeys.Where(HoverFieldCatalog.IsKnownKey).ToList();
        }

        /// <summary>Catalog keys not currently selected, in catalog order (for Add-field UI).</summary>
        public IReadOnlyList<string> GetAddableKeys()
        {
            var selected = new HashSet<string>(selectedFieldKeys);
            return HoverFieldCatalog.GetAllKeysInCatalogOrder()
                .Where(k => !selected.Contains(k))
                .ToList();
        }

        public bool MoveEnabled(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= selectedFieldKeys.Count)
            {
                return false;
            }

            if (toIndex < 0 || toIndex > selectedFieldKeys.Count)
            {
                return false;
            }

            if (fromIndex == toIndex)
            {
                return true;
            }

            var key = selectedFieldKeys[fromIndex];
            selectedFieldKeys.RemoveAt(fromIndex);
            var insert = toIndex;
            if (insert > fromIndex)
            {
                insert--;
            }

            insert = System.Math.Max(0, System.Math.Min(insert, selectedFieldKeys.Count));
            selectedFieldKeys.Insert(insert, key);
            SetValue(ref selectedFieldKeys, new List<string>(selectedFieldKeys), nameof(SelectedFieldKeys), nameof(SelectedFieldCount));
            CoalesceDisabledOrder();
            return true;
        }

        public bool MoveDisabled(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= disabledFieldKeysOrder.Count)
            {
                return false;
            }

            if (toIndex < 0 || toIndex > disabledFieldKeysOrder.Count)
            {
                return false;
            }

            if (fromIndex == toIndex)
            {
                return true;
            }

            var key = disabledFieldKeysOrder[fromIndex];
            disabledFieldKeysOrder.RemoveAt(fromIndex);
            var insert = toIndex;
            if (insert > fromIndex)
            {
                insert--;
            }

            insert = System.Math.Max(0, System.Math.Min(insert, disabledFieldKeysOrder.Count));
            disabledFieldKeysOrder.Insert(insert, key);
            SetValue(ref disabledFieldKeysOrder, new List<string>(disabledFieldKeysOrder), nameof(DisabledFieldKeysOrder));
            return true;
        }

        public bool EnableFieldAt(string key, int enabledInsertIndex)
        {
            if (!HoverFieldCatalog.IsKnownKey(key))
            {
                return false;
            }

            if (selectedFieldKeys.Contains(key))
            {
                return true;
            }

            // Key is not selected; it should appear in the disabled pool after Coalesce, but tolerate
            // legacy or inconsistent state (still list it in GetAddableKeys) instead of failing the add.
            disabledFieldKeysOrder.Remove(key);

            var ins = System.Math.Max(0, System.Math.Min(enabledInsertIndex, selectedFieldKeys.Count));
            selectedFieldKeys.Insert(ins, key);
            SetValue(ref selectedFieldKeys, new List<string>(selectedFieldKeys), nameof(SelectedFieldKeys), nameof(SelectedFieldCount));
            CoalesceDisabledOrder();
            return true;
        }

        public bool DisableFieldAt(int enabledIndex, int disabledInsertIndex)
        {
            if (enabledIndex < 0 || enabledIndex >= selectedFieldKeys.Count)
            {
                return false;
            }

            var key = selectedFieldKeys[enabledIndex];
            selectedFieldKeys.RemoveAt(enabledIndex);
            var ins = System.Math.Max(0, System.Math.Min(disabledInsertIndex, disabledFieldKeysOrder.Count));
            disabledFieldKeysOrder.Insert(ins, key);
            if (selectedFieldKeys.Count == 0)
            {
                foreach (var d in FactoryDefaultSelectedKeys)
                {
                    selectedFieldKeys.Add(d);
                    disabledFieldKeysOrder.Remove(d);
                }
            }

            SetValue(ref selectedFieldKeys, new List<string>(selectedFieldKeys), nameof(SelectedFieldKeys), nameof(SelectedFieldCount));
            CoalesceDisabledOrder();
            return true;
        }

        public void BeginEdit()
        {
            SuppressHoverLiveUpdates = true;
            hoverWidthOriginal = HoverWidth;
            showDelayMsOriginal = ShowDelayMs;
            hoverFieldBlockSpacingDipOriginal = HoverFieldBlockSpacingDip;
            hoverContentPaddingDipOriginal = HoverContentPaddingDip;
            hoverDisabledOriginal = HoverDisabled;
            hoverDisabledInFullscreenOriginal = HoverDisabledInFullscreen;
            hideFieldTitlesInHoverOriginal = HideFieldTitlesInHover;
            showFieldInlineIconsInHoverOriginal = ShowFieldInlineIconsInHover;
            hideIconChipBackgroundOriginal = HideIconChipBackground;
            hideFieldDividersOriginal = HideFieldDividers;
            hidePanelBorderOriginal = HidePanelBorder;
            hoverBodyFontSizeOriginal = HoverBodyFontSize;
            hoverTitleFontSizeOriginal = HoverTitleFontSize;
            hoverIconStyleOriginal = HoverIconStyle;
            hoverIconChipSizeDipOriginal = HoverIconChipSizeDip;
            hoverIconChipPaddingDipOriginal = HoverIconChipPaddingDip;
            hoverIconChipShapeOriginal = HoverIconChipShape;
            useThemeChromeOriginal = UseThemeChrome;
            hoverBackgroundStyleOriginal = HoverBackgroundStyle;
            hoverChromeBackgroundHexOriginal = HoverChromeBackgroundHex;
            hoverChromeBorderHexOriginal = HoverChromeBorderHex;
            hoverChromeDividerHexOriginal = HoverChromeDividerHex;
            hoverChromeIconHexOriginal = HoverChromeIconHex;
            hoverChromeIconBackgroundHexOriginal = HoverChromeIconBackgroundHex;
            hoverChromeTextHexOriginal = HoverChromeTextHex;
            hoverChromeBackgroundOpacityOriginal = HoverChromeBackgroundOpacity;
            selectedFieldKeysOriginal = new List<string>(SelectedFieldKeys);
            disabledFieldKeysOrderOriginal = new List<string>(DisabledFieldKeysOrder);
        }

        public void CancelEdit()
        {
            SuppressSettingsViewRebuilds = true;
            try
            {
                HoverWidth = hoverWidthOriginal;
                ShowDelayMs = showDelayMsOriginal;
                HoverFieldBlockSpacingDip = hoverFieldBlockSpacingDipOriginal;
                HoverContentPaddingDip = hoverContentPaddingDipOriginal;
                HoverDisabled = hoverDisabledOriginal;
                HoverDisabledInFullscreen = hoverDisabledInFullscreenOriginal;
                HideFieldTitlesInHover = hideFieldTitlesInHoverOriginal;
                ShowFieldInlineIconsInHover = showFieldInlineIconsInHoverOriginal;
                HideIconChipBackground = hideIconChipBackgroundOriginal;
                HideFieldDividers = hideFieldDividersOriginal;
                HidePanelBorder = hidePanelBorderOriginal;
                HoverBodyFontSize = hoverBodyFontSizeOriginal;
                HoverTitleFontSize = hoverTitleFontSizeOriginal;
                HoverIconStyle = hoverIconStyleOriginal;
                HoverIconChipSizeDip = hoverIconChipSizeDipOriginal;
                HoverIconChipPaddingDip = hoverIconChipPaddingDipOriginal;
                HoverIconChipShape = hoverIconChipShapeOriginal;
                HoverBackgroundStyle = hoverBackgroundStyleOriginal;
                RunWithThemeHexSync(() =>
                {
                    UseThemeChrome = useThemeChromeOriginal;
                    HoverChromeBackgroundHex = hoverChromeBackgroundHexOriginal;
                    HoverChromeBorderHex = hoverChromeBorderHexOriginal;
                    HoverChromeDividerHex = hoverChromeDividerHexOriginal;
                    HoverChromeIconHex = hoverChromeIconHexOriginal;
                    HoverChromeIconBackgroundHex = hoverChromeIconBackgroundHexOriginal;
                    HoverChromeTextHex = hoverChromeTextHexOriginal;
                });
                HoverChromeBackgroundOpacity = hoverChromeBackgroundOpacityOriginal;
                SelectedFieldKeys = new List<string>(selectedFieldKeysOriginal ?? new List<string>(FactoryDefaultSelectedKeys));
                DisabledFieldKeysOrder = new List<string>(disabledFieldKeysOrderOriginal ?? new List<string>());
            }
            finally
            {
                SuppressSettingsViewRebuilds = false;
                SuppressHoverLiveUpdates = false;
            }

            plugin.NotifyHoverSettingsApplied();
        }

        public void EndEdit()
        {
            // Persist only. Do not re-assign live properties here — that rebuilt the
            // settings preview (library art scans) on the UI thread and froze Save.
            plugin.SavePluginSettings(ToPersistedState());
            SuppressHoverLiveUpdates = false;
            plugin.NotifyHoverSettingsApplied();
        }

        private GameHoverDetailsPersistedState ToPersistedState()
        {
            return new GameHoverDetailsPersistedState
            {
                HoverWidth = HoverWidth,
                ShowDelayMs = ShowDelayMs,
                HoverFieldBlockSpacingDip = HoverFieldBlockSpacingDip,
                HoverContentPaddingDip = HoverContentPaddingDip,
                HoverDisabled = HoverDisabled,
                HoverDisabledInFullscreen = HoverDisabledInFullscreen,
                HideFieldTitlesInHover = HideFieldTitlesInHover,
                ShowFieldInlineIconsInHover = ShowFieldInlineIconsInHover,
                HideIconChipBackground = HideIconChipBackground,
                HideFieldDividers = HideFieldDividers,
                HidePanelBorder = HidePanelBorder,
                HoverBodyFontSize = HoverBodyFontSize,
                HoverTitleFontSize = HoverTitleFontSize,
                HoverIconStyle = HoverIconStyle,
                HoverIconChipSizeDip = HoverIconChipSizeDip,
                HoverIconChipPaddingDip = HoverIconChipPaddingDip,
                HoverIconChipShape = HoverIconChipShape,
                UseThemeChrome = UseThemeChrome,
                HoverBackgroundStyle = HoverBackgroundStyle,
                HoverChromeBackgroundHex = HoverChromeBackgroundHex,
                HoverChromeBorderHex = HoverChromeBorderHex,
                HoverChromeDividerHex = HoverChromeDividerHex,
                HoverChromeIconHex = HoverChromeIconHex,
                HoverChromeIconBackgroundHex = HoverChromeIconBackgroundHex,
                HoverChromeTextHex = HoverChromeTextHex,
                HoverChromeBackgroundOpacity = HoverChromeBackgroundOpacity,
                SelectedFieldKeys = new List<string>(SelectedFieldKeys),
                DisabledFieldKeysOrder = new List<string>(DisabledFieldKeysOrder)
            };
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            if (HoverWidth < MinWidth || HoverWidth > MaxWidth)
            {
                errors.Add($"Hover width must be between {MinWidth} and {MaxWidth} pixels.");
            }

            if (HoverFieldBlockSpacingDip < MinFieldBlockSpacingDip || HoverFieldBlockSpacingDip > MaxFieldBlockSpacingDip)
            {
                errors.Add($"Field spacing must be between {MinFieldBlockSpacingDip} and {MaxFieldBlockSpacingDip} pixels.");
            }

            if (HoverContentPaddingDip < MinContentPaddingDip || HoverContentPaddingDip > MaxContentPaddingDip)
            {
                errors.Add($"List padding must be between {MinContentPaddingDip} and {MaxContentPaddingDip} pixels.");
            }

            if (HoverChromeBackgroundOpacity < MinChromeOpacity || HoverChromeBackgroundOpacity > MaxChromeOpacity)
            {
                errors.Add($"Background opacity must be between {MinChromeOpacity} and {MaxChromeOpacity} percent.");
            }

            if (HoverBodyFontSize < MinBodyFontSize || HoverBodyFontSize > MaxBodyFontSize)
            {
                errors.Add($"Regular text size must be between {MinBodyFontSize} and {MaxBodyFontSize}.");
            }

            if (HoverTitleFontSize < MinTitleFontSize || HoverTitleFontSize > MaxTitleFontSize)
            {
                errors.Add($"Title text size must be between {MinTitleFontSize} and {MaxTitleFontSize}.");
            }

            if (HoverIconChipSizeDip < MinIconChipSizeDip || HoverIconChipSizeDip > MaxIconChipSizeDip)
            {
                errors.Add($"Icon size must be between {MinIconChipSizeDip} and {MaxIconChipSizeDip} pixels.");
            }

            if (HoverIconChipPaddingDip < MinIconChipPaddingDip || HoverIconChipPaddingDip > MaxIconChipPaddingDip)
            {
                errors.Add($"Icon padding must be between {MinIconChipPaddingDip} and {MaxIconChipPaddingDip} pixels.");
            }

            if (!HoverChromePalette.TryParseHex(HoverChromeBackgroundHex, out _))
            {
                errors.Add("Hover background color is not a valid hex color.");
            }

            if (!HoverChromePalette.TryParseHex(HoverChromeBorderHex, out _))
            {
                errors.Add("Hover border color is not a valid hex color.");
            }

            if (!HoverChromePalette.TryParseHex(HoverChromeDividerHex, out _))
            {
                errors.Add("Hover divider color is not a valid hex color.");
            }

            if (!HoverChromePalette.TryParseHex(HoverChromeIconHex, out _))
            {
                errors.Add("Hover icon color is not a valid hex color.");
            }

            if (!HoverChromePalette.TryParseHex(HoverChromeIconBackgroundHex, out _))
            {
                errors.Add("Hover icon background color is not a valid hex color.");
            }

            if (!HoverChromePalette.TryParseHex(HoverChromeTextHex, out _))
            {
                errors.Add("Hover text color is not a valid hex color.");
            }

            if (SelectedFieldKeys.Count == 0)
            {
                errors.Add("Select at least one field.");
            }

            return errors.Count == 0;
        }

        internal static string NormalizeIconChipShape(string value)
        {
            if (string.Equals(value, IconChipShapeRectangle, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Square", System.StringComparison.OrdinalIgnoreCase))
            {
                return IconChipShapeRectangle;
            }

            if (string.Equals(value, IconChipShapeRounded, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "RoundedRectangle", System.StringComparison.OrdinalIgnoreCase))
            {
                return IconChipShapeRounded;
            }

            if (string.Equals(value, IconChipShapeSoftRounded, System.StringComparison.OrdinalIgnoreCase))
            {
                return IconChipShapeSoftRounded;
            }

            if (string.Equals(value, IconChipShapeSquircle, System.StringComparison.OrdinalIgnoreCase))
            {
                return IconChipShapeSquircle;
            }

            if (string.Equals(value, IconChipShapeArch, System.StringComparison.OrdinalIgnoreCase))
            {
                return IconChipShapeArch;
            }

            if (string.Equals(value, IconChipShapeTile, System.StringComparison.OrdinalIgnoreCase))
            {
                return IconChipShapeTile;
            }

            if (string.Equals(value, IconChipShapeLeaf, System.StringComparison.OrdinalIgnoreCase))
            {
                return IconChipShapeLeaf;
            }

            return IconChipShapeCircle;
        }

        internal static CornerRadius ResolveIconChipCornerRadius(string shape, double chipSize)
        {
            var half = chipSize / 2.0;
            if (half < 0)
            {
                half = 0;
            }

            switch (NormalizeIconChipShape(shape))
            {
                case IconChipShapeRectangle:
                    return new CornerRadius(0);
                case IconChipShapeRounded:
                    return new CornerRadius(System.Math.Max(3, chipSize * 0.18));
                case IconChipShapeSoftRounded:
                    return new CornerRadius(System.Math.Max(4, chipSize * 0.28));
                case IconChipShapeSquircle:
                    return new CornerRadius(System.Math.Max(6, chipSize * 0.38));
                case IconChipShapeArch:
                    return new CornerRadius(half, half, 0, 0);
                case IconChipShapeTile:
                    return new CornerRadius(0, 0, half, half);
                case IconChipShapeLeaf:
                    return new CornerRadius(half, 0, half, 0);
                default:
                    return new CornerRadius(half);
            }
        }

        private static int ClampIconChipPaddingDip(int v)
        {
            if (v < MinIconChipPaddingDip)
            {
                return MinIconChipPaddingDip;
            }

            return v > MaxIconChipPaddingDip ? MaxIconChipPaddingDip : v;
        }

        internal static string NormalizeIconStyle(string value)
        {
            if (string.Equals(value, IconStylePhosphor, System.StringComparison.OrdinalIgnoreCase))
            {
                return IconStylePhosphor;
            }

            if (string.Equals(value, IconStyleHugeIcons, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Huge", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Hugeicons", System.StringComparison.OrdinalIgnoreCase))
            {
                return IconStyleHugeIcons;
            }

            return IconStyleUnicons;
        }

        private static double ClampBodyFontSize(double v)
        {
            if (v < MinBodyFontSize)
            {
                return MinBodyFontSize;
            }

            return v > MaxBodyFontSize ? MaxBodyFontSize : v;
        }

        private static double ClampTitleFontSize(double v)
        {
            if (v < MinTitleFontSize)
            {
                return MinTitleFontSize;
            }

            return v > MaxTitleFontSize ? MaxTitleFontSize : v;
        }

        private static int ClampIconChipSizeDip(int v)
        {
            if (v < MinIconChipSizeDip)
            {
                return MinIconChipSizeDip;
            }

            return v > MaxIconChipSizeDip ? MaxIconChipSizeDip : v;
        }

        private static string NormalizeBackgroundStyle(string value)
        {
            if (string.Equals(value, BackgroundStyleGameCover, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "GameBackground", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Background", System.StringComparison.OrdinalIgnoreCase))
            {
                return BackgroundStyleGameCover;
            }

            return BackgroundStyleRegular;
        }

        private static int ClampWidth(int v)
        {
            if (v < MinWidth)
            {
                return MinWidth;
            }

            return v > MaxWidth ? MaxWidth : v;
        }

        private static int ClampShowDelayMs(int v)
        {
            if (v < MinShowDelayMs)
            {
                return MinShowDelayMs;
            }

            return v > MaxShowDelayMs ? MaxShowDelayMs : v;
        }

        private static int ClampFieldBlockSpacingDip(int v)
        {
            if (v < MinFieldBlockSpacingDip)
            {
                return MinFieldBlockSpacingDip;
            }

            return v > MaxFieldBlockSpacingDip ? MaxFieldBlockSpacingDip : v;
        }

        private static int ClampContentPaddingDip(int v)
        {
            if (v < MinContentPaddingDip)
            {
                return MinContentPaddingDip;
            }

            return v > MaxContentPaddingDip ? MaxContentPaddingDip : v;
        }

        private static int ClampChromeOpacity(int v)
        {
            if (v < MinChromeOpacity)
            {
                return MinChromeOpacity;
            }

            return v > MaxChromeOpacity ? MaxChromeOpacity : v;
        }

        private static List<string> NormalizeKeys(List<string> keys)
        {
            var seen = new HashSet<string>();
            var list = new List<string>();
            foreach (var k in keys ?? new List<string>())
            {
                if (string.IsNullOrEmpty(k) || !HoverFieldCatalog.IsKnownKey(k) || seen.Contains(k))
                {
                    continue;
                }

                seen.Add(k);
                list.Add(k);
            }

            if (list.Count == 0)
            {
                return new List<string>(FactoryDefaultSelectedKeys);
            }

            return list;
        }

        private void CoalesceDisabledOrder()
        {
            var all = HoverFieldCatalog.GetAllKeysInCatalogOrder();
            var enabled = new HashSet<string>(selectedFieldKeys);
            var next = new List<string>();
            foreach (var k in disabledFieldKeysOrder)
            {
                if (HoverFieldCatalog.IsKnownKey(k) && !enabled.Contains(k) && !next.Contains(k))
                {
                    next.Add(k);
                }
            }

            foreach (var k in all)
            {
                if (!enabled.Contains(k) && !next.Contains(k))
                {
                    next.Add(k);
                }
            }

            if (ListsEqual(disabledFieldKeysOrder, next))
            {
                return;
            }

            SetValue(ref disabledFieldKeysOrder, next, nameof(DisabledFieldKeysOrder));
        }

        private static bool ListsEqual(List<string> a, List<string> b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a == null || b == null || a.Count != b.Count)
            {
                return false;
            }

            for (var i = 0; i < a.Count; i++)
            {
                if (!string.Equals(a[i], b[i], System.StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
