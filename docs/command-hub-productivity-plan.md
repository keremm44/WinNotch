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

- Keep one lightweight draft with explicit clear/copy controls.
- Persist atomically under `%LOCALAPPDATA%/WinNotch` only when the text changes.
- Apply a strict size cap and never create a background worker or autosave timer.

Acceptance: draft survives Command Hub close/application restart, clear is explicit,
and empty notes leave no retained file.

## 3. Timer

- Presets plus a custom minute value; start, pause/resume and cancel.
- Keep a single dispatcher timer only while a countdown is active.
- Show the remaining time in Command Hub and a lightweight notch completion state.

Acceptance: only one countdown exists, pause/resume is accurate, completion is visible
without stealing focus, and cancellation releases the timer.

## 4. QR generation

- Generate from typed text or current clipboard content only on demand.
- Render a bounded QR bitmap and provide copy/save controls.
- Load/retain no bitmap while the QR panel is closed.

Acceptance: Unicode and URL values scan correctly, oversized input is rejected, and
all bitmap resources are released when Command Hub closes.

## Cross-cutting constraints

- Preserve hover-only media expansion, File Shelf drag behavior, contextual actions,
  settings access, Auto fullscreen behavior and context-menu dismissal.
- Keep controls keyboard reachable and use existing semantic theme resources.
- Add pure unit tests for parsers/state transitions; perform final WPF runtime checks
  on Windows after each stage before enabling the next tool.
