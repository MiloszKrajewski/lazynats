using lazynats.Components;

namespace lazynats.Templates;

// Read-only readout of a single template, per the tab's "same split as other tabs" shape - never
// focusable, never edits anything (Ctrl+E opens TemplateDialog instead, from TemplatesTab). Unlike
// every other PollingDetailsView subclass, there is nothing to poll: the highlighted list item
// already carries the template's full value (name/subject/headers/payload type/payload), the same
// way BucketDetails/StreamDetails are Shown straight from an already-fetched list item on
// highlight change - so PollInterval is disabled entirely and FetchAsync is never actually called.
// Still built on PollingDetailsView rather than a bespoke View, to reuse its label:value + body
// rendering (the "Name/Subject/Payload Type, blank, headers, blank, payload" layout) instead of
// duplicating OnDrawingContent.
internal sealed class TemplateDetails: PollingDetailsView<Template, Template>
{
    protected override TimeSpan? PollInterval => null;

    public void SetTarget(Template? template)
    {
        if (template is not null) SetPollTarget(template); else ClearPollTarget();
    }

    protected override Task<Template?> FetchAsync(Template target) => Task.FromResult<Template?>(target);

    protected override (string Label, string Value)[] BuildRows(Template template) => [
        ("Name", template.Name),
        ("Subject", template.Subject),
        ("Payload Type", template.PayloadType.ToString()),
    ];

    // Headers (one "key: value" line each) then a blank line then Payload, all as one body string
    // - PollingDetailsView's own renderer already inserts the header-rows/body separator blank
    // line for free (see its OnDrawingContent), so this only needs to add the second blank line
    // between Headers and Payload. An empty Headers dictionary still renders that separator (no
    // headers, then the blank line, then Payload) rather than collapsing it away, so the layout
    // stays in the same fixed shape regardless of whether the template has any headers.
    protected override string? BuildBody(Template template)
    {
        var headerLines = template.Headers.Select(pair => $"{pair.Key}: {pair.Value}");
        return string.Join('\n', headerLines.Append(string.Empty)) + "\n" + template.Payload;
    }
}
