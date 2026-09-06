using System.Collections.ObjectModel;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace lazynats.Components;

// Lists every shortcut the focused view (and its ancestors) currently advertises via
// IShortcutSource, alphabetically by name - deliberately excludes MainWindow's hardcoded
// top-level shortcuts (Alt-1..4, Alt-P, Alt-Q, this dialog's own trigger key), since those are
// already permanently visible in the status bar and would just be redundant here; see the
// caller in MainWindow. No buttons: Enter on the highlighted row sets Result and closes, Esc
// cancels via Dialog<T>'s own inherited behavior (Result stays null), same compact "commits on
// Enter" shape as PatternDialog/HeaderDialog. The caller invokes Result?.Action only after this
// dialog has fully closed - see openspec/changes/add-shortcut-picker/design.md's
// "close-then-invoke" decision.
// TResult is ShortcutHint? (not ShortcutHint) because Dialog<T>/Runnable<T>'s cancellation
// convention is "Result stays null" - for a non-nullable value type like ShortcutHint, a
// non-nullable TResult would instead return default(ShortcutHint) on Esc, indistinguishable from
// a real (if degenerate) selection.
internal sealed class ShortcutPickerDialog: Dialog<ShortcutHint?>
{
    public ShortcutPickerDialog(IEnumerable<ShortcutHint> hints)
    {
        Title = " Shortcuts ";
        Padding.Thickness = new Thickness(1, 0, 1, 0);

        var sorted = hints.OrderBy(hint => hint.Text, StringComparer.OrdinalIgnoreCase).ToList();

        // Reachable now that the top-level set is excluded: a view advertising nothing of its own
        // (e.g. the live feed) would otherwise open onto a blank, content-less dialog.
        if (sorted.Count == 0)
        {
            Add(new Label { Text = "No shortcuts for this view", X = 0, Y = 0 });
            return;
        }

        // Key first, then action name - reversed from the old "Action (Key)" order - so the part
        // that answers "which key do I press" reads first and stands out, matching the live
        // feed's pattern of coloring the part of a row that identifies it (see Theme.ShortcutKeyColor).
        // keyColumnWidth is recomputed fresh from this open's own entry set (never hardcoded), so
        // no key chord - however long - is ever truncated.
        var keyTexts = sorted.Select(hint => hint.Key.ToString()).ToList();
        var keyColumnWidth = keyTexts.Max(text => text.Length);
        var rows = sorted.Zip(
            keyTexts, (hint, keyText) => new ColoredRow(
                [
                    new RowSegment(Theme.ShortcutKeyColor, keyText.PadRight(keyColumnWidth)),
                    new RowSegment(null, $"  {hint.Text}"),
                ])).ToList();
        var width = Math.Clamp(rows.Max(row => row.Length) + 4, 30, 60);
        var height = Math.Clamp(rows.Count, 1, 15);

        // TabStop = NoStop, not the default TabStop: per ListView.MoveUp/MoveDown's own docs,
        // Up/Down at the list's edge only wraps around when TabStop is NoStop - otherwise the key
        // is left unhandled and bubbles further up, where some other (invisible, in this
        // buttonless dialog) focusable element picks it up, stealing focus from the list. That
        // manifested as: pressing Up from the first row dims the list's highlight (it's lost
        // focus) and Enter afterwards closes the dialog but invokes nothing (Result never gets
        // set, since the Accepting handler below is on listView, which no longer has focus).
        // There's nothing else in this dialog to Tab to anyway, so opting out of the Tab-stop
        // protocol costs nothing.
        var listView = new ListView { X = 0, Y = 0, Width = width, Height = height, TabStop = TabBehavior.NoStop };
        listView.Source = new ColoredRowListDataSource(new ObservableCollection<ColoredRow>(rows));
        // A fresh ListView starts with no selection (SelectedItem null) - without this, Enter
        // pressed before ever navigating invokes nothing, per the same reasoning as
        // ListEditorView.EnsureValidSelection/DrillableListView's initial selection setup.
        listView.SelectedItem = 0;
        // Accepting (pre), not Accepted (post): same reasoning as PatternDialog's field-level
        // handler and CreateStreamDialog's Create button. Left unhandled, the same Enter keypress
        // also bubbles up as an unhandled Accept to Dialog<T>'s own default accept handling, which
        // unconditionally closes the dialog with Result cleared to null - clobbering the Result
        // this handler just set, since that default handling runs after this one (confirmed via
        // tmux: Down-navigation correctly moves SelectedItem, but without e.Handled = true here,
        // Enter always closed with Result null regardless of which entry was selected).
        listView.Accepting += (_, e) => {
            e.Handled = true;
            if (listView.SelectedItem is { } index) Result = sorted[index];
            RequestStop();
        };
        Add(listView);
        listView.SetFocus();
    }
}
