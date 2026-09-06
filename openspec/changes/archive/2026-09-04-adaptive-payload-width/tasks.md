## 1. Clamp extensions

- [x] 1.1 Add `Core/NumericExtensions.cs` with generic `NotLessThan<T>(this T value, T min)` and
      `NotMoreThan<T>(this T value, T max)`, both using `Comparer<T>.Default`.

## 2. Width-aware presentation rendering

- [x] 2.1 Change `PayloadPresentation.Render` to `Render(byte[] data, PayloadType type, int width)`;
      pass `width` through unused for `Json` (superseded by task 5.2: `Text` now uses `width` too).
- [x] 2.2 Change `RenderHex` to accept `width`, compute bytes/row as the largest of
      `8, 16, 24, 32, 48, 64` whose formatted row (`3n - 1` chars) fits `width`, clamped
      `.NotLessThan(8).NotMoreThan(64)`.
- [x] 2.3 Change the `Base64` branch to wrap `Convert.ToBase64String(data)` into lines of
      `width` floored to the nearest multiple of 4, clamped `.NotLessThan(24).NotMoreThan(144)`,
      joined with `\n`.

## 3. Dialog wiring

- [x] 3.1 In `MessageDetailDialog`, compute the payload `EditFrame`'s resolved label width once
      (after initial layout, following `EditFrame`'s own `child.X=2`/`Width=Dim.Fill(1)` math),
      store it, and pass it to both `Render` call sites (constructor and
      `OnPresentationChanged`) - never recomputed afterward, including on terminal resize.

## 4. Verification

- [x] 4.1 Via tmux (see CLAUDE.md), open the Message Detail dialog for a `Hex`-default (binary)
      payload at a wide terminal width and confirm rows use more than 16 bytes/row; narrow the
      terminal, reopen, and confirm rows shrink toward 8.
- [x] 4.2 Via tmux, switch a payload to `Base64` presentation with a payload large enough to have
      previously overflowed the label width, and confirm it now wraps within the visible width
      instead of running off-screen.
- [x] 4.3 Confirm `dotnet build src/lazynats.sln` succeeds with no other callers of the old
      two-argument `PayloadPresentation.Render` left unupdated.

## 5. Post-review fixes (found via manual testing)

- [x] 5.1 Reserve a constant `ScrollbarWidth = 1` unconditionally when computing
      `_payloadLabelWidth` in `MessageDetailDialog`, since the vertical scrollbar (turned on later,
      once the actual rendered line count is known) would otherwise claim a column the earlier
      `Hex`/`Base64` width measurement didn't account for, running content under it.
- [x] 5.2 Change the `Text` branch (`RenderText`) to wrap to `width`, splitting existing lines on
      `\n` first and joining wrapped results back with `\n` - `Text`'s decoded bytes are frequently
      one unbroken line (e.g. minified JSON viewed as `Text`) with nothing else to keep them inside
      the visible width. (Superseded by task 6.2: wrapping is now fixed-character-count, not
      `TextFormatter.WordWrapText`.)
- [x] 5.3 Update `design.md`/specs to reflect both fixes: `Text` now depends on the available
      width (only `Json` doesn't), and the scrollbar-width reservation.
- [x] 5.4 Via tmux, confirm a tall `Hex`/`Base64` payload (enough rows to show the scrollbar) no
      longer runs its rightmost column(s) under the scrollbar, and confirm a long single-line
      `Text` payload now wraps instead of running off-screen.

## 6. Second round of post-review fixes (found via manual testing against real JetStream traffic)

- [x] 6.1 Reserve a second constant, `ScrollbarGap = 1`, alongside `ScrollbarWidth` when computing
      `_payloadLabelWidth` - `ScrollbarWidth` alone stops content overlapping the scrollbar but
      leaves it flush against that column with no breathing room, which reads as crowded.
- [x] 6.2 Replace `RenderText`'s use of `TextFormatter.WordWrapText` with the same fixed-width
      chunking `Hex`/`Base64` already use (factored into a shared `ChunkFixedWidth` helper) -
      `WordWrapText`'s word-boundary/long-word-fallback behavior produced ragged, misleading output
      against real minified-JSON payloads (effectively one giant "word"): a short, mostly-empty
      line followed by a hard split landing mid-word for no reason tied to any actual word
      boundary. Removes `PayloadPresentation`'s last `Terminal.Gui` type reference.
- [x] 6.3 Update `design.md`/specs to reflect both fixes: `Text` wraps at a fixed character count
      (not word boundaries), and the `ScrollbarGap` reservation.
- [x] 6.4 Via tmux, reproduce the original repro (subscribe to `>`, switch to Streams tab to
      generate JetStream traffic, open the last `$JS.EVENT.ADVISORY.API` message) and confirm: Json
      still pretty-prints correctly by default; Text wraps every row to a consistent full width with
      no ragged mid-word artifacts; Base64 has a visible gap before the scrollbar.
