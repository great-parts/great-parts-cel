# Roadmap

What comes after 1.0. The plan has no dates: a step starts when the one before it is done, and a step can change when its result is known. What is already in the library is in [CHANGELOG.md](CHANGELOG.md).

## 1.0

The first stable release: the language and the standard library, the extension sets of cel-go, objects of the application, protobuf messages, and a result of the CEL conformance suite that is published on the page [Conformance](docs/conformance.md). From 1.0 on, the versions follow [Semantic Versioning](docs/versioning.md): a breaking change comes only in a major version.

## After 1.0

The first two steps follow one another.

1. **A speed comparison.** The first comparison of the speed of this library with other CEL libraries for .NET is made on the published package 1.0, on compiling and evaluating expressions in a set of scenarios, and its result decides what comes next. Releases 1.x do not wait for it.
2. **Release 2.0.** Its content follows from the comparison:
   - a faster version of each operation in which the comparison shows room for it. A breaking change of the public API is allowed for this, as 2.0 is a major version;
   - a deterministic finite automaton and a search for a literal prefix in `matches`, which make a match faster without changing its result or its cost;
   - a public API that is easier to use in different kinds of projects; a breaking change is allowed for this too;
   - the way data enters an evaluation, if the comparison shows that it needs a new form. Today `CelBindings` binds the variables by name through a dictionary that is made for every evaluation, and a field or an argument crosses the border of the library as a 32-byte `CelValue`. A new form, such as binding by slot or typed reads of fields and arguments, would be added next to the present ones and would not break 1.x.
3. **Security fixes for 1.x.** After 2.0 comes out, security fixes for the latest 1.x version continue for 6 months, as [SECURITY.md](SECURITY.md) says.

## Other packages

Two packages are planned after 1.0: `Great.Parts.Cel.Policy`, for policies in the CEL policy format, and `Great.Parts.Cel.Filtering`, for filter expressions. Their design starts after 1.0; the core of the library has the hooks they need, such as the position of an expression in its source document (`CelCompileOptions.SourceLocation`).
