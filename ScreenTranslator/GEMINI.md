# Screen Translator

AI-powered screen translation tool for Windows. Hotkey-driven snip-and-translate workflow using Claude Vision API.

---

## Project Purpose

Replace the manual workflow of "screenshot → paste into Zalo/browser → translate" with a single hotkey:

1. User presses `Ctrl+Shift+D` anywhere in Windows
2. A snipping overlay appears (Snipping Tool style)
3. User drags to select a region of the screen
4. The selected region is sent to Claude Vision API for translation
5. Translation result appears in a floating panel near the snipped region, auto-copied to clipboard

**Primary use case:** translating foreign-language UI of factory machine-operating software (Chinese, Japanese, Korean) into Vietnamese for factory workers with limited foreign-language proficiency.

**Deployment scope:** personal tool for the developer (Bryan, RFID Director). Not multi-user, not deployed to factory floor in v1.

---

## Tech Stack

- **Language / Runtime:** C# / .NET 8 (`net8.0-windows`)
- **UI Framework:** WPF (primary) + Windows Forms (for `NotifyIcon` only)
- **AI Provider:** Anthropic Claude API
  - Default model: `claude-sonnet-4-6` (Sonnet 4.6 — best balance of vision quality, latency, cost)
  - Alternative: `claude-haiku-4-5-20251001` (faster, cheaper, for simple UIs)
  - Premium option: `claude-opus-4-7` (3× vision resolution, for dense text screens)
- **Persistence:** SQLite + Dapper (for translation history)
- **API Key Storage:** Windows DPAPI (`ProtectedData.Protect`, `CurrentUser` scope)

### Why this stack

- C# WPF over Python: native global hotkey via `RegisterHotKey`, smooth transparent overlays via GPU compositing, single-file `.exe` deployment with no runtime dependencies.
- Sonnet 4.6 over OCR-first approach: vision model handles complex UI layouts, weird fonts, and contextual translation (e.g., "Start" on a CNC machine vs. "Start" in an office app) without per-language tuning.

---

## Project Structure

```
ScreenTranslator/
├── App.xaml                    # Application root (ShutdownMode=OnExplicitShutdown)
├── App.xaml.cs                 # Tray icon, hotkey registration, app lifecycle
├── ScreenTranslator.csproj     # net8.0-windows, UseWPF + UseWindowsForms
│
├── Hotkey/
│   └── HotkeyManager.cs        # P/Invoke RegisterHotKey, message-only window for WM_HOTKEY
│
├── Snip/
│   └── SnipOverlayWindow.xaml(.cs)   # Fullscreen transparent overlay, region selection
│
├── Translation/                # [M4] Claude API integration
│   └── TranslationService.cs   # HttpClient → /v1/messages with image + prompt
│
├── UI/                         # [M5] Result panel
│   └── ResultPanelWindow.xaml(.cs)   # Floating panel near snipped region
│
├── Settings/                   # [M4 + M6] Configuration
│   ├── SettingsWindow.xaml(.cs)
│   ├── AppSettings.cs          # API key (DPAPI), default target, model, hotkey
│   └── SettingsStore.cs        # JSON serialization to %LocalAppData%
│
└── History/                    # [M6] Translation history
    ├── HistoryRepository.cs    # Dapper-based SQLite access
    └── HistoryEntry.cs         # Schema: id, created_at, image_blob, thumbnail, source/target lang, text, model
```

Database file location: `%LocalAppData%\ScreenTranslator\history.db`
Settings file location: `%LocalAppData%\ScreenTranslator\settings.json`

---

## Architecture: Six Components

1. **Tray Host** (`App.xaml.cs`) — root of the application. Hidden WPF app with `NotifyIcon` in system tray, context menu (Settings / Pause / Exit), owns the message loop and hotkey registration. Auto-starts via `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.

2. **Hotkey Listener** (`Hotkey/HotkeyManager.cs`) — uses `RegisterHotKey` Win32 API via P/Invoke. Listens for `WM_HOTKEY` messages on a hidden message-only window (`HWND_MESSAGE` parent). Detects conflicts when another app already owns the hotkey.

3. **Snip Overlay** (`Snip/SnipOverlayWindow.xaml`) — borderless WPF window covering `SystemInformation.VirtualScreen` (all monitors), `Topmost=true`, semi-transparent black mask with selection-rectangle "hole" using `CombinedGeometry` + `GeometryCombineMode.Exclude`. Captures region via `Graphics.CopyFromScreen` after hiding itself briefly.

4. **Translation Service** (`Translation/TranslationService.cs`) — accepts `byte[] imageBytes` + `targetLanguage` + `CancellationToken`. POSTs to `https://api.anthropic.com/v1/messages` with base64 image and a terse system prompt. Returns translated text.

