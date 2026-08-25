# Screen Translator

Windows tray tool built around the Google Gemini API. Three AI-assisted features share one API
client: snip-and-translate from the screen, Vietnamese-to-English daily report writing, and an
English writing assistant that learns which mistakes the user repeats.

---

## Project Purpose

Replace the manual workflow of "screenshot → paste into Zalo/browser → translate" with a single
hotkey, then extend the same API client to the two writing tasks that come up daily at work:

1. User presses `Ctrl+Shift+D` anywhere in Windows
2. A snipping overlay appears (Snipping Tool style)
3. User drags to select a region of the screen
4. The selected region is sent to Gemini for translation
5. Result appears in the chat window and is auto-copied to the clipboard

**Primary use case:** translating foreign-language UI of factory machine-operating software
(Chinese, Japanese, Korean) into Vietnamese for factory workers with limited foreign-language
proficiency.

**Deployment scope:** personal tool for the developer (Bryan, RFID supervisor). Not multi-user, not
deployed to the factory floor.

---

## Tech Stack

- **Language / Runtime:** C# / .NET 8 (`net8.0-windows`)
- **UI Framework:** WPF (primary) + Windows Forms (for `NotifyIcon` only)
- **AI Provider:** Google Gemini API
  - Endpoint: `https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key=...`
  - Default model: `gemini-2.5-flash` (`AppSettings.Model`)
  - Auth: API key in the query string, stored via DPAPI
  - `Settings → List Models` calls `/v1beta/models` to discover what the key can actually reach
- **Persistence:** SQLite + Dapper (`history.db`, `report.db`) plus two Markdown logs for the
  writing assistant
- **API Key Storage:** Windows DPAPI (`ProtectedData.Protect`, `CurrentUser` scope)

### Why this stack

- C# WPF over Python: native global hotkey via `RegisterHotKey`, smooth transparent overlays via GPU
  compositing, single-file `.exe` deployment with no runtime dependencies.
- Vision model over an OCR-first approach: it handles complex UI layouts, weird fonts, and
  contextual translation (e.g. "Start" on a CNC machine vs. "Start" in an office app) without
  per-language tuning.

---

## Project Structure

```
ScreenTranslator/
├── App.xaml(.cs)               # Tray icon, hotkey registration, snip → translate flow
├── HotkeyManager.cs            # namespace ScreenTranslator.Hotkey (file sits at the root)
├── ScreenTranslator.csproj     # net8.0-windows, UseWPF + UseWindowsForms, embeds Prompts/*.md
│
├── Prompts/                    # Prompt templates, embedded as resources
│   ├── ImageTranslation.md         # {{TARGET_LANGUAGE}}
│   ├── ReportStyleTranslation.md   # {{GLOSSARY}} {{STYLE_EXAMPLES}} {{VIETNAMESE_INPUT}}
│   ├── WritingAssistant.md         # {{USER_PROFILE}} {{RECIPIENT_CONTEXT}} {{RECURRING_MISTAKES}}
│   │                               # {{PATTERN_CATALOG}} {{USER_TEXT}}
│   └── UserProfile.md              # Who the writer is, their weak points, their voice
│
├── Snip/
│   └── SnipOverlayWindow.xaml(.cs) # Fullscreen transparent overlay, region selection
│
├── Translation/
│   ├── TranslationService.cs   # All three Gemini calls + WritingAudience + result models
│   └── PromptLibrary.cs        # Loads embedded templates, fills {{PLACEHOLDER}} slots
│
├── UI/
│   ├── ResultChatWindow.xaml(.cs)        # Chat-style translation history window
│   ├── WritingAssistantWindow.xaml(.cs)  # Writing assistant chat + recipient picker
│   ├── TranslationMessage.cs
│   └── IconGenerator.cs
│
├── Writing/
│   ├── PatternCatalog.cs       # Closed mistake taxonomy + alias mapping + group resolution
│   └── WritingHistoryStore.cs  # Markdown chat/mistake logs, repeat detection
│
├── DailyReport/                # Daily report window, SQLite repo, Excel export, style import
│   └── ...
│
├── Settings/
│   ├── SettingsWindow.xaml(.cs)
│   ├── AppSettings.cs          # API key (DPAPI), target language, model, hotkey, report options
│   └── SettingsStore.cs        # JSON serialization to %LocalAppData%
│
└── History/
    ├── HistoryRepository.cs    # Dapper-based SQLite access
    └── HistoryEntry.cs
```

