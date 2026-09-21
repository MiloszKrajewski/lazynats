using lazynats.Core.Payloads;

namespace lazynats.Publish;

// Four-field boundary value for pre-populating PublishDialog (e.g. from a template's Publish
// shortcut - see openspec/changes/publish-from-template/design.md). Deliberately not
// Templates.Template itself: Publish must not depend on Templates (see that design.md's "Seed
// shape" decision).
internal readonly record struct PublishSeed(
    string Subject,
    IReadOnlyDictionary<string, string> Headers,
    PayloadType PayloadType,
    byte[] PayloadBytes);
