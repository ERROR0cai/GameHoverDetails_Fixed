using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Plugins;

namespace Autogrid
{
    public class AutogridPlugin : GenericPlugin
    {
        private static readonly Guid PluginId = Guid.Parse("7F3E9B82-4D1C-4E8A-9F2B-6C5A891D0E2F");
        private static readonly ILogger Logger = LogManager.GetLogger();

        private readonly AutogridSettings settings;
        private Window hookedWindow;
        private DispatcherTimer debounceTimer;
        private DispatcherTimer saveSettingsTimer;
        private object pendingSaveAppSettings;
        private bool reflectionBroken;
        private bool reflectionBrokenLogged;
        private bool handlersAttached;
        private bool startupApplyComplete;
        private int startupApplyAttemptsRemaining;
        private bool settingsApplyPosted;

        public override Guid Id => PluginId;

        public AutogridPlugin(IPlayniteAPI api) : base(api)
        {
            settings = new AutogridSettings(this);
            settings.PropertyChanged += OnSettingsPropertyChanged;
            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override System.Windows.Controls.UserControl GetSettingsView(bool firstRunSettings)
        {
            return new AutogridSettingsView();
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            PlayniteApi.MainView.UIDispatcher.BeginInvoke(
                new Action(AttachWhenReady),
                DispatcherPriority.Loaded);
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            settings.PropertyChanged -= OnSettingsPropertyChanged;
            settings.PersistPluginSettings();
            FlushPendingAppSettingsSave();
            DetachWindowHooks();
        }

        private void OnSettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (reflectionBroken)
            {
                return;
            }

            var n = e.PropertyName;
            if (n != null &&
                n != nameof(AutogridSettings.SizingMode) &&
                n != nameof(AutogridSettings.TargetColumns) &&
                n != nameof(AutogridSettings.TargetRows) &&
                n != nameof(AutogridSettings.ViewportAdjustPx) &&
                n != nameof(AutogridSettings.Enabled))
            {
                return;
            }

            RequestApplyFromSettings();
        }

        private void RequestApplyFromSettings()
        {
            if (reflectionBroken || settingsApplyPosted)
            {
                return;
            }

            settingsApplyPosted = true;
            PlayniteApi.MainView.UIDispatcher.BeginInvoke(
                new Action(() =>
                {
                    settingsApplyPosted = false;
                    try
                    {
                        ApplyAutogrid();
                    }
                    catch (Exception ex)
                    {
                        LatchReflectionBroken(ex);
                    }
                }),
                DispatcherPriority.Loaded);
        }