Data files in `%LocalAppData%\ScreenTranslator\`:

| File | Written by |
|---|---|
| `history.db` | snip translations |
| `report.db` | daily report entries, style examples, glossary, templates |
| `settings.json` | settings (API key DPAPI-encrypted) |
| `chat_history.md` | writing assistant: original + all three rewrites |
| `mistake_history.md` | writing assistant: one block per mistake, used for repeat counting |

---

## The three AI features

All three go through `TranslationService` and post a single user-role `parts[].text` — there is no
system instruction, and no conversation state carries between calls.

### 1. Snip translation — `TranslateImageAsync`

`Prompts/ImageTranslation.md` plus `inline_data` with the PNG as base64. Terse and single-purpose:
identify the visible text, translate to the target language, output only the translation, and keep
the visual grouping of separate UI elements as line breaks. Result is returned verbatim.

### 2. Daily report translation — `TranslateTextStyledAsync`

`Prompts/ReportStyleTranslation.md`, assembled from:

- **Glossary** — terms kept verbatim, from the `glossary` table (seeded by `ReportDefaults`).
- **Style examples** — 25 English sentences drawn at random from the user's own past reports
  (`SELECT EnText FROM style_example ORDER BY RANDOM() LIMIT 25`). Few-shot by demonstration rather
  than by description, so the model copies a real voice instead of an adjective.
- The prompt ends on a dangling `ENGLISH:` label to discourage a preamble, and `CleanResult` strips
  the label, bullets and wrapping quotes when the model adds them anyway.

### 3. Writing assistant — `ImproveWritingAsync`

The only call that requests structured output (`generationConfig.responseMimeType =
"application/json"`, with `ExtractJson` as a fallback for fenced replies). The prompt carries four
context blocks the other two features do not have:

- **`UserProfile.md`** — who the writer is, the seven mistakes they actually repeat, and explicit
  do-not-inflate rules for their voice (including a ~30% length ceiling on rewrites).
- **`RECIPIENT_CONTEXT`** — from the `WritingAudience` picked in the UI (colleague / manager /
  SG report), which sets how formal the rewrite may get.
- **`RECURRING_MISTAKES`** — the top patterns already recorded in `mistake_history.md`, so the
  assistant can name a repeat instead of silently fixing it again.
- **`PATTERN_CATALOG`** — the closed list of mistake ids, see below.

It returns three rewrites: `minimal` (their own sentences, errors fixed only — the version meant
for learning), `casual`, and `professional`.

---

## The mistake taxonomy and why it is closed

`Writing/PatternCatalog.cs` holds a fixed list of ~20 snake_case ids. The model must pick one; it
may not invent a name. `PatternCatalog.GroupOf()` then assigns the severity group, so the group is
never the model's decision either.

**This exists because the open version did not work.** The old prompt asked for "a consistent,
reusable snake_case value" and let the model choose. Sending the *same paragraph* twice produced
two disjoint sets of ids — `missing_definite_article` vs `article_omission_with_countable_noun`,
`adjective_for_adverb` (group 2) vs `adverb_form_choice` (group 3), and so on. Every recorded
pattern therefore had a count of exactly 1, and the `totalOccurrences >= 3` reminder in
`WritingHistoryStore` could never fire. A stateless model cannot be consistent with names it has
never seen.

`PatternCatalog.Normalize()` also maps the historical free-form ids onto the closed list, so logs
written by earlier versions keep counting. On the existing history that collapses 25 distinct ids
to 15 real patterns with nothing falling through to `other`.

---

## Prompt editing

Prompts are Markdown files under `Prompts/`, embedded via `<EmbeddedResource Include="Prompts\*.md" />`
and read through `PromptLibrary`:

- `PromptLibrary.Load(name)` — reads `ScreenTranslator.Prompts.{name}.md`, cached after first use.
- `PromptLibrary.Render(name, values)` — substitutes every `{{KEY}}`, then collapses blank runs.
- `PromptLibrary.Section(heading, body)` — a titled block, or an empty string when the body is
  empty, so optional sections leave no orphan heading behind.

Editing a prompt means editing Markdown and rebuilding — no C# changes. When adding a placeholder,
add the matching key at the call site in `TranslationService`; a missing key leaves a literal
`{{KEY}}` in the prompt and there is no compile-time check for that.

---

## Coding Conventions

### WPF + WinForms namespace conflicts

This project mixes WPF (`System.Windows.*`) and Windows Forms (`System.Windows.Forms.*`) because
`NotifyIcon` is only available in WinForms. Many types collide between the two namespaces:

- `Application` — aliased: `using Application = System.Windows.Application;`
- `MessageBox`, `Clipboard`, `MouseEventArgs`, `KeyEventArgs`, `Point`, `HorizontalAlignment`,
  `VerticalAlignment`, `PixelFormat` — **always use full namespace** at point of use:

```csharp
System.Windows.MessageBox.Show("...");
System.Drawing.Imaging.PixelFormat.Format32bppArgb
System.Windows.HorizontalAlignment.Left
```

**Rule:** prefer full namespace at usage site over file-level `using` aliases. Verbose but
unambiguous; aliases scattered across files cause confusion.

### DPI awareness

`<ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>` in `.csproj`. Do **not** put DPI
settings in `app.manifest` — WinForms warns and prefers the project property (warning WFAC010).

### Coordinate systems

- Mouse position in overlay: window-relative (`e.GetPosition(RootGrid)`)
- `Graphics.CopyFromScreen`: screen-absolute (physical pixels)
- Conversion: add `SystemInformation.VirtualScreen.Left/Top` offset
- Multi-monitor: `VirtualScreen` may have negative coordinates (monitors left of primary)

### Async / cancellation

- All API calls accept a `CancellationToken`
- Re-translation must cancel the previous in-flight request before starting the new one, to avoid
  out-of-order responses

### Language

Code, identifiers, comments and log strings are English. User-facing UI strings and the
`description` / `learningTip` fields returned by the writing assistant are Vietnamese.

---

## Feature Status

| Milestone | State |
|---|---|
| M1 — Tray app shell | ✅ done |
| M2 — Global hotkey (`RegisterHotKey`, `MOD_NOREPEAT`, pause toggle) | ✅ done |
| M3 — Snip overlay across all monitors | ✅ done |
| M4 — Gemini API integration + settings (DPAPI, model discovery) | ✅ done |
| M5 — Result chat window, auto-copy, re-translate | ✅ done |
| M6 — SQLite history + retention cleanup | ✅ done |
| M7 — Daily Report (style few-shot, glossary, templates, Excel export) | ✅ done |
| M8 — Writing Assistant (three rewrites, closed taxonomy, repeat reminders) | ✅ done |

### Database schemas

```sql
-- history.db
CREATE TABLE history (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  CreatedAt TEXT NOT NULL,
  ImageBlob BLOB,
  TranslatedText TEXT NOT NULL,
  IsError INTEGER DEFAULT 0
);

