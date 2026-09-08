## 1. Responsive dialog width

- [x] 1.1 In `CreateKeyDialog.cs`, resolve `IApplication` via
      `Services.Root.GetRequiredService<IApplication>()` (mirroring `ValueDetailDialog`'s
      constructor) and set the Dialog's `Width` to
      `Dim.Func(_ => Math.Min(PreferredDialogWidth, app.Screen.Width - TerminalWidthMargin))`,
      with `PreferredDialogWidth = 132` and `TerminalWidthMargin = 4` as private consts.
- [x] 1.2 Change `WrapField`'s hardcoded `Width = 43` to `Width = Dim.Fill()` so both the Name
      and Value fields track the Dialog's new responsive width.

## 2. Responsive Value field height

- [x] 2.1 Add `MinValueHeight = 10` and `MaxValueHeight = 30` private consts and a
      `ReservedChromeRows = 14` private const (or equivalent derived from the existing fixed Y
      offsets already in the constructor).
- [x] 2.2 Compute the Value field's height once in the constructor as
      `Math.Clamp(app.Screen.Height - ReservedChromeRows, MinValueHeight, MaxValueHeight)` and
      pass it to `WrapField` in place of the current hardcoded `10`.
- [x] 2.3 Confirm the Name field's `WrapField` call keeps its fixed height (`3`), unaffected by
      the new computed Value height.

## 3. Verification

- [x] 3.1 Build (`dotnet build src/lazynats.sln`) and confirm no warnings/errors from the new
      `Dim.Func`/`Math.Clamp` usage.
- [x] 3.2 Via tmux against a running instance (`dotnet run --project src/lazynats`) with a NATS
      server at `nats://localhost:4222`: open New Key (`N`) and Edit Key (`E`) at a few terminal
      sizes (small/near-floor and large) and confirm via `tmux capture-pane` that the dialog's
      Name/Value field widths and the Value field's height visibly change with terminal size,
      the dialog stays fully on-screen at the small size, and the dialog never shrinks the Value
      field below its previous 10-row size.
- [x] 3.3 Confirm Create/Edit/Cancel behavior, field validation, and the printable-text edit
      guard are all unaffected (existing `nats-kv` "Create Key"/"Edit Key"/"Edit Key
      Printable-Text Guard" scenarios still hold).
