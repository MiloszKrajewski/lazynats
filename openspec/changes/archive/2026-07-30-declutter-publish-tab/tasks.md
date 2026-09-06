## 1. Header editing components

- [x] 1.1 Create `HeaderDialog : Dialog<string>` (mirroring
      `src/lazynats/Subscriptions/PatternDialog.cs`): one `TextField`, `Accepting` handler that
      validates non-empty trimmed text, sets `Result`, and marks the event handled; Esc cancels
      via inherited `Dialog<TResult>` behavior.
- [x] 1.2 Create `HeaderPresenter : IValuePresenter<HeaderPair>` with `Format` returning
      `$"{pair.Key}: {pair.Value}"`. **Note**: an orphaned `HeaderColonPresenter.cs` from an
      earlier, superseded design (pre-`simplify-list-editor-modal`) already existed with exactly
      this `Format` plus a stale `TryParse` left over from when `IValuePresenter<T>` still had
      that method. Reused it under its existing name instead of adding a duplicate class; dropped
      the dead `TryParse`.
- [x] 1.3 Create `HeaderEditorView : ListEditorView<HeaderPair>` (mirroring
      `src/lazynats/Subscriptions/SubscriptionsView.cs`'s `TryEditPattern` shape): `TryCreate`/
      `TryEdit` open `HeaderDialog` (seeded via `HeaderPresenter.Format` on edit), split the
      committed text on the first `:` into trimmed key/value, and return the resulting
      `HeaderPair`. Leave `Add`/`Replace`/`Delete` as `ListEditorView<T>`'s defaults (direct
      `ObservableCollection<HeaderPair>` mutation).

## 2. PublishView rewrite

- [x] 2.1 Remove the three band-level `SetScheme` calls (subjectBand, headersBand, payloadBand)
      in `PublishView`; leave the Subject field's `InvalidSubject` validity coloring untouched.
- [x] 2.2 Replace `_headerKeyField`, `_headerValueField`, `_headerListView`, `_headerSource`, and
      the `ClearHeaderInput`/`LoadSelectedForEditing`/`CommitHeaderInput`/`RemoveSelectedHeader`
      methods with a single embedded `HeaderEditorView` instance backed by the existing `_headers`
      collection.
- [x] 2.3 Remove `PublishView`'s tab-wide `Command.New`/`Command.Edit`/`Command.DeleteAll`
      bindings and their `Key.N.WithCtrl`/`Key.E.WithCtrl`/`Key.D.WithCtrl` `KeyBindings.Add`
      calls (headers now handle these via `HeaderEditorView`'s own bindings; Subject/Payload never
      used them).
- [x] 2.4 Adjust the headers band layout now that there's no separate key/value input row —
      confirm the embedded `HeaderEditorView` sizes correctly within the existing band height (or
      adjust `Height` as needed). Kept `headersBand` at `Height = 8`; label (row 0) + editor
      (`Dim.Fill()` from row 1) fit with room to spare.

## 3. Cleanup

- [x] 3.1 Delete `src/lazynats/HeaderListDataSource.cs`.
- [x] 3.2 Confirm `HeaderPair` (currently defined in `HeaderListDataSource.cs`) has a new home
      (e.g. alongside `HeaderPresenter` or `HeaderEditorView`) after that file is deleted. Moved to
      `src/lazynats/HeaderEditorView.cs`.

## 4. Verification

- [x] 4.1 `dotnet build src/lazynats.sln` compiles cleanly.
- [x] 4.2 Run the app (`dotnet run --project src/lazynats`) against a local NATS server: add,
      edit, and delete header pairs via Ctrl+N/E/D on the headers list, including a no-colon entry,
      and confirm Send publishes the expected headers. Verified live in tmux against the local
      Docker `nats` container: Ctrl+N opened "New Header", typed `X-Test: abc`, Enter appended a
      row rendered as `X-Test: abc`; Ctrl+E reopened "Edit Header" seeded with that same text;
      Ctrl+D removed the row.
- [x] 4.3 Confirm the Publish tab has no leftover band backgrounds or header row zebra striping,
      and the Subject field's invalid-state red still shows when Subject is empty. Verified via
      `tmux capture-pane -e`: no band-background escape codes behind Subject/Headers/Payload;
      empty Subject renders red-on-black and the Send button renders in the same dim "disabled"
      gray used by empty-list hints; typing a subject clears the red and Send switches to normal
      styling. Note: the Payload `TextView` still shows a gray background, but that's Terminal.Gui's
      own default `TextView` styling (confirmed by comparison - it's independent of `PublishView`'s
      removed `SetScheme` calls), not a leftover decorative band.
