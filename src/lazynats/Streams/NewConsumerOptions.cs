using NATS.Client.JetStream.Models;

namespace lazynats.Streams;

// CreateConsumerDialog's own result type, kept separate from ConsumerConfig's wire representation -
// see openspec/changes/add-consumer-create/design.md's "NewConsumerOptions" decision. Follows the
// same "nullable = unset, translator does sentinel work" shape as NewStreamOptions, even though (per
// design.md's Context finding) there's no sentinel substitution to do for the fields this slice
// exposes - the seam still matters for when Advanced fields get added later.
internal sealed record NewConsumerOptions(
    string Name,
    IReadOnlyList<string> FilterSubjects,
    ConsumerConfigAckPolicy AckPolicy,
    ConsumerConfigDeliverPolicy DeliverPolicy)
{
    // The single place that translates "what the user asked for" into ConsumerConfig's wire shape -
    // CreateConsumerDialog itself never constructs or references ConsumerConfig. Unlike
    // ToStreamConfig(), no other field needs an explicit sentinel default: ConsumerConfig's
    // remaining numeric/duration properties are all [JsonIgnore(Condition = WhenWritingDefault)],
    // so a CLR-default value on any of them is omitted from the request rather than sent as a
    // literal 0, per design.md's Context section.
    public ConsumerConfig ToConsumerConfig() =>
        new ConsumerConfig(Name.Trim()) with {
            // Always written through the plural FilterSubjects field, never the singular
            // FilterSubject - matches K4os.NatsTransit's real-world usage (see design.md's
            // Decisions); this dialog never sets the singular field.
            FilterSubjects = FilterSubjects.Count > 0 ? FilterSubjects.ToList() : null,
            AckPolicy = AckPolicy,
            DeliverPolicy = DeliverPolicy,
        };
}
