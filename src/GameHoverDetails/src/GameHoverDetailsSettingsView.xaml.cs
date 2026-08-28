using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace GameHoverDetails
{
    public partial class GameHoverDetailsSettingsView : UserControl
    {
        private const double InnerScrollEpsilon = 0.5;

        private GameHoverDetailsSettings boundSettings;
        private bool suppressAddComboSelectionChanged;
        private bool suppressAddFieldTextChanged;
        private bool addFieldTextHandlerAttached;
        private string addFieldSearchText = string.Empty;
        /// <summary>Skip clearing search when the dropdown closes because a field was just added.</summary>
        private bool keepAddFieldSearchOnClose;
        /// <summary>Set when an add was triggered from item PreviewMouseDown so matching PreviewMouseUp does not dismiss the dropdown.</summary>
        private bool addFieldComboItemHandledMouseDown;
        private bool fieldsListWheelHooked;
        private Game previewSampleGame;
        private ImageSource cachedFallbackIcon;
        private ImageSource cachedPreviewCover;
        private readonly Dictionary<string, ImageSource> previewArtByField = new Dictionary<string, ImageSource>();
        private bool attaching;
        private bool previewRefreshQueued;

        /// <summary>Stable ItemsSource for Add field — sync in place so the open dropdown does not close/reopen on each add.</summary>
        private readonly ObservableCollection<AddFieldOption> addFieldComboItems = new ObservableCollection<AddFieldOption>();

        private sealed class AddFieldOption
        {
            public AddFieldOption(string key, string displayName, string iconStyle)
            {
                Key = key;
                DisplayName = displayName;
                IconStyle = iconStyle ?? GameHoverDetailsSettings.IconStyleUnicons;
            }

            public string Key { get; }
            public string DisplayName { get; }
            public string IconStyle { get; }
            public string SettingsGlyph => HoverFieldCatalog.GetGlyph(Key, IconStyle);
            public FontFamily GlyphFontFamily => HoverFieldCatalog.GetGlyphFontFamily(IconStyle);

            public override string ToString()
            {
                return DisplayName;
            }
        }

        private sealed class EnabledFieldRow
        {
            public EnabledFieldRow(string key, string displayName, int index, int count, string iconStyle)
            {
                Key = key;
                DisplayName = displayName;
                Index = index;
                Count = count;
                IconStyle = iconStyle ?? GameHoverDetailsSettings.IconStyleUnicons;
            }

            public string Key { get; }
            public string DisplayName { get; }
            public int Index { get; }
            public int Count { get; }
            public string IconStyle { get; }
            public bool CanMoveUp => Index > 0;
            public bool CanMoveDown => Index < Count - 1;
            public string SettingsGlyph => HoverFieldCatalog.GetGlyph(Key, IconStyle);
            public FontFamily GlyphFontFamily => HoverFieldCatalog.GetGlyphFontFamily(IconStyle);
        }

        private sealed class PreviewFieldRow
        {
            public PreviewFieldRow(
                string fieldKey,
                string displayName,
                string sampleValue,
                string glyphText,
                bool showInlineGlyph,
                bool showFieldTitleRow,
                bool showTopSeparator,
                double separatorPadDip,
                bool isLastBlock,
                double fieldBlockSpacingDip,
                ImageSource previewArt,
                double previewInnerContentWidthDip,
                bool showIconBesideGameName,
                string besideIconGameName,
                HoverChromePalette palette,
                GameHoverDetailsSettings settings)
            {
                DisplayName = displayName;
                SampleValue = sampleValue ?? string.Empty;
                GlyphText = glyphText;
                ShowInlineGlyph = showInlineGlyph;
                ShowFieldTitleRow = showFieldTitleRow;
                ShowTopSeparator = showTopSeparator;
                SeparatorPadDip = separatorPadDip;
                ContentBlockMargin = new Thickness(0, 0, 0, isLastBlock ? 0 : fieldBlockSpacingDip * 0.5);
                PreviewArt = previewArt;
                ShowIconBesideGameName = showIconBesideGameName;
                BesideIconGameName = besideIconGameName ?? string.Empty;
                BodyForeground = palette?.BodyText;
                LabelForeground = palette?.LabelText;
                ChipBackground = palette?.GlyphChipBackground;
                ChipGlyphForeground = palette?.GlyphChipGlyph;
                SeparatorBrush = palette?.Separator;
                SeparatorLineHeight = settings != null && settings.HideFieldDividers ? 0 : 1;
                var chip = settings != null
                    ? (double)settings.HoverIconChipOuterSizeDip
                    : GameHoverDetailsSettings.DefaultIconChipSizeDip;
                ChipSize = chip;
                ChipCornerRadius = settings != null
                    ? settings.ResolveIconChipCornerRadius()
                    : new CornerRadius(chip / 2);
                GlyphFontSize = settings?.HoverIconGlyphFontSize ?? 15;
                GlyphFontFamily = HoverFieldCatalog.GetGlyphFontFamily(settings?.HoverIconStyle);
                BodyFontSize = settings?.HoverBodyFontSize ?? GameHoverDetailsSettings.DefaultBodyFontSize;
                BodyLineHeight = settings?.HoverBodyLineHeight ?? 18;
                BodyMaxHeight = BodyLineHeight * HoverDetailValuePresenter.MaxValueLines;
                TitleFontSize = settings?.HoverTitleFontSize ?? GameHoverDetailsSettings.DefaultTitleFontSize;
                TitleLineHeight = settings?.HoverTitleLineHeight ?? 14;
                BesideIconNameMaxWidth = showIconBesideGameName
                    ? System.Math.Max(48.0, previewInnerContentWidthDip - 40.0 - 10.0)
                    : 0;
                StatTextMaxWidth = System.Math.Max(48.0, previewInnerContentWidthDip - chip - 10.0);
                if (previewArt != null)
                {
                    switch (fieldKey)
                    {
                        case "Icon":
                            PreviewArtMaxWidth = 40;
                            PreviewArtMaxHeight = 40;
                            break;
                        case "CoverImage":
                            PreviewArtMaxWidth = previewInnerContentWidthDip;
                            PreviewArtMaxHeight = 220;
                            break;
                        default:
                            PreviewArtMaxWidth = previewInnerContentWidthDip;
                            PreviewArtMaxHeight = 140;
                            break;
                    }
                }
            }

            public string DisplayName { get; }
            public string SampleValue { get; }
            public string GlyphText { get; }
            public FontFamily GlyphFontFamily { get; }
            public double ChipSize { get; }
            public CornerRadius ChipCornerRadius { get; }
            public double GlyphFontSize { get; }
            public double BodyFontSize { get; }
            public double BodyLineHeight { get; }
            public double BodyMaxHeight { get; }
            public double TitleFontSize { get; }
            public double TitleLineHeight { get; }
            public bool ShowInlineGlyph { get; }
            /// <summary>Title row matches hover: off for game-art keys (Icon, cover, background).</summary>
            public bool ShowFieldTitleRow { get; }
            public bool ShowTopSeparator { get; }
            public double SeparatorPadDip { get; }
            /// <summary>Bottom half-inset after each block (pairs with separator + next block top half); zero on last row (matches hover after <c>TrimLastContentBottomMargin</c>).</summary>
            public Thickness ContentBlockMargin { get; }
            public Thickness SeparatorMargin => new Thickness(0, SeparatorPadDip, 0, SeparatorPadDip);
            public ImageSource PreviewArt { get; }
            public bool ShowPreviewArt => PreviewArt != null;
            public bool ShowSampleText => !ShowPreviewArt || !string.IsNullOrWhiteSpace(SampleValue);
            public double PreviewArtMaxWidth { get; }
            public double PreviewArtMaxHeight { get; }
            public double StatTextMaxWidth { get; }
            /// <summary>When Icon is the only selected field and art loads, hover shows game name beside the icon (vertically centered).</summary>
            public bool ShowIconBesideGameName { get; }
            public string BesideIconGameName { get; }
            public double BesideIconNameMaxWidth { get; }
            public Brush BodyForeground { get; }
            public Brush LabelForeground { get; }
            public Brush ChipBackground { get; }
            public Brush ChipGlyphForeground { get; }
            public Brush SeparatorBrush { get; }
            public double SeparatorLineHeight { get; }
            public bool ShowArtVerticalStack => ShowPreviewArt && !ShowIconBesideGameName;
            public bool ShowIconBesideGameNameRow => ShowIconBesideGameName;

            public bool ShowStatRowLayout => !ShowPreviewArt && ShowFieldTitleRow && ShowInlineGlyph;

            public bool ShowTitleBodyNoIconLayout => !ShowPreviewArt && ShowFieldTitleRow && !ShowInlineGlyph;

            public bool ShowChipValueNoTitleLayout => !ShowPreviewArt && !ShowFieldTitleRow && ShowInlineGlyph;

            public bool ShowValueOnlyLayout => !ShowPreviewArt && !ShowFieldTitleRow && !ShowInlineGlyph;

            private const double FieldTitleToValueGapDip = 4;

            /// <summary>Muted title row: after a separator, top padding matches hover <c>AppendTextDetailInner</c> label <c>topInset</c> (half of field spacing).</summary>
            public Thickness PreviewFieldTitleMargin =>
                new Thickness(0, ShowTopSeparator ? SeparatorPadDip : 0, 0, FieldTitleToValueGapDip);

            /// <summary>Stat/chip row: whole grid gets top inset after a divider (hover applies <c>topInset</c> on the outer grid).</summary>
            public Thickness ContinuationRowOutermostMargin =>
                new Thickness(0, ShowTopSeparator ? SeparatorPadDip : 0, 0, 0);

        }

        public GameHoverDetailsSettingsView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyLayoutFlow();
            if (FieldsList != null && !fieldsListWheelHooked)
            {
                FieldsList.PreviewMouseWheel += FieldsList_PreviewMouseWheel;
                fieldsListWheelHooked = true;
            }

            if (PreviewChromeBody != null)
            {
                PreviewChromeBody.SizeChanged -= PreviewChromeBodyOnSizeChanged;
                PreviewChromeBody.SizeChanged += PreviewChromeBodyOnSizeChanged;
            }

            EnsureAddFieldSearchHooked();
            TryAttachSettings();
        }

        private void ApplyLayoutFlow()
        {
            var api = (DataContext as GameHoverDetailsSettings)?.TryGetPlayniteApi();
            var flow = HoverLoc.LayoutFlow(api, this);
            FlowDirection = flow;
            if (AddFieldCombo != null)
            {
                AddFieldCombo.HorizontalContentAlignment = flow == FlowDirection.RightToLeft
                    ? HorizontalAlignment.Right
                    : HorizontalAlignment.Left;
            }
            if (PreviewChromeBorder != null)
            {
                PreviewChromeBorder.FlowDirection = flow;
                PreviewChromeBorder.HorizontalAlignment = flow == FlowDirection.RightToLeft
                    ? HorizontalAlignment.Right
                    : HorizontalAlignment.Left;
            }
        }

        private void UserControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            TryAttachSettings();
        }

        /// <summary>Subscribe to settings and refresh lists; safe if <see cref="DataContext"/> is set after <see cref="UserControl.Loaded"/>.</summary>
        private void TryAttachSettings()
        {
            var s = DataContext as GameHoverDetailsSettings;
            if (s == null)
            {
                if (boundSettings != null)
                {
                    boundSettings.PropertyChanged -= BoundSettingsOnPropertyChanged;
                    boundSettings = null;
                }

                return;
            }

            if (ReferenceEquals(boundSettings, s))
            {
                return;
            }

            if (boundSettings != null)
            {
                boundSettings.PropertyChanged -= BoundSettingsOnPropertyChanged;
            }

            attaching = true;
            try
            {
                boundSettings = s;
                s.PropertyChanged += BoundSettingsOnPropertyChanged;
                if (s.UseThemeChrome)
                {
                    s.ApplyThemeColorsToPickers();
                }

                ApplyLayoutFlow();
                TryPickPreviewSampleGame();
                RefreshFieldsList();
                RefreshAddCombo();
                RefreshPreviewFields();
                ApplyPreviewChrome();
            }
            finally
            {
                attaching = false;
            }
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (FieldsList != null && fieldsListWheelHooked)
            {
                FieldsList.PreviewMouseWheel -= FieldsList_PreviewMouseWheel;
                fieldsListWheelHooked = false;
            }

            if (boundSettings != null)
            {
                boundSettings.PropertyChanged -= BoundSettingsOnPropertyChanged;
            }

            if (PreviewChromeBody != null)
            {
                PreviewChromeBody.SizeChanged -= PreviewChromeBodyOnSizeChanged;
            }

            boundSettings = null;
            previewSampleGame = null;
            cachedFallbackIcon = null;
            cachedPreviewCover = null;
            previewArtByField.Clear();
        }

        private void FieldsList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (FieldsScrollViewer == null || !(sender is FrameworkElement listHost))
            {
                return;
            }

            var inner = FindVisualChild<ScrollViewer>(listHost);
            if (inner == null || inner.ScrollableHeight <= InnerScrollEpsilon)
            {
                var delta = e.Delta;
                var wheelLine = Mouse.MouseWheelDeltaForOneLine;
                if (wheelLine == 0)
                {
                    wheelLine = 120;
                }

                var step = delta / (double)wheelLine * 48.0;
                var next = FieldsScrollViewer.VerticalOffset - step;
                if (next < 0)
                {
                    next = 0;
                }
                else if (next > FieldsScrollViewer.ScrollableHeight)
                {
                    next = FieldsScrollViewer.ScrollableHeight;
                }

                FieldsScrollViewer.ScrollToVerticalOffset(next);
                e.Handled = true;
            }
        }

        private void BoundSettingsOnPropertyChanged(object o, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (attaching || boundSettings == null || boundSettings.SuppressSettingsViewRebuilds)
            {
                return;
            }

            if (e.PropertyName == nameof(GameHoverDetailsSettings.SelectedFieldKeys) ||
                e.PropertyName == nameof(GameHoverDetailsSettings.DisabledFieldKeysOrder) ||
                e.PropertyName == nameof(GameHoverDetailsSettings.SelectedFieldCount))
            {
                RefreshFieldsList();
                RefreshAddCombo();
                QueuePreviewRefresh();
                return;
            }

            if (e.PropertyName == nameof(GameHoverDetailsSettings.UseThemeChrome))
            {
                if (boundSettings.UseThemeChrome)
                {
                    boundSettings.ApplyThemeColorsToPickers();
                }

                QueuePreviewRefresh();
                return;
            }

            if (e.PropertyName == nameof(GameHoverDetailsSettings.HoverIconStyle) ||
                e.PropertyName == nameof(GameHoverDetailsSettings.HoverIconFontFamily))
            {
                RefreshFieldsList();
                RefreshAddCombo();
                QueuePreviewRefresh();
                return;
            }

            if (e.PropertyName == nameof(GameHoverDetailsSettings.HideFieldTitlesInHover) ||
                e.PropertyName == nameof(GameHoverDetailsSettings.HoverTitlesInHover) ||
                e.PropertyName == nameof(GameHoverDetailsSettings.ShowFieldInlineIconsInHover) ||
                e.PropertyName == nameof(GameHoverDetailsSettings.HideIconChipBackground) ||
                e.PropertyName == nameof(GameHoverDetailsSettings.ShowIconChipBackground) ||
                e.PropertyName == nameof(GameHoverDetailsSettings.HideFieldDividers) ||
                e.PropertyName == nameof(GameHoverDetailsSettings.ShowFieldDividers) ||
                e.PropertyName == nameof(GameHoverDetailsSettings.HidePanelBorder) ||
                e.PropertyName == nameof(GameHoverDetailsSettings.ShowPanelBorder) ||
                e.PropertyName == nameof(GameHoverDetailsSettings.HoverFieldBlockSpacingDip) ||
                e.PropertyName == nameof(GameHoverDetailsSettings.HoverContentPaddingDip) ||
                e.PropertyName == nameof(GameHoverDetailsSettings.HoverWidth) ||
                e.PropertyName == nameof(GameHoverDetailsSettings.HoverChromeBackgroundHex) ||
                e.PropertyName == nameof(GameHoverDetailsSettings.HoverChromeBorderHex) ||
                e.PropertyName == nameof(GameHoverDetailsSettings.HoverChromeDividerHex) ||
                e.PropertyName == nameof(GameHoverDetailsSettings.HoverChromeIconBackgroundHex) ||
                e.PropertyName == nameof(GameHoverDetailsSettings.HoverChromeBackgroundOpacity) ||
                e.PropertyName == nameof(GameHoverDetailsSettings.HoverBackgroundStyle) ||
                e.PropertyName == nameof(GameHoverDetailsSettings.HoverBackgroundStyleIndex) ||
                e.PropertyName == nameof(GameHoverDetailsSettings.UseGameBackground) ||
                e.PropertyName == nameof(GameHoverDetailsSettings.HoverBodyFontSize) ||
                e.PropertyName == nameof(GameHoverDetailsSettings.HoverTitleFontSize) ||
                e.PropertyName == nameof(GameHoverDetailsSettings.HoverIconChipSizeDip) ||
                e.PropertyName == nameof(GameHoverDetailsSettings.HoverIconChipPaddingDip) ||
                e.PropertyName == nameof(GameHoverDetailsSettings.HoverIconChipOuterSizeDip) ||
                e.PropertyName == nameof(GameHoverDetailsSettings.HoverIconChipShape) ||
                e.PropertyName == nameof(GameHoverDetailsSettings.HoverIconGlyphFontSize) ||
                e.PropertyName == nameof(GameHoverDetailsSettings.HoverBodyLineHeight) ||
                e.PropertyName == nameof(GameHoverDetailsSettings.HoverTitleLineHeight))
            {
                QueuePreviewRefresh();
            }
        }

        private void QueuePreviewRefresh()
        {
            if (previewRefreshQueued || attaching || boundSettings == null || boundSettings.SuppressSettingsViewRebuilds)
            {
                return;
            }

            previewRefreshQueued = true;
            Dispatcher.BeginInvoke(
                (Action)(() =>
                {
                    previewRefreshQueued = false;
                    if (boundSettings == null || attaching || boundSettings.SuppressSettingsViewRebuilds)
                    {
                        return;
                    }

                    RefreshPreviewFields();
                    ApplyPreviewChrome();
                }),
                DispatcherPriority.Background);
        }

        private void RefreshPreviewFields()
        {
            if (PreviewFieldsList == null || boundSettings == null)
            {
                return;
            }

            var spacing = (double)System.Math.Max(4, System.Math.Min(36, boundSettings.HoverFieldBlockSpacingDip));
            var showGlyph = boundSettings.ShowFieldInlineIconsInHover;
            var showTitles = boundSettings.HoverTitlesInHover;
            var api = boundSettings.TryGetPlayniteApi();
            var game = previewSampleGame;
            var previewCap = boundSettings.PreviewChromeMaxWidth;
            var previewChromeWidth = System.Math.Min(previewCap, System.Math.Max(120, boundSettings.ResolveHoverPanelWidth()));
            var pad = ClampPreviewContentPaddingDip(boundSettings.HoverContentPaddingDip);
            var chromeHorizontalPadding = pad * 2.0;
            var previewInnerContentWidth = System.Math.Max(48.0, previewChromeWidth - chromeHorizontalPadding);
            var separatorPad = spacing * 0.5;
            var palette = HoverChromePalette.Resolve(boundSettings);
            var rows = new List<PreviewFieldRow>();
            var keyList = boundSettings.SelectedFieldKeys.Where(HoverFieldCatalog.IsKnownKey).ToList();
            var iconOnlyBesideName = keyList.Count == 1 && keyList[0] == "Icon";
            for (var i = 0; i < keyList.Count; i++)
            {
                var key = keyList[i];
                var isLastBlock = i == keyList.Count - 1;

                var displayName = HoverFieldCatalog.GetDisplayName(key);
                var glyph = HoverFieldCatalog.GetGlyph(key, boundSettings.HoverIconStyle);
                var inline = showGlyph && !HoverFieldCatalog.IsGameArtImageField(key);
                var showFieldTitleRow = showTitles && !HoverFieldCatalog.IsGameArtImageField(key);
                var showTopSeparator = i > 0;

                string sample;
                ImageSource art = null;
                if (game != null && api != null)
                {
                    if (HoverFieldCatalog.IsGameArtImageField(key))
                    {
                        art = GetCachedPreviewArt(key, game, api);
                        if (key == "Icon" && art == null)
                        {
                            art = TryLoadFallbackLibraryGameIcon(api, game);
                        }

                        sample = art != null ? string.Empty : HoverPreviewSampleText.ForKey(key);
                    }
                    else
                    {
                        var raw = HoverFieldFormatter.Format(key, game, api);
                        var preview = HoverPreviewSampleText.FormatValueForPreview(key, raw);
                        sample = HoverPreviewSampleText.LooksLikeMissingData(preview)
                            ? HoverPreviewSampleText.ForKey(key)
                            : preview;
                    }
                }
                else
                {
                    sample = HoverPreviewSampleText.ForKey(key);
                }

                var showBesideName = iconOnlyBesideName && key == "Icon" && art != null;
                var besideName = !showBesideName
                    ? string.Empty
                    : game != null
                        ? HoverFieldFormatter.Format("Name", game, api)
                        : HoverPreviewSampleText.ForKey("Name");

                rows.Add(new PreviewFieldRow(
                    key,
                    displayName,
                    sample,
                    glyph,
                    inline,
                    showFieldTitleRow,
                    showTopSeparator,
                    separatorPad,
                    isLastBlock,
                    spacing,
                    art,
                    previewInnerContentWidth,
                    showBesideName,
                    besideName,
                    palette,
                    boundSettings));
            }

            PreviewFieldsList.ItemsSource = rows;
        }

        private ImageSource GetCachedPreviewArt(string fieldKey, Game game, IPlayniteAPI api)
        {
            if (previewArtByField.TryGetValue(fieldKey, out var cached))
            {
                return cached;
            }

            var art = HoverBitmapLoader.TryLoadGameArt(fieldKey, game, api);
            previewArtByField[fieldKey] = art;
            return art;
        }

        private const int MaxFallbackArtLoads = 8;
        private const int MaxSampleGameScan = 80;

        /// <summary>
        /// When the preview game has no usable icon, try a few other library games that have an icon path.
        /// Cached for the life of the settings view so slider churn does not hit disk again.
        /// </summary>
        private ImageSource TryLoadFallbackLibraryGameIcon(IPlayniteAPI api, Game previewGame)
        {
            if (cachedFallbackIcon != null)
            {
                return cachedFallbackIcon;
            }

            cachedFallbackIcon = TryLoadFirstLibraryArt(api, previewGame, "Icon");
            return cachedFallbackIcon;
        }

        private ImageSource TryLoadFirstLibraryArt(IPlayniteAPI api, Game skipGame, string fieldKey)
        {
            if (api?.Database?.Games == null)
            {
                return null;
            }

            var attempts = 0;
            foreach (var g in api.Database.Games)
            {
                if (g == null)
                {
                    continue;
                }

                if (skipGame != null && g.Id == skipGame.Id)
                {
                    continue;
                }

                string path;
                switch (fieldKey)
                {
                    case "Icon":
                        path = g.Icon;
                        break;
                    case "CoverImage":
                        path = g.CoverImage;
                        break;
                    case "BackgroundImage":
                        path = g.BackgroundImage;
                        break;
                    default:
                        path = g.BackgroundImage;
                        break;
                }

                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                var bmp = HoverBitmapLoader.TryLoadGameArt(fieldKey, g, api);
                if (bmp != null)
                {
                    return bmp;
                }

                attempts++;
                if (attempts >= MaxFallbackArtLoads)
                {
                    return null;
                }
            }

            return null;
        }

        /// <summary>First library game with usable art metadata; does not walk the whole library.</summary>
        private void TryPickPreviewSampleGame()
        {
            if (previewSampleGame != null || boundSettings == null)
            {
                return;
            }

            var api = boundSettings.TryGetPlayniteApi();
            var games = api?.Database?.Games;
            if (games == null)
            {
                return;
            }

            try
            {
                var preferFanart = boundSettings.IsGameCoverBackgroundStyle;
                Game firstAny = null;
                Game withArt = null;
                var scanned = 0;
                foreach (var g in games)
                {
                    if (g == null)
                    {
                        continue;
                    }

                    if (firstAny == null)
                    {
                        firstAny = g;
                    }

                    var hasFanart = !string.IsNullOrWhiteSpace(g.BackgroundImage);
                    var hasCover = !string.IsNullOrWhiteSpace(g.CoverImage);
                    var hasIcon = !string.IsNullOrWhiteSpace(g.Icon);
                    if (preferFanart ? hasFanart : (hasIcon || hasCover))
                    {
                        withArt = g;
                        break;
                    }

                    scanned++;
                    if (scanned >= MaxSampleGameScan)
                    {
                        break;
                    }
                }

                previewSampleGame = withArt ?? firstAny;
                previewArtByField.Clear();
            }
            catch
            {
                previewSampleGame = null;
            }
        }

        private void RefreshFieldsList()
        {
            if (FieldsList == null || boundSettings == null)
            {
                return;
            }

            var keys = boundSettings.SelectedFieldKeys;
            var n = keys.Count;
            var style = boundSettings.HoverIconStyle;
            FieldsList.ItemsSource = keys
                .Select((k, i) => new EnabledFieldRow(k, HoverFieldCatalog.GetDisplayName(k), i, n, style))
                .ToList();
        }

        private void EnsureAddFieldSearchHooked()
        {
            if (addFieldTextHandlerAttached || AddFieldCombo == null)
            {
                return;
            }

            AddFieldCombo.AddHandler(
                TextBox.TextChangedEvent,
                new TextChangedEventHandler(AddFieldCombo_TextChanged),
                true);
            addFieldTextHandlerAttached = true;
        }

        private static bool MatchesAddFieldSearch(AddFieldOption option, string filter)
        {
            if (option == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(filter))
            {
                return true;
            }

            if (option.DisplayName.IndexOf(filter, StringComparison.CurrentCultureIgnoreCase) >= 0)
            {
                return true;
            }

            return option.Key.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void RefreshAddCombo()
        {
            if (AddFieldCombo == null || boundSettings == null)
            {
                return;
            }

            if (!ReferenceEquals(AddFieldCombo.ItemsSource, addFieldComboItems))
            {
                AddFieldCombo.ItemsSource = addFieldComboItems;
            }

            suppressAddComboSelectionChanged = true;
            suppressAddFieldTextChanged = true;
            try
            {
                var style = boundSettings.HoverIconStyle;
                if (addFieldComboItems.Count > 0 &&
                    !string.Equals(addFieldComboItems[0].IconStyle, style, StringComparison.Ordinal))
                {
                    addFieldComboItems.Clear();
                }

                var filter = (addFieldSearchText ?? string.Empty).Trim();
                var addable = boundSettings.GetAddableKeys()
                    .Select(k => new AddFieldOption(k, HoverFieldCatalog.GetDisplayName(k), style))
                    .ToList();
                var desired = addable
                    .Where(o => MatchesAddFieldSearch(o, filter))
                    .OrderBy(o => o.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

                var desiredKeys = new HashSet<string>(desired.Select(o => o.Key));
                for (var i = addFieldComboItems.Count - 1; i >= 0; i--)
                {
                    if (!desiredKeys.Contains(addFieldComboItems[i].Key))
                    {
                        addFieldComboItems.RemoveAt(i);
                    }
                }

                var currentKeys = new HashSet<string>(addFieldComboItems.Select(o => o.Key));
                foreach (var opt in desired)
                {
                    if (currentKeys.Contains(opt.Key))
                    {
                        continue;
                    }

                    var insert = 0;
                    while (insert < addFieldComboItems.Count &&
                           string.Compare(
                               addFieldComboItems[insert].DisplayName,
                               opt.DisplayName,
                               StringComparison.CurrentCultureIgnoreCase) < 0)
                    {
                        insert++;
                    }

                    addFieldComboItems.Insert(insert, opt);
                    currentKeys.Add(opt.Key);
                }

                AddFieldCombo.SelectedIndex = -1;
                AddFieldCombo.IsEnabled = addable.Count > 0;
                if (!string.Equals(AddFieldCombo.Text ?? string.Empty, addFieldSearchText ?? string.Empty, StringComparison.Ordinal))
                {
                    AddFieldCombo.Text = addFieldSearchText ?? string.Empty;
                }

                UpdateAddFieldSearchHint();
            }
            finally
            {
                suppressAddFieldTextChanged = false;
                suppressAddComboSelectionChanged = false;
            }
        }

        private void UpdateAddFieldSearchHint()
        {
            if (AddFieldSearchHint == null)
            {
                return;
            }

            AddFieldSearchHint.Visibility = string.IsNullOrEmpty(addFieldSearchText)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void OpenAddFieldSelector()
        {
            if (AddFieldCombo == null || !AddFieldCombo.IsEnabled)
            {
                return;
            }

            RefreshAddCombo();
            if (!AddFieldCombo.IsDropDownOpen)
            {
                AddFieldCombo.IsDropDownOpen = true;
            }
        }

        private void AddFieldCombo_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (suppressAddComboSelectionChanged || suppressAddFieldTextChanged || attaching || AddFieldCombo == null)
            {
                return;
            }

            var text = AddFieldCombo.Text ?? string.Empty;
            if (string.Equals(text, addFieldSearchText, StringComparison.Ordinal))
            {
                return;
            }

            addFieldSearchText = text;
            UpdateAddFieldSearchHint();
            RefreshAddCombo();
            if (AddFieldCombo.IsEnabled && !AddFieldCombo.IsDropDownOpen)
            {
                AddFieldCombo.IsDropDownOpen = true;
            }
        }

        private void ClearAddFieldSearch()
        {
            addFieldSearchText = string.Empty;
            if (AddFieldCombo == null)
            {
                return;
            }

            suppressAddFieldTextChanged = true;
            try
            {
                AddFieldCombo.Text = string.Empty;
            }
            finally
            {
                suppressAddFieldTextChanged = false;
            }

            UpdateAddFieldSearchHint();
            RefreshAddCombo();
        }

        /// <summary>
        /// Add from keyboard without using <see cref="ComboBox.SelectionChanged"/> (that path closes the popup).
        /// </summary>
        private void AddFieldCombo_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (suppressAddComboSelectionChanged || boundSettings == null || AddFieldCombo == null)
            {
                return;
            }

            if (e.Key == Key.Escape)
            {
                ClearAddFieldSearch();
                return;
            }

            if (e.Key != Key.Return && e.Key != Key.Enter)
            {
                return;
            }

            var opt = AddFieldCombo.SelectedItem as AddFieldOption ?? addFieldComboItems.FirstOrDefault();
            if (opt == null)
            {
                return;
            }

            TryAddFieldFromCombo(opt);
            e.Handled = true;
        }

        /// <summary>
        /// Intercept item clicks before the ComboBox applies selection and closes the dropdown (avoids flicker).
        /// </summary>
        private void AddFieldComboItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (suppressAddComboSelectionChanged || boundSettings == null || AddFieldCombo == null)
            {
                return;
            }

            if (!(sender is ComboBoxItem item) || !(item.Content is AddFieldOption opt))
            {
                return;
            }

            addFieldComboItemHandledMouseDown = true;
            TryAddFieldFromCombo(opt);
            e.Handled = true;
        }

        private void AddFieldComboItem_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!addFieldComboItemHandledMouseDown)
            {
                return;
            }

            addFieldComboItemHandledMouseDown = false;
            e.Handled = true;
        }

        private void AddFieldCombo_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (FindAncestor<ComboBoxItem>(e.OriginalSource as DependencyObject) != null)
            {
                return;
            }

            OpenAddFieldSelector();
        }

        private void AddFieldCombo_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (FindAncestor<ComboBoxItem>(e.NewFocus as DependencyObject) != null)
            {
                return;
            }

            OpenAddFieldSelector();
        }

        private static T FindAncestor<T>(DependencyObject start) where T : DependencyObject
        {
            var current = start;
            while (current != null)
            {
                if (current is T match)
                {
                    return match;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private void AddFieldCombo_DropDownOpened(object sender, EventArgs e)
        {
            if (AddFieldCombo == null)
            {
                return;
            }

            RefreshAddCombo();
            Dispatcher.BeginInvoke(
                (Action)(() =>
                {
                    if (AddFieldCombo.Template.FindName("PART_EditableTextBox", AddFieldCombo) is TextBox box)
                    {
                        box.Focus();
                        box.CaretIndex = box.Text != null ? box.Text.Length : 0;
                    }
                }),
                DispatcherPriority.Input);
        }

        private void AddFieldCombo_DropDownClosed(object sender, EventArgs e)
        {
            addFieldComboItemHandledMouseDown = false;
            if (keepAddFieldSearchOnClose || (AddFieldCombo != null && AddFieldCombo.IsKeyboardFocusWithin))
            {
                return;
            }

            ClearAddFieldSearch();
        }

        /// <summary>
        /// WPF often closes the dropdown when <see cref="RefreshAddCombo"/> removes the picked row from <see cref="addFieldComboItems"/>.
        /// Reopen after layout so multiple fields can be added without reopening the menu.
        /// </summary>
        private void ScheduleReopenAddFieldDropDownIfNeeded()
        {
            if (AddFieldCombo == null)
            {
                return;
            }

            void Reopen()
            {
                if (AddFieldCombo == null || addFieldComboItems.Count == 0 || !AddFieldCombo.IsEnabled)
                {
                    return;
                }

                AddFieldCombo.IsDropDownOpen = true;
            }

            AddFieldCombo.Dispatcher.BeginInvoke((Action)Reopen, DispatcherPriority.Loaded);
            AddFieldCombo.Dispatcher.BeginInvoke((Action)Reopen, DispatcherPriority.ApplicationIdle);
        }

        private void TryAddFieldFromCombo(AddFieldOption opt)
        {
            if (boundSettings == null || opt == null)
            {
                return;
            }

            keepAddFieldSearchOnClose = true;
            boundSettings.EnableFieldAt(opt.Key, boundSettings.SelectedFieldKeys.Count);

            if (AddFieldCombo != null && addFieldComboItems.Count > 0)
            {
                AddFieldCombo.IsDropDownOpen = true;
            }

            ScheduleReopenAddFieldDropDownIfNeeded();
            Dispatcher.BeginInvoke(
                (Action)(() => keepAddFieldSearchOnClose = false),
                DispatcherPriority.ApplicationIdle);
        }

        private void EnabledMoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (boundSettings == null || !(sender is FrameworkElement fe) || !(fe.DataContext is EnabledFieldRow row))
            {
                return;
            }

            if (row.Index <= 0)
            {
                return;
            }

            boundSettings.MoveEnabled(row.Index, row.Index - 1);
        }

        private void EnabledMoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (boundSettings == null || !(sender is FrameworkElement fe) || !(fe.DataContext is EnabledFieldRow row))
            {
                return;
            }

            if (row.Index >= row.Count - 1)
            {
                return;
            }

            boundSettings.MoveEnabled(row.Index, row.Index + 2);
        }

        private void EnabledRemove_Click(object sender, RoutedEventArgs e)
        {
            if (boundSettings == null || !(sender is FrameworkElement fe) || !(fe.DataContext is EnabledFieldRow row))
            {
                return;
            }

            boundSettings.DisableFieldAt(row.Index, 0);
        }

        private void ApplyPreviewChrome()
        {
            if (PreviewChromeBorder == null || boundSettings == null)
            {
                return;
            }

            var cover = boundSettings.IsGameCoverBackgroundStyle ? TryLoadPreviewCover() : null;
            var coverActive = cover != null;
            if (PreviewCoverImage != null)
            {
                PreviewCoverImage.Source = cover;
                PreviewCoverImage.Visibility = coverActive ? Visibility.Visible : Visibility.Collapsed;
            }

            if (PreviewCoverTint != null)
            {
                PreviewCoverTint.Background = HoverChromePalette.CoverTintBrush;
                var tintOpacity = boundSettings.HoverChromeBackgroundOpacity / 100.0;
                if (tintOpacity < 0)
                {
                    tintOpacity = 0;
                }
                else if (tintOpacity > 1)
                {
                    tintOpacity = 1;
                }

                PreviewCoverTint.Opacity = tintOpacity;
                PreviewCoverTint.Visibility = coverActive ? Visibility.Visible : Visibility.Collapsed;
            }

            HoverChromePalette.ApplyToChromeBorder(PreviewChromeBorder, boundSettings, coverActive);
            PreviewChromeBorder.MaxHeight = double.PositiveInfinity;
            PreviewChromeBorder.MinHeight = 0;
            if (PreviewFieldsList != null)
            {
                var pad = ClampPreviewContentPaddingDip(boundSettings.HoverContentPaddingDip);
                PreviewFieldsList.Margin = new Thickness(pad, pad, pad, pad);
                PreviewFieldsList.VerticalAlignment = VerticalAlignment.Top;
            }

            UpdatePreviewCoverClip();
        }

        private void PreviewChromeBodyOnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdatePreviewCoverClip();
        }

        private void UpdatePreviewCoverClip()
        {
            if (PreviewChromeBody == null)
            {
                return;
            }

            var w = PreviewChromeBody.ActualWidth;
            var h = PreviewChromeBody.ActualHeight;
            if (w <= 0 || h <= 0)
            {
                PreviewChromeBody.Clip = null;
                return;
            }

            PreviewChromeBody.Clip = new RectangleGeometry(new Rect(0, 0, w, h), 7, 7);
        }

        private ImageSource TryLoadPreviewCover()
        {
            if (cachedPreviewCover != null)
            {
                return cachedPreviewCover;
            }

            var api = boundSettings?.TryGetPlayniteApi();
            if (api == null)
            {
                return null;
            }

            var bmp = HoverBitmapLoader.TryLoadGameArt("BackgroundImage", previewSampleGame, api, 720);
            if (bmp != null)
            {
                cachedPreviewCover = bmp;
                return bmp;
            }

            cachedPreviewCover = TryLoadFirstLibraryArt(api, previewSampleGame, "BackgroundImage");
            return cachedPreviewCover;
        }

        private void BackgroundSwatch_Click(object sender, RoutedEventArgs e)
        {
            if (boundSettings == null)
            {
                return;
            }

            if (TryPickHexColor(boundSettings.HoverChromeBackgroundHex, out var hex))
            {
                boundSettings.HoverChromeBackgroundHex = hex;
            }
        }

        private void BorderSwatch_Click(object sender, RoutedEventArgs e)
        {
            if (boundSettings == null)
            {
                return;
            }

            if (TryPickHexColor(boundSettings.HoverChromeBorderHex, out var hex))
            {
                boundSettings.HoverChromeBorderHex = hex;
            }
        }

        private void DividerSwatch_Click(object sender, RoutedEventArgs e)
        {
            if (boundSettings == null)
            {
                return;
            }

            if (TryPickHexColor(boundSettings.HoverChromeDividerHex, out var hex))
            {
                boundSettings.HoverChromeDividerHex = hex;
            }
        }

        private void IconBackgroundSwatch_Click(object sender, RoutedEventArgs e)
        {
            if (boundSettings == null)
            {
                return;
            }

            if (TryPickHexColor(boundSettings.HoverChromeIconBackgroundHex, out var hex))
            {
                boundSettings.HoverChromeIconBackgroundHex = hex;
            }
        }

        private void ResetChromeColors_Click(object sender, RoutedEventArgs e)
        {
            boundSettings?.ResetCustomChromeColors();
        }

        private static int ClampPreviewContentPaddingDip(int pad)
        {
            if (pad < GameHoverDetailsSettings.MinContentPaddingDip)
            {
                return GameHoverDetailsSettings.MinContentPaddingDip;
            }

            return pad > GameHoverDetailsSettings.MaxContentPaddingDip
                ? GameHoverDetailsSettings.MaxContentPaddingDip
                : pad;
        }

        private static bool TryPickHexColor(string currentHex, out string hex)
        {
            hex = null;
            if (!HoverChromePalette.TryParseHex(currentHex, out var wpf))
            {
                wpf = HoverChromePalette.FallbackFillColor;
            }

            using (var dlg = new System.Windows.Forms.ColorDialog())
            {
                dlg.AllowFullOpen = true;
                dlg.FullOpen = true;
                dlg.Color = System.Drawing.Color.FromArgb(wpf.R, wpf.G, wpf.B);
                if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return false;
                }

                var picked = dlg.Color;
                hex = HoverChromePalette.ToHex(Color.FromRgb(picked.R, picked.G, picked.B));
                return true;
            }
        }

        private static T FindVisualChild<T>(DependencyObject parent)
            where T : DependencyObject
        {
            if (parent == null)
            {
                return null;
            }

            var count = VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match)
                {
                    return match;
                }

                var nested = FindVisualChild<T>(child);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }
    }
}