5. **Result Panel** (`UI/ResultPanelWindow.xaml`) — floating window with no border, drop shadow, positioned next to the snipped region (flips to opposite side if too close to screen edge). States: Loading → Success (text + target-language dropdown + Copy button) → Error. Auto-copies text to clipboard on success. Re-translation: changing the dropdown calls API again with the original image and updated target.

6. **Settings Store** (`Settings/SettingsStore.cs`) — JSON file with API key encrypted via DPAPI. Stores: API key, default target language, model preference, custom hotkey, history retention limit.

---

## V1 Specification (Locked)

- **Hotkey:** `Ctrl+Shift+D` (customizable in Settings)
- **Source language:** Auto-detect (handled by Claude Vision)
- **Target language:** Default Vietnamese; changeable via dropdown on result panel
- **Behavior on result:** show floating panel + auto-copy translation to clipboard
- **History:** SQLite, viewable in Settings → History tab, with thumbnail + re-translate from original image
- **Auto-start:** registers in HKCU Run key on launch
- **Pause mode:** tray menu toggle, bypasses hotkey when paused
- **Cancel:** Escape key cancels overlay or closes result panel
- **Re-trigger:** pressing hotkey while overlay is open closes the overlay (intent: cancel)

---

## Coding Conventions

### WPF + WinForms namespace conflicts

This project mixes WPF (`System.Windows.*`) and Windows Forms (`System.Windows.Forms.*`) because `NotifyIcon` is only available in WinForms. Many types collide between the two namespaces:

- `Application` — aliased: `using Application = System.Windows.Application;`
- `MessageBox`, `Clipboard`, `MouseEventArgs`, `KeyEventArgs`, `Point`, `HorizontalAlignment`, `VerticalAlignment`, `PixelFormat` — **always use full namespace** at point of use:

```csharp
System.Windows.MessageBox.Show("...");
System.Drawing.Imaging.PixelFormat.Format32bppArgb
System.Windows.HorizontalAlignment.Left
```

**Rule:** prefer full namespace at usage site over file-level `using` aliases. Verbose but unambiguous; aliases scattered across files cause confusion.

### DPI awareness

`<ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>` in `.csproj`. Do **not** put DPI settings in `app.manifest` — WinForms warns and prefers project property (warning WFAC010).

### Coordinate systems

- Mouse position in overlay: window-relative (`e.GetPosition(RootGrid)`)
- `Graphics.CopyFromScreen`: screen-absolute (physical pixels)
- Conversion: add `SystemInformation.VirtualScreen.Left/Top` offset
- Multi-monitor: `VirtualScreen` may have negative coordinates (monitors to the left of primary)

### Async / cancellation

- All API calls must accept `CancellationToken`
- Re-translation (changing target language dropdown) must cancel the previous in-flight request before starting the new one to avoid out-of-order responses

---

## Implementation Roadmap

### M1 — Tray app shell ✅ DONE
- WPF app with hidden startup, `NotifyIcon`, context menu (Settings / Pause / Exit)
- Auto-start registry entry
- `ShutdownMode=OnExplicitShutdown`

### M2 — Global hotkey ✅ DONE
- `HotkeyManager` with P/Invoke `RegisterHotKey` / `UnregisterHotKey`
- Message-only window receives `WM_HOTKEY`
- `Modifiers.NoRepeat` flag prevents typematic re-fires
- Conflict detection via `RegisterHotKey` return value
- Pause toggle bypasses hotkey

### M3 — Snip overlay ✅ DONE
- Fullscreen transparent overlay covering all monitors
- Selection rectangle with mask hole via `CombinedGeometry`
- Capture via `Graphics.CopyFromScreen` after `Hide()` + brief delay
- Escape cancels, second hotkey press closes overlay

### M4 — Claude API integration ⬜ TODO
- `TranslationService` class
- Settings UI: API key (PasswordBox), default target (ComboBox), model (ComboBox)
- DPAPI encryption for API key
- HTTP POST to `https://api.anthropic.com/v1/messages`
- Request structure: model, max_tokens=1024, messages with image base64 + text prompt
- System prompt (terse, single-purpose):
  > "You are a translation tool. Look at the image, identify all visible text, translate it to {target_language}. Output ONLY the translation, no preamble, no explanations. If text spans multiple UI elements, preserve their visual grouping with line breaks."
