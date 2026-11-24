# Filtering: the test corpus of AIP-160

AIP-160 has no official test suite. The package `Great.Parts.Cel.Filtering` is checked by a corpus of its own, run as a suite of the conformance runner `tests/Great.Parts.Cel.Conformance` next to the cel-spec suite, the CEL Policy suite and the regression corpus of the differential run ([conformance.md](conformance.md)). The corpus is not a conformance suite: it has a count of cases and no share of conformance.

## Files

| Path | Content |
| :- | :- |
| `tests/filtering/proto/filter_test.proto` | The schema of the corpus, package `greatparts.filtering.test`; compiled by `Grpc.Tools` in the runner project only |
| `tests/filtering/testdata/*.textproto` | The cases, one file for each topic: `syntax`, `values`, `has`, `traversal`, `wildcards`, `functions`, `search`, `limits`, `aip_examples` |
| `tests/Great.Parts.Cel.Conformance/FilterCorpus/` | `FilteringSuite.cs` reads the files into the common case model of the runner, `FilterJudge.cs` judges a case, `FilterEnvironment.cs` is the environment of the cases; `FilteringSuiteTests.cs` and `FilterJudgeTests.cs` are the tests |

The data go into the runner like those of the cel-spec suite: a build target encodes each file with `protoc --encode=greatparts.filtering.test.FilterTestFile`, and the text and the binary form are embedded resources named by their paths. The text serves only the places of the cases in the findings: a scan that skips comments and strings records the `test` token of each case of each `section`.

## Schema

```proto
message FilterTestFile { string name = 1; string description = 2; repeated FilterTestSection section = 3; }
message FilterTestSection { string name = 1; string description = 2; repeated FilterTest test = 3; }
message FilterTest {
  string name = 1;
  string filter = 2;                    // with repeat above 0: the unit that is repeated
  map<string, string> enum_types = 3;   // CelFilterOptions.EnumTypes
  repeated string search_fields = 4;    // CelFilterOptions.SearchFields
  string cel = 5;                       // the expected tree as CEL text; empty: not compared
  repeated Resource resource = 6;       // the items and the expected result for each
  ErrorExpectation error = 7;           // the expected first error instead of a program
  int32 repeat = 8;                     // filter is written repeat times, then tail follows
  string tail = 9;
}
message Resource { Item value = 1; bool match = 2; }
message ErrorExpectation {
  string code = 1;                      // a member of CelErrorCode
  int32 start = 2; int32 length = 3;    // the place of the first error, in UTF-16 code units
  string subject = 4;                   // empty: not compared
  int32 error_count = 5;                // 0: not compared
  string last_code = 6;                 // empty: not compared
}
```

`repeat` and `tail` write a filter that is too long to keep in a file: 65,537 characters, a chain of 2,500 restrictions. `error_count` and `last_code` check the limit of 100 errors, where the last error is `TooManyErrors`.

## Environment of the cases

One environment for all cases: `UseInput<Item>()` over the message `Item` of the schema, described by the provider of `Great.Parts.Cel.Protobuf` that is added with `AddTypes`, so the fields of the message are the fields of the filters. `Item` has a field of each kind that a filter can meet: numbers of every width, `string`, `bytes`, `bool`, an enumeration, the wrappers, `Timestamp`, `Duration`, `Struct` and `Value` (as `dyn`), repeated fields, maps with string and number keys, and messages nested three deep. The environment has the functions `lower`, `isRecent`, `add`, `seven` and `acme.shout`. The message of the corpus and not `TestAllTypes` of cel-spec is the item because that message has the fields `in`, `as`, `for` and other words that are not names of variables, and the environment would not build.

An item of a protobuf message has a limit that a .NET type does not have: a field of a message type that is not set reads as the empty message, not as `null`. A filter on such a field that is compared with `null` therefore cannot tell not set from set and empty, for example `author:*` for the field `author` of the item. The translation table of the package says so (the row "A field of a message type that is not set reads as an empty message" of "Differences from AIP-160" in the README), and a section of `has.textproto` fixes the documented behavior: for an item without `author`, `author:*` and `author != null` are true and `author = null` is false, `author.name != "X"` and `author.nickname = null` are true, and `author.address:*` is false, because a nested field is checked with `has()`. The other cases of the corpus that touch such a field check the translation by `cel` and by an item whose field is set.

## A case

1. A compiler of filters is made with the options of the case; the filter is the text of the case, or its unit repeated `repeat` times and `tail`.
2. With `error`: the filter does not compile, and the first error has the code, the place and, when given, the subject; `error_count` and `last_code` are checked when given.
3. Without `error`: `Parse` succeeds; with `cel` the tree equals the tree of `Parse(cel)` of the environment by the internal tree comparer, without numbers and positions; `Compile` succeeds.
4. The text of the tree, `CelSyntax.Format`, is compiled in the same environment and must compile.
5. With resources: the program of the filter and the program of the text of the tree both give `match` for each item.

`FilterJudgeTests` runs the judge on cases that are wrong in every way a case can be wrong: another tree, another result, another code, another place, another subject, another count, an error for a filter that compiles, and a filter that has no tree.

## Sources of cases

- Every example of the text of AIP-160 (read on 2026-11-19; CC BY 4.0, which `aip_examples.textproto` names in its description), adapted to the fields of the item.
- Every row of the translation and of the values: a case that translates and a case that is an error.
- Every code that a filter reports: `FilterSyntaxError`, `InvalidFilterValue`, `InvalidFilterRestriction`, `ExpressionTooLong`, `NestingTooDeep`, `TooManyErrors`, and the errors of type checking `UndeclaredReference`, `NoSuchField` and `NoMatchingOverload`.
- Every lexical rule: white space, strings and their escapes, the minus, characters outside ASCII, the keywords.
- Every difference from the text of AIP-160 that the README of the package lists: the section `differences from the text` of `aip_examples.textproto`.
- The limits: length, nesting, 100 errors, long chains of `AND`, `OR` and a sequence.

The tests of `einride/aip-go` are not copied: the meaning differs, since the package translates to executable CEL. The corpus has at least 200 cases, and `Corpus_HasAtLeastTwoHundredCases` keeps it so.

## Publication

The runner counts the cases of the suite and writes the count with the date and the cell to its summary line, `aip-160 corpus: N/N passed, <date> (<cell>)`. The README of the package and the page of conformance carry one line, `AIP-160: own test corpus, N cases, run on <date>`, with the link of the corpus. There is no percentage, no badge and no word "conformance" for this suite; the badge and the date of the run of cel-spec stay with the core only.

`eng/checks corpus` compares N in both places with the number of the cases of the files of `tests/filtering/testdata`: a line that is missing, differs in form, counts another number or has a percentage is `ENG307` ([eng-checks.md](eng-checks.md)).

## Modes

The corpus runs as a test of the runner project under JIT and under Native AOT, on both targets. It does not run on `browser-wasm`: the package is checked there by the Blazor test application, which takes `Great.Parts.Cel.Filtering` as a root of the trimmer and runs a small filter, so the publish reports any warning of trimming from the package.
