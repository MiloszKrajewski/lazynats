using System.Drawing;
using System.Text;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Attribute = Terminal.Gui.Drawing.Attribute;
using Color = Terminal.Gui.Drawing.Color;

namespace lazynats.Components;

// Wraps a single edit-capable SubView (TextField, TextView, ListEditorView<T>, ...) in a thin
// padded frame - breathing room that reads as "this is an input" without the visual weight of a
// full bordered box (see doc/ui-design.md, doc/glyphs.md for the glyph/position naming used here).
// Never takes focus itself: CanFocus=true but with no focusable content of its own, so Tab/click
// focus resolves straight through to the wrapped SubView via normal focus drill-down, and the
// frame just listens to that SubView's own HasFocusChanged to know when to recolor.
internal sealed class EditFrame: View
{
    // See doc/glyphs.md for the full glyph/position naming reference.
    private static readonly Rune Hbd = new('▄'); // TOP row fill
    private static readonly Rune Hbu = new('▀'); // BOT row fill
    private static readonly Rune Qlr = new('▗'); // TL corner
    private static readonly Rune Qur = new('▝'); // BL corner
    private static readonly Rune Hbr = new('▐'); // LM (left margin, outer tick)
    private static readonly Rune Ful = new('█'); // LP (left padding) and RM (right margin)

    // Default EAC (edge accent color) when the caller hasn't set one explicitly.
    private static readonly Color DefaultEdgeAccent = new(ColorName16.White);

    private readonly View _child;

    private Color? _outerBackground;
    private Color _innerBackgroundNormal;
    private Color _innerBackgroundFocused;
    private Color? _innerBackgroundOverride;
    private Color? _edgeAccent;

    public EditFrame(View child)
    {
        // Terminal.Gui requires CanFocus=true on every ancestor for a descendant to be focusable
        // at all - CanFocus=false here would block the child from ever receiving focus, not make
        // the frame transparent to it. Pass-through instead relies on normal focus drill-down (as
        // already used by PublishTab's own subjectBand/headersBand/payloadBand wrappers): with no
        // focusable content of its own, EditFrame is never where focus actually lands.
        CanFocus = true;
        _child = child;

        // LM + LP on the left, RM on the right, TOP/BOT rows top and bottom.
        child.X = 2;
        child.Y = 1;
        child.Width = Dim.Fill(1);
        child.Height = Dim.Fill(1);
        Add(child);

        child.HasFocusChanged += (_, _) => SetNeedsDraw();
    }

    // Null means "inherit from SuperView" - since EditFrame itself never sets its own scheme,
    // GetAttributeForRole already resolves to whatever the SuperView's ambient background is.
    public Color? OuterBackground
    {
        get => _outerBackground;
        set { _outerBackground = value; SetNeedsDraw(); }
    }

    public Color InnerBackgroundNormal
    {
        get => _innerBackgroundNormal;
        set { _innerBackgroundNormal = value; SetNeedsDraw(); }
    }

    public Color InnerBackgroundFocused
    {
        get => _innerBackgroundFocused;
        set { _innerBackgroundFocused = value; SetNeedsDraw(); }
    }

    // A generic escape hatch, not an "invalid-state color": EditFrame has no concept of validity,
    // disabled-ness, or any other specific state - it just prefers this over Normal/Focused
    // whenever the caller sets it, and reverts to focus-driven coloring when cleared.
    public Color? InnerBackgroundOverride
    {
        get => _innerBackgroundOverride;
        set { _innerBackgroundOverride = value; SetNeedsDraw(); }
    }

    // Null means "use the default white accent" - the left edge's outermost column (TL/LM/BL) is
    // inked in this color rather than IBC, to read as a distinct accent line rather than blending
    // into the field's own color.
    public Color? EdgeAccent
    {
        get => _edgeAccent;
        set { _edgeAccent = value; SetNeedsDraw(); }
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        var outer = _outerBackground ?? GetAttributeForRole(VisualRole.Normal).Background;
        var inner = _innerBackgroundOverride ?? (_child.HasFocus ? _innerBackgroundFocused : _innerBackgroundNormal);
        var innerAttribute = new Attribute(inner, outer);
        var accentAttribute = new Attribute(_edgeAccent ?? DefaultEdgeAccent, outer);

        var width = Viewport.Width;
        var bottom = Viewport.Height - 1;
        var contentHeight = bottom - 1;

        // TOP row: TL corner (accent) then HBD fill the rest of the way, including the top-right
        // corner cell - there is no distinct top-right glyph.
        SetAttribute(innerAttribute);
        FillRect(new Rectangle(1, 0, width - 1, 1), Hbd);
        SetAttribute(accentAttribute);
        Move(0, 0);
        AddRune(Qlr);

        // BOT row: mirrors TOP.
        SetAttribute(innerAttribute);
        FillRect(new Rectangle(1, bottom, width - 1, 1), Hbu);
        SetAttribute(accentAttribute);
        Move(0, bottom);
        AddRune(Qur);

        if (contentHeight > 0) {
            // LM: left margin, outer tick column, accent-colored.
            SetAttribute(accentAttribute);
            FillRect(new Rectangle(0, 1, 1, contentHeight), Hbr);

            // LP through RM (columns 1..width-1): solid inner-colored fill, covering the child's
            // own content area as well as its left/right padding. The child (TextField, TextView,
            // ...) only paints under its actual content/text - e.g. an empty TextField paints
            // nothing at all - so without this base fill, any cell the child doesn't touch falls
            // through to whatever's behind it (typically the root Toplevel's own Normal
            // background) instead of reading as part of the editable region. A solid block glyph
            // (not a space) is used throughout, same as TOP/BOT, because a plain space doesn't
            // reliably carry a custom background through Terminal.Gui's cell buffer.
            SetAttribute(innerAttribute);
            FillRect(new Rectangle(1, 1, width - 1, contentHeight), Ful);
        }

        return true;
    }
}
