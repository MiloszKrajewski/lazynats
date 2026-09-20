#if DEBUG
using System.Diagnostics;
using System.Text;
using NATS.Client.Core;

namespace lazynats.LiveFeed;

// Dev convenience only (excluded from Release/AOT builds): forwards Trace.Write/WriteLine calls
// into the Live Feed as synthetic $TRACE-subject envelopes, bypassing SubscriptionRegistry
// entirely - see openspec/changes/add-trace-listener/design.md.
internal sealed class TraceFeedListener(IObserver<FeedEnvelope> sink) : TraceListener
{
    internal static readonly Guid SubscriptionId = Guid.Empty;
    private const string TraceSubject = "$TRACE";

    public override void Write(string? message) => Emit(message);

    public override void WriteLine(string? message) => Emit(message);

    private void Emit(string? message)
    {
        if (string.IsNullOrEmpty(message)) return;

        var msg = new NatsMsgBuilder<byte[]> { Subject = TraceSubject, Data = Encoding.UTF8.GetBytes(message) }.Msg;
        sink.OnNext(new FeedEnvelope(DateTimeOffset.UtcNow, SubscriptionId, msg));
    }
}
#endif
