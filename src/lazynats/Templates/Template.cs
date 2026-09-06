namespace lazynats.Templates;

// The value TemplateListView (a DrillableListView<Template>) holds per row - Name is the
// lazynats-templates KV key; Subject/Headers/PayloadType/Payload are the fields persisted in the
// corresponding TemplateDocument (see design.md's "Storage shape" decision: Name is never
// duplicated inside the stored document since it's already the key).
internal sealed record Template(
    string Name,
    string Subject,
    IReadOnlyDictionary<string, string> Headers,
    PayloadType PayloadType,
    string Payload);
