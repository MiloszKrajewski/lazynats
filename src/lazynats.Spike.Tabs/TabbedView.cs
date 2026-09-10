using System.Drawing;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;
using Color = Terminal.Gui.Drawing.Color;

namespace lazynats.Spike.Tabs;

/// <summary>
/// A from-scratch tabbed container: a full bordered box whose top border line carries every tab's
/// caption in a single-line strip, carved out of the border via
/// <see cref="LineCanvas.Exclude(Region)"/> rather than compositing separate bordered per-tab
/// frames. "TabbedView", not "TabStrip" - the strip is just the header; this class owns the whole
/// bordered container (the tab content area included), the direct analog of Terminal.Gui's own
/// <c>Tabs</c>/lazynats's <c>ManagementTabs</c>, not a widget meant to be composited with one. See
/// FINDINGS.md for the API specifics this design rests on. Not derived from
/// <c>Tabs</c>/<c>ManagementTabs</c>, though it reproduces their keyboard behavior natively.
/// </summary>
internal class TabbedView : View
{
    // Flanks the whole caption run the same way Terminal.Gui's own Border/Title rendering flanks a
    // normal frame title (confirmed live via tmux against MainWindow's native Title:
    // "┌┤<title>├──┐") - this control is meant to read as "a regular frame title, just with
    // several selectable values in it," i.e. ONE outer ┤...├ pair around the whole run of
    // captions, not one pair per caption. Individual captions are separated from each other by a
    // single plain "│", not a second tee pair.
    private const string LeftTeeGlyph = "┤";
    private const string RightTeeGlyph = "├";
    private const string SeparatorGlyph = "│";

    // Two orthogonal concepts, per the user's metaphor: WHICH tab is selected, and whether
    // keyboard focus is currently anywhere inside this strip at all (header proxy or any tab's
    // content - see StripHasFocus) - "you are here and your input is being captured" vs. "you are
    // not here, but this is where you'll land if you switch back."
    //
    // Focused:     frame yellow, selected title black-on-yellow, other titles dim, separators dim.
    // Not focused: frame default, selected title default ("grey, but not dim" - distinct from the
    //              other titles without being an accent color), other titles dim, separators dim
    //              (unchanged by focus - see UpdateCaptionAttributes).
    //
    // There's a second, narrower focus level on top of the above ("focus squared"): the header
    // itself focused (HeaderHasFocus), i.e. Left/Right is actively switching the selected tab
    // right now - not merely "focus is somewhere inside this strip" (StripHasFocus, true for
    // content focus too). Only in that narrower state do the OTHER (non-selected) titles also
    // turn plain yellow, previewing "these are the tabs Left/Right would land you on" - StripHasFocus
    // with a tab's content focused still shows them dim, same as fully unfocused.
    //
    // Only the selected+focused caption is a genuine solid-color highlight (it intentionally
    // paints over the background). Every other non-default attribute only overrides the
    // foreground - the background always comes from GetAmbientBackground(), not a hardcoded
    // color, so the strip matches whatever's actually behind it instead of forcing true black
    // (confirmed live: a hardcoded Color.Black background rendered as flat black that didn't match
    // the rest of the app's - not pure black - background).
    //
    // All three colors below are plain, publicly settable properties (not a hardcoded palette) so
    // a consumer can restyle this control without subclassing - reasonable defaults, not a fixed
    // design. Terminal.Gui does have a heavier, app-wide theming system for this
    // (Configuration.SchemeManager / ThemeManager, JSON-config-driven, per-view-type default
    // Schemes) but that's overkill for three accent colors on one control; this mirrors the main
    // app's own lighter-weight `Theme.cs` convention instead (centralize tunable colors, read from
    // there rather than repeating literals).
    public virtual Color AccentColor { get; set; } = Color.BrightYellow;
    public virtual Color DimColor { get; set; } = Color.Gray;
    public virtual Color SelectedForegroundColor { get; set; } = Color.Black;

    // AddCommand is `protected` on View, so a plain Label instance can't have Command.HotKey
    // overridden from outside its own class hierarchy - this thin subclass exists solely to expose
    // that one hook.
    private sealed class CaptionLabel : Label
    {
        public void SetHotKeyAction(Action action) => AddCommand(Command.HotKey, () => { action(); return true; });
    }

    private sealed class TabEntry
    {
        public required string Caption; // raw, may contain a HotKeySpecifier ('_') marker
        public required int DisplayLength; // rendered width - marker stripped, what layout/hit-testing use
        public required View Content;
        public required Label CaptionLabel;
        public int X;
    }

