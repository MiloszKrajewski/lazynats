using System.Collections.ObjectModel;
using System.Reactive.Linq;
using lazynats.Components;
using lazynats.Core;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace lazynats.LiveFeed;

// SelectedIndex is only meaningful when Following is false ("sticky" - the selection has stuck to
// a specific message while the feed keeps arriving underneath it); while Following, the feed is
// always scrolled to the newest message so there is no independent position to report.
internal readonly record struct LiveFeedStatus(int Count, bool Following, int SelectedIndex);

internal sealed class LiveUpdatesView: View, IShortcutSource
{
    private const int MaximumFeedLength = 10_000;
    private static readonly TimeSpan BufferWindow = TimeSpan.FromMilliseconds(25);

    private readonly ObservableCollection<FeedEnvelope> _events = [];
    private readonly LiveLogDataSource _dataSource;
    private readonly ListView _listView;
    private IDisposable? _subscription;
    private bool _following = true;
    private bool _suppressValueChanged;

    public event Action<FeedEnvelope>? ItemSelected;
    public event Action<LiveFeedStatus>? StatusChanged;

    // App resolves via the SuperView chain, so it's unavailable during the constructor (this
    // view has no SuperView yet - it's added to feedFrame/MainWindow afterward). Wiring the Rx
    // chain here instead, once Initialized fires, guarantees App is live.
    public LiveUpdatesView(IObservable<FeedEnvelope> feed, MessageDeduplicator dedup)
    {
        CanFocus = true;

        _dataSource = new LiveLogDataSource(_events);
        _listView = new ListView { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() };
        _listView.KeystrokeNavigator = null;
        _listView.ViewportSettings |= ViewportSettingsFlags.HasVerticalScrollBar;
        _listView.Source = _dataSource;
        _listView.Accepted += OnAccepted;
        _listView.ValueChanged += (_, _) => {
            if (_suppressValueChanged) return;
            _following = false;
            RaiseStatusChanged();
        };

        // ListView claims Space for its own mark-toggle behavior (Command.Toggle) before it ever
        // bubbles up here, and - confirmed empirically against the running app - that claim isn't
        // a removable KeyBindings entry: _listView.KeyBindings.Remove(Key.Space) compiles and runs
        // but has no effect, Space still never reaches this view's own Command.Toggle handler
        // below. KeyDown fires before key-binding dispatch on the view that receives it (per
        // Terminal.Gui's own NewKeyDownEvent docs), so intercepting it here - on _listView itself,
        // the focused view that would otherwise see Space first - is what actually pre-empts it.
        _listView.KeyDown += (_, key) => {
            if (key != Key.Space) return;
            ToggleFollow();
            key.Handled = true;
        };

        Add(_listView);

        AddCommand(Command.DeleteAll, () => { Clear(); return true; });
        KeyBindings.Add(Key.C, Command.DeleteAll);

        // Space is already bound to Command.Toggle by View's own default key bindings - just
        // supply the handler, don't re-bind the key (KeyBindings.Add throws if the key is already
        // bound - confirmed empirically against the running app). Belt-and-suspenders alongside
        // the _listView.KeyDown interception above, in case focus ever lands on this view directly.
        AddCommand(Command.Toggle, () => { ToggleFollow(); return true; });

        Initialized += (_, _) => {
            _subscription = feed
                .Where(envelope => !dedup.IsDuplicate(envelope))
                .Buffer(BufferWindow)
                .Where(batch => batch.Count > 0)
                .ObserveOnApp(App!)
                .Subscribe(batch => {
                    foreach (var envelope in batch) OnEvent(envelope);
                });
        };
    }

    public void Clear()
    {
        _events.Clear();
        SetSelectedItemGuarded(null);
        RaiseStatusChanged();
    }

    public IEnumerable<ShortcutHint> Shortcuts => [
        new(Key.C, "Clear", Clear),
        new(Key.Space, "Follow/Pause", ToggleFollow)
    ];

    private void ToggleFollow()
    {
        _following = !_following;
        if (_following) MoveEndGuarded();
        RaiseStatusChanged();
    }

    private void OnEvent(FeedEnvelope envelope)
    {
        var selectedItem = _listView.SelectedItem;
        _events.Add(envelope);

        // Eviction always removes from the front, so a not-yet-evicted selection just needs to
        // shift down by one per removal to keep tracking the same message - no identity search
        // needed (contrast DrillableListView.ReplaceItems, which searches because its refresh can
        // reorder/replace arbitrarily). Once the selected message itself is the one evicted (index
        // reaches 0), there's nothing left to track - it stays at 0, landing on whatever message
        // just became the new oldest. This runs regardless of follow state so the buffer is always
        // trimmed to its cap; only the selection write below is conditional.
        while (_events.Count > MaximumFeedLength) {
            _events.RemoveAt(0);
            if (selectedItem is > 0) selectedItem--;
        }

        if (_following) MoveEndGuarded();
        else if (selectedItem != _listView.SelectedItem) SetSelectedItemGuarded(selectedItem);

        RaiseStatusChanged();
    }

    private void RaiseStatusChanged() =>
        StatusChanged?.Invoke(new LiveFeedStatus(_events.Count, _following, _listView.SelectedItem ?? 0));

    private void SetSelectedItemGuarded(int? value)
    {
        _suppressValueChanged = true;
        try { _listView.SelectedItem = value; } finally { _suppressValueChanged = false; }
    }

    private void MoveEndGuarded()
    {
        _suppressValueChanged = true;
        try { _listView.MoveEnd(); } finally { _suppressValueChanged = false; }
    }

    private void OnAccepted(object? sender, CommandEventArgs e)
    {
        if (_listView.SelectedItem is { } index and >= 0 && index < _events.Count)
            ItemSelected?.Invoke(_events[index]);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _subscription?.Dispose();
            _dataSource.Dispose();
        }

        base.Dispose(disposing);
    }
}
