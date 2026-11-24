# The YAML reader of the CEL Policy package

`Great.Parts.Cel.Policy` reads YAML with its own internal reader: no YAML library, no reflection, no recursion over nesting. It reads what `go.yaml.in/yaml/v3` v3.0.4 reads into a `yaml.Node` — the library cel-go uses — so that a document is accepted, and its plain scalars are tagged, as cel-go sees them. The reader serves the policy parser and, through `InternalsVisibleTo`, the conformance runner (`config.yaml` of a category). Its files are in `src/Great.Parts.Cel.Policy/Yaml/`.

## Model

| Node | Data |
| :- | :- |
| `YamlMapping` | pairs in document order (duplicate keys kept, as `yaml.Node`), style (block or flow), tag, position |
| `YamlSequence` | items, style, tag, position |
| `YamlScalar` | value, style (plain, single-quoted, double-quoted, literal, folded), tag (explicit or resolved) and whether the tag was written, position of the node, position of the value, segments of the value |
| alias | the node of the anchor itself — the same object; no copy |

A position is an offset in the document (UTF-16 code units) with its line and column from the line table of the document (`\n`, `\r\n`, `\r`). The position of a quoted scalar is that of its opening quote; its value starts after it.

## Scanning and parsing

`YamlScanner` turns the text into tokens with positions (YAML 1.2.2, chapters 5–9): indicators, plain and quoted scalars with their folding, block scalar headers and contents, anchors, aliases, tags, comments, directives and document markers. `YamlParser` is an event state machine over the tokens, as the parser of `yaml/v3` (`parserc.go`), with an explicit stack of states, and the composer in `YamlReader` builds the nodes from the events with an explicit stack of open collections. Indentation, simple keys and flow levels follow the scanner of `yaml/v3` (`scannerc.go`) and have its limits: 10 000 flow levels, 10 000 indentation levels and 1024 characters in a simple key, each an `InvalidYaml` at the token that passes it. The first error stops the reading; the message says what was expected, in the words of `yaml/v3` where it has them. A character that YAML does not allow (a control character, U+FFFE, U+FFFF, an unpaired surrogate) is an error at that character, found in a window of 512 characters as `yaml/v3` looks in a window of 512 bytes.

Supported: block and flow collections; plain, single- and double-quoted scalars with line folding and escapes; literal and folded block scalars with chomping and indentation indicators; comments; the directive `%YAML` for version 1.1 only, as in `yaml/v3` (another version is `InvalidYaml`), and `%TAG`; tag handles and verbatim tags; anchors and aliases (an alias to an unknown anchor is an error); explicit keys `?`; a byte order mark at the start; `---` and `...`. Only the first document of a stream is read, as `yaml.Unmarshal` into a `yaml.Node` does; what follows it is not scanned, so an error there that `yaml/v3` would find by its lookahead of comments is not reported (the first document is the same). A stream without a document gives no root node. Tabs are not indentation. Folding counts `\n`, `\r\n`, `\r`, NEL, LS and PS as line breaks, as `yaml/v3` does; the positions count `\n`, `\r\n` and `\r` only, as the core does.

## Tags

A plain scalar without a tag is resolved by the table of `resolve.go` of `yaml/v3` v3.0.4 (`YamlResolver`): `null` (`~`, `null`, empty), `bool` (`true`, `false` in three casings), `int` (decimal, `0x`, `0o`, `0b`, octal `0777`, with signs and underscores as `yaml/v3` accepts), `float` (`.inf`, `.nan` and the decimal forms), `timestamp` (the forms of `allowedTimestampFormats`), else `str`. Quoted and block scalars are `str`; `!` is `str`; `!!x` is `tag:yaml.org,2002:x`; a local tag such as `!txt` is kept as written; `%TAG` handles expand. The tests have a row per line of the table.

## Segments of a scalar value

For each string scalar the reader records the segments of its value against the document (`YamlSegment`: the offset in the value and the offset in the document), so that an error inside an expression maps to the same character in the document:

- a plain or quoted scalar on one line, without escapes: one segment at the start of the value;
- a line fold (a line break folded to a space, or kept as `\n` after an empty line) and the indentation removed after it: a segment at the value offset of the first character of the next line;
- an escape sequence of a double-quoted scalar (`\"`, `\n`, `é`, `\x41`, a line continuation `\` at the end of a line) and `''` of a single-quoted one: a segment after it, at the value offset of the next character; the characters an escape produced map to the start of the escape;
- a block scalar read by the rules of YAML (only for the conformance runner: the policy takes a block scalar without a tag raw, see [policy](policy.md), "Parsing"): a segment per content line, at the first character after its indentation, also with `\r\n` lines.

The segments are built in the same pass that builds the value, without a second scan.

## Untrusted input

The reader never recurses over the depth of the document: the scanner, the parser and the composer keep explicit stacks, and the walk of the policy over the nodes uses a stack of its own. Aliases are shared nodes, so a document of nested aliases is a small graph; the policy reader bounds what it walks at 100 000 rules, matches and variables together, and the text it builds from the nodes (strings, their segments, and the keys and tags that its errors repeat) at four times the length of the document plus 65 536 units. The time is linear in the length of the text. Any text gives nodes, no document or one `InvalidYaml`, never an exception.

## Tests

Per construct and per style above; the tag table; positions and segments (a random scalar of each style, the character at every segment, `\n` and `\r\n`); errors with positions; nesting of 100 000 levels without a stack overflow; corrupted documents; the documents of `policy/testdata` of cel-go and of `cel-expr/cel-policy`. They live in `tests/Great.Parts.Cel.Policy.Tests` ([tests](tests.md)).