    private readonly View _borderView;

    // A real, separate focusable SubView of _borderView representing "the strip's header has
    // focus" - not this View itself. TabbedView (the SuperView of every tab's content) can't
    // reliably reclaim focus from its own descendant via its own SetFocus(): confirmed live that
    // it returns false and leaves focus on the descendant, because a container's SetFocus()
    // delegates back down to whatever was PreviouslyFocused rather than becoming the leaf focus
    // target itself. Mirrors how Tabs/BorderView use a genuine, separate TitleView SubView for
    // exactly this reason (see ManagementTabs.cs in the main app).
    private readonly View _headerProxy;

    // The single outer tee pair bracketing the whole caption run (not one pair per caption - see
    // the class-level comment on LeftTeeGlyph/RightTeeGlyph).
    private readonly Label _leftTee;
    private readonly Label _rightTee;
    private int _leftTeeX;
    private int _rightTeeX;

    private readonly List<TabEntry> _tabs = [];
    private readonly List<Label> _separators = [];
    private readonly List<int> _separatorX = [];
    private int _selectedIndex = -1;
    private bool _subscribedFocusedChanged;
    private IApplication? _subscribedApp; // cached at subscribe time so Dispose can unsubscribe from the same instance even after removal from the tree

    public TabbedView()
    {
        // A descendant (content, or the header proxy) can only actually receive focus if every
        // ancestor up the SuperView chain is itself CanFocus - confirmed live: with this false,
        // SetFocus() on a perfectly focusable, visible descendant silently returned false.
        CanFocus = true;
        // FrameView.DefaultBorderStyle, not a hardcoded LineStyle.Single: this control is meant to
        // look like an ordinary bordered container (see the class doc), so it should track whatever
        // the app's other framed containers (MainWindow's own Live Feed FrameView) resolve to by
        // default/theme, rather than pinning its own separate value that could silently drift from
        // theirs.
        BorderStyle = FrameView.DefaultBorderStyle;
        _borderView = Border!.GetOrCreateView();
        _borderView.CanFocus = true; // required for _headerProxy (its SubView) to be focusable at all - same rule as above

        // X = 1, never 0: confirmed live that a SubView occupying the literal corner cell (0,0)
        // of the border ring suppresses the "┌" corner glyph entirely, even though that cell is
        // never Exclude()'d - unlike an ordinary border-ring cell (where a SubView is simply
        // overwritten by the border's later LineCanvas flush unless explicitly excluded, per the
        // task-2 finding above), a corner cell behaves differently and loses its glyph outright.
        // Root cause not fully traced (not worth the time for a spike), but corners are a known
        // special case in this codebase's LineCanvas dealings (see doc/focus-accent-color.md's
        // GetAttributeForIntersects/intersection-resolution notes) so a special-cased corner
        // behavior here is plausible. Column 1 sits under/behind the first tab's own caption
        // (already an ordinary, non-corner, already-Exclude()'d cell), so it's always safe.
        _headerProxy = new View { X = 1, Y = 0, Width = 1, Height = 1, CanFocus = true, BorderStyle = LineStyle.None };
        _borderView.Add(_headerProxy);

        _leftTee = new Label { Y = 0, Width = 1, Height = 1, Text = LeftTeeGlyph };
        _rightTee = new Label { Y = 0, Width = 1, Height = 1, Text = RightTeeGlyph };
        _borderView.Add(_leftTee);
        _borderView.Add(_rightTee);

        AddCommand(Command.Up, FocusOwnHeader);
        AddCommand(Command.Down, FocusOwnContent);
        AddCommand(Command.Left, () => SwitchSelected(-1));
        AddCommand(Command.Right, () => SwitchSelected(1));
        KeyBindings.Add(Key.CursorUp, Command.Up);
        KeyBindings.Add(Key.CursorDown, Command.Down);
        KeyBindings.Add(Key.CursorLeft, Command.Left);
        KeyBindings.Add(Key.CursorRight, Command.Right);

        _borderView.MouseEvent += OnBorderMouseEvent;
    }

    /// <summary>Number of tabs currently added.</summary>
    public int TabCount => _tabs.Count;

