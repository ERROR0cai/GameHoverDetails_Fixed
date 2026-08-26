using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace GameHoverDetails
{
    internal sealed class GameHoverDetailsHoverService
    {
        private static readonly ILogger Logger = LogManager.GetLogger();

        /// <summary>Horizontal inner inset: must match <see cref="EnsurePopupShell"/> stack margin (14 + 14).</summary>
        private const double ChromePadding = 28;
        private const double PlacementGapDip = 8;
        private const double EnterAnimationMs = 80;
        private const double HideDebounceMs = 70;

        private static FontFamily HoverFieldInlineIconFontFamily => HoverFieldCatalog.GlyphFontFamily;

        private const double LabelToValueGapDip = 4;
        private const double FirstBlockHeaderTopDip = 0;
        private const double GlyphChipSizeDip = 32;
        private const double GlyphChipGlyphFontSize = 15;
        private const double StatRowGlyphToTextGapDip = 10;
        private const double ChromeCornerRadiusDip = 8;
        private const double FrostBlurRadius = 24;

        /// <summary>Half of field block spacing: used above and below each divider and as top/bottom inset per block so the spacing slider affects both sides.</summary>
        private double FieldBlockSpacingHalfDip()
        {
            return FieldBlockSpacingDip() * 0.5;
        }

        private double FieldBlockSpacingDip()
        {
            var s = settings.HoverFieldBlockSpacingDip;
            if (s < 4)
            {
                return 4;
            }

            return s > 36 ? 36 : s;
        }

        private readonly Window mainWindow;
        private readonly IPlayniteAPI playniteApi;
        private readonly GameHoverDetailsSettings settings;
        private readonly Dispatcher dispatcher;

        private bool broken;
        private bool attached;
        private DispatcherTimer hideDebounceTimer;
        private DispatcherTimer showDelayTimer;
        private Game pendingShowGame;
        private FrameworkElement pendingShowAnchor;
        private Popup popup;
        private Border chromeRoot;
        private Border chromeBorder;
        private Border frostHost;
        private Image frostImage;
        private TranslateTransform chromeFlyTransform;
        private StackPanel contentStack;
        private Game lastShownGame;
        private FrameworkElement lastShownAnchor;
        private Storyboard enterStoryboard;
        private int layoutInvokeGeneration;
        private string lastBuiltFieldsFingerprint;
        private HoverChromePalette palette;
        private bool settingsNotifyQueued;
        private int hotkeyHideGeneration;

        public GameHoverDetailsHoverService(Window mainWindow, IPlayniteAPI playniteApi, GameHoverDetailsSettings settings)
        {
            this.mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            this.playniteApi = playniteApi ?? throw new ArgumentNullException(nameof(playniteApi));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            dispatcher = mainWindow.Dispatcher;
        }

        public void NotifySettingsChanged()
        {
            if (broken)
            {
                return;
            }

            if (settingsNotifyQueued)
            {
                return;
            }

            settingsNotifyQueued = true;
            dispatcher.BeginInvoke(new Action(FlushSettingsChanged), DispatcherPriority.DataBind);
        }

        private void FlushSettingsChanged()
        {
            settingsNotifyQueued = false;
            if (!attached || broken)
            {
                return;
            }

            ApplySettingsChanged();
        }

        private void ApplySettingsChanged()
        {
            if (!broken && settings.IsHoverSuppressed())
            {
                showDelayTimer?.Stop();
                pendingShowGame = null;
                pendingShowAnchor = null;
                HidePopup();
                return;
            }

            if (broken)
            {
                return;
            }

            try
            {
                if (popup != null)
                {
                    ApplyChrome();
                }

                if (popup == null || !popup.IsOpen || lastShownGame == null)
                {
                    return;
                }

                lastBuiltFieldsFingerprint = null;
                ShowOrUpdatePopup(lastShownGame, lastShownAnchor);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "GameHoverDetails failed to refresh hover content.");
            }
        }

        public void Attach()
        {
            if (attached || broken)
            {
                return;
            }

            hideDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(HideDebounceMs)
            };
            hideDebounceTimer.Tick += HideDebounceTimerOnTick;

            showDelayTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1)
            };
            showDelayTimer.Tick += ShowDelayTimerOnTick;

            mainWindow.PreviewMouseMove += MainWindowOnPreviewMouseMove;
            mainWindow.PreviewKeyDown += MainWindowOnPreviewKeyDown;
            mainWindow.StateChanged += MainWindowOnStateChanged;
            mainWindow.Closed += MainWindowOnClosed;
            if (Application.Current != null)
            {
                Application.Current.Deactivated += ApplicationOnDeactivated;
            }

            attached = true;

            dispatcher.BeginInvoke(new Action(WarmupPopupShell), DispatcherPriority.ContextIdle);
        }

        private void WarmupPopupShell()
        {
            if (broken || !attached)
            {
                return;
            }

            try
            {
                EnsurePopupShell();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "GameHoverDetails popup shell warmup failed.");
            }
        }

        public void Detach()
        {
            if (!attached)
            {
                return;
            }

            mainWindow.PreviewMouseMove -= MainWindowOnPreviewMouseMove;
            mainWindow.PreviewKeyDown -= MainWindowOnPreviewKeyDown;
            mainWindow.StateChanged -= MainWindowOnStateChanged;
            mainWindow.Closed -= MainWindowOnClosed;
            hotkeyHideGeneration++;
            if (Application.Current != null)
            {
                Application.Current.Deactivated -= ApplicationOnDeactivated;
            }

            hideDebounceTimer?.Stop();
            if (hideDebounceTimer != null)
            {
                hideDebounceTimer.Tick -= HideDebounceTimerOnTick;
            }

            hideDebounceTimer = null;

            showDelayTimer?.Stop();
            if (showDelayTimer != null)
            {
                showDelayTimer.Tick -= ShowDelayTimerOnTick;
            }

            showDelayTimer = null;

            StopEnterStoryboard();
            HidePopup();
            if (chromeRoot != null)
            {
                chromeRoot.SizeChanged -= ChromeRootOnSizeChanged;
            }

            if (chromeBorder != null)
            {
                chromeBorder.PreviewMouseMove -= ChromeBorderOnPointerOverChrome;
                chromeBorder.MouseEnter -= ChromeBorderOnPointerOverChrome;
            }

            popup = null;
            chromeRoot = null;
            chromeBorder = null;
            frostHost = null;
            frostImage = null;
            chromeFlyTransform = null;
            contentStack = null;
            lastShownGame = null;
            lastShownAnchor = null;
            lastBuiltFieldsFingerprint = null;

            attached = false;
        }

        private void MainWindowOnClosed(object sender, EventArgs e)
        {
            Detach();
        }

        private void ApplicationOnDeactivated(object sender, EventArgs e)
        {
            HidePopupForForegroundLoss();
        }

        private void MainWindowOnStateChanged(object sender, EventArgs e)
        {
            if (mainWindow.WindowState == WindowState.Minimized)
            {
                HidePopupForForegroundLoss();
            }
        }

        private void HidePopupForForegroundLoss()
        {
            if (broken)
            {
                return;
            }

            try
            {
                hideDebounceTimer?.Stop();
                HidePopup();
            }
            catch (Exception ex)
            {
                LatchBroken(ex);
            }
        }

        private void MainWindowOnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (broken || e.IsRepeat || e.Key == Key.None)
            {
                return;
            }

            var popupOpen = popup != null && popup.IsOpen;
            var showPending = pendingShowGame != null || (showDelayTimer != null && showDelayTimer.IsEnabled);
            if (!popupOpen && !showPending)
            {
                return;
            }

            var generation = ++hotkeyHideGeneration;
            dispatcher.BeginInvoke(
                new Action(() => HidePopupIfPointerLeftGame(generation)),
                DispatcherPriority.ContextIdle);
        }

        /// <summary>
        /// After Playnite handles the key (F4/F9 overlays), hide only if the pointer is no longer on a game.
        /// Still over a tile → keep the tooltip.
        /// </summary>
        private void HidePopupIfPointerLeftGame(int generation)
        {
            if (broken || !attached || generation != hotkeyHideGeneration)
            {
                return;
            }

            if (settings.IsHoverSuppressed())
            {
                hideDebounceTimer?.Stop();
                HidePopup();
                return;
            }

            try
            {
                var hit = Mouse.DirectlyOver as DependencyObject;
                Game game;
                FrameworkElement unused;
                TryResolveGameAndAnchor(hit, playniteApi, out game, out unused);
                if (game != null)
                {
                    return;
                }

                hideDebounceTimer?.Stop();
                HidePopup();
            }
            catch (Exception ex)
            {
                LatchBroken(ex);
            }
        }

        private void MainWindowOnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (broken)
            {
                return;
            }

            if (settings.IsHoverSuppressed())
            {
                showDelayTimer?.Stop();
                pendingShowGame = null;
                pendingShowAnchor = null;
                HidePopup();
                return;
            }

            try
            {
                var hit = Mouse.DirectlyOver as DependencyObject;
                Game game;
                FrameworkElement anchor;
                TryResolveGameAndAnchor(hit, playniteApi, out game, out anchor);

                if (game != null)
                {
                    hideDebounceTimer?.Stop();
                    ScheduleShowAfterDelay(game, anchor);
                }
                else
                {
                    showDelayTimer?.Stop();
                    pendingShowGame = null;
                    pendingShowAnchor = null;
                    hideDebounceTimer?.Stop();
                    hideDebounceTimer?.Start();
                }
            }
            catch (Exception ex)
            {
                LatchBroken(ex);
            }
        }

        private void ScheduleShowAfterDelay(Game game, FrameworkElement anchor)
        {
            if (settings.IsHoverSuppressed())
            {
                showDelayTimer?.Stop();
                pendingShowGame = null;
                pendingShowAnchor = null;
                return;
            }

            pendingShowGame = game;
            pendingShowAnchor = anchor;
            showDelayTimer?.Stop();
            var delay = settings.ShowDelayMs;
            if (delay <= 0)
            {
                pendingShowGame = null;
                pendingShowAnchor = null;
                ShowOrUpdatePopup(game, anchor);
                return;
            }

            showDelayTimer.Interval = TimeSpan.FromMilliseconds(delay);
            showDelayTimer.Start();
        }

        private void ShowDelayTimerOnTick(object sender, EventArgs e)
        {
            if (broken)
            {
                return;
            }

            try
            {
                showDelayTimer?.Stop();
                var hit = Mouse.DirectlyOver as DependencyObject;
                Game g;
                FrameworkElement anchor;
                TryResolveGameAndAnchor(hit, playniteApi, out g, out anchor);
                if (g == null || pendingShowGame == null || g.Id != pendingShowGame.Id)
                {
                    pendingShowGame = null;
                    pendingShowAnchor = null;
                    return;
                }

                if (settings.IsHoverSuppressed())
                {
                    pendingShowGame = null;
                    pendingShowAnchor = null;
                    return;
                }

                var useAnchor = anchor ?? pendingShowAnchor;
                pendingShowGame = null;
                pendingShowAnchor = null;
                ShowOrUpdatePopup(g, useAnchor);
            }
            catch (Exception ex)
            {
                LatchBroken(ex);
            }
        }

        private void HideDebounceTimerOnTick(object sender, EventArgs e)
        {
            if (broken)
            {
                return;
            }

            try
            {
                hideDebounceTimer?.Stop();
                var hit = Mouse.DirectlyOver as DependencyObject;
                Game g;
                FrameworkElement unused;
                TryResolveGameAndAnchor(hit, playniteApi, out g, out unused);
                if (g != null)
                {
                    return;
                }

                HidePopup();
            }
            catch (Exception ex)
            {
                LatchBroken(ex);
            }
        }

        private void LatchBroken(Exception ex)
        {
            if (broken)
            {
                return;
            }

            broken = true;
            Logger.Error(ex, "GameHoverDetails hover UI disabled after an error.");
            try
            {
                Detach();
            }
            catch
            {
                // ignore
            }
        }

        /// <summary>
        /// Outer-most visual ancestor that still carries the same game (stable anchor). Outside grid view,
        /// suppresses hover when the pointer is on an embedded ButtonBase under that host (play/info/toggles).
        /// Grid view keeps the hover so moving from cover to those icons does not dismiss the popup.
        /// </summary>
        private static void TryResolveGameAndAnchor(DependencyObject hit, IPlayniteAPI api, out Game game, out FrameworkElement anchor)
        {
            game = null;
            anchor = null;
            if (hit == null)
            {
                return;
            }

            Game resolvedGame = null;
            FrameworkElement outerGameFe = null;
            for (var current = hit; current != null; current = VisualTreeHelper.GetParent(current))
            {
                if (!(current is FrameworkElement fe))
                {
                    continue;
                }

                var g = TryGetGameFromDataContext(fe.DataContext);
                if (g == null)
                {
                    continue;
                }

                if (resolvedGame == null)
                {
                    resolvedGame = g;
                    outerGameFe = fe;
                }
                else if (resolvedGame.Id == g.Id)
                {
                    outerGameFe = fe;
                }
                else
                {
                    break;
                }
            }

            if (resolvedGame == null || outerGameFe == null)
            {
                return;
            }

            if (!IsGridDesktopView(api))
            {
                for (var current = hit; current != null; current = VisualTreeHelper.GetParent(current))
                {
                    if (ReferenceEquals(current, outerGameFe))
                    {
                        break;
                    }

                    if (current is ButtonBase)
                    {
                        return;
                    }
                }
            }

            game = resolvedGame;
            anchor = outerGameFe;
        }

        private static bool IsGridDesktopView(IPlayniteAPI api)
        {
            try
            {
                return api?.MainView != null && api.MainView.ActiveDesktopView == DesktopView.Grid;
            }
            catch
            {
                return false;
            }
        }

        private static Game TryGetGameFromDataContext(object dc)
        {
            if (dc == null)
            {
                return null;
            }

            if (dc is Game g)
            {
                return g;
            }

            try
            {
                var t = dc.GetType();
                var p = t.GetProperty("Game", BindingFlags.Instance | BindingFlags.Public);
                if (p != null && typeof(Game).IsAssignableFrom(p.PropertyType))
                {
                    return p.GetValue(dc, null) as Game;
                }
            }
            catch
            {
                // ignore reflection failures for unknown VMs
            }

            return null;
        }

        private static string BuildFieldsFingerprint(System.Collections.Generic.IReadOnlyList<string> keys)
        {
            return keys == null || keys.Count == 0 ? string.Empty : string.Join("\x1e", keys);
        }

        private void StopEnterStoryboard()
        {
            if (enterStoryboard != null)
            {
                enterStoryboard.Stop();
                enterStoryboard = null;
            }

            // Storyboard.Stop() does not always release the animation clock on Opacity; without this,
            // assigning Opacity = 1 can be ignored and the hover stays invisible (see debug opacity stuck at 0).
            if (chromeBorder != null)
            {
                chromeBorder.BeginAnimation(UIElement.OpacityProperty, null);
            }
        }

        private void HidePopup()
        {
            showDelayTimer?.Stop();
            pendingShowGame = null;
            pendingShowAnchor = null;
            StopEnterStoryboard();
            if (popup != null)
            {
                popup.IsOpen = false;
            }

            if (chromeRoot != null)
            {
                chromeRoot.Opacity = 1;
            }

            if (chromeFlyTransform != null)
            {
                chromeFlyTransform.X = 0;
            }

            lastShownGame = null;
            lastShownAnchor = null;
            lastBuiltFieldsFingerprint = null;
        }

        /// <summary>
        /// Points are relative to the placement target's top-left (net462 CustomPopupPlacementCallback).
        /// List view: below the row, start-aligned (left in LTR, right in RTL); fallback above the row.
        /// Other views: prefer the end side of the target (right in LTR, left in RTL), then the start side.
        /// </summary>
        private CustomPopupPlacement[] PlacePopupForCurrentDesktopView(Size popupSize, Size targetSize, Point offset)
        {
            if (IsListViewDesktop())
            {
                return PlacePopupListViewBottomThenTopStart(popupSize, targetSize, offset);
            }

            return PlacePopupGridOrDefault(popupSize, targetSize, offset);
        }

        private bool IsListViewDesktop()
        {
            try
            {
                return playniteApi?.MainView != null && playniteApi.MainView.ActiveDesktopView == DesktopView.List;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// List row: open downward with start edges aligned; fallback above the row.
        /// </summary>
        private CustomPopupPlacement[] PlacePopupListViewBottomThenTopStart(Size popupSize, Size targetSize, Point offset)
        {
            var gap = PlacementGapDip;
            var popupW = popupSize.Width;
            if (popupW < 8)
            {
                popupW = Math.Max(120, settings.HoverWidth);
            }

            // RTL target origin is top-right; -popupW aligns the popup's start (visual right) with the row.
            var startX = HoverLoc.IsRightToLeftLayout(playniteApi, mainWindow)
                ? -popupW + offset.X
                : offset.X;
            var below = new Point(startX, targetSize.Height + gap + offset.Y);
            var above = new Point(startX, -popupSize.Height - gap + offset.Y);
            return new[]
            {
                new CustomPopupPlacement(below, PopupPrimaryAxis.Vertical),
                new CustomPopupPlacement(above, PopupPrimaryAxis.Vertical)
            };
        }

        /// <summary>
        /// Grid (and other) views: prefer the end side of the tile, then the start side.
        /// Left-side math must use a real width — a 0-width callback places x≈0 and the panel grows over the tile.
        /// </summary>
        private CustomPopupPlacement[] PlacePopupGridOrDefault(Size popupSize, Size targetSize, Point offset)
        {
            var gap = PlacementGapDip;
            var popupW = popupSize.Width;
            if (popupW < 8)
            {
                popupW = Math.Max(120, settings.HoverWidth);
            }

            // LTR PlacementTarget origin is top-left. RTL targets (Hebrew UI) use top-right;
            // -popupW-gap from that origin still overlaps the tile (runtime: popupX ≈ tileRight - popupW).
            Point right;
            Point left;
            var rtl = HoverLoc.IsRightToLeftLayout(playniteApi, mainWindow);
            if (rtl)
            {
                left = new Point(-targetSize.Width - popupW - gap + offset.X, offset.Y);
                right = new Point(gap + offset.X, offset.Y);
            }
            else
            {
                right = new Point(targetSize.Width + gap + offset.X, offset.Y);
                left = new Point(-popupW - gap + offset.X, offset.Y);
            }
            // #region agent log
            AgentLog("H1", "PlacePopupGridOrDefault", "custom placement candidates",
                "{\"rtl\":" + (rtl ? "true" : "false")
                + ",\"popupW\":" + popupW.ToString("0.##", CultureInfo.InvariantCulture)
                + ",\"rawPopupW\":" + popupSize.Width.ToString("0.##", CultureInfo.InvariantCulture)
                + ",\"targetW\":" + targetSize.Width.ToString("0.##", CultureInfo.InvariantCulture)
                + ",\"targetH\":" + targetSize.Height.ToString("0.##", CultureInfo.InvariantCulture)
                + ",\"leftX\":" + left.X.ToString("0.##", CultureInfo.InvariantCulture)
                + ",\"rightX\":" + right.X.ToString("0.##", CultureInfo.InvariantCulture)
                + ",\"hOff\":" + (popup != null ? popup.HorizontalOffset.ToString("0.##", CultureInfo.InvariantCulture) : "0")
                + ",\"pref\":\"" + (rtl ? "left" : "right") + "\"}");
            // #endregion
            if (rtl)
            {
                return new[]
                {
                    new CustomPopupPlacement(left, PopupPrimaryAxis.Horizontal),
                    new CustomPopupPlacement(right, PopupPrimaryAxis.Horizontal)
                };
            }

            return new[]
            {
                new CustomPopupPlacement(right, PopupPrimaryAxis.Horizontal),
                new CustomPopupPlacement(left, PopupPrimaryAxis.Horizontal)
            };
        }

        private void ShowOrUpdatePopup(Game game, FrameworkElement anchor)
        {
            if (settings.IsHoverSuppressed())
            {
                HidePopup();
                return;
            }

            EnsurePopupShell();
            ApplyChrome();
            var wasOpen = popup.IsOpen;
            var previousId = lastShownGame?.Id;
            var sameGameContinue = wasOpen && previousId != null && previousId == game.Id;
            var gameChanged = lastShownGame == null || lastShownGame.Id != game.Id;

            var orderedKeys = settings.GetOrderedSelectedKeys();
            var w = Math.Max(120, settings.HoverWidth);
            var fieldsFingerprint =
                BuildFieldsFingerprint(orderedKeys)
                + "\x1e" + w.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "\x1e" + (settings.HideFieldTitlesInHover ? "1" : "0")
                + "\x1e" + (settings.ShowFieldInlineIconsInHover ? "1" : "0")
                + "\x1e" + settings.HoverFieldBlockSpacingDip.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "\x1e" + HoverChromePalette.ContentFingerprint(settings);
            var canSkipContentRebuild =
                popup.IsOpen &&
                lastShownGame != null &&
                lastShownGame.Id == game.Id &&
                lastBuiltFieldsFingerprint == fieldsFingerprint;

            if (popup.IsOpen && gameChanged)
            {
                popup.IsOpen = false;
            }

            chromeBorder.MinWidth = w;
            chromeBorder.MaxWidth = w;
            if (chromeRoot != null)
            {
                chromeRoot.MinWidth = w;
                chromeRoot.MaxWidth = w;
            }
            var innerMax = Math.Max(60, w - ChromePadding);

            if (!canSkipContentRebuild)
            {
                contentStack.Children.Clear();
                var onlyIconSelected = orderedKeys.Count == 1 && orderedKeys[0] == "Icon";
                foreach (var key in orderedKeys)
                {
                    var isFirstBlock = contentStack.Children.Count == 0;
                    switch (key)
                    {
                        case "Icon":
                        case "CoverImage":
                        case "BackgroundImage":
                            TryAppendGameArtRow(key, game, innerMax, isFirstBlock, onlyIconSelected);
                            break;
                        case "Platform":
                            AppendPlatformRow(game, key, innerMax, isFirstBlock);
                            break;
                        default:
                            AppendTextDetailRow(key, game, innerMax, isFirstBlock);
                            break;
                    }
                }

                TrimLastContentBottomMargin(contentStack);

                lastBuiltFieldsFingerprint = fieldsFingerprint;
            }

            if (anchor != null && anchor.IsVisible)
            {
                var sameAnchorContinue = sameGameContinue
                    && popup.IsOpen
                    && ReferenceEquals(popup.PlacementTarget, anchor)
                    && popup.Placement == PlacementMode.Custom;
                popup.PlacementTarget = anchor;
                popup.Placement = PlacementMode.Custom;
                // #region agent log
                AgentLog("H2", "ShowOrUpdatePopup", "reset offset before custom place",
                    "{\"sameGameContinue\":" + (sameGameContinue ? "true" : "false")
                    + ",\"sameAnchorContinue\":" + (sameAnchorContinue ? "true" : "false")
                    + ",\"wasOpen\":" + (wasOpen ? "true" : "false")
                    + ",\"prevHOff\":" + popup.HorizontalOffset.ToString("0.##", CultureInfo.InvariantCulture)
                    + ",\"prevVOff\":" + popup.VerticalOffset.ToString("0.##", CultureInfo.InvariantCulture)
                    + ",\"gen\":" + (layoutInvokeGeneration + 1).ToString(CultureInfo.InvariantCulture) + "}");
                // #endregion
                if (!sameAnchorContinue)
                {
                    popup.HorizontalOffset = 0;
                    popup.VerticalOffset = 0;
                }

                popup.CustomPopupPlacementCallback = PlacePopupForCurrentDesktopView;
            }
            else
            {
                popup.CustomPopupPlacementCallback = null;
                popup.PlacementTarget = mainWindow;
                popup.Placement = PlacementMode.Mouse;
                popup.HorizontalOffset = 8;
                popup.VerticalOffset = 8;
            }

            StopEnterStoryboard();
            if (sameGameContinue)
            {
                chromeRoot.Opacity = 1;
                chromeFlyTransform.X = 0;
            }
            else
            {
                chromeRoot.Opacity = 0;
                chromeFlyTransform.X = 0;
            }

            popup.IsOpen = true;
            lastShownGame = game;
            lastShownAnchor = anchor;

            var runEnterAnimation = !sameGameContinue;
            var invokeGen = ++layoutInvokeGeneration;
            dispatcher.BeginInvoke(
                new Action(() => AfterPopupLayout(runEnterAnimation, invokeGen)),
                DispatcherPriority.Loaded);
        }

        private void AfterPopupLayout(bool runEnterAnimation, int invokedGeneration)
        {
            if (invokedGeneration != layoutInvokeGeneration)
            {
                return;
            }

            if (broken || popup?.Child == null || !popup.IsOpen)
            {
                return;
            }

            try
            {
                popup.Child.UpdateLayout();
                // #region agent log
                AgentLog("H6", "AfterPopupLayout", "layout pass",
                    "{\"gen\":" + invokedGeneration.ToString(CultureInfo.InvariantCulture)
                    + ",\"runAnim\":" + (runEnterAnimation ? "true" : "false")
                    + ",\"hOff\":" + popup.HorizontalOffset.ToString("0.##", CultureInfo.InvariantCulture)
                    + ",\"vOff\":" + popup.VerticalOffset.ToString("0.##", CultureInfo.InvariantCulture)
                    + ",\"childW\":" + popup.Child.RenderSize.Width.ToString("0.##", CultureInfo.InvariantCulture)
                    + ",\"rtl\":" + (HoverLoc.IsRightToLeftLayout(playniteApi, mainWindow) ? "true" : "false") + "}");
                // #endregion
                NudgeRtlPopupOutsideAnchor();
                ClampPopupToVirtualScreen();
                UpdateFrostBackdrop();
                if (!runEnterAnimation)
                {
                    chromeRoot.Opacity = 1;
                    chromeFlyTransform.X = 0;
                    return;
                }

                popup.Child.UpdateLayout();
                chromeFlyTransform.X = 0;
                BeginEnterStoryboard();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "GameHoverDetails hover layout/animation failed.");
                StopEnterStoryboard();
                if (chromeRoot != null)
                {
                    chromeRoot.Opacity = 1;
                }

                chromeFlyTransform.X = 0;
            }
        }

        private void BeginEnterStoryboard()
        {
            StopEnterStoryboard();
            chromeFlyTransform.X = 0;
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            var duration = TimeSpan.FromMilliseconds(EnterAnimationMs);

            var opacityAnim = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = duration,
                EasingFunction = ease
            };
            Storyboard.SetTarget(opacityAnim, chromeRoot);
            Storyboard.SetTargetProperty(opacityAnim, new PropertyPath(UIElement.OpacityProperty));

            enterStoryboard = new Storyboard();
            enterStoryboard.Children.Add(opacityAnim);
            enterStoryboard.Begin();
        }

        /// <summary>
        /// RTL side placement: put the panel fully left of the tile, top-aligned. WPF custom placement
        /// often reports width 0 on first open, which parks the HWND on the tile.
        /// </summary>
        private void NudgeRtlPopupOutsideAnchor()
        {
            if (popup?.Child == null || lastShownAnchor == null || popup.Placement != PlacementMode.Custom)
            {
                // #region agent log
                AgentLog("H4", "NudgeRtlPopupOutsideAnchor", "skip no popup/anchor/custom",
                    "{\"hasChild\":" + (popup?.Child != null ? "true" : "false")
                    + ",\"hasAnchor\":" + (lastShownAnchor != null ? "true" : "false")
                    + ",\"placement\":\"" + (popup != null ? popup.Placement.ToString() : "null") + "\"}");
                // #endregion
                return;
            }

            if (IsListViewDesktop() || !HoverLoc.IsRightToLeftLayout(playniteApi, mainWindow))
            {
                // #region agent log
                AgentLog("H4", "NudgeRtlPopupOutsideAnchor", "skip list or not rtl",
                    "{\"list\":" + (IsListViewDesktop() ? "true" : "false")
                    + ",\"rtl\":" + (HoverLoc.IsRightToLeftLayout(playniteApi, mainWindow) ? "true" : "false") + "}");
                // #endregion
                return;
            }

            var child = popup.Child;
            var width = child.RenderSize.Width;
            var height = child.RenderSize.Height;
            var tileW = lastShownAnchor.ActualWidth;
            var tileH = lastShownAnchor.ActualHeight;
            if (width < 8 || height < 8 || tileW < 8 || tileH < 8)
            {
                // #region agent log
                AgentLog("H4", "NudgeRtlPopupOutsideAnchor", "skip tiny size",
                    "{\"w\":" + width.ToString("0.##", CultureInfo.InvariantCulture)
                    + ",\"h\":" + height.ToString("0.##", CultureInfo.InvariantCulture)
                    + ",\"tileW\":" + tileW.ToString("0.##", CultureInfo.InvariantCulture)
                    + ",\"tileH\":" + tileH.ToString("0.##", CultureInfo.InvariantCulture) + "}");
                // #endregion
                return;
            }

            var source = PresentationSource.FromVisual(child) as HwndSource;
            if (source?.CompositionTarget == null)
            {
                // #region agent log
                AgentLog("H4", "NudgeRtlPopupOutsideAnchor", "skip no hwnd", "{}");
                // #endregion
                return;
            }

            var fromDevice = source.CompositionTarget.TransformFromDevice;
            Point popupA;
            Point popupB;
            Point tileA;
            Point tileB;
            var popupRect = GetVisualScreenRectDip(child, width, height, fromDevice, out popupA, out popupB);
            var tileRect = GetVisualScreenRectDip(lastShownAnchor, tileW, tileH, fromDevice, out tileA, out tileB);
            if (popupRect.Width < 1 || tileRect.Width < 1)
            {
                return;
            }

            const double margin = 8;
            var vsLeft = SystemParameters.VirtualScreenLeft;
            var vsRight = vsLeft + SystemParameters.VirtualScreenWidth;
            var gap = PlacementGapDip;

            var desiredLeft = tileRect.Left - popupRect.Width - gap;
            if (desiredLeft < vsLeft + margin)
            {
                desiredLeft = tileRect.Right + gap;
                if (desiredLeft + popupRect.Width > vsRight - margin)
                {
                    desiredLeft = vsRight - margin - popupRect.Width;
                }
            }

            var desiredTop = tileRect.Top;
            var deltaX = desiredLeft - popupRect.X;
            var deltaY = desiredTop - popupRect.Y;
            if (Math.Abs(deltaX) < 0.5 && Math.Abs(deltaY) < 0.5)
            {
                // #region agent log
                AgentLog("H3", "NudgeRtlPopupOutsideAnchor", "already aligned",
                    "{\"popupX\":" + popupRect.X.ToString("0.##", CultureInfo.InvariantCulture)
                    + ",\"tileX\":" + tileRect.X.ToString("0.##", CultureInfo.InvariantCulture)
                    + ",\"desiredLeft\":" + desiredLeft.ToString("0.##", CultureInfo.InvariantCulture)
                    + ",\"overlaps\":" + (popupRect.IntersectsWith(tileRect) ? "true" : "false") + "}");
                // #endregion
                return;
            }

            // #region agent log
            AgentLog("H3", "NudgeRtlPopupOutsideAnchor", "nudge apply",
                "{\"popupX\":" + popupRect.X.ToString("0.##", CultureInfo.InvariantCulture)
                + ",\"popupY\":" + popupRect.Y.ToString("0.##", CultureInfo.InvariantCulture)
                + ",\"popupW\":" + popupRect.Width.ToString("0.##", CultureInfo.InvariantCulture)
                + ",\"popupH\":" + popupRect.Height.ToString("0.##", CultureInfo.InvariantCulture)
                + ",\"tileX\":" + tileRect.X.ToString("0.##", CultureInfo.InvariantCulture)
                + ",\"tileY\":" + tileRect.Y.ToString("0.##", CultureInfo.InvariantCulture)
                + ",\"tileW\":" + tileRect.Width.ToString("0.##", CultureInfo.InvariantCulture)
                + ",\"tileH\":" + tileRect.Height.ToString("0.##", CultureInfo.InvariantCulture)
                + ",\"popupA\":\"" + popupA.X.ToString("0.##", CultureInfo.InvariantCulture) + "," + popupA.Y.ToString("0.##", CultureInfo.InvariantCulture) + "\""
                + ",\"popupB\":\"" + popupB.X.ToString("0.##", CultureInfo.InvariantCulture) + "," + popupB.Y.ToString("0.##", CultureInfo.InvariantCulture) + "\""
                + ",\"tileA\":\"" + tileA.X.ToString("0.##", CultureInfo.InvariantCulture) + "," + tileA.Y.ToString("0.##", CultureInfo.InvariantCulture) + "\""
                + ",\"tileB\":\"" + tileB.X.ToString("0.##", CultureInfo.InvariantCulture) + "," + tileB.Y.ToString("0.##", CultureInfo.InvariantCulture) + "\""
                + ",\"desiredLeft\":" + desiredLeft.ToString("0.##", CultureInfo.InvariantCulture)
                + ",\"deltaX\":" + deltaX.ToString("0.##", CultureInfo.InvariantCulture)
                + ",\"deltaY\":" + deltaY.ToString("0.##", CultureInfo.InvariantCulture)
                + ",\"hOffBefore\":" + popup.HorizontalOffset.ToString("0.##", CultureInfo.InvariantCulture)
                + ",\"overlaps\":" + (popupRect.IntersectsWith(tileRect) ? "true" : "false") + "}");
            // #endregion
            // Offset apply removed: it fought ShowOrUpdatePopup's offset reset (jumping). Placement math is the fix.
        }

        private static Rect GetVisualScreenRectDip(Visual visual, double layoutWidth, double layoutHeight, System.Windows.Media.Matrix fromDevice, out Point a, out Point b)
        {
            a = fromDevice.Transform(visual.PointToScreen(new Point(0, 0)));
            b = fromDevice.Transform(visual.PointToScreen(new Point(layoutWidth, layoutHeight)));
            var x1 = Math.Min(a.X, b.X);
            var y1 = Math.Min(a.Y, b.Y);
            return new Rect(x1, y1, Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));
        }

        private void ClampPopupToVirtualScreen()
        {
            if (popup?.Child == null || !popup.IsOpen)
            {
                return;
            }

            try
            {
                popup.Child.UpdateLayout();
                var child = popup.Child;
                var width = child.RenderSize.Width;
                var height = child.RenderSize.Height;
                if (width <= 0 || height <= 0)
                {
                    return;
                }

                var source = PresentationSource.FromVisual(child) as HwndSource;
                if (source?.CompositionTarget == null)
                {
                    return;
                }

                var fromDevice = source.CompositionTarget.TransformFromDevice;

                var tlPx = child.PointToScreen(new Point(0, 0));
                var brPx = child.PointToScreen(new Point(width, height));
                var tlDip = fromDevice.Transform(tlPx);
                var brDip = fromDevice.Transform(brPx);

                const double margin = 8;
                var vsLeft = SystemParameters.VirtualScreenLeft;
                var vsTop = SystemParameters.VirtualScreenTop;
                var vsRight = vsLeft + SystemParameters.VirtualScreenWidth;
                var vsBottom = vsTop + SystemParameters.VirtualScreenHeight;

                var deltaX = 0.0;
                var deltaY = 0.0;
                var customSidePlacement = popup.Placement == PlacementMode.Custom;

                if (!customSidePlacement)
                {
                    if (brDip.X > vsRight - margin)
                    {
                        deltaX -= brDip.X - (vsRight - margin);
                    }

                    if (tlDip.X + deltaX < vsLeft + margin)
                    {
                        deltaX += vsLeft + margin - tlDip.X - deltaX;
                    }
                }

                if (brDip.Y > vsBottom - margin)
                {
                    deltaY -= brDip.Y - (vsBottom - margin);
                }

                if (tlDip.Y + deltaY < vsTop + margin)
                {
                    deltaY += vsTop + margin - tlDip.Y - deltaY;
                }

                if (Math.Abs(deltaX) > 0.01 || Math.Abs(deltaY) > 0.01)
                {
                    popup.HorizontalOffset += deltaX;
                    popup.VerticalOffset += deltaY;
                }
            }
            catch
            {
                // ignore clamp failures
            }
        }

        private static void TrimLastContentBottomMargin(Panel panel)
        {
            if (panel.Children.Count == 0)
            {
                return;
            }

            if (!(panel.Children[panel.Children.Count - 1] is FrameworkElement last))
            {
                return;
            }

            var m = last.Margin;
            if (m.Bottom <= 0.01)
            {
                return;
            }

            last.Margin = new Thickness(m.Left, m.Top, m.Right, 0);
        }

        private void AppendFieldBlockSeparator(bool isFirstBlock)
        {
            if (isFirstBlock)
            {
                return;
            }

            var pad = FieldBlockSpacingHalfDip();
            contentStack.Children.Add(
                new Border
                {
                    Height = 1,
                    Margin = new Thickness(0, pad, 0, pad),
                    Background = Palette.Separator,
                    IsHitTestVisible = false
                });
        }

        private Border CreateGlyphChip(string glyph)
        {
            var glyphTb = new TextBlock
            {
                Text = glyph,
                FontFamily = HoverFieldInlineIconFontFamily,
                FontSize = GlyphChipGlyphFontSize,
                Foreground = Palette.GlyphChipGlyph,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FlowDirection = FlowDirection.LeftToRight,
                IsHitTestVisible = false
            };

            return new Border
            {
                Width = GlyphChipSizeDip,
                Height = GlyphChipSizeDip,
                CornerRadius = new CornerRadius(GlyphChipSizeDip / 2),
                Background = Palette.GlyphChipBackground,
                Child = glyphTb,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                FlowDirection = FlowDirection.LeftToRight,
                IsHitTestVisible = false
            };
        }

        /// <summary>Text/stat row layouts (separator is caller's responsibility).</summary>
        private void AppendTextDetailInner(string key, Game game, double innerMax, bool isFirstBlock)
        {
            var showTitle = !settings.HideFieldTitlesInHover;
            var useInlineGlyph = settings.ShowFieldInlineIconsInHover && !HoverFieldCatalog.IsGameArtImageField(key);
            var labelText = HoverFieldCatalog.GetDisplayName(key);
            var valueText = HoverFieldFormatter.Format(key, game, playniteApi);
            var topInset = isFirstBlock ? FirstBlockHeaderTopDip : FieldBlockSpacingHalfDip();
            var bottomInset = FieldBlockSpacingHalfDip();
            var textMaxStat = Math.Max(48, innerMax - GlyphChipSizeDip - StatRowGlyphToTextGapDip);

            if (showTitle && useInlineGlyph)
            {
                var row = new Grid
                {
                    MaxWidth = innerMax,
                    Margin = new Thickness(0, topInset, 0, bottomInset)
                };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(GlyphChipSizeDip) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var chip = CreateGlyphChip(HoverFieldCatalog.GetSettingsGlyph(key));
                Grid.SetColumn(chip, 0);

                var label = new TextBlock { Margin = new Thickness(0, 0, 0, LabelToValueGapDip) };
                HoverDetailValuePresenter.ConfigureFieldLabelTextBlock(label, textMaxStat, Palette.LabelText);
                HoverDetailValuePresenter.SetHeaderText(label, labelText, textMaxStat);

                var body = new TextBlock();
                HoverDetailValuePresenter.ConfigureBodyTextBlock(body, textMaxStat, Palette.BodyText);
                HoverDetailValuePresenter.SetBodyContent(body, valueText);

                var textCol = new StackPanel { Margin = new Thickness(StatRowGlyphToTextGapDip, 0, 0, 0) };
                textCol.Children.Add(label);
                textCol.Children.Add(body);
                Grid.SetColumn(textCol, 1);

                row.Children.Add(chip);
                row.Children.Add(textCol);
                contentStack.Children.Add(row);
                return;
            }

            if (showTitle && !useInlineGlyph)
            {
                var label = new TextBlock { Margin = new Thickness(0, topInset, 0, LabelToValueGapDip) };
                HoverDetailValuePresenter.ConfigureFieldLabelTextBlock(label, innerMax, Palette.LabelText);
                HoverDetailValuePresenter.SetHeaderText(label, labelText, innerMax);

                var body = new TextBlock { Margin = new Thickness(0, 0, 0, bottomInset) };
                HoverDetailValuePresenter.ConfigureBodyTextBlock(body, innerMax, Palette.BodyText);
                HoverDetailValuePresenter.SetBodyContent(body, valueText);

                contentStack.Children.Add(label);
                contentStack.Children.Add(body);
                return;
            }

            if (useInlineGlyph)
            {
                var row = new Grid
                {
                    MaxWidth = innerMax,
                    Margin = new Thickness(0, topInset, 0, bottomInset)
                };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(GlyphChipSizeDip) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var chip = CreateGlyphChip(HoverFieldCatalog.GetSettingsGlyph(key));
                Grid.SetColumn(chip, 0);

                var body = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center
                };
                HoverDetailValuePresenter.ConfigureBodyTextBlock(body, textMaxStat, Palette.BodyText);
                HoverDetailValuePresenter.SetBodyContent(body, valueText);
                Grid.SetColumn(body, 1);
                body.Margin = new Thickness(StatRowGlyphToTextGapDip, 0, 0, 0);

                row.Children.Add(chip);
                row.Children.Add(body);
                contentStack.Children.Add(row);
                return;
            }

            var bodyOnly = new TextBlock
            {
                Margin = new Thickness(0, topInset, 0, bottomInset)
            };
            HoverDetailValuePresenter.ConfigureBodyTextBlock(bodyOnly, innerMax, Palette.BodyText);
            HoverDetailValuePresenter.SetBodyContent(bodyOnly, valueText);
            contentStack.Children.Add(bodyOnly);
        }

        private void AppendTextDetailRow(string key, Game game, double innerMax, bool isFirstBlock)
        {
            AppendFieldBlockSeparator(isFirstBlock);
            AppendTextDetailInner(key, game, innerMax, isFirstBlock);
        }

        private const double HoverIconBoxPx = 40;

        private void TryAppendGameArtRow(string key, Game game, double innerMax, bool isFirstBlock, bool showGameNameBesideIcon)
        {
            var bmp = HoverBitmapLoader.TryLoadGameArt(key, game, playniteApi);
            if (bmp == null)
            {
                return;
            }

            AppendFieldBlockSeparator(isFirstBlock);

            double maxW;
            double maxH;
            switch (key)
            {
                case "Icon":
                    maxW = HoverIconBoxPx;
                    maxH = HoverIconBoxPx;
                    break;
                case "CoverImage":
                    maxW = innerMax;
                    maxH = 220;
                    break;
                default:
                    maxW = innerMax;
                    maxH = 140;
                    break;
            }

            var top = isFirstBlock ? FirstBlockHeaderTopDip : FieldBlockSpacingHalfDip();
            var bottom = FieldBlockSpacingHalfDip();

            if (key == "Icon" && showGameNameBesideIcon)
            {
                var textMax = Math.Max(48, innerMax - HoverIconBoxPx - StatRowGlyphToTextGapDip);
                var row = new Grid
                {
                    MaxWidth = innerMax,
                    Margin = new Thickness(0, top, 0, bottom)
                };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var img = new Image
                {
                    Source = bmp,
                    Stretch = Stretch.Uniform,
                    MaxWidth = maxW,
                    MaxHeight = maxH,
                    Width = HoverIconBoxPx,
                    Height = HoverIconBoxPx,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsHitTestVisible = false
                };
                Grid.SetColumn(img, 0);

                var nameTb = new TextBlock
                {
                    Margin = new Thickness(StatRowGlyphToTextGapDip, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                HoverDetailValuePresenter.ConfigureBodyTextBlock(nameTb, textMax, Palette.BodyText);
                HoverDetailValuePresenter.SetBodyContent(nameTb, HoverFieldFormatter.Format("Name", game, playniteApi));
                Grid.SetColumn(nameTb, 1);

                row.Children.Add(img);
                row.Children.Add(nameTb);
                contentStack.Children.Add(row);
                return;
            }

            var imgOnly = new Image
            {
                Source = bmp,
                Stretch = Stretch.Uniform,
                MaxWidth = maxW,
                MaxHeight = maxH,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, top, 0, bottom),
                IsHitTestVisible = false
            };

            contentStack.Children.Add(imgOnly);
        }

        private void AppendPlatformRow(Game game, string key, double innerMax, bool isFirstBlock)
        {
            AppendFieldBlockSeparator(isFirstBlock);

            var showTitle = !settings.HideFieldTitlesInHover;
            var labelText = HoverFieldCatalog.GetDisplayName(key);

            var topInset = isFirstBlock ? FirstBlockHeaderTopDip : FieldBlockSpacingHalfDip();
            var bottomInset = FieldBlockSpacingHalfDip();
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
                MaxWidth = innerMax,
                Margin = new Thickness(0, topInset, 0, bottomInset)
            };

            if (game.Platforms != null)
            {
                foreach (var platform in game.Platforms)
                {
                    var iconBmp = HoverBitmapLoader.TryLoadPlatformIcon(platform, playniteApi);
                    if (iconBmp == null)
                    {
                        continue;
                    }

                    panel.Children.Add(
                        new Image
                        {
                            Source = iconBmp,
                            Height = HoverIconBoxPx,
                            Width = HoverIconBoxPx,
                            MaxHeight = HoverIconBoxPx,
                            MaxWidth = HoverIconBoxPx,
                            Margin = new Thickness(0, 0, 6, 0),
                            Stretch = Stretch.Uniform,
                            HorizontalAlignment = HorizontalAlignment.Left
                        });
                }
            }

            if (panel.Children.Count > 0)
            {
                if (showTitle)
                {
                    var label = new TextBlock { Margin = new Thickness(0, topInset, 0, LabelToValueGapDip) };
                    HoverDetailValuePresenter.ConfigureFieldLabelTextBlock(label, innerMax, Palette.LabelText);
                    HoverDetailValuePresenter.SetHeaderText(label, labelText, innerMax);
                    contentStack.Children.Add(label);
                    panel.Margin = new Thickness(0, 0, 0, bottomInset);
                }

                contentStack.Children.Add(panel);
                return;
            }

            AppendTextDetailInner(key, game, innerMax, isFirstBlock);
        }

        private void ChromeBorderOnPointerOverChrome(object sender, MouseEventArgs e)
        {
            if (broken)
            {
                return;
            }

            try
            {
                hideDebounceTimer?.Stop();
                HidePopup();
                e.Handled = true;
            }
            catch (Exception ex)
            {
                LatchBroken(ex);
            }
        }

        private void EnsurePopupShell()
        {
            if (popup != null)
            {
                return;
            }

            contentStack = new StackPanel
            {
                Margin = new Thickness(14, 12, 14, 12),
                IsHitTestVisible = true
            };

            frostImage = new Image
            {
                Stretch = Stretch.Fill,
                IsHitTestVisible = false
            };

            frostHost = new Border
            {
                CornerRadius = new CornerRadius(ChromeCornerRadiusDip),
                ClipToBounds = true,
                IsHitTestVisible = false,
                Child = frostImage,
                Visibility = Visibility.Collapsed
            };

            chromeFlyTransform = new TranslateTransform();
            chromeBorder = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(ChromeCornerRadiusDip),
                Child = contentStack,
                IsHitTestVisible = true
            };
            chromeBorder.PreviewMouseMove += ChromeBorderOnPointerOverChrome;
            chromeBorder.MouseEnter += ChromeBorderOnPointerOverChrome;

            var shadow = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(72, 0, 0, 0)),
                CornerRadius = new CornerRadius(ChromeCornerRadiusDip),
                IsHitTestVisible = false,
                RenderTransform = new TranslateTransform(0, 2)
            };

            var layers = new Grid();
            layers.Children.Add(shadow);
            layers.Children.Add(frostHost);
            layers.Children.Add(chromeBorder);

            chromeRoot = new Border
            {
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(ChromeCornerRadiusDip),
                Child = layers,
                IsHitTestVisible = true,
                RenderTransform = chromeFlyTransform,
                RenderTransformOrigin = new Point(0, 0)
            };
            ApplyChromeFlowDirection();
            chromeRoot.SizeChanged += ChromeRootOnSizeChanged;

            popup = new Popup
            {
                AllowsTransparency = true,
                StaysOpen = true,
                PopupAnimation = PopupAnimation.None,
                Child = chromeRoot,
                IsHitTestVisible = true
            };

            ApplyChrome();
        }

        private void ChromeRootOnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateFrostClip();
        }

        private void UpdateFrostClip()
        {
            if (frostHost == null || chromeRoot == null)
            {
                return;
            }

            var w = chromeRoot.ActualWidth;
            var h = chromeRoot.ActualHeight;
            if (w <= 0 || h <= 0)
            {
                frostHost.Clip = null;
                return;
            }

            frostHost.Clip = new RectangleGeometry(
                new Rect(0, 0, w, h),
                ChromeCornerRadiusDip,
                ChromeCornerRadiusDip);
        }

        private void UpdateFrostBackdrop()
        {
            if (frostHost == null || frostImage == null || chromeRoot == null)
            {
                return;
            }

            var useFrost = settings.HoverChromeBackgroundOpacity < 100;
            if (!useFrost)
            {
                frostHost.Visibility = Visibility.Collapsed;
                frostImage.Source = null;
                frostImage.Effect = null;
                return;
            }

            EnsureFrostBlurEffect();
            frostHost.Visibility = Visibility.Visible;

            if (popup == null || !popup.IsOpen || chromeRoot.ActualWidth < 2 || chromeRoot.ActualHeight < 2)
            {
                return;
            }

            // CopyFromScreen of a visible panel would snapshot the hover itself. Capture only
            // while the chrome is still transparent (enter animation / first layout).
            if (chromeRoot.Opacity >= 0.2)
            {
                if (frostImage.Source == null)
                {
                    frostHost.Visibility = Visibility.Collapsed;
                }

                return;
            }

            try
            {
                var snapshot = CapturePopupScreenRect();
                if (snapshot == null)
                {
                    frostHost.Visibility = Visibility.Collapsed;
                    frostImage.Source = null;
                    return;
                }

                frostImage.Source = snapshot;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "GameHoverDetails frost snapshot failed.");
                frostHost.Visibility = Visibility.Collapsed;
                frostImage.Source = null;
            }
        }

        private void EnsureFrostBlurEffect()
        {
            if (frostImage == null || frostImage.Effect != null)
            {
                return;
            }

            frostImage.Effect = new BlurEffect
            {
                Radius = FrostBlurRadius,
                KernelType = KernelType.Gaussian
            };
        }

        private BitmapSource CapturePopupScreenRect()
        {
            var w = chromeRoot.ActualWidth;
            var h = chromeRoot.ActualHeight;
            var dpiX = 96.0;
            var dpiY = 96.0;
            var source = PresentationSource.FromVisual(chromeRoot) ?? PresentationSource.FromVisual(mainWindow);
            if (source?.CompositionTarget != null)
            {
                var m = source.CompositionTarget.TransformToDevice;
                dpiX = 96.0 * m.M11;
                dpiY = 96.0 * m.M22;
            }

            var pixelW = Math.Max(1, (int)Math.Ceiling(w * dpiX / 96.0));
            var pixelH = Math.Max(1, (int)Math.Ceiling(h * dpiY / 96.0));
            if (pixelW > 4096 || pixelH > 4096)
            {
                return null;
            }

            var topLeft = chromeRoot.PointToScreen(new Point(0, 0));
            var screenX = (int)Math.Round(topLeft.X);
            var screenY = (int)Math.Round(topLeft.Y);

            using (var bmp = new System.Drawing.Bitmap(
                pixelW,
                pixelH,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                using (var g = System.Drawing.Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(
                        screenX,
                        screenY,
                        0,
                        0,
                        new System.Drawing.Size(pixelW, pixelH),
                        System.Drawing.CopyPixelOperation.SourceCopy);
                }

                var hBitmap = bmp.GetHbitmap();
                try
                {
                    var image = Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap,
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    image.Freeze();
                    return image;
                }
                finally
                {
                    DeleteObject(hBitmap);
                }
            }
        }

        [DllImport("gdi32.dll", ExactSpelling = true)]
        private static extern bool DeleteObject(IntPtr hObject);

        private HoverChromePalette Palette => palette ?? (palette = HoverChromePalette.Resolve(settings));

        private void ApplyChrome()
        {
            palette = HoverChromePalette.Resolve(settings);
            HoverChromePalette.ApplyToChromeBorder(chromeBorder, settings);
            ApplyChromeFlowDirection();
            UpdateFrostBackdrop();
        }

        private void ApplyChromeFlowDirection()
        {
            var flow = HoverLoc.LayoutFlow(playniteApi, mainWindow);
            // Popup HWND placement is physical (top-left origin). RTL on Popup mirrors
            // CustomPopupPlacementCallback points and drops the panel on the tile.
            if (popup != null)
            {
                popup.FlowDirection = FlowDirection.LeftToRight;
            }

            if (chromeRoot != null)
            {
                chromeRoot.FlowDirection = flow;
            }

            // #region agent log
            AgentLog("H5", "ApplyChromeFlowDirection", "flow applied",
                "{\"layoutFlow\":\"" + flow + "\",\"popupFd\":\"" + (popup != null ? popup.FlowDirection.ToString() : "null")
                + "\",\"chromeFd\":\"" + (chromeRoot != null ? chromeRoot.FlowDirection.ToString() : "null") + "\"}");
            // #endregion

            if (contentStack != null)
            {
                contentStack.FlowDirection = flow;
            }
        }

        // #region agent log
        private static void AgentLog(string hypothesisId, string location, string message, string dataJson)
        {
            try
            {
                var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var line = "{\"sessionId\":\"848814\",\"runId\":\"post-fix\",\"hypothesisId\":\"" + hypothesisId
                    + "\",\"location\":\"" + location + "\",\"message\":\"" + message
                    + "\",\"data\":" + dataJson + ",\"timestamp\":" + ts.ToString(CultureInfo.InvariantCulture) + "}\n";
                File.AppendAllText(@"E:\Projects\Playnite Extensions\debug-848814.log", line);
            }
            catch
            {
            }
        }
        // #endregion
    }
}
