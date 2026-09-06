## 1. Dialog padding fix

- [x] 1.1 In `Streams/CreateStreamDialog.cs`, change `Padding.Thickness = new Thickness(1, 0, 1, 0);`
      to `new Thickness(1, 1, 1, 0)`.
- [x] 1.2 In `Streams/CreateConsumerDialog.cs`, change `Padding.Thickness = new Thickness(1, 0, 1, 0);`
      to `new Thickness(1, 1, 1, 0)`.
- [x] 1.3 In `KVStore/CreateBucketDialog.cs`, change `Padding.Thickness = new Thickness(1, 0, 1, 0);`
      to `new Thickness(1, 1, 1, 0)`.

## 2. Documentation

- [x] 2.1 Add the new dialog-spacing rule to CLAUDE.md's "UI conventions" section: a `Dialog<T>`
      subclass with a button row gives its content area one blank row above the first
      field/control, matching the blank row already above the button row (single-field,
      button-less dialogs like `PatternDialog`/`HeaderDialog` are exempt and stay compact).

## 3. Verification

- [x] 3.1 Launch the app via tmux (per CLAUDE.md's testing guidance) and open each of "New
      Stream", "New Consumer", "New Bucket" — confirm one blank row now appears above the "Name"
      label and the top/bottom spacing reads as symmetric.
- [x] 3.2 Confirm `PatternDialog`/`HeaderDialog` (e.g. "New Header", "New Subscription") are
      visually unchanged.
- [x] 3.3 `dotnet build src/lazynats.sln` succeeds with no new warnings.
