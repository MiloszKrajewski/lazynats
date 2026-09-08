using System.Drawing;
using System.Text;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;
using Color = Terminal.Gui.Drawing.Color;

namespace lazynats.Components;

// Wraps a single edit-capable SubView (TextField, TextView, ListEditorView<T>, ...) in a thin
// padded frame - breathing room that reads as "this is an input" without the visual weight of a
// full bordered box (see doc/ui-design.md, doc/glyphs.md for the glyph/position naming used here).
// Never takes focus itself: CanFocus=true but with no focusable content of its own, so Tab/click
// focus resolves straight through to the wrapped SubView via normal focus drill-down, and the
// frame just listens to that SubView's own HasFocusChanged to know when to recolor - both the
// inner background (InnerBackgroundNormal/InnerBackgroundFocused) and the left-edge accent
// (EdgeAccent/EdgeAccentFocused) switch based on that focus state.
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
    private Color? _edgeAccentFocused;

    // Builds a read-only, non-focusable frame wrapping a plain multi-line Label - the shape shared
    // by every read-only text section in this codebase (MessageDetailDialog's Subject/Headers,
    // PayloadDetailSection's own label, the KV Value Detail dialog's Details frame). See
    // openspec/changes/kv-value-peek-and-view/design.md Decision 3.
    public static EditFrame CreateReadOnly(string text, int y, int height, out Label view)
    {
        // HotKeySpecifier must be disabled before Text is assigned - Label parses '_' out of Text
        // at assignment time using whatever HotKeySpecifier is current, and Label defaults it to
        // '_' (unlike the plain View base, which defaults to disabled). Displayed content is
        // arbitrary (NATS subjects/JSON/headers/KV values routinely contain '_'), so leaving the
        // default on would silently eat underscores and underline the following character instead
        // of rendering the text verbatim.
        var label = new Label { HotKeySpecifier = (Rune)0xffff, Text = text };
        label.TextFormatter.MultiLine = true;
        label.TextFormatter.WordWrap = false;
        Theme.ApplyEditableScheme(label);

        var frame = new EditFrame(label) {
            X = 0, Y = y, Width = Dim.Fill(), Height = height,
            InnerBackgroundNormal = Theme.EditableBackground, InnerBackgroundFocused = Theme.EditableBackground,
        };
        // Unlike every other EditFrame in this codebase (which wraps a focusable TextField/
        // TextView), this one wraps a plain non-focusable Label - leaving the frame focusable
        // would pull Tab focus (and the owning dialog's own scroll key bindings) onto an empty
        // frame instead of staying on the dialog itself.
        frame.CanFocus = false;

        view = label;
        return frame;
    }

    public EditFrame(View child)
    {
        // Terminal.Gui requires CanFocus=true on every ancestor for a descendant to be focusable
        // at all - CanFocus=false here would block the child from ever receiving focus, not make
        // the frame transparent to it. Pass-through instead relies on normal focus drill-down (as
        // already used by every EditFrame-wrapped field, e.g. CreateKeyDialog/PublishDialog): with
        // no focusable content of its own, EditFrame is never where focus actually lands.
        CanFocus = true;
        _child = child;

        // LM + LP on the left, RM on the right, TOP/BOT rows top and bottom.
        child.X = 2;
        child.Y = 1;
        child.Width = Dim.Fill(1);
        child.Height = Dim.Fill(1);
        Add(child);

        child.HasFocusChanged += (_, _) => SetNeedsDraw();

        Initialized += (_, _) => WireInitialPasteFocusFix();
    }

    // WORKAROUND for a Terminal.Gui bug, source-verified against the actual Terminal.Gui source
    // (both v2.4.10, which this project depends on, and current v2.4.17 - present, unfixed, in
    // both):
    //
    //     Terminal.Gui/Views/Runnable/Runnable.cs, RaiseIsModalChangedEvent:
    //         SetFocus ();
    //         App?.Navigation?.SetFocused (Focused);
    //
    // SetFocus() correctly cascades HasFocus all the way down to the deepest focusable
    // descendant (e.g. the TextField nested inside this EditFrame). But the very next line
    // re-syncs Application.Navigation's own tracked "focused view" using `Focused` - which View
    // itself defines as only *one level deep* ("the currently focused SubView... of this view")
    // - instead of `MostFocused` (the actual deepest leaf). So right after a Dialog becomes
    // modal, Application.Navigation.GetFocused() points at this EditFrame, not the field inside
    // it the cursor is actually sitting in.
    //
    // That matters because bracketed paste - what a real terminal sends for a physical Ctrl+V -
    // reads Application.Navigation.GetFocused() directly (ApplicationImpl.RaisePasteEvent), not
    // the normal per-key HasFocus chain that ordinary typing walks. So this EditFrame (whose
    // OnPaste is the base View no-op) silently swallows the very first paste after a dialog
    // opens; typing, the cursor, and everything else are unaffected, since those all walk the
    // (correctly-set) HasFocus chain instead of Application.Navigation's cached value. A real
    // Tab/Shift-Tab "fixes" it permanently because AdvanceFocus re-syncs Application.Navigation
    // through a different, correct code path (View.Navigation.cs's RaiseFocusChanging) that this
    // bug doesn't touch.
    //
    // WireInitialPasteFocusFix re-triggers that same correct code path itself: once this
    // EditFrame is attached to the tree (Initialized), it finds the nearest ancestor Runnable
    // (the Dialog) and subscribes to its IsModalChanged - fired right after the buggy sync above
    // - so it runs after, not before, Terminal.Gui's own clobbering. It then blurs and
    // immediately re-focuses `_child`, forcing Application.Navigation to resolve to the field
    // itself instead of this frame. The HasFocus guard makes this safe to embed unconditionally:
    // in a dialog with several EditFrames, only the one whose child genuinely ended up focused
    // (whether by default initial focus or a dialog explicitly redirecting it, e.g. the isEdit
    // branches in CreateBucketDialog/CreateKeyDialog/...) will ever act; the rest are no-ops.
    //
    // DELETE THIS METHOD (and its Initialized subscription above) once fixed upstream - it's a
    // one-word fix (`Focused` -> `MostFocused`) in RaiseIsModalChangedEvent. No upstream issue
    // filed yet as of 2026-09-03 - confirmed by hand (PublishDialog's Subject field) against a
    // real terminal paste, not just tmux's bracketed-paste injection.
    private void WireInitialPasteFocusFix()
    {
        for (var ancestor = SuperView; ancestor is not null; ancestor = ancestor.SuperView)
        {
            if (ancestor is not Runnable runnable) 
                continue;

            runnable.IsModalChanged += (_, e) => {
                if (!e.Value || !_child.HasFocus) return;

                _child.HasFocus = false;
                _child.SetFocus();
            };

            return;
        }
    }

    // Null means "inherit from SuperView" - since EditFrame itself never sets its own scheme,
    // GetAttributeForRole already resolves to whatever the SuperView's ambient background is.
    public Color? OuterBackground
    {
        get => _outerBackground;
        set
        {
            _outerBackground = value;
            SetNeedsDraw();
        }
    }

    public Color InnerBackgroundNormal
    {
        get => _innerBackgroundNormal;
        set
        {
            _innerBackgroundNormal = value;
            SetNeedsDraw();
        }
    }

    public Color InnerBackgroundFocused
    {
        get => _innerBackgroundFocused;
        set
        {
            _innerBackgroundFocused = value;
            SetNeedsDraw();
        }
    }

    // A generic escape hatch, not an "invalid-state color": EditFrame has no concept of validity,
    // disabled-ness, or any other specific state - it just prefers this over Normal/Focused
    // whenever the caller sets it, and reverts to focus-driven coloring when cleared.
    public Color? InnerBackgroundOverride
    {
        get => _innerBackgroundOverride;
        set
        {
            _innerBackgroundOverride = value;
            SetNeedsDraw();
        }
    }

    // Edge accent color (EAC) while the wrapped child does NOT have focus. Null means "use the
    // default white accent" - the left edge's outermost column (TL/LM/BL) is inked in this color
    // rather than IBC, to read as a distinct accent line rather than blending into the field's own
    // color.
    public Color? EdgeAccent
    {
        get => _edgeAccent;
        set
        {
            _edgeAccent = value;
            SetNeedsDraw();
        }
    }

    // Edge accent color while the wrapped child DOES have focus - mirrors the
    // InnerBackgroundNormal/InnerBackgroundFocused split, but for the accent line instead of the
    // inner background. Null means "use Theme.EditFrameEdgeAccentFocused", so every EditFrame gets
    // a focus-driven accent without any caller having to set this explicitly.
    public Color? EdgeAccentFocused
    {
        get => _edgeAccentFocused;
        set
        {
            _edgeAccentFocused = value;
            SetNeedsDraw();
        }
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        var outer = _outerBackground ?? GetAttributeForRole(VisualRole.Normal).Background;
        var inner = _innerBackgroundOverride ?? (_child.HasFocus ? _innerBackgroundFocused : _innerBackgroundNormal);
        var innerAttribute = new Attribute(inner, outer);
        var edgeAccent = _child.HasFocus
            ? _edgeAccentFocused ?? Theme.EditFrameEdgeAccentFocused
            : _edgeAccent ?? DefaultEdgeAccent;
        var accentAttribute = new Attribute(edgeAccent, outer);

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

        if (contentHeight <= 0)
            return true;

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

        return true;
    }
}
