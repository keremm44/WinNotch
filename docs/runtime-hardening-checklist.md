# WinNotch runtime hardening checklist

This checklist is the release gate for `arena/01a04480-winnotch` while feature work is frozen.
Automated checks are necessary but do not replace live Windows interaction checks. Test totals are intentionally not hard-coded here; the current GitHub Actions run is the source of truth.

## VOL-1 — File Shelf + primary interaction

### Automated

- [x] File-drop acceptance policy has explicit rejection reasons.
- [x] Preview drag entry resolves a valid Explorer file drag to `Copy` immediately.
- [x] Internal shelf drag-out is rejected by incoming-drop preview handling.
- [x] Existing file metadata and source-removal behavior are regression tested.
- [x] Drag-out uses a canonical Windows/WPF `FileDropList` payload and keeps the source effect Copy-only.
- [x] Missing source paths and case-insensitive duplicates are filtered before transfer.

### Manual Windows runtime gate — passed on the hardening branch

- [x] Explorer -> notch: `.txt` shows an allowed cursor immediately and drops successfully.
- [x] Explorer -> notch: `.png`, `.pdf`, `.zip` each drop successfully.
- [x] Explorer -> notch: folder drops successfully.
- [x] Explorer -> notch: multiple files are retained without duplicate entries.
- [x] Shelf -> Desktop/Explorer: drag-out produces a normal Windows file drop.
- [x] Shelf `Kopyala` -> Explorer `Ctrl+V`: transfers real file-drop clipboard entries.
- [x] Deleted/moved source: shelf reports the missing source and does not crash.
- [x] Clear shelf -> drop again: state returns to normal and accepts the next drop.

### Diagnostic capture when a drag fails

Use a Debug build and capture `[DragDrop]` lines. Each line records routed event, File Shelf module state, internal drag-out state, FileDrop presence, allowed effects, resolved effect, decision reason, notch state and advertised Windows formats.

If Explorer ever shows `NoDrop` again, use this trace to separate format, effect, state, hit-test and privilege-boundary failures before changing code.

## VOL-2 — Settings + focus

Code audit: the branch keeps one `SettingsWindow`, restores it from minimized state and routes Command Hub settings requests through the same application-level open path.

- [ ] Tray -> Settings opens one window.
- [ ] Command Hub -> Settings opens the same settings surface.
- [ ] Repeated Settings clicks never create duplicate windows.
- [ ] Minimized Settings restores and activates.
- [ ] Twenty open/close cycles show no stale-window behavior.
- [ ] Editor mode temporarily activates WinNotch and restores `WS_EX_NOACTIVATE` afterward.
- [ ] Focus returns to the prior foreground app after editor exit where Windows permits it.

Automated support:

- [x] UI and Tray assemblies are referenced by the Windows test project.
- [x] An STA WPF lifecycle smoke constructs/loads/closes MainWindow and SettingsWindow repeatedly and constructs all primary lazy child views.

## VOL-3 — Clipboard + screenshot

Automated policy coverage:

- [x] Balanced plain text stays silent by design.
- [x] Balanced URL/file path/e-mail/phone/color behavior is regression tested.
- [x] Active plain text can surface subtly.
- [x] WinNotch-originated text/image writes use one-shot suppression armed before the clipboard mutation.
- [x] Attention cooldown/budget timing uses monotonic `TimeProvider` timestamps.

Manual Windows gate:

- [ ] `Win+Shift+S` produces one screenshot notification without stealing focus.
- [ ] Repeated screenshots respect the attention budget without leaving a stuck state.

## VOL-4 — State + media + fullscreen

Automated coverage includes state priority/return rules and fullscreen geometry classification.

- [ ] Idle -> Command Hub -> Idle transition is stable in live use.
- [ ] Shelf and media persistent states are restored after temporary notifications.
- [ ] Timer completion obeys priority rules in live use.
- [ ] Media session changes do not leave stale metadata after source/focus changes.
- [ ] Chrome/Edge F11 and YouTube/VLC fullscreen hide in Auto mode and restore on exit.
- [ ] Normal maximized windows are not misclassified as fullscreen.

## VOL-5 — DPI + multi-monitor + shell geometry

- [x] Native corner radius calculation is regression tested at 96/120/144/192 DPI.
- [x] Runtime size animation no longer writes a second legacy region every frame; render-size changes own physical hit bounds, DPI-aware region and recentering.
- [ ] 100%, 125%, 150% and 200% scaling remain top-centered in live Windows use.
- [ ] Mixed-DPI two-monitor layout remains centered on the selected display.
- [ ] Disconnecting the selected monitor falls back safely.
- [ ] Resolution/display changes do not leave stale native geometry.

## VOL-6 — appearance + accessibility

- [x] Controlled themes: Obsidian, Aurora, Graphite, Monochrome, Paper and Frost.
- [x] Theme chooser exposes the six presets with visual shell swatches.
- [x] Explicit WinNotch preset owns light/dark notch-border treatment instead of inheriting an unrelated OS theme border.
- [x] Runtime notch and appearance preview share the same shell depth tokens.
- [x] High Contrast removes decorative highlight/shadow layers and keeps system colors authoritative.
- [x] Reduced Motion and Windows animation accessibility preference remain authoritative.
- [ ] Live visual sweep: all six themes across Idle, Clipboard, Screenshot, Shelf, Media and Command Hub.

## VOL-7 — resource + release gate

CI release gates now include:

- [x] Release build and full regression test suite are hard failures.
- [x] Performance smoke uses a 10-second warm-up, three repeated samples and medians.
- [x] Missing executable or early process exit fails the performance step.
- [x] A bounded all-modules runtime soak checks private-memory, handle and thread growth.
- [x] Version metadata is centralized as `0.2.0-rc1` / `0.2.0.0`.
- [x] CI publishes a self-contained single-file `win-x64` RC package and uploads the zip artifact.
- [x] Tray tooltip/menu exposes the running product version.
- [ ] Current branch head completes every Windows CI release gate successfully.
- [ ] Manual runtime gates above are complete before tagging `v0.2.0-rc1`.

## Repository gate

- [ ] Protect `main` and require the Windows CI status before merge. This must be configured in repository settings/rulesets; do not treat a green PR as equivalent to branch protection.
- [ ] After RC validation, merge PR #2 with a controlled squash rather than carrying the full hardening commit history into `main`.
