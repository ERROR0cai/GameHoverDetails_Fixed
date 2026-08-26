using System.Collections.Generic;
using System.Linq;
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
        private const int MinChromeOpacity = 0;
        private const int MaxChromeOpacity = 100;
        private const int DefaultChromeOpacity = 100;

        private static readonly string[] FactoryDefaultSelectedKeys = { "Icon", "Name", "LastPlayed" };

        [DontSerialize]
        private GameHoverDetailsPlugin plugin;

        private int hoverWidth = 360;
        private int showDelayMs;
        private int hoverFieldBlockSpacingDip = DefaultFieldBlockSpacingDip;
        private bool hoverDisabled;
        private bool hoverDisabledInFullscreen = true;
        private bool hideFieldTitlesInHover;
        private bool showFieldInlineIconsInHover;
        private bool useThemeChrome = true;
        private string hoverChromeBackgroundHex = HoverChromePalette.DefaultFillHex;
        private string hoverChromeBorderHex = HoverChromePalette.DefaultBorderHex;
        private string hoverChromeIconHex = HoverChromePalette.DefaultIconHex;
        private string hoverChromeIconBackgroundHex = HoverChromePalette.DefaultIconBackgroundHex;
        private string hoverChromeTextHex = HoverChromePalette.DefaultTextHex;
        private int hoverChromeBackgroundOpacity = DefaultChromeOpacity;
        private List<string> selectedFieldKeys = new List<string>(FactoryDefaultSelectedKeys);
        private List<string> disabledFieldKeysOrder = new List<string>();

        private int hoverWidthOriginal;
        private int showDelayMsOriginal;
        private int hoverFieldBlockSpacingDipOriginal;
        private bool hoverDisabledOriginal;
        private bool hoverDisabledInFullscreenOriginal;
        private bool hideFieldTitlesInHoverOriginal;
        private bool showFieldInlineIconsInHoverOriginal;
        private bool useThemeChromeOriginal;
        private string hoverChromeBackgroundHexOriginal;
        private string hoverChromeBorderHexOriginal;
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
            set => SetValue(ref showFieldInlineIconsInHover, value, nameof(ShowFieldInlineIconsInHover));
        }

        [DontSerialize]
        private bool syncingThemeIntoHex;

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
                HoverChromeIconBackgroundHex = hexes.IconBackground;
            });
        }

        /// <summary>Restore factory colors and turn off theme-sync.</summary>
        public void ResetCustomChromeColors()
        {
            UseThemeChrome = false;
            HoverChromeBackgroundHex = HoverChromePalette.DefaultFillHex;
            HoverChromeBorderHex = HoverChromePalette.DefaultBorderHex;
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
                hoverDisabled = saved.HoverDisabled;
                hoverDisabledInFullscreen = saved.HoverDisabledInFullscreen ?? true;
                hideFieldTitlesInHover = saved.HideFieldTitlesInHover;
                showFieldInlineIconsInHover = saved.ShowFieldInlineIconsInHover;
                useThemeChrome = saved.UseThemeChrome ?? true;
                hoverChromeBackgroundHex = HoverChromePalette.NormalizeHexOrDefault(
                    saved.HoverChromeBackgroundHex,
                    HoverChromePalette.DefaultFillHex);
                hoverChromeBorderHex = HoverChromePalette.NormalizeHexOrDefault(
                    saved.HoverChromeBorderHex,
                    HoverChromePalette.DefaultBorderHex);
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
            hoverWidthOriginal = HoverWidth;
            showDelayMsOriginal = ShowDelayMs;
            hoverFieldBlockSpacingDipOriginal = HoverFieldBlockSpacingDip;
            hoverDisabledOriginal = HoverDisabled;
            hoverDisabledInFullscreenOriginal = HoverDisabledInFullscreen;
            hideFieldTitlesInHoverOriginal = HideFieldTitlesInHover;
            showFieldInlineIconsInHoverOriginal = ShowFieldInlineIconsInHover;
            useThemeChromeOriginal = UseThemeChrome;
            hoverChromeBackgroundHexOriginal = HoverChromeBackgroundHex;
            hoverChromeBorderHexOriginal = HoverChromeBorderHex;
            hoverChromeIconHexOriginal = HoverChromeIconHex;
            hoverChromeIconBackgroundHexOriginal = HoverChromeIconBackgroundHex;
            hoverChromeTextHexOriginal = HoverChromeTextHex;
            hoverChromeBackgroundOpacityOriginal = HoverChromeBackgroundOpacity;
            selectedFieldKeysOriginal = new List<string>(SelectedFieldKeys);
            disabledFieldKeysOrderOriginal = new List<string>(DisabledFieldKeysOrder);
        }

        public void CancelEdit()
        {
            HoverWidth = hoverWidthOriginal;
            ShowDelayMs = showDelayMsOriginal;
            HoverFieldBlockSpacingDip = hoverFieldBlockSpacingDipOriginal;
            HoverDisabled = hoverDisabledOriginal;
            HoverDisabledInFullscreen = hoverDisabledInFullscreenOriginal;
            HideFieldTitlesInHover = hideFieldTitlesInHoverOriginal;
            ShowFieldInlineIconsInHover = showFieldInlineIconsInHoverOriginal;
            RunWithThemeHexSync(() =>
            {
                UseThemeChrome = useThemeChromeOriginal;
                HoverChromeBackgroundHex = hoverChromeBackgroundHexOriginal;
                HoverChromeBorderHex = hoverChromeBorderHexOriginal;
                HoverChromeIconHex = hoverChromeIconHexOriginal;
                HoverChromeIconBackgroundHex = hoverChromeIconBackgroundHexOriginal;
                HoverChromeTextHex = hoverChromeTextHexOriginal;
            });
            HoverChromeBackgroundOpacity = hoverChromeBackgroundOpacityOriginal;
            SelectedFieldKeys = new List<string>(selectedFieldKeysOriginal ?? new List<string>(FactoryDefaultSelectedKeys));
            DisabledFieldKeysOrder = new List<string>(disabledFieldKeysOrderOriginal ?? new List<string>());
        }

        public void EndEdit()
        {
            // Persist only. Re-assigning hex/lists here fired PropertyChanged (and used to
            // uncheck theme-sync) while the settings view was still loaded, which rebuilt
            // the preview and hover on the UI thread for a couple of seconds.
            plugin.SavePluginSettings(ToPersistedState());
        }

        private GameHoverDetailsPersistedState ToPersistedState()
        {
            return new GameHoverDetailsPersistedState
            {
                HoverWidth = HoverWidth,
                ShowDelayMs = ShowDelayMs,
                HoverFieldBlockSpacingDip = HoverFieldBlockSpacingDip,
                HoverDisabled = HoverDisabled,
                HoverDisabledInFullscreen = HoverDisabledInFullscreen,
                HideFieldTitlesInHover = HideFieldTitlesInHover,
                ShowFieldInlineIconsInHover = ShowFieldInlineIconsInHover,
                UseThemeChrome = UseThemeChrome,
                HoverChromeBackgroundHex = HoverChromeBackgroundHex,
                HoverChromeBorderHex = HoverChromeBorderHex,
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

            if (HoverChromeBackgroundOpacity < MinChromeOpacity || HoverChromeBackgroundOpacity > MaxChromeOpacity)
            {
                errors.Add($"Background opacity must be between {MinChromeOpacity} and {MaxChromeOpacity} percent.");
            }

            if (!HoverChromePalette.TryParseHex(HoverChromeBackgroundHex, out _))
            {
                errors.Add("Hover background color is not a valid hex color.");
            }

            if (!HoverChromePalette.TryParseHex(HoverChromeBorderHex, out _))
            {
                errors.Add("Hover border color is not a valid hex color.");
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
