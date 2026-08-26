# GameHoverDetails — extension notes

## What this extension does

**GameHoverDetails** is a **Playnite** **GenericPlugin** (`net462`, **WPF**) that shows a **hover popup** anchored to the library tile when the cursor is over an item whose `DataContext` resolves to a **`Game`**. Content is **user-configurable** (width, up to five detail fields).

## Implementation

1. **Lifecycle** — `GameHoverDetailsPlugin` attaches on `OnApplicationStarted` via `UIDispatcher` / `ApplicationIdle`, mirroring Autogrid’s main-window readiness pattern. Detaches on `OnApplicationStopped`.
2. **Hover** — `GameHoverDetailsHoverService` handles **`PreviewMouseMove`**: resolves **`Game`** and anchor synchronously and updates the popup immediately (no trailing debounce for show/switch). A **separate ~70ms trailing debounce** hides the popup after the pointer leaves game tiles (reduces flicker over gaps). **`PreviewKeyDown`** defers a hide until after Playnite handles the key: if the pointer is still on a game tile the tooltip stays; if the hotkey opened another UI (F4/F9) and hit-test is no longer a game, the popup hides. `Application.Deactivated` / minimize still hide as before. With an anchor, placement uses **`PlacementMode.Custom`** and **`CustomPopupPlacementCallback`**: prefer the **end** side of the target (**right** in LTR, **left** in RTL), else the start side (WPF picks the first option that fits on-screen). List view stays start-aligned under the row. Hover **`FlowDirection`** is applied to the inner chrome only — never the `Popup` (RTL on the popup mirrors custom placement onto the tile). **`ClampPopupToVirtualScreen`** only adjusts **vertical** position in that mode so horizontal side choice is preserved. **`PopupAnimation.None`** plus a short **opacity-only** storyboard runs when opening or switching games; **same `Game.Id`** with unchanged field keys skips rebuilding inner content and replays no enter animation (placement/anchor still updates). Hover is suppressed when globally disabled or when Fullscreen mode is on and **Show hover in Fullscreen** is off (`ApplicationInfo.Mode`). Popup chrome comes from **`HoverChromePalette`**: theme-sync fill is a **darker, slightly desaturated `GlyphBrush` mixed toward black**; icons stay accent; custom uses picker hex for fill/border/icon-bg. Labels, values, and icon glyphs always use Playnite **`TextBrush`**. Dividers use the **border** brush. Opacity below 100% frosts a **GDI `CopyFromScreen` of the popup rect only** (never a `VisualBrush`/`RenderTargetBitmap` of MainWindow — that froze the UI for seconds). **`BlurEffect`** is created only when frost is on; shadow is a cheap offset border, not `DropShadowEffect`. Do **not** use **`MainWindow.MouseLeave`** to close the popup: opening a **`Popup`** caused spurious leave events and flicker.
3. **Fragility** — Playnite does not document item `DataContext` shapes; templates may change between versions. Failures are latched and logged once; the service detaches.

## Key files

| Area | Path |
|------|------|
| Plugin lifecycle | `src/GameHoverDetails/src/GameHoverDetailsPlugin.cs` |
| Hover UI | `src/GameHoverDetails/src/GameHoverDetailsHoverService.cs` |
| Settings | `src/GameHoverDetails/src/GameHoverDetailsSettings.cs`, `GameHoverDetailsSettingsView.xaml` |
| Chrome / theme | `HoverChromePalette.cs` |
| Field catalog / text | `HoverFieldCatalog.cs`, `HoverFieldFormatter.cs`, `HoverLoc.cs` |
| Localization | `Localization/*.xaml` (all Playnite Crowdin locales; English fallback) |
| Field / settings glyphs | `fonts/Phosphor.ttf` (Phosphor Icons, MIT) |
| Manifest | `src/GameHoverDetails/info/extension.yaml` |

## Settings

- UI strings load from **`Localization/{locale}.xaml`** (Playnite language); English is the fallback. RTL languages (e.g. Hebrew) set **inner** hover/settings **`FlowDirection`** from Playnite language / main window and **mirror** placement (prefer left of the tile).
- **Add-ons → Extension settings → Generic → GameHoverDetails**: **Enable hover details**; **Show hover in Fullscreen mode** (off by default — Playnite Desktop and Fullscreen are separate processes; change this from Desktop settings). **Use Playnite theme colors** (on by default) keeps **background**, **border**, and **icon background** in sync with the Playnite theme (darker desaturated accent fill); pickers stay visible as a live mirror. Labels, values, and **icon glyphs always use Playnite `TextBrush`** (no text or icon-color picker). Editing a swatch/hex or **Reset to default colors** turns theme-sync off. **Background opacity** (0–100%) tints the fill; below 100% the live hover frosts the library behind the panel (settings preview shows tint only). **Tooltip appear delay** (0–500 ms, 0 = immediate), hover width (120–500 px), up to **five** detail fields (same catalog as Playnite’s details panel; factory default **Icon**, **Name**, **Last Played** on first run). The hover popup **dismisses as soon as the pointer moves onto the panel** (hit-testable chrome; you cannot “rest” the cursor on the tooltip). Body is a single stacked column with no inner scroll. Settings use a **single ordered list** (top = first in hover): **↑ / ↓** and **Remove** per row; **Add field** is a dropdown of catalog entries not yet selected (appends to the bottom when fewer than five are shown).

## Gotchas

- Keep **`GameHoverDetails_BA249C5D`**, **`GameHoverDetails.dll`**, and plugin **`Guid`** stable for shipped users.
- **`EndEdit`** only persists JSON — do not re-assign hex/field lists on Save (that unchecks theme-sync and rebuilds the settings preview, including a full-library icon scan).
- Do not use **`MainWindow.MouseLeave`** to close the hover popup (spurious leave when a `Popup` opens).
- Do not poll hit-testing to close on hotkeys; **`PreviewKeyDown`** hides only after the key is processed **and** the pointer is no longer on a game.
- Hover is off in Fullscreen by default (`HoverDisabledInFullscreen`); Desktop and Fullscreen are separate Playnite processes — toggle **Show hover in Fullscreen mode** from Desktop add-on settings.
- Theme-sync fill is a darkened, slightly desaturated **`GlyphBrush`** mixed further toward black (not `PopupBackgroundBrush`); editing pickers or Reset turns **Use Playnite theme colors** off. Field dividers use the same brush as the panel **border**. Labels, values, and icon glyphs always use Playnite **`TextBrush`**. Frosted backdrop is live-hover only (settings preview is tint-only). Do not snapshot MainWindow with `VisualBrush`/`RenderTargetBitmap` — capture the popup screen rect only, and only while chrome opacity is still near 0.
