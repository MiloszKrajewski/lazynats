## 1. Tab switch shortcuts

- [x] 1.1 In `MainWindow.cs`, change `subscribeTabShortcut`'s key from `Key.B.WithAlt` to
      `Key.D1.WithAlt` (Alt+1) and `publishTabShortcut`'s key from `Key.P.WithAlt` to
      `Key.D2.WithAlt` (Alt+2).

## 2. Tab titles

- [x] 2.1 Update `subscribeTab`'s `Title` from `" Subscribe "` to `" 1:Subscribe "`.
- [x] 2.2 Update `publishTab`'s `Title` from `" Publish "` to `" 2:Publish "`.

## 3. Documentation

- [x] 3.1 Update `doc/UI.md`'s tab shortcut table: replace the Alt+letter column with Alt+digit
      (Alt+1..Alt+6 for the six listed tabs, in table order) and replace each `Title` column entry
      with its `N:Title` form (e.g. `1:Subscribe`, `2:Publish`, `3:Streams`, ...).
- [x] 3.2 Remove or rewrite the now-inapplicable note "Subscribe uses Alt+B rather than Alt+S so
      it doesn't collide with Streams" — the Alt+digit scheme has no letter collisions to explain.

## 4. Verification

- [x] 4.1 Run the app (`dotnet run --project src/lazynats`) and confirm Alt+1 and Alt+2 switch
      tabs from anywhere, including with focus inside the other tab's content, and that the tab
      titles read `1:Subscribe` / `2:Publish`.
- [x] 4.2 Confirm Alt+S inside the Publish tab still activates the Send button (`_Send`) and no
      longer competes with any tab-switch binding.
