using NATS.Client.JetStream.Models;

namespace lazynats.Streams;

// CreateStreamDialog's own result type, kept separate from StreamConfig's wire representation -
// see openspec/changes/add-stream-create/design.md's "NewStreamOptions" decision. StreamConfig
// uses inconsistent per-field sentinels for "unset" (e.g. -1 for unlimited MaxMsgs/MaxBytes, but
// a bare TimeSpan.Zero for unlimited MaxAge); this record expresses "unset" as null instead, so
// the dialog and its validation never have to know StreamConfig's sentinel conventions.
internal sealed record NewStreamOptions(
    string Name,
    IReadOnlyList<string> Subjects,
    StreamConfigRetention Retention,
    TimeSpan? MaxAge)
{
    private static readonly char[] SubjectDelimiters = [' ', ',', ';'];

    // The single place that translates "what the user asked for" into StreamConfig's wire shape -
    // CreateStreamDialog itself never constructs or references StreamConfig.
    public StreamConfig ToStreamConfig() =>
        new(Name, Subjects.ToList()) {
            Retention = Retention,
            MaxAge = MaxAge ?? TimeSpan.Zero,
            // A bare `new StreamConfig(...)` leaves these at the CLR default of 0, which for
            // MaxMsgs/MaxBytes/MaxConsumers/MaxMsgSize/MaxMsgsPerSubject means "admit zero" (a
            // stream that rejects every message on arrival), not "unset" - -1 is their own
            // "unlimited" sentinel. NewStreamOptions doesn't expose any of these fields yet, so
            // they're hardcoded here rather than left at the CLR default; the day this record
            // grows a real MaxMsgs/NumReplicas field, this is the one place that stops
            // hardcoding them.
            MaxMsgs = -1,
            MaxBytes = -1,
            MaxConsumers = -1,
            MaxMsgSize = -1,
            MaxMsgsPerSubject = -1,
            NumReplicas = 1,
        };

    public static IReadOnlyList<string> ParseSubjects(string text) =>
        text.Split(SubjectDelimiters, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // Empty/whitespace input is a valid "leave it unset" - only text that fails to parse as a
    // TimeSpan is actually invalid.
    public static bool TryParseMaxAge(string text, out TimeSpan? result)
    {
        if (string.IsNullOrWhiteSpace(text)) {
            result = null;
            return true;
        }

        if (TimeSpan.TryParse(text.Trim(), out var parsed)) {
            result = parsed;
            return true;
        }

        result = null;
        return false;
    }
}
