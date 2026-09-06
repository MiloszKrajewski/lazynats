using System.Threading.Channels;

namespace lazynats;

internal sealed class FeedReaderLoop(ChannelReader<FeedEnvelope> reader, MessageDeduplicator dedup, Action<IReadOnlyList<FeedEnvelope>> onBatch)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        try {
            while (await reader.WaitToReadAsync(cancellationToken)) {
                List<FeedEnvelope>? batch = null;
                while (reader.TryRead(out var envelope))
                    if (!dedup.IsDuplicate(envelope))
                        (batch ??= []).Add(envelope);

                if (batch is { Count: > 0 }) onBatch(batch);
            }
        } catch (OperationCanceledException) {
            // Expected when the owning view is disposed and cancels its token.
        }
    }
}
