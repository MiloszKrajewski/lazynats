## ADDED Requirements

### Requirement: Filter Expression Grammar
The system SHALL support compiling a filter expression built from literal characters, the `.`
token separator, and three wildcards: `*` (matches any run of characters within one token, never
crossing a `.`), `?` (matches exactly one character, excluding `.`), and `>` (matches one or more
remaining whole tokens when it is the entire final segment; anywhere else — a non-terminal whole
segment, or mixed inline with other characters, in either position within a segment — it instead
matches an arbitrary run of characters, including `.`, at the position it appears).

#### Scenario: `*` matches exactly one whole token when used alone
- **WHEN** the expression `orders.*.completed` is compiled and matched against candidate keys
- **THEN** `orders.eu.completed` matches and `orders.eu.west.completed` does not

#### Scenario: `*` does not cross a token boundary when used inline
- **WHEN** the expression `ord*.completed` is compiled and matched against candidate keys
- **THEN** `order.completed` and `ord.completed` match, and `ord.v2.completed` does not

#### Scenario: `?` matches exactly one non-`.` character
- **WHEN** the expression `k?y` is compiled and matched against candidate keys
- **THEN** `key` matches, and `ky`, `keyy`, and `k.y` do not

#### Scenario: `>` as the final whole segment matches one or more remaining tokens
- **WHEN** the expression `orders.>` is compiled and matched against candidate keys
- **THEN** `orders.eu` and `orders.eu.completed` match, and `orders` alone does not

#### Scenario: `>` outside the final whole-segment position matches across token boundaries
- **WHEN** the expression `x.y>` is compiled and matched against candidate keys
- **THEN** `x.y`, `x.yz`, and `x.y.z.1` all match, and `x.other` does not

#### Scenario: `>` position determines the match, not just its presence
- **WHEN** the expressions `>errors` and `errors>` are compiled and matched against candidate keys
- **THEN** `>errors` matches `myerrors` and `x.errors` (ends with `errors`), while `errors>` matches
  `errorsxyz` and `errors.x` (starts with `errors`) — the two expressions are not equivalent

### Requirement: Native-Compatible Expressions Skip The Regex Phase
The system SHALL, when an expression is already valid native NATS subject-wildcard syntax (only
literal tokens, a bare `*` token, and/or a bare `>` token as the final segment), compile it to that
expression verbatim as the native filter and produce no client-side regex — matching is fully
resolved by the native filter alone.

#### Scenario: A plain native expression compiles without a regex
- **WHEN** the expression `orders.*.completed` is compiled
- **THEN** the resulting native filter is `orders.*.completed` and no client-side regex is produced

#### Scenario: An expression using `?` or a non-bare-terminal `>` requires a regex
- **WHEN** the expression `ord?rs.completed` is compiled
- **THEN** a client-side regex is produced alongside the native filter, and both are needed to
  resolve the expression's exact matches

### Requirement: Native Filter Derivation Over-Approximates Non-Native Segments
For an expression requiring the regex phase, the system SHALL derive a native filter that matches
a superset of the expression's true matches: a single-token segment whose exact shape can't be
expressed natively compiles to `*` at that position, and a segment containing a `>` that isn't a
bare final segment compiles to `>`, absorbing every subsequent segment.

#### Scenario: A partially-wildcarded single-token segment over-approximates to `*`
- **WHEN** the expression `ord?rs.completed` is compiled
- **THEN** the native filter's first token is `*`, matching any single token there, not only ones
  shaped like `ord?rs`

#### Scenario: A non-terminal `>` collapses the remainder of the expression to `>`
- **WHEN** the expression `x.y>` is compiled
- **THEN** the native filter is `x.>`

#### Scenario: A non-terminal `>` collapses the remainder even when a literal suffix follows it
- **WHEN** the expression `x.>.z` is compiled
- **THEN** the native filter is `x.>`, and the client-side regex additionally requires the literal
  `.z`-shaped suffix that the native filter alone does not enforce (e.g. `x.z` is admitted by the
  native filter but rejected by the regex)

### Requirement: Expression Validation
The system SHALL reject a filter expression containing an empty token — a leading, trailing, or
doubled `.` — as invalid, and SHALL accept every other combination of literals and the `*`/`?`/`>`
wildcards as valid, including a `>` outside its native terminal position.

#### Scenario: A leading or trailing dot is invalid
- **WHEN** the expression `.completed` or `orders.` is validated
- **THEN** it is reported as invalid

#### Scenario: A doubled dot is invalid
- **WHEN** the expression `orders..completed` is validated
- **THEN** it is reported as invalid

#### Scenario: A stray `>` is valid, not rejected
- **WHEN** the expression `x.>.y` is validated
- **THEN** it is accepted, interpreted per the non-terminal-`>`-matches-an-arbitrary-span rule

### Requirement: Case-Sensitive Literal Matching
Literal characters in a filter expression SHALL be matched case-sensitively, consistent with NATS
subject semantics, regardless of whether a candidate is matched via the native filter or the
client-side regex phase.

#### Scenario: A case mismatch does not match
- **WHEN** the expression `Orders.*` is compiled and matched against the key `orders.eu`
- **THEN** it does not match