        private void AttachWhenReady()
        {
            try
            {
                var app = Application.Current;
                if (app == null)
                {
                    return;
                }

                if (app.MainWindow != null)
                {
                    TryHookMainWindow(app.MainWindow);
                }
                else
                {
                    app.Activated += OnApplicationActivatedOnce;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Autogrid failed to attach to the main window.");
            }
        }

        private void OnApplicationActivatedOnce(object sender, EventArgs e)
        {
            try
            {
                Application.Current.Activated -= OnApplicationActivatedOnce;
                if (Application.Current?.MainWindow != null)
                {
                    TryHookMainWindow(Application.Current.MainWindow);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Autogrid failed on application activated.");
            }
        }

        private void TryHookMainWindow(Window window)
        {
            if (window == null || handlersAttached)
            {
                return;
            }

            hookedWindow = window;
            debounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            debounceTimer.Tick += DebounceTimerOnTick;

            window.SizeChanged += OnWindowLayoutHint;
            window.LayoutUpdated += OnWindowLayoutHint;
            window.StateChanged += OnWindowLayoutHint;
            window.Loaded += WindowOnLoadedOnce;
            window.ContentRendered += WindowOnContentRenderedOnce;
            window.Closed += MainWindowOnClosed;

            handlersAttached = true;
            BeginStartupApply();
        }

        private void WindowOnLoadedOnce(object sender, RoutedEventArgs e)
        {
            if (hookedWindow != null)
            {
                hookedWindow.Loaded -= WindowOnLoadedOnce;
            }

            TryApplyStartupNow();
        }

        private void WindowOnContentRenderedOnce(object sender, EventArgs e)
        {
            if (hookedWindow != null)
            {
                hookedWindow.ContentRendered -= WindowOnContentRenderedOnce;
            }

            TryApplyStartupNow();
        }

        private void BeginStartupApply()
        {
            if (reflectionBroken)
            {
                return;
            }

            startupApplyComplete = false;
            startupApplyAttemptsRemaining = 8;
            TryApplyStartupNow();
        }

        private void TryApplyStartupNow()
        {
            if (startupApplyComplete || reflectionBroken)
            {
                return;
            }

            if (TryApplyOnce())
            {
                startupApplyComplete = true;
                return;
            }

            if (startupApplyAttemptsRemaining <= 0)
            {
                startupApplyComplete = true;
                return;
            }

            startupApplyAttemptsRemaining--;
            PlayniteApi.MainView.UIDispatcher.BeginInvoke(
                new Action(TryApplyStartupNow),
                DispatcherPriority.Render);
        }

        private bool TryApplyOnce()
        {
            try
            {
                return ApplyAutogrid();
            }
            catch (Exception ex)
            {
                LatchReflectionBroken(ex);
                return false;
            }
        }

        private void RequestDebouncedApply()
        {
            if (reflectionBroken)
            {
                return;
            }

            debounceTimer?.Stop();
            debounceTimer?.Start();
        }

        private void MainWindowOnClosed(object sender, EventArgs e)
        {
            DetachWindowHooks();
        }

        private void OnWindowLayoutHint(object sender, EventArgs e)
        {
            if (!startupApplyComplete)
            {
                if (TryApplyOnce())
                {
                    startupApplyComplete = true;
                }

                return;
            }

            RequestDebouncedApply();
        }

        private void DebounceTimerOnTick(object sender, EventArgs e)
        {
            debounceTimer?.Stop();
            try
            {
                ApplyAutogrid();
            }
            catch (Exception ex)
            {
                LatchReflectionBroken(ex);
            }
        }

        private bool ApplyAutogrid()
        {
            if (reflectionBroken)
            {
                return false;
            }

            if (!settings.Enabled)
            {
                RestoreUserCoverSize();
                return true;
            }

            if (PlayniteApi.MainView.ActiveDesktopView != DesktopView.Grid)
            {
                return false;
            }

            var window = hookedWindow ?? Application.Current?.MainWindow;
            if (window == null)
            {
                return false;
            }

            var appSettings = GridLayoutService.TryResolveAppSettings(window);
            if (appSettings == null)
            {
                return false;
            }

            if (!GridLayoutService.TryGetGridLayoutInputs(appSettings, out var currentSpacing, out var currentWidth))
            {
                LatchReflectionBroken(null);
                return false;
            }

            CaptureUserLayoutIfNeeded(currentWidth, currentSpacing);

            var metrics = GridLayoutService.ResolveViewportMetrics(window, PlayniteApi, settings.ViewportAdjustPx);
            double targetWidth;
            var targetSpacing = settings.HasSavedUserLayout
                ? settings.SavedUserGridItemSpacing
                : currentSpacing;

            if (settings.SizingMode == GridSizingMode.Rows)
            {
                if (metrics.PickedScrollViewer == null ||
                    !GridLayoutService.TryMeasureTileVerticalPitch(metrics.PickedScrollViewer, out var tileMeasurements))
                {
                    return false;
                }

                var verticalMargin = GridLayoutService.SpacingToAxisMargin(targetSpacing);
                targetWidth = GridLayoutService.ComputeTargetGridItemWidthForRows(
                    metrics.ViewportHeight,
                    settings.TargetRows,
                    verticalMargin,
                    currentWidth,
                    tileMeasurements);
            }
            else
            {
                if (metrics.Viewport <= 0)
                {
                    return false;
                }

                if (!GridLayoutService.ComputeColumnLayout(
                        metrics.Viewport,
                        settings.TargetColumns,
                        targetSpacing,
                        out targetWidth,
                        out targetSpacing))
                {
                    return false;
                }
            }

            if (targetWidth <= 0)
            {
                return false;
            }

            var widthChanged = Math.Abs(currentWidth - targetWidth) >= 0.01;
            var spacingChanged = currentSpacing != targetSpacing;
            if (!widthChanged && !spacingChanged)
            {
                return true;
            }

            if (spacingChanged && !GridLayoutService.TrySetGridItemSpacing(appSettings, targetSpacing))
            {
                LatchReflectionBroken(null);
                return false;
            }

            if (widthChanged && !GridLayoutService.TrySetGridItemWidth(appSettings, targetWidth))
            {
                LatchReflectionBroken(null);
                return false;
            }

            ScheduleSaveAppSettings(appSettings);
            return true;
        }

        private void CaptureUserLayoutIfNeeded(double currentWidth, int currentSpacing)
        {
            if (settings.HasSavedUserLayout)
            {
                return;
            }

            settings.HasSavedUserLayout = true;
            settings.SavedUserGridItemWidth = currentWidth;
            settings.SavedUserGridItemSpacing = currentSpacing;
        }

        private void RestoreUserCoverSize()
        {
            if (reflectionBroken || !settings.HasSavedUserLayout)
            {
                return;
            }

            var window = hookedWindow ?? Application.Current?.MainWindow;
            var appSettings = window != null ? GridLayoutService.TryResolveAppSettings(window) : null;
            if (appSettings == null)
            {
                return;
            }

            var widthOk = GridLayoutService.TrySetGridItemWidth(appSettings, settings.SavedUserGridItemWidth);
            var spacingOk = GridLayoutService.TrySetGridItemSpacing(appSettings, settings.SavedUserGridItemSpacing);
            if (!widthOk)
            {
                LatchReflectionBroken(null);
                return;
            }

            if (!spacingOk)
            {
                Logger.Warn("Autogrid restored cover width but could not write GridItemSpacing.");
            }

            GridLayoutService.TrySaveAppSettings(appSettings);
            CancelPendingAppSettingsSave();
            settings.HasSavedUserLayout = false;
        }

        private void LatchReflectionBroken(Exception ex)
        {
            reflectionBroken = true;
            if (!reflectionBrokenLogged)
            {
                reflectionBrokenLogged = true;
                if (ex != null)
                {
                    Logger.Error(ex, "Autogrid stopped: Playnite settings reflection failed.");
                }
                else
                {
                    Logger.Error("Autogrid stopped: Playnite settings reflection failed.");
                }
            }
        }

        private void ScheduleSaveAppSettings(object appSettings)
        {
            pendingSaveAppSettings = appSettings;
            if (saveSettingsTimer == null)
            {
                saveSettingsTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(250)
                };
                saveSettingsTimer.Tick += SaveSettingsTimerOnTick;
            }

            saveSettingsTimer.Stop();
            saveSettingsTimer.Start();
        }

        private void SaveSettingsTimerOnTick(object sender, EventArgs e)
        {
            saveSettingsTimer?.Stop();
            var appSettings = pendingSaveAppSettings;
            pendingSaveAppSettings = null;
            if (appSettings != null)
            {
                GridLayoutService.TrySaveAppSettings(appSettings);
            }
        }

        private void FlushPendingAppSettingsSave()
        {
            saveSettingsTimer?.Stop();
            var appSettings = pendingSaveAppSettings;
            pendingSaveAppSettings = null;
            if (appSettings != null)
            {
                GridLayoutService.TrySaveAppSettings(appSettings);
            }
        }

        private void CancelPendingAppSettingsSave()
        {
            saveSettingsTimer?.Stop();
            pendingSaveAppSettings = null;
        }

        private void DetachWindowHooks()
        {
            FlushPendingAppSettingsSave();

            if (saveSettingsTimer != null)
            {
                saveSettingsTimer.Tick -= SaveSettingsTimerOnTick;
            }

            saveSettingsTimer = null;
            pendingSaveAppSettings = null;

            debounceTimer?.Stop();
            if (debounceTimer != null)
            {
                debounceTimer.Tick -= DebounceTimerOnTick;
            }

            debounceTimer = null;

            if (hookedWindow != null && handlersAttached)
            {
                hookedWindow.SizeChanged -= OnWindowLayoutHint;
                hookedWindow.LayoutUpdated -= OnWindowLayoutHint;
                hookedWindow.StateChanged -= OnWindowLayoutHint;
                hookedWindow.Loaded -= WindowOnLoadedOnce;
                hookedWindow.ContentRendered -= WindowOnContentRenderedOnce;
                hookedWindow.Closed -= MainWindowOnClosed;
            }

            hookedWindow = null;
            handlersAttached = false;
            startupApplyComplete = false;

            try
            {
                Application.Current.Activated -= OnApplicationActivatedOnce;
            }
            catch
            {
                // ignore
            }
        }
    }
}