-- report.db
CREATE TABLE report_entry (Id INTEGER PRIMARY KEY AUTOINCREMENT, WorkDate TEXT NOT NULL,
                           No INTEGER NOT NULL, ViText TEXT, EnText TEXT, IsFixed INTEGER DEFAULT 0);
CREATE TABLE style_example (Id INTEGER PRIMARY KEY AUTOINCREMENT, EnText TEXT NOT NULL UNIQUE);
CREATE TABLE glossary     (Id INTEGER PRIMARY KEY AUTOINCREMENT, Term TEXT NOT NULL UNIQUE);
CREATE TABLE template     (Id INTEGER PRIMARY KEY AUTOINCREMENT, Title TEXT NOT NULL,
                           ViText TEXT, EnText TEXT);
```

---

## Known Quirks & Lessons Learned

1. **`ShowDialog()` is dangerous in tray-only apps.** Even with `ShutdownMode=OnExplicitShutdown`,
   `ShowDialog` from a window with no proper parent can trigger app shutdown when it closes. Use
   `Show()` + `Closed`, and set `MainWindow = null` afterwards so WPF drops the reference.

2. **`Graphics.CopyFromScreen` captures the overlay too** if you don't hide it first. Needs
   `Hide()` + `Application.DoEvents()` + ~50ms `Thread.Sleep` to let the compositor refresh. There
   is no reliable "window is fully hidden" event in WPF.

3. **`AllowsTransparency=True` requires `WindowStyle=None`.** Mandatory combo for transparent
   overlays. Forgetting it gives a confusing runtime error.

4. **Layer ordering in the overlay matters for hit-testing.** The transparent `Canvas` receiving
   mouse input must be topmost; every visual layer needs `IsHitTestVisible="False"`.

5. **`RegisterHotKey` `MOD_NOREPEAT` (`0x4000`)** is essential, or holding the hotkey fires
   continuously.

6. **Auto-start path issue in dev:** `Process.GetCurrentProcess().MainModule.FileName` returns the
   Debug path during `dotnet run`, so the registry entry points at `bin\Debug\...` until a Release
   build is published and run once.

7. **DPI scaling:** `PerMonitorV2` set via `<ApplicationHighDpiMode>` in csproj (not manifest).
   WPF DIPs then map 1:1 to physical pixels for capture math.

8. **Multiline regex over CRLF files.** `mistake_history.md` is written with `AppendLine`, so lines
   end `\r\n`. In `RegexOptions.Multiline`, `$` matches only before `\n`, so a pattern ending
   `[^\r\n]+$` matches nothing at all — the character class stops before the `\r` that `$` will not
   step over. Always allow for it: `^- Pattern: (?<id>.+?)\s*$`. This silently returns zero matches
   rather than throwing, and the build stays green.

9. **The model does not reliably honour "output only X".** Every call site has a cleanup pass —
   `CleanResult` for the report translator, `ExtractJson` for the writing assistant. Keep them.

10. **A locked `.exe` fails the build, not the code.** If `dotnet build` reports MSB3021/MSB3027 on
    `ScreenTranslator.exe`, the tray app is still running — exit it from the tray first.

---

## Build & Run

```powershell
# From project root
dotnet build
dotnet run

# Single-file Release publish
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

Output binary path (Release):
`bin\Release\net8.0-windows\win-x64\publish\ScreenTranslator.exe`

There is no test project. The verification signal for a change is `dotnet build`; anything touching
prompt content or model behaviour has to be confirmed by running the app and sending a real
message.

---

## API Cost

Billed by Google under the Gemini API pricing for whichever model `AppSettings.Model` names —
check the current rates rather than trusting a number written here. A snip is roughly 1,000–1,500
input tokens for an 800×400px image plus a few hundred output tokens; the writing assistant sends a
larger prompt because the profile, the pattern catalog and the recurring-mistake block travel with
every request. Personal usage stays small either way.
