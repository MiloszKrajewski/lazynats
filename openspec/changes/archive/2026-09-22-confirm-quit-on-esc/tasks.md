## 1. Verify the interception point

- [x] 1.1 Check how Terminal.Gui 2.4.17 routes `Application.QuitKey` (context7 / XML docs / ILSpy): does a top-level Esc reach `MainWindow`'s own `Command.Quit` binding, or is it handled at the Application level first?
- [x] 1.2 Pick the intercept per design Decision 1: `KeyDown` on the quit key (Application-level binding, no `MainWindow` command to override)

## 2. Implement

- [x] 2.1 In `MainWindow`'s `KeyDown` loop, mark the key handled when it matches `Application.GetDefaultKeys(Command.Quit)` (no dialog)
- [x] 2.2 Leave the Alt+Q `ShortcutHint` calling `App!.RequestStop()` directly
- [x] 2.3 `dotnet build src/lazynats.sln` succeeds with no new warnings

## 3. Verify via tmux against a real NATS server

- [x] 3.1 Top-level Esc (Subscribe tab list, repeated; Live Feed) does nothing; app keeps running
- [x] 3.2 Esc on a drill-down child level (Streams -> consumers) still ascends
- [x] 3.3 Esc inside About closes only that dialog
- [x] 3.4 Alt+Q exits immediately