    /// <summary>
    /// Gets or sets which tab is selected. Setting is equivalent to calling <see cref="Select"/>;
    /// unlike keyboard/mouse selection this does not move focus.
    /// </summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        set => Select(value);
    }

    /// <summary>Raised after <see cref="SelectedIndex"/> changes, for any reason (keyboard, mouse, or direct assignment).</summary>
    public event EventHandler<TabbedViewSelectedIndexChangedEventArgs>? SelectedIndexChanged;

    public void AddTab(string caption, View content)
    {
        content.X = 0;
        content.Y = 0;
        content.Width = Dim.Fill();
        content.Height = Dim.Fill();
        content.Visible = false;
        Add(content);

        var index = _tabs.Count;
        var label = new CaptionLabel { Y = 0, Text = caption };

        // Standard Terminal.Gui mnemonic convention (like Button's "_Ok"): a `_` in the caption
        // marks a hotkey. Setting Text already makes Terminal.Gui register an app-wide Alt+<key>
        // binding and render the marked letter distinctly (Scheme's HotNormal/HotFocus roles are
        // derived automatically from Normal - see UpdateCaptionAttributes) - overriding the
        // label's own Command.HotKey implementation replaces its default ("focus the next view")
        // action with "select and focus this tab" instead, without any manual Key parsing here.
        label.SetHotKeyAction(() => SelectAndFocusContent(index));

        _borderView.Add(label);

        // Only the first '_' is a hotkey marker and gets stripped from the rendered text -
        // Terminal.Gui's Title/Text doc: "Only the first HotKey specifier found ... is supported."
        // Any further '_' renders literally, so DisplayLength must match that, not strip every '_'.
        var displayLength = caption.Length - (caption.Contains('_') ? 1 : 0);
        _tabs.Add(new TabEntry { Caption = caption, DisplayLength = displayLength, Content = content, CaptionLabel = label });

        RecomputeLayout();

        if (_selectedIndex == -1)
        {
            Select(0);
        }
    }

    public void Select(int index)
    {
        if (index < 0 || index >= _tabs.Count || index == _selectedIndex)
        {
            return;
        }

        var previousIndex = _selectedIndex;
        _selectedIndex = index;
        for (var i = 0; i < _tabs.Count; i++)
        {
            _tabs[i].Content.Visible = i == index;
        }

        UpdateCaptionAttributes();
        SetNeedsDraw();
        OnSelectedIndexChanged(previousIndex, index);
    }

    /// <summary>
    /// Called after <see cref="SelectedIndex"/> changes; the base implementation raises
    /// <see cref="SelectedIndexChanged"/>. Override to add behavior (e.g. a lazynats subclass
    /// refreshing status-bar shortcuts) without needing to re-implement <see cref="Select"/> itself.
    /// </summary>
    protected virtual void OnSelectedIndexChanged(int previousIndex, int newIndex) =>
        SelectedIndexChanged?.Invoke(this, new TabbedViewSelectedIndexChangedEventArgs(previousIndex, newIndex));

    // Selects `index` and moves focus to its content's first focusable descendant - the path every
    // direct-selection shortcut (mouse click, a caption's own hotkey) goes through (mirrors
    // ManagementTabs.SelectTab).
    protected void SelectAndFocusContent(int index)
    {
        if (index < 0 || index >= _tabs.Count)
        {
            return;
        }

        Select(index);
        var content = _tabs[index].Content;
        var target = FindFirstFocusableDescendant(content) ?? content;
        BlurCurrentFocus();
        target.SetFocus();
    }

    // Whatever currently holds focus (this TabbedView container itself on first startup, or the
    // header proxy, or some prior tab's content) has to be explicitly blurred before a fresh
    // SetFocus() elsewhere in the tree will actually take - confirmed live: without this,
    // SetFocus() on a perfectly focusable, visible target silently returns false and leaves focus
    // wherever it already was (a container reclaims focus from PreviouslyFocused rather than
    // releasing it, so the still-focused prior view keeps winning until blurred first).
    protected void BlurCurrentFocus()
    {
        // Blurring a leaf only cascades focus up to its nearest focusable ANCESTOR, not all the
        // way out - confirmed live: blurring a focused Button left its container page View
        // focused, and a subsequent SetFocus() elsewhere in the tree still failed against that.
        // Keep blurring whatever's now focused until nothing is. Bounded by lack of progress, but
        // "progress" has to mean "never seen this view before in this pass," not merely "different
        // from the immediately preceding one" - confirmed live (froze the app, 100% CPU spin, no
        // response even to Esc-to-quit): blurring a view whose ancestor's own focus-restore logic
        // re-delegates straight back down to that same view (via PreviouslyFocused) produces a
        // period-2 bounce - view, ancestor, view, ancestor, ... - that a same-as-last-only check
        // never catches, since consecutive values always differ. Scales with actual tree size
        // rather than an arbitrary iteration cap that a sufficiently nested tab-content tree could
        // exceed.
        var seen = new HashSet<View>();
        while (App?.Navigation?.GetFocused() is { } current)
        {
            if (!seen.Add(current))
            {
                break;
            }

            current.HasFocus = false;
        }
    }

    protected static View? FindFirstFocusableDescendant(View view)
    {
        foreach (var sub in view.SubViews)
        {
            if (!sub.Visible || !sub.Enabled)
            {
                continue;
            }

            var deeper = FindFirstFocusableDescendant(sub);
            if (deeper is not null)
            {
                return deeper;
            }

            if (sub.CanFocus)
            {
                return sub;
            }
        }

        return null;
    }

    private bool HeaderHasFocus => App?.Navigation?.GetFocused() == _headerProxy;

    // "Focus is anywhere inside this strip" - the header proxy, or any tab's content - as opposed
    // to HeaderHasFocus above (header vs. content specifically, still used to route Up/Down/
    // Left/Right). Walks the focused view's SuperView chain looking for this strip itself, since a
    // tab's content can nest arbitrarily deep (a Button inside a page View inside Content, etc.).
    // Also matches _borderView specifically: it's reached via IAdornment.Parent, not View.SuperView
    // - Border.GetOrCreateView()'s result is never actually Add()'d as a literal SubView of `this`,
    // so walking SuperView alone stops there and never reaches `this` - confirmed live: without
    // this extra check, focusing _headerProxy (a real SubView of _borderView) read as "not
    // focused" and left the frame unaccented after Up.
    private bool StripHasFocus
    {
        get
        {
            for (var v = App?.Navigation?.GetFocused(); v is not null; v = v.SuperView)
            {
                if (v == this || v == _borderView)
                {
                    return true;
                }
            }

            return false;
        }
    }

    // The background this strip would render with if nothing here ever called SetScheme at all -
    // read from `this` specifically, since `this` never gets an explicit Scheme (see the
    // scheme-bleeding comment on _borderView below), so it always reflects the real ambient/
    // inherited background instead of a hardcoded guess. Confirmed live: hardcoding Color.Black as
    // the background for every non-highlighted attribute rendered as flat black that didn't match
    // the rest of the (not actually pure-black) app background. Virtual so a subclass can force a
    // specific background instead of inheriting the ambient one.
    protected virtual Color GetAmbientBackground() => GetAttributeForRole(VisualRole.Normal).Background;

    // Memoized against the app-wide FocusedChanged event: that event fires for ANY focus move
    // anywhere in the app, not just within this strip, so without this most calls are triggered by
    // something entirely unrelated (e.g. focusing a widget on a different tab's content) and would
    // otherwise reallocate every Attribute/Scheme and re-push them onto every caption/separator
    // Label for no visible change. TabCount is included so a newly added tab still gets its
    // initial scheme set on the next call even when none of the other inputs moved.
    private (Color Accent, Color Dim, Color SelectedFg, Color Background, bool Focused, bool HeaderFocused, int SelectedIndex, int TabCount)? _lastCaptionAttributesState;

    protected virtual void UpdateCaptionAttributes()
    {
        var focused = StripHasFocus;
        var headerFocused = HeaderHasFocus;
        var ambientBackground = GetAmbientBackground();

        var state = (AccentColor, DimColor, SelectedForegroundColor, ambientBackground, focused, headerFocused, _selectedIndex, _tabs.Count);
        if (_lastCaptionAttributesState == state)
        {
            return;
        }

        _lastCaptionAttributesState = state;

        var frameAttribute = new Attribute(AccentColor, ambientBackground);
        var dimAttribute = new Attribute(DimColor, ambientBackground);
        var selectedFocusedAttribute = new Attribute(SelectedForegroundColor, AccentColor);

        var frameScheme = focused ? new Scheme(frameAttribute) : null;

        // Scheme goes on _borderView, never on `this`: `this` is the SuperView of every tab's
        // content page, and an unset Scheme resolves by walking up the SuperView chain - setting
        // it on `this` was confirmed live to bleed the accent color into plain, un-schemed content
        // (e.g. a placeholder page's body Label rendered yellow text it never asked for).
        // `_borderView` recolors the border glyphs on its own (per doc/focus-accent-color.md's
        // Attempt 1: Border resolves its LineCanvas lines' attribute from its own View's scheme,
        // even though the lines themselves live on `this.LineCanvas`) without being an ancestor of
        // any tab content.
        _borderView.SetScheme(frameScheme);
        _leftTee.SetScheme(frameScheme);
        _rightTee.SetScheme(frameScheme);

        // Separators are understated punctuation between captions, not a focus indicator - always
        // dim, regardless of focus. Confirmed live this was backwards before: switching them to the
        // brighter ambient default when the strip lost focus read as "become brighter when not
        // focused," which is the opposite of what dims/highlights should communicate.
        var separatorScheme = new Scheme(dimAttribute);
        foreach (var separator in _separators)
        {
            separator.SetScheme(separatorScheme);
        }

        // "Focus squared" - see the class-level comment above: only while the header itself is
        // focused (actively navigating tabs via Left/Right) do the other, non-selected captions
        // also turn plain yellow. StripHasFocus alone (e.g. a tab's content focused) is not enough.
        for (var i = 0; i < _tabs.Count; i++)
        {
            // Selected + focused: solid black-on-yellow highlight. Selected + NOT focused: no
            // override at all (ambient default) - a step brighter than the other, dim captions
            // without reaching for an accent color, distinct the same way the frame itself already
            // goes ambient (not dim) when unfocused. Not selected + header focused ("focus
            // squared"): plain yellow (same hue as the frame/selected highlight, just not
            // inverted). Not selected, otherwise (dim strip focus, or none): dim, same as
            // separators.
            Scheme? captionScheme = i != _selectedIndex
                ? new Scheme(headerFocused ? frameAttribute : dimAttribute)
                : focused ? new Scheme(selectedFocusedAttribute) : null;
            _tabs[i].CaptionLabel.SetScheme(captionScheme);
        }
    }

    private void OnAppFocusedChanged(object? sender, EventArgs e)
    {
        UpdateCaptionAttributes();
        SetNeedsDraw();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _subscribedApp is not null)
        {
            _subscribedApp.Navigation!.FocusedChanged -= OnAppFocusedChanged;
            _subscribedApp = null;
        }

        base.Dispose(disposing);
    }

    private bool? FocusOwnHeader()
    {
        if (!HeaderHasFocus)
        {
            BlurCurrentFocus();
            _headerProxy.SetFocus();
        }

        return true;
    }

    private bool? FocusOwnContent()
    {
        if (!HeaderHasFocus || _selectedIndex < 0)
        {
            return true;
        }

        var content = _tabs[_selectedIndex].Content;
        var target = FindFirstFocusableDescendant(content) ?? content;
        BlurCurrentFocus();
        target.SetFocus();
        return true;
    }

    private bool? SwitchSelected(int direction)
    {
        if (!HeaderHasFocus || _tabs.Count == 0)
        {
            return true;
        }

        Select((_selectedIndex + direction + _tabs.Count) % _tabs.Count);
        return true;
    }

    // Hit-tests against the same tab.X/DisplayLength table used for drawing and Exclude (design
    // decision: one layout computation, three consumers). Uses ScreenPosition rather than
    // mouse.Position: Position is relative to mouse.View, which for a click that lands on a
    // caption is that caption's own Label (0-based within its own small Frame), not _borderView -
    // converting from screen coordinates sidesteps that ambiguity entirely.
    private void OnBorderMouseEvent(object? sender, Mouse mouse)
    {
        if (!mouse.IsSingleClicked)
        {
            return;
        }

        var viewportPos = _borderView.ScreenToViewport(mouse.ScreenPosition);
        for (var i = 0; i < _tabs.Count; i++)
        {
            var tab = _tabs[i];
            if (viewportPos.X >= tab.X && viewportPos.X < tab.X + tab.DisplayLength && viewportPos.Y == 0)
            {
                // Focuses content (SelectAndFocusContent), not _headerProxy: clicking a caption
                // reads as "activate this tab" (same effect as Alt+N), not "enter header
                // keyboard-navigation mode" - a click landing in the header-focused visual state
                // read as wrong/surprising in manual testing.
                SelectAndFocusContent(i);
                mouse.Handled = true;
                return;
            }
        }
    }

    // Relaid out from AddTab and whenever the resolved Frame changes (see OnFrameChanged) - item 8
    // ("relayouting is a must"): captions were previously only positioned once, at add-time, so a
    // terminal resize never moved anything. Virtual so a subclass adding overflow scrolling (task
    // 6) can extend this instead of re-deriving the X-position math from scratch.
    protected virtual void RecomputeLayout()
    {
        // Whole-strip layout: caption rectangles for drawing, Exclude, and (later) hit-testing all
        // come from this one pass - one computation, several consumers, per design.md. One outer
        // tee pair brackets the whole run (┤...├, like a native frame title), with plain single-
        // glyph separators between individual captions (┤Subscribe│Streams│Values├).
        while (_separators.Count < _tabs.Count - 1)
        {
            var separator = new Label { Y = 0, Width = 1, Height = 1, Text = SeparatorGlyph };
            _borderView.Add(separator);
            _separators.Add(separator);
            _separatorX.Add(0);
        }

        var x = 1; // one cell in from the left corner
        _leftTeeX = x;
        _leftTee.X = x;
        x += 1;

        for (var i = 0; i < _tabs.Count; i++)
        {
            var tab = _tabs[i];
            tab.X = x;
            tab.CaptionLabel.X = x;
            tab.CaptionLabel.Width = tab.DisplayLength;
            tab.CaptionLabel.Height = 1;
            x += tab.DisplayLength;

            if (i < _separators.Count)
            {
                _separatorX[i] = x;
                _separators[i].X = x;
                x += 1;
            }
        }

        _rightTeeX = x;
        _rightTee.X = x;
    }

    // FrameChanged is the canonical "resolved size changed" hook (fires after a layout pass
    // resolves Width = Dim.Fill(), not just on explicit assignment) - relayout on resize (item 8),
    // not just on AddTab. Guarded on _borderView: View's own constructor can trigger an initial
    // FrameChanged before this instance's own field initializers/constructor body have run.
    protected override void OnFrameChanged(in Rectangle frame)
    {
        base.OnFrameChanged(in frame);
        if (_borderView is not null)
        {
            RecomputeLayout();
        }
    }

    protected override bool OnDrawingAdornments()
    {
        var stop = base.OnDrawingAdornments();

        // Lazy-subscribe: App is null until this view is added to a running application's
        // hierarchy, so this can't happen in the constructor. App.Navigation.FocusedChanged, not
        // this.HasFocusChanged, per the modal-Dialog lesson in doc/focus-accent-color.md.
        if (!_subscribedFocusedChanged && App is not null)
        {
            _subscribedApp = App;
            _subscribedApp.Navigation!.FocusedChanged += OnAppFocusedChanged;
            _subscribedFocusedChanged = true;

            // Terminal.Gui's default startup focus assignment lands on TabbedView itself (the first
            // CanFocus view it finds, depth-first) rather than descending into a tab's content -
            // confirmed live. Redirect that to the initially-selected tab's content, matching
            // ManagementTabs' own startup behavior. Deferred via AddTimeout(Zero, ...) rather than
            // called inline here: SetFocus() called synchronously from inside this very first draw
            // pass silently returned false - confirmed live - so it's pushed to the next main-loop
            // iteration instead, after this draw (and Terminal.Gui's own startup focus pass)
            // completes.
            var app = App;
            app.AddTimeout(TimeSpan.Zero, () => {
                if (app.Navigation?.GetFocused() is (null or TabbedView) && _selectedIndex >= 0)
                {
                    SelectAndFocusContent(_selectedIndex);
                }

                return false;
            });
        }

        LineCanvas.Exclude(new Region(_borderView.ViewportToScreen(new Rectangle(_leftTeeX, 0, 1, 1))));
        LineCanvas.Exclude(new Region(_borderView.ViewportToScreen(new Rectangle(_rightTeeX, 0, 1, 1))));

        foreach (var tab in _tabs)
        {
            var rect = new Rectangle(tab.X, 0, tab.DisplayLength, 1);
            LineCanvas.Exclude(new Region(_borderView.ViewportToScreen(rect)));
        }

        foreach (var separatorX in _separatorX)
        {
            var rect = new Rectangle(separatorX, 0, 1, 1);
            LineCanvas.Exclude(new Region(_borderView.ViewportToScreen(rect)));
        }

        return stop;
    }
}

/// <summary>Event args for <see cref="TabbedView.SelectedIndexChanged"/>.</summary>
internal sealed class TabbedViewSelectedIndexChangedEventArgs(int previousIndex, int currentIndex) : EventArgs
{
    public int PreviousIndex { get; } = previousIndex;
    public int CurrentIndex { get; } = currentIndex;
}
