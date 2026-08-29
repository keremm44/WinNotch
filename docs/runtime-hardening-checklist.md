# WinNotch runtime hardening checklist

This checklist is the release gate for `arena/01a04480-winnotch` while feature work is frozen.
Automated checks are necessary but do not replace live Windows interaction checks.

## VOL-1 — File Shelf + primary interaction

### Automated

- [x] File-drop acceptance policy has explicit rejection reasons.
- [x] Preview drag entry resolves a valid Explorer file drag to `Copy` immediately.
- [x] Internal shelf drag-out is rejected by incoming-drop preview handling.
- [x] Existing file metadata and source-removal behavior are regression tested.
- [x] Drag-out uses a canonical Windows/WPF `FileDropList` payload and keeps the source effect Copy-only.
- [x] Missing source paths and case-insensitive duplicates are filtered before transfer.
- [x] Release build passes after hardening changes with 0 warnings / 0 errors.
- [x] Full unit suite passes after hardening changes: 171 / 171.

### Manual Windows runtime gate

Run a direct Release build, not a debugger-hosted process unless diagnosing a failure.

- [ ] Explorer -> notch: `.txt` shows an allowed cursor immediately and drops successfully.
- [ ] Explorer -> notch: `.png`, `.pdf`, `.zip` each drop successfully.
- [ ] Explorer -> notch: folder drops successfully.
- [ ] Explorer -> notch: multiple files are retained without duplicate entries.
- [ ] Shelf -> Desktop/Explorer: drag-out produces a normal Windows file drop.
- [ ] Shelf `Kopyala` -> Explorer `Ctrl+V`: transfers real file-drop clipboard entries.
- [ ] Deleted/moved source: shelf reports the missing source and does not crash.
- [ ] Clear shelf -> drop again: state returns to normal and accepts the next drop.

### Diagnostic capture when a drag fails

Use a Debug build and capture `[DragDrop]` lines. Each line records:

- routed event (`PreviewDragEnter` / `PreviewDragOver`)
- File Shelf module state
- internal drag-out state
- presence of `FileDrop`
- whether the source allows `Copy`
- source allowed effects
- resolved WinNotch effect
- rejection/acceptance reason
- current notch state
- advertised Windows data formats

If Explorer still shows `NoDrop`, do not guess at the next fix. Use this trace to separate format, effect, state, hit-test and privilege-boundary failures.

## VOL-2 — Settings + focus

Code audit: the branch already keeps one `SettingsWindow`, restores it from minimized state and routes Command Hub settings requests through the same application-level open path. No speculative rewrite is required before live verification.

- [ ] Tray -> Settings opens one window.
- [ ] Command Hub -> Settings opens the same settings surface.
- [ ] Repeated Settings clicks never create duplicate windows.
- [ ] Minimized Settings restores and activates.
- [ ] Twenty open/close cycles show no stale-window behavior.
- [ ] Editor mode temporarily activates WinNotch and restores `WS_EX_NOACTIVATE` afterward.
- [ ] Focus returns to the prior foreground app after editor exit where Windows permits it.

## VOL-3 — Clipboard + screenshot

- [ ] Balanced: plain text stays silent by design.
- [ ] Balanced: URL/file path/e-mail/phone/color follow the documented attention policy.
- [ ] Active: plain text can surface subtly.
- [ ] WinNotch-originated copy actions do not recursively notify.
- [ ] `Win+Shift+S` produces one screenshot notification without stealing focus.
- [ ] Repeated screenshots respect the attention budget without leaving a stuck state.

## VOL-4 — State + media + fullscreen

- [ ] Idle -> Command Hub -> Idle transition is stable.
- [ ] Shelf and media persistent states are restored after temporary notifications.
- [ ] Timer completion obeys priority rules.
- [ ] Media session changes do not leave stale metadata after source/focus changes.
- [ ] Chrome/Edge F11 and YouTube/VLC fullscreen hide in Auto mode and restore on exit.
- [ ] Normal maximized windows are not misclassified as fullscreen.

## VOL-5 — DPI + multi-monitor

- [ ] 100%, 125%, 150% and 200% scaling remain top-centered.
- [ ] Mixed-DPI two-monitor layout remains centered on the selected display.
- [ ] Disconnecting the selected monitor falls back safely.
- [ ] Resolution/display changes do not leave stale native geometry.

## VOL-6 — resource + release gate

Current smoke remains informational; the latest successful run still measures one short post-start sample per configuration. Do not treat those values as release-grade performance evidence yet.

- [ ] Performance smoke uses a warm-up period and repeated samples.
- [ ] Startup/process-exit failures fail CI rather than being hidden by `continue-on-error`.
- [ ] Repeated Command Hub/QR/Settings cycles do not show unbounded handle/thread/private-memory growth.
- [x] Release build has zero warnings and errors.
- [x] Full test suite passes: 171 / 171.
- [ ] Manual runtime gates above are complete before tagging an RC.