- Test isolation: console harness with hardcoded image before integration

### M5 — Result panel + end-to-end ⬜ TODO
- WPF window: borderless, `AllowsTransparency=true`, drop shadow
- Position: adjacent to snipped region, flip if close to screen edge (use `Screen.GetWorkingArea`)
- States: Loading (spinner) / Success (text + dropdown + Copy) / Error
- Auto-copy on success via `Clipboard.SetText` BEFORE animating panel in
- Re-translation: dropdown change → cancel prior request → API call with same image + new target → update text + clipboard
- Escape or click-outside closes panel

### M6 — History + polish ⬜ TODO
- SQLite schema:
  ```sql
  CREATE TABLE translations (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    created_at TEXT NOT NULL,
    source_image_blob BLOB NOT NULL,
    thumbnail_blob BLOB NOT NULL,
    source_lang_detected TEXT,
    target_lang TEXT NOT NULL,
    translated_text TEXT NOT NULL,
    model_used TEXT NOT NULL,
    image_width INTEGER,
    image_height INTEGER
  );
  ```
- Dapper repository
- History tab in Settings: ListView with thumbnail + snippet + timestamp; click to view full; "Translate again" button (re-call API with original blob); Copy button
- Auto-cleanup retention limit (default 500 entries)
- Customizable hotkey UI (capture key combo, validate, re-register)
- Single-instance enforcement via `Mutex`
- Custom app icon (replace `SystemIcons.Application`)
- Network error handling: 30s timeout, 1× retry, clear error messages

---

## Known Quirks & Lessons Learned

1. **`ShowDialog()` is dangerous in tray-only apps.** Even with `ShutdownMode=OnExplicitShutdown`, `ShowDialog` from a window with no proper parent can trigger app shutdown when it closes. Use `Show()` + `Closed` event instead, and explicitly set `MainWindow = null` after the window closes to prevent WPF from holding a reference.

2. **`Graphics.CopyFromScreen` captures the overlay too** if you don't hide it first. Need `Hide()` + `Application.DoEvents()` + ~50ms `Thread.Sleep` to let the desktop compositor refresh before capturing. There's no reliable "wait until window is fully hidden" event in WPF.

3. **`AllowsTransparency=True` requires `WindowStyle=None`.** Mandatory combo for transparent overlays. Forgetting this gives a confusing runtime error.

4. **Layer ordering in overlay matters for hit-testing.** The transparent `Canvas` for receiving mouse input must be the topmost layer. All visual layers (mask, selection rectangle, hint text) need `IsHitTestVisible="False"` so they don't block mouse events.

5. **`RegisterHotKey` `MOD_NOREPEAT` flag (`0x4000`)** is essential. Without it, holding the hotkey triggers continuous events.

6. **Auto-start path issue in dev:** `Process.GetCurrentProcess().MainModule.FileName` returns the Debug build path during `dotnet run`. Registry entry will point to `bin\Debug\...` until you publish a Release build and run it once. Acceptable for dev, but plan for Release publish before relying on auto-start.

7. **DPI scaling:** `PerMonitorV2` mode set via `<ApplicationHighDpiMode>` in csproj (not manifest). With this, WPF DIPs map 1:1 to physical pixels for capture math.

---

## Build & Run

```powershell
# From project root
dotnet build
dotnet run

# Single-file Release publish (for "real" deployment)
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

Output binary path (Release):
`bin\Release\net8.0-windows\win-x64\publish\ScreenTranslator.exe`

---

## API Cost Reference (April 2026)

| Model | Input ($/MTok) | Output ($/MTok) | Notes |
|---|---|---|---|
| Sonnet 4.6 | $3.00 | $15.00 | Recommended default |
| Haiku 4.5 | $1.00 | $5.00 | Faster, cheaper, simpler UIs |
| Opus 4.7 | $5.00 | $25.00 | 3× vision resolution, dense text |

Per-snip cost estimate (Sonnet 4.6): ~1,000–1,500 input tokens for an 800×400px image, ~200–500 output tokens → roughly $0.005–$0.01 per translation. Personal usage budget likely <$5/month.

Once M3 is unblocked, M4 (Claude API integration) is the next milestone and is mostly self-contained — can be developed and tested in a console harness before wiring back into the snip flow.
