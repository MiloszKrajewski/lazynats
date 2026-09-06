using System.Text;

namespace lazynats;

internal static class FeedRowFormatter
{
    public static string Format(FeedEnvelope envelope)
    {
        var message = envelope.Message;
        var headerText = message.Headers is { Count: > 0 } headers
            ? string.Join(' ', headers.Select(kv => $"{kv.Key}={kv.Value}"))
            : string.Empty;
        var payloadText = message.Data is { } data ? Encoding.UTF8.GetString(data) : string.Empty;

        return $"{envelope.ReceivedAt:HH:mm:ss.fff}  {message.Subject}  {headerText}  {payloadText}";
    }
}
