using lazynats.Components;
using lazynats.Core.Subjects;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace lazynats.Subscriptions;

internal sealed class SubscribeTab: View, IShortcutSource
{
    private const string ExclusionLabelText = "Excluded Subjects (regex)";

    // Same red-on-EditableBackground look as PatternDialog/HeaderDialog's invalid state; yellow for a
    // valid edit that isn't in effect yet (nats-subscriptions' "Editing and Applying the Exclusion
    // Filter").
    private static readonly Attribute InvalidAttribute = new(ColorName16.Red, Theme.EditableBackground);
    private static readonly Attribute PendingAttribute = new(Theme.PendingEditColor, Theme.EditableBackground);

    private readonly SubscriptionsView _subscriptionsView;
    private readonly SubjectExclusionFilter _exclusion;
    private readonly Label _exclusionLabel;
    private readonly TextField _exclusionField;
    private readonly Button _applyButton;

    public SubscribeTab(SubscriptionRegistry registry, SubjectExclusionFilter exclusion)
    {
        CanFocus = true;
        _exclusion = exclusion;

        _subscriptionsView = new SubscriptionsView(registry) { Background = Theme.EditableBackground };
        var subscriptionsLabel = new Label { Text = "Subscriptions", X = 0, Y = 0 };
        // Fill(4) leaves room for the exclusion label (1 row) + its EditFrame (3 rows) below.
        var subscriptionsFrame = new EditFrame(_subscriptionsView) {
            X = 0, Y = 1, Width = Dim.Fill(), Height = Dim.Fill(4),
            InnerBackgroundNormal = Theme.EditableBackground, InnerBackgroundFocused = Theme.EditableBackground,
        };

        _exclusionLabel = new Label { Text = ExclusionLabelText, X = 0, Y = Pos.AnchorEnd(4) };
        _exclusionField = new TextField { Text = exclusion.Pattern };
        _exclusionField.FixPasteRedraw();
        // Button's own width: "[ Apply ]" is 9 columns, plus a 1-column gap to the frame.
        var exclusionFrame = new EditFrame(_exclusionField) {
            X = 0, Y = Pos.AnchorEnd(3), Width = Dim.Fill(10), Height = 3,
            InnerBackgroundNormal = Theme.EditableBackground, InnerBackgroundFocused = Theme.EditableBackground,
        };
        // No shadow (it would take a second row) and no '_' hotkey in the text - Alt+A is already
        // the global About shortcut.
        _applyButton = new Button {
            Text = "Apply", X = Pos.Right(exclusionFrame) + 1, Y = Pos.AnchorEnd(2), ShadowStyle = ShadowStyles.None,
        };

        _exclusionField.ValueChanged += (_, _) => UpdateExclusionState();
        // Enter applies. Handled by Accepting and always marked e.Handled (same as PatternDialog):
        // unhandled, it would bubble up to TabbedView, whose Command.Accept is its Tab-advance.
        _exclusionField.Accepting += (_, e) => {
            e.Handled = true;
            ApplyExclusion();
        };
        // Esc reverts an unapplied edit; with nothing to revert it's left alone so it keeps
        // whatever meaning it has further up (MainWindow swallows a stray quit-Esc).
        _exclusionField.KeyDown += (_, key) => {
            if (key != Key.Esc || _exclusion.Classify(_exclusionField.Text) == ExclusionEditState.Applied) return;
            key.Handled = true;
            _exclusionField.Text = _exclusion.Pattern;
        };
        _applyButton.Accepting += (_, e) => {
            e.Handled = true;
            ApplyExclusion();
            // Applying makes the button unfocusable - put focus back on the field explicitly
            // rather than leaving it to wherever Terminal.Gui falls back to.
            _exclusionField.SetFocus();
        };

        Add(subscriptionsLabel, subscriptionsFrame, _exclusionLabel, exclusionFrame, _applyButton);
        UpdateExclusionState();
    }

    // No-op unless the field holds a Pending edit - Invalid text can't be applied, Applied text
    // already is.
    private void ApplyExclusion()
    {
        var text = _exclusionField.Text;
        if (_exclusion.Classify(text) != ExclusionEditState.Pending) return;

        _exclusion.TryApply(text);
        UpdateExclusionState();
    }

    // The label's "*" marker and the button's enablement carry the same state as the text color,
    // so it's readable on colorless terminals (and in tmux captures) too.
    private void UpdateExclusionState()
    {
        var state = _exclusion.Classify(_exclusionField.Text);
        // Same explicit Editable override as PatternDialog: new Scheme(Attribute) alone silently
        // drops the background.
        _exclusionField.SetScheme(state switch {
            ExclusionEditState.Invalid => new Scheme(InvalidAttribute) { Editable = InvalidAttribute },
            ExclusionEditState.Pending => new Scheme(PendingAttribute) { Editable = PendingAttribute },
            _ => null,
        });
        // CanFocus too, not just Enabled: a disabled Button is still a Tab stop in Terminal.Gui v2
        // (confirmed in tmux - Tab from the field landed on it), so Tab would dead-end on it.
        _applyButton.Enabled = _applyButton.CanFocus = state == ExclusionEditState.Pending;
        _exclusionLabel.Text = state == ExclusionEditState.Applied ? ExclusionLabelText : $"{ExclusionLabelText} *";
    }

    // No KeyBindings/AddCommand for specific keys here - that would hardcode which keys this tab
    // forwards. Instead this fires once Terminal.Gui has already tried the focused view (and its own
    // ancestors) and found no handler, at which point it's this tab's turn; whatever key
    // _subscriptionsView's own TabOperations happens to expose (New/Edit/Delete today) is what gets
    // dispatched, so the list is free to add or drop an operation without this tab needing to know
    // about it in advance. Gated on the list's focus (as is Shortcuts below): while the exclusion
    // field or Apply button is focused, the list's operations neither apply nor get advertised.
    protected override bool OnKeyDownNotHandled(Key key)
    {
        if (_subscriptionsView.HasFocus && _subscriptionsView.TabOperations.FirstOrDefault(h => h.Key == key) is { Action: { } action }) {
            action();
            return true;
        }

        return base.OnKeyDownNotHandled(key);
    }

    public IEnumerable<ShortcutHint> Shortcuts => _subscriptionsView.HasFocus ? _subscriptionsView.TabOperations : [];
}
