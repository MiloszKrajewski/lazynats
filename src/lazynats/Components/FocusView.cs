using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;
using Color = Terminal.Gui.Drawing.Color;

namespace lazynats.Components;

// A FrameView whose border recolors to AccentColor while keyboard focus is anywhere within it
// (itself or any descendant), reverting to the ambient/default border color otherwise - the same
// focus-accent mechanism TabbedView uses for its own frame (App.Navigation.FocusedChanged, not
// per-view HasFocusChanged, per doc/focus-accent-color.md's modal-Dialog lesson; lazy-subscribed
// from OnDrawingAdornments since App is null until this view joins a running application's
// hierarchy), trimmed down to just the border recolor since a plain frame has no header/captions
// to accent separately. Generic and Theme-independent, matching TabbedView's own
// AccentColor/Theme-in-the-consumer split (see Theme.cs's TabAccentColor comment) - a consumer
// assigns its own color (e.g. MainWindow's Live Feed frame uses Theme.LiveFeedFocusAccentColor).
internal class FocusView: FrameView
{
    public virtual Color AccentColor { get; set; } = Color.BrightYellow;

    private readonly View _borderView;
    private bool _subscribedFocusedChanged;
    private IApplication? _subscribedApp; // cached at subscribe time so Dispose can unsubscribe from the same instance even after removal from the tree

    public FocusView() => _borderView = Border!.GetOrCreateView();

    // Walks the focused view's SuperView chain looking for this frame - matches _borderView
    // specifically too, since Border.GetOrCreateView()'s result is reached via IAdornment.Parent,
    // not View.SuperView (see TabbedView.StripHasFocus for the same reasoning).
    private bool HasFocusWithin
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

    // Read from `this`, not hardcoded - see TabbedView.GetAmbientBackground for why a hardcoded
    // background drifted from the app's actual (not pure-black) background.
    protected virtual Color GetAmbientBackground() => GetAttributeForRole(VisualRole.Normal).Background;

    private (Color Accent, Color Background, bool Focused)? _lastState;

    private void UpdateAccent()
    {
        var focused = HasFocusWithin;
        var background = GetAmbientBackground();
        var state = (AccentColor, background, focused);
        if (_lastState == state)
        {
            return;
        }

        _lastState = state;
        _borderView.SetScheme(focused ? new Scheme(new Attribute(AccentColor, background)) : null);
    }

    private void OnAppFocusedChanged(object? sender, EventArgs e)
    {
        UpdateAccent();
        SetNeedsDraw();
    }

    protected override bool OnDrawingAdornments()
    {
        var stop = base.OnDrawingAdornments();

        if (!_subscribedFocusedChanged && App is not null)
        {
            _subscribedApp = App;
            _subscribedApp.Navigation!.FocusedChanged += OnAppFocusedChanged;
            _subscribedFocusedChanged = true;
            UpdateAccent();
        }

        return stop;
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
}
