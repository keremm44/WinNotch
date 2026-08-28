# Command Hub productivity tools plan

The tools are delivered in four independently testable stages. Each stage keeps its
runtime objects lazy and releases them when Command Hub closes.

## 1. Smart clipboard transformations

- Read text only when the user opens the tool; never poll the clipboard.
- Clean whitespace, Turkish-aware upper/lower/title case, pretty-print JSON, and
  URL encode/decode.
- Show a preview before copying and suppress WinNotch's notification for its own copy.
- Reject empty and unexpectedly large payloads without blocking the UI.

Acceptance: repeated open/transform/copy cycles work, invalid JSON keeps the original
text, and copying does not close Command Hub.

## 2. Temporary note

- Keep one lightweight in-memory draft with explicit clear/copy controls.
- Retain it while Command Hub opens/closes, but erase it when WinNotch exits.
- Apply a strict size cap and never create a file, background worker or autosave timer.

Acceptance: draft survives Command Hub close during the current process, clear is
explicit, and no note content is written to disk.

## 3. Timer

- Presets plus a custom minute value; start, pause/resume and cancel.
- Keep a single dispatcher timer only while a countdown is active.
- Show the remaining time in Command Hub and a lightweight notch completion state.

Acceptance: only one countdown exists, pause/resume is accurate, completion is visible
without stealing focus, and cancellation releases the timer.

## 4. QR generation

- Generate from typed text or privacy-safe current clipboard text only on demand.
- Render PNG through the MIT-licensed QRCoder `PngByteQRCode` path (no System.Drawing).
- Provide image-copy and PNG-save controls with a bounded 1,500 UTF-8 byte payload.
- Expand Command Hub only while the preview is open and release PNG/bitmap references
  when the panel or Command Hub closes.

Acceptance: Unicode and URL values render correctly, excluded/password-manager
clipboard content is never read, oversized input is rejected, save cancellation is
safe, and all bitmap resources are released when Command Hub closes.

## Cross-cutting constraints

- Preserve hover-only media expansion, File Shelf drag behavior, contextual actions,
  settings access, Auto fullscreen behavior and context-menu dismissal.
- Keep controls keyboard reachable and use existing semantic theme resources.
- Add pure unit tests for parsers/state transitions; perform final WPF runtime checks
  on Windows after each stage before enabling the next tool.
