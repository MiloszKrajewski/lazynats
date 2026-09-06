using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace lazynats;

// Single declared place for the app's tunable theme colors (see openspec/changes/add-dark-theme).
// Swap a value here to experiment with alternatives - every dependent (Program.cs's Base/Dialog
// scheme overrides, and the various invalid-input highlight Attributes in PublishDialog/HeaderDialog/
// PatternDialog) reads from here instead of repeating the literal.
internal static class Theme
{
    // Darker than ColorName16.DarkGray (118,118,118) so field text stays legible without the
    // control looking "shiny". On a terminal without truecolor support this falls back to
    // ColorName16.Black, not DarkGray: Color.GetClosestNamedColor16() confirms (32,32,32) is
    // nearer Black than any other 16-color palette entry.
    public static readonly Color EditableBackground = new(32, 32, 32);

    private static readonly Attribute EditableAttribute = new(ColorName16.White, EditableBackground);

    // Dimmed foreground, same EditableBackground - explicit rather than left to derive from
    // Normal. A disabled (Enabled=false) DropDownList's closed-state text is read via
    // VisualRole.Editable, which DropDownList's own ReadOnly redirect resolves back through this
    // Scheme's Normal/Focus rather than Disabled - so Terminal.Gui's normal "Disabled derives
    // from Normal" inheritance never actually kicks in for it, and it fell through to the
    // ambient/inherited scheme's own Disabled (a visibly different gray) instead. Setting it here
    // explicitly closes that gap so a disabled dropdown matches a disabled TextField's black look
    // (TextField's own Editable-role Disabled redirect already resolves correctly via the ambient
    // Dialog scheme - only DropDownList needed this).
    private static readonly Attribute DisabledAttribute = new(ColorName16.DarkGray, EditableBackground);

    // DropDownList redirects its VisualRole.Editable lookups to Normal/Focus in its default
    // (read-only) mode (see openspec/changes/dropdown-visual-consistency/design.md), so it never
    // picks up Program.cs's ApplyColorTheme() Editable attribute the way a TextField does. Setting
    // an explicit Scheme directly on the instance gives its closed, unfocused state the same
    // White-on-EditableBackground look as a TextField.
    //
    // Focus is deliberately left to derive Terminal.Gui's default inverted fg/bg bar, the same
    // mechanism ListEditorView/DrillableListView rely on for a selected list row's highlight (see
    // ListEditorView.SetBackgroundColor). A TextField gets its focus affordance from its blinking
    // cursor and stays background-stable per the color-theme spec; DropDownList has no cursor
    // (ReadOnly), so without this it was indistinguishable focused vs. unfocused while closed.
    public static void ApplyEditableScheme(View view) =>
        view.SetScheme(new Scheme(EditableAttribute) { Disabled = DisabledAttribute });
}
