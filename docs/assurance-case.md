# Assurance case

This page argues why Great.Parts.Cel meets its security requirements. It states the requirements, the threat model and the trust boundaries, shows how secure design principles are applied, and lists the common implementation weaknesses that apply to an expression engine with the measures against each. Every claim points to the code that implements it and to the tests or checks that verify it. Where the library has no protection, the page says so and names what the application has to do instead.

The page covers the packages `Great.Parts.Cel` and `Great.Parts.Cel.Protobuf` and the source generator that ships inside `Great.Parts.Cel`, on the branch `main`. How to report a vulnerability is in [SECURITY.md](../SECURITY.md).

The page uses three kinds of statements: a **claim** is what the library guarantees; the **evidence** is the code and the tests that make the claim checkable; a **limitation** is where the claim stops. A claim that is measured on a set of inputs, not proven for all inputs, says so.

## Security requirements

These are the requirements the case argues. They are the properties an application relies on when it compiles and evaluates expressions that come from people it does not trust.

| # | Requirement |
| :- | :- |
| 1 | Compiling any text ends with a program or a list of errors: no exception escapes for a wrong expression, the process does not crash, and the work is bounded. A text over the length or the nesting limit is an error with its position. |
| 2 | An application can bound the work of an evaluation: with a cost limit, the time and the memory of an evaluation are bounded by the limit, and an evaluation over it ends with an error. An evaluation can be cancelled. |
| 3 | `matches` takes time linear in the length of the string for a fixed pattern; no pattern makes it backtrack. |
| 4 | An expression has no authority of its own: it reads only the values that the application binds and calls only the functions that the environment declares. The library does not access files, the network, environment variables, processes, threads, the clock or the culture of the process. |
| 5 | Results are those of the CEL language definition, do not depend on the platform, the culture, the time zone of the process or JIT versus Native AOT, and do not change when one environment and one program are used from many threads at once. |
| 6 | Errors of parsing, checking and evaluation, and exceptions of application code called during evaluation, come back as results. The library throws only when a documented precondition of a call is broken, and `OperationCanceledException` on cancellation. |
| 7 | A package on nuget.org is the one that the release workflow built from the tagged commit, and the packages add no dependencies beyond `Google.Protobuf` for `Great.Parts.Cel.Protobuf`. |

## Threat model

### Assets

- **Availability of the host process**: its CPU, memory, thread stacks and threads. A library that evaluates untrusted expressions inside the process of the application can take the whole process down.
- **Integrity of results**: an expression often decides an authorization, a validation or a routing. A wrong result, or a result that differs between platforms, is a security defect of the application that relies on it.
- **Confidentiality of the host**: data that the application did not bind — files, environment variables, other objects of the process.
- **Integrity of the packages** that applications install.

### Actors

| Actor | Trust | What it controls |
| :- | :- | :- |
| Author of an expression | untrusted | the text of the expression |
| Source of data | untrusted | the values the application binds: strings, lists, maps, `JsonElement`, protobuf messages, the bytes inside `google.protobuf.Any`, the text of a pattern of `matches` that comes from data |
| Source of an environment description in JSON | untrusted when the application reads descriptions from users | the text of the description that `CelEnvironmentJson.Apply` reads |
| Application developer | trusted | the environment in code, application functions and their cost functions, objects of the application and their getters, the time zone database, the cost limit and the cancellation token |
| Maintainers and CI | trusted, verified by the checks of [Verification](#verification-that-keeps-the-case-true) | the code, the build and the release |

### Threats

| Threat | Actor | Entry point | Countered in |
| :- | :- | :- | :- |
| Exhaust CPU or memory by a long, deeply nested or error-filled text | author | `Compile`, `Evaluate` with text | [Weaknesses](#common-weaknesses-and-their-countermeasures): CWE-400, CWE-674 |
| Crash the process by a stack overflow, which .NET cannot catch | author, data | parsing, checking, planning, evaluation, comparison of nested values | CWE-674 |
| Exhaust CPU or memory during evaluation: loops of macros, large lists, strings and maps | author, data | `CelProgram<T>.Evaluate` | CWE-400, CWE-770 |
| Make `matches` or a substring search take super-linear time | author, data | patterns and strings | CWE-1333, CWE-407 |
| Make map lookups linear with keys chosen to collide | author, data | map keys | CWE-407 |
| Turn an error into an exception storm or an unhandled exception | author, data | any stage | CWE-248, CWE-755 |
| Read data or run code outside what the application allowed | author | functions, field selection, type names | CWE-94, CWE-470, CWE-502 |
| Get a different result on another platform, culture or time zone, or a wrong result from integer overflow | author, data | arithmetic, conversions, time | CWE-190, CWE-681, requirement 5 |
| Get a different result under concurrent use | author | shared environments and programs | CWE-362 |
| Install a package that was not built from the repository | supply chain | nuget.org | [Verification](#verification-that-keeps-the-case-true) |

Spoofing and repudiation, in the STRIDE sense, belong to the application: the library has no identities, sessions or logs.

## Trust boundaries

The library is a component inside the process of the application. It has no network interface and no state outside the objects the application creates.

| Boundary | What crosses it | Who is trusted on each side | How the library treats what crosses |
| :- | :- | :- | :- |
| B1. Expression text into compilation | the text given to `CelEnvironment.Compile<T>`, `CelEnvironment.Evaluate<T>` and `CelCompilationCache` | the text is untrusted; the environment is trusted | fixed limits of [parsing](limits.md#parsing); every error is a `CelError`; type checking accepts only declared names and overloads |
| B2. Data into evaluation | the values of `CelBindings`, the content of `google.protobuf.Any`, `JsonElement` documents, patterns from data | the data is untrusted | every operation is charged before it runs when a cost limit is set ([Cost](cost.md)); conversions and lookups check types and ranges; errors are values |
| B3. Environment description into the builder | JSON read by `CelEnvironmentJson.Apply` | untrusted when it comes from users | strict reading, nesting up to 64 levels, every error a result, all or nothing ([Environment description in JSON](environment-json.md#reading)) |
| B4. Library into application code | calls of application functions and cost functions, getters of application objects, delegates of runtime types | application code is trusted by the library | its exceptions become `ApplicationException` (3014) with the original exception; its time and memory are not measured ([Functions of the application](cost.md#functions-of-the-application)) |
| B5. Repository into the package | the build of `release.yml` | CI is trusted, the result is verifiable | attestation of origin of every `.nupkg`, Trusted Publishing, pinned actions ([Where the packages come from](../README.md#where-the-packages-come-from)) |

The source generator runs inside the compiler of the application, on the code of the application, at build time. It sees no expression and no data, so it is on the trusted side of every boundary.

## Secure design principles

The principles are those of Saltzer and Schroeder, as the OpenSSF Best Practices criteria name them, with input validation.

| Principle | How it is applied | Evidence |
| :- | :- | :- |
| Economy of mechanism | One evaluator interprets an immutable plan; there is no code generation at run time and no reflection. The parser of patterns, the engine of `matches` and the substring search are small components of the library with one purpose each, not the general .NET facilities | [regex-engine](design/regex-engine.md), [evaluation](design/evaluation.md); `System.Linq.Expressions` compilation, reflection and `System.Text.RegularExpressions.Regex` are banned in [eng/BannedSymbols.txt](../eng/BannedSymbols.txt) |
| Fail-safe defaults | Every fixed limit is on without configuration: the length and nesting of the text, the limits of patterns, the depth of `Any`, the ranges of values. A missing binding, an unknown time zone or an undeclared function is an error, never a fallback: a missing IANA zone is `TimeZoneNotFound` (3022), not the zone of the host. **Deviation:** the cost limit is off by default, as in cel-go, and an evaluation without it counts nothing; [SECURITY.md](../SECURITY.md), the README and [Limits](limits.md#type-checking-and-evaluation) tell an application that evaluates untrusted expressions to set it | [Limits](limits.md); [TimeZoneNameTests.cs](../tests/Great.Parts.Cel.Tests/Examples/Library/TimeZoneNameTests.cs) |
| Complete mediation | Type checking resolves every name and every call against the declarations of the environment, and evaluation calls only an overload that checking accepted for that call. Evaluation without type checking exists only as an internal member for the conformance runner (`CelEnvironment.EvaluateWithoutTypeCheck`); the public API has no such entry. Every value that enters or leaves an expression through a declared type is checked against it | [CelEnvironment.cs](../src/Great.Parts.Cel/CelEnvironment.cs), [Planner.cs](../src/Great.Parts.Cel/Evaluation/Planner.cs); the public API snapshot `src/Great.Parts.Cel/PublicAPI/PublicAPI.Shipped.txt` is enforced by the PublicApiAnalyzers in the build; [Values checked against their declared type](cost.md#values-checked-against-their-declared-type) |
| Open design | The code, the design pages, the limits and the cost table are public; no security property depends on a secret. The only randomness, the seed of the hash of map keys, is the per-process seed of .NET | this repository; [KeyHashRules.cs](../src/Great.Parts.Cel/Values/KeyHashRules.cs) |
| Separation of privilege | Building an environment, compiling an expression and evaluating a program are separate calls. The application decides what an expression may use when it builds the environment; the expression decides nothing about it | [API concepts](api-concepts.md) |
| Least privilege | The library itself has no access to files, the network, environment variables, processes, threads, timers, the clock or the culture of the process: these APIs are banned in the packages and in the code that the source generator creates, and a use is the build error RS0030. An expression reads only bound variables and calls only declared functions | [eng/BannedSymbols.txt](../eng/BannedSymbols.txt), wired in [Great.Parts.Cel.csproj](../src/Great.Parts.Cel/Great.Parts.Cel.csproj) and `src/Great.Parts.Cel.Protobuf/Great.Parts.Cel.Protobuf.csproj`; warnings are errors in [Directory.Build.props](../Directory.Build.props) |
| Least common mechanism | No cache is global to the process: the cache of patterns belongs to one environment, the compilation cache to the application that creates it, and the state of one evaluation is a frame of its own. A built environment and a compiled program never change | [PatternCache.cs](../src/Great.Parts.Cel/Library/Regex/PatternCache.cs), [Frame.cs](../src/Great.Parts.Cel/Evaluation/Frame.cs); [ConcurrencyTests.cs](../tests/Great.Parts.Cel.Properties/Facade/ConcurrencyTests.cs) |
| Psychological acceptability | Errors carry a code of `CelErrorCode`, a stage and a position in the text, so an application can show the author of an expression what went wrong without exposing a stack trace. The cost of each operation is a published number, so a limit can be chosen from data, not guessed | [Error codes](error-codes.md), [Cost](cost.md) |
| Input validation | All untrusted input is validated where it enters: the text by the parser and the checker, data by the conversions and the checks of declared types, environment JSON by a strict reader, the content of `Any` by the parser of its registered type | sections below |

## Common weaknesses and their countermeasures

The weaknesses are those of the CWE list that apply to an engine that compiles and evaluates untrusted expressions inside the host process. Weaknesses of web applications, such as injection into SQL or HTML, do not apply: the library builds no queries and no markup.

### CWE-674 Uncontrolled recursion

A stack overflow ends a .NET process and cannot be caught, so the library must never reach one.

- **Claim.** The parser stops at 250 levels of nesting and at a syntax tree of depth 250 ([Limits](limits.md#parsing)). Macro expansion is not recursive and runs inside the guarded parse. Before each recursive step of parsing, type checking, planning, evaluation and the comparison of nested values, the library calls `RuntimeHelpers.TryEnsureSufficientExecutionStack()` and reports `InsufficientStack` (9000) instead of going deeper. The parser of patterns of `matches` and the build of their programs use no recursion at all, on a stack of their own.
- **Evidence.** Code: [Parser.cs](../src/Great.Parts.Cel/Syntax/Parser.cs), [Checker.cs](../src/Great.Parts.Cel/Checking/Checker.cs), [Planner.cs](../src/Great.Parts.Cel/Evaluation/Planner.cs), [PlanNode.cs](../src/Great.Parts.Cel/Evaluation/PlanNode.cs), [EqualityRules.cs](../src/Great.Parts.Cel/Values/EqualityRules.cs), [PatternParser.Stack.cs](../src/Great.Parts.Cel/Library/Regex/PatternParser.Stack.cs). Tests: `ParsingOnASmallStack_IsInsufficientStackInsteadOfAnOverflow` in [ParserLimitsTests.cs](../tests/Great.Parts.Cel.Tests/Syntax/ParserLimitsTests.cs); `EvaluationWithoutStack_IsInsufficientStackAtTheNodeThatDidNotDescend` in [StackAndInternalErrorTests.cs](../tests/Great.Parts.Cel.Tests/Examples/Evaluation/StackAndInternalErrorTests.cs); `DeeplyNestedValues_AreInsufficientStack` in [EqualityRulesTests.cs](../tests/Great.Parts.Cel.Tests/Values/EqualityRulesTests.cs); [NoRecursionTests.cs](../tests/Great.Parts.Cel.Tests/Regex/NoRecursionTests.cs) parses the deepest patterns on a 128 KB stack.
- **Limitation.** Within the limits of parsing, `InsufficientStack` happens only on a thread with a small stack; the application chooses the threads.

### CWE-400 Uncontrolled resource consumption, CWE-770 Allocation without limits

- **Claim, compilation.** The text is at most 65,536 UTF-8 bytes; a longer text is refused before parsing. Parsing stops after 100 errors. Parsing and type checking of the worst inputs the tests know — maximal nesting of each kind, long chains, lists of mismatched literals, nested comprehensions, a long selection chain in a container — take at most 1 s each on a standard GitHub-hosted Linux x64 runner, and CI fails otherwise. Compilation takes no cancellation token: its work is bounded by these limits.
- **Evidence.** Code: [Parser.cs](../src/Great.Parts.Cel/Syntax/Parser.cs). Tests: [ParserLimitsTests.cs](../tests/Great.Parts.Cel.Tests/Syntax/ParserLimitsTests.cs) at both sides of each limit; [WorstInputTests.cs](../tests/Great.Parts.Cel.Properties/Syntax/WorstInputTests.cs) and [CheckerWorstInputTests.cs](../tests/Great.Parts.Cel.Properties/Checking/CheckerWorstInputTests.cs) for the time; `NeverThrows` in [ParserProperties.cs](../tests/Great.Parts.Cel.Properties/Syntax/ParserProperties.cs), which also generates texts over each limit and checks the error and its position.
- **Limitation, compilation.** The bound of 1 s is measured on the inputs of the tests, not proven for every input. Planning, the last step of compilation, is not timed by these tests. Compilation is not charged against a cost limit; an application that compiles texts from many users bounds how many it compiles, and keeps compiled programs in a `CelCompilationCache`, whose two capacities it must set ([Compilation cache](limits.md#compilation-cache)).
- **Claim, evaluation.** With `CelEvalOptions.CostLimit` set, every operation is charged before it runs, by a formula of the size of its data where its time or memory grows with the data, and an operation whose charge would pass the limit does not run: the evaluation ends with `CostLimitExceeded` (3013). One unit stands for a constant number of steps and at most 64 bytes allocated beyond the frame of the evaluation. A total that does not fit the counter is over every limit. Functions of extension sets whose result could grow without bound have fixed limits of their own (`lists.range` up to 1,000,000 elements, a precision of `format` up to 100).
- **Evidence.** Code: `Charge` in [Frame.cs](../src/Great.Parts.Cel/Evaluation/Frame.cs), the handler of the evaluation in [Plan.cs](../src/Great.Parts.Cel/Evaluation/Plan.cs). Tests: [CostLimitTests.cs](../tests/Great.Parts.Cel.Tests/Evaluation/CostLimitTests.cs) (a total equal to the limit is allowed, the charge stops the operation before it runs, overflow of the counter); `Row_AllocatesWithinCost` in [CostTests.cs](../tests/Great.Parts.Cel.Properties/Evaluation/CostTests.cs) and in the cost tests of each library area under [tests/Great.Parts.Cel.Properties/Evaluation](../tests/Great.Parts.Cel.Properties/Evaluation) (`StandardCostTests.cs`, `ExtensionCostTests.cs`, `JsonDataCostTests.cs`, `ProtobufCostTests.cs`, `OptionalCostTests.cs`, `GeneratedDataCostTests.cs`, `DecimalDataCostTests.cs`) compares the bytes allocated with the units charged for every row of the cost table on data of sizes 0, 1, 10, 100 and 1,000 and fails when a row is above 64 bytes per unit ([Calibration](cost.md#calibration)); `Limit_StopsAllocationWithinCost` checks that a limit stops the allocation; the property `AllocationWithinCost` does the same on 1,500 generated well-typed expressions; [ExtensionLimitTests.cs](../tests/Great.Parts.Cel.Tests/Examples/Evaluation/ExtensionLimitTests.cs).
- **Claim, cancellation.** With a token that can be cancelled, the evaluation checks it before the first step of a macro or call of application code and then every 64 such events, and ends with `OperationCanceledException`.
- **Evidence.** `Event` in [Frame.cs](../src/Great.Parts.Cel/Evaluation/Frame.cs); `AfterTheSignal_AtMost64EventsBegin` and the other tests of [CancellationTests.cs](../tests/Great.Parts.Cel.Tests/Evaluation/CancellationTests.cs).
- **Limitation, evaluation.** Without a cost limit nothing is counted and an evaluation is not bounded. The memory bound of 64 bytes per unit is measured on the data sizes of these tests (up to 1,000 elements) and on the generated expressions, not proven. The time and memory of application functions are not measured: an application function declares its cost with a `CelCostFunction`, and one without it costs 1 unit a call. Cancellation is checked between the steps of macros and the calls of application code, not inside one operation, so one long operation on large data is stopped only by the cost limit. The conversion of bindings by `CelBindings.Add` before the evaluation and the conversion of the result to a .NET type after it are not charged. Data that the application binds — its size and its nesting — is the application's to bound.

### CWE-1333 Inefficient regular expression complexity (ReDoS)

- **Claim.** `matches` uses an engine of the library with RE2 syntax and semantics: a Thompson automaton run over two sparse sets of states, with no backtracking and no recursion. For a pattern whose program has size m and a string of n UTF-16 code units, it takes at most (n + 1) · m steps. Backreferences and lookaround are refused at compilation, as in RE2. A repetition count is at most 1,000, the product of nested counts at most 1,000, the height of the tree of the pattern below 1,000 and the size of the program at most 100,000; the limits are checked before repetitions are expanded. With a cost limit, the parse, the build and the search are each charged before they run. The .NET `Regex` is banned in the packages.
- **Evidence.** Code: [Matcher.cs](../src/Great.Parts.Cel/Library/Regex/Matcher.cs), [ProgramBuilder.cs](../src/Great.Parts.Cel/Library/Regex/ProgramBuilder.cs), [PatternSize.cs](../src/Great.Parts.Cel/Library/Regex/PatternSize.cs); design: [regex-engine](design/regex-engine.md). Tests: [RegexLinearityTests.cs](../tests/Great.Parts.Cel.Properties/Regex/RegexLinearityTests.cs) counts the steps of the engine on patterns that make a backtracking engine exponential, such as `(a*)*b` and `^(a+)+$`, on strings of 0 to 100,000 code units, and asserts the bound; [MatchesLimitTests.cs](../tests/Great.Parts.Cel.Tests/Examples/Library/MatchesLimitTests.cs) and [PatternSizeTests.cs](../tests/Great.Parts.Cel.Tests/Regex/PatternSizeTests.cs) test both sides of each limit; [RefusedConstructsTests.cs](../tests/Great.Parts.Cel.Tests/Regex/RefusedConstructsTests.cs). The rows of `matches` are on the page [Cost](cost.md#matches).
- **Limitation.** Linear is not small: a string of 10,000 characters against a pattern at the size limit takes about 1 s ([Pattern of `matches`](limits.md#pattern-of-matches)). Without a cost limit nothing stops such a call.

### CWE-407 Inefficient algorithmic complexity

- **Claim, string search.** `contains`, `indexOf`, `lastIndexOf`, `replace`, `split` and the other searches for a substring use a linear-time search of the library. The .NET methods whose worst case is O(n · m) are banned in the packages, with one exception: a pattern of at most 64 UTF-16 code units is searched by the vectorized ordinal search of .NET through one wrapper, where the worst case is 64 · n comparisons, linear with a constant factor; a longer pattern is searched by Knuth–Morris–Pratt.
- **Evidence.** [SubstringSearch.cs](../src/Great.Parts.Cel/Library/Text/SubstringSearch.cs); the section "Substring search" of [eng/BannedSymbols.txt](../eng/BannedSymbols.txt); [SubstringSearchLinearityTests.cs](../tests/Great.Parts.Cel.Properties/Library/SubstringSearchLinearityTests.cs) and [StringSearchLinearityTests.cs](../tests/Great.Parts.Cel.Properties/Library/StringSearchLinearityTests.cs) assert a bound of steps.
- **Claim, hash flooding.** The hash of a map key — an `int`, a `uint`, a `string` — is mixed with the random seed of the process, so keys cannot be chosen in advance to collide. A key lookup in a `JsonElement` object, which .NET does not index, is charged by the number of its properties.
- **Evidence.** [KeyHashRules.cs](../src/Great.Parts.Cel/Values/KeyHashRules.cs); `Hash_OfAnIntegralValue_IsNotTheHashOfItsBits` in [KeyRulesTests.cs](../tests/Great.Parts.Cel.Tests/Values/KeyRulesTests.cs); [Data of the application](cost.md#data-of-the-application).
- **Limitation.** A map that the application binds keeps its own dictionary and comparer; the library does not rehash it.

### CWE-248 Uncaught exception, CWE-755 Improper handling of exceptional conditions

- **Claim.** Parsing, type checking and planning, the step of `Compile` after type checking, run inside a boundary that turns an unexpected exception, other than `OutOfMemoryException`, into `InternalError` (9001) after the errors found before it. Evaluation has one handler at its root: an exceeded limit becomes `CostLimitExceeded`, any other exception but `OutOfMemoryException` and the cancellation of the evaluation's own token becomes `InternalError` at the node that was running. An exception of application code — a function, a cost function, a getter of an application object, a delegate of a runtime type — becomes `ApplicationException` (3014) with the original exception in `CelError.Exception`. A cancellation of another token inside application code is an `ApplicationException`, not a cancellation of the evaluation. An error of an operation is a value, not a thrown exception, so a stream of failing expressions costs no more than a stream of succeeding ones.
- **Evidence.** Code: `ParseWithinBoundary`, `Check` and `TryPlanWithinBoundary` in [CelEnvironment.cs](../src/Great.Parts.Cel/CelEnvironment.cs), `Run` and `IsInternal` in [Plan.cs](../src/Great.Parts.Cel/Evaluation/Plan.cs), `IsApplicationException` in [Frame.cs](../src/Great.Parts.Cel/Evaluation/Frame.cs). Tests: `NeverThrows` in [ParserProperties.cs](../tests/Great.Parts.Cel.Properties/Syntax/ParserProperties.cs) (random characters, random tokens, damaged and repeated grammatical texts, texts over each limit: no exception escapes and no error is `InternalError`) and in [CheckerProperties.cs](../tests/Great.Parts.Cel.Properties/Checking/CheckerProperties.cs); [PlanBoundaryTests.cs](../tests/Great.Parts.Cel.Tests/Facade/PlanBoundaryTests.cs) (an exception in planning is `InternalError` of the stage `Check` with the exception); `UnexpectedExceptionInsideEvaluation_IsInternalErrorWithTheException` in [StackAndInternalErrorTests.cs](../tests/Great.Parts.Cel.Tests/Examples/Evaluation/StackAndInternalErrorTests.cs); [ApplicationFunctionTests.cs](../tests/Great.Parts.Cel.Tests/Examples/Evaluation/ApplicationFunctionTests.cs); `CancellationOfAnotherToken_IsApplicationException` in [CancellationTests.cs](../tests/Great.Parts.Cel.Tests/Evaluation/CancellationTests.cs).
- **Limitation.** `OutOfMemoryException` passes through every boundary; with a cost limit the memory of an evaluation is bounded as above. The differential run below compares evaluation, not this property.

### CWE-94 Code injection, CWE-470 Unsafe reflection, CWE-502 Deserialization of untrusted data

- **Claim.** An expression cannot run code that the application did not register. The library compiles no code at run time — `LambdaExpression.Compile` is banned — and uses no reflection to read objects: objects of the application are described by the source generator at build time or by descriptions the application writes, and `Type.GetProperties`, `MethodBase.Invoke`, `Activator.CreateInstance` and `MakeGenericType` are banned. Type names in an expression resolve only to types that the environment declares. The content of a `google.protobuf.Any` is parsed only when its type URL names a message that the type source of the application registered, by the parser of that message, and a chain of `Any` deeper than 16 is an error. An environment description in JSON maps one to one onto calls of the builder: function declarations in it bind only to implementations that the application registered in code.
- **Evidence.** [eng/BannedSymbols.txt](../eng/BannedSymbols.txt); `IsAotCompatible` in [Great.Parts.Cel.csproj](../src/Great.Parts.Cel/Great.Parts.Cel.csproj) makes the trimming and Native AOT warnings build errors, and CI runs the tests of `Great.Parts.Cel.Tests` and `Great.Parts.Cel.Conformance` under Native AOT (the property tests and the tests of the generator run under JIT only); [AnyUnpacker.cs](../src/Great.Parts.Cel.Protobuf/AnyUnpacker.cs) with `ChainOfSixteenAny_Unpacks_AndTheSeventeenthIsRefused` and `UnpackIsChargedBeforeTheParse_AndAnUnsetAnyAddsNothing` in [AnyUnpackingTests.cs](../tests/Great.Parts.Cel.Tests/Protobuf/AnyUnpackingTests.cs); [Environment description in JSON](environment-json.md).
- **Limitation.** The parse of the content of an `Any` is charged by its length but relies on the parser of `Google.Protobuf` and its default limits for messages nested inside it.

### CWE-20 Improper input validation (environment descriptions)

- **Claim.** `CelEnvironmentJson.Apply` reads RFC 8259 JSON strictly with `Utf8JsonReader`: text that is not UTF-8, escaped lone surrogates, unknown and repeated keys, values of the wrong kind and nesting deeper than 64 levels are errors with their place. With any error the builder does not change.
- **Evidence.** [EnvironmentJsonFormatTests.cs](../tests/Great.Parts.Cel.Tests/Examples/Configuration/EnvironmentJsonFormatTests.cs), among them `NestingDeeperThan64Levels_IsAnError` and `WrongDescription_LeavesTheContainerFunctionsAndTypeSourcesAsTheyWere`; the property tests of [EnvironmentJsonTests.cs](../tests/Great.Parts.Cel.Properties/Configuration/EnvironmentJsonTests.cs).
- **Limitation.** The library sets no limit on the size of a description or on the number of declarations in it. An application that reads descriptions from users bounds their size itself.

### CWE-190 Integer overflow, CWE-681 Incorrect conversion between numeric types

- **Claim.** `int` and `uint` arithmetic outside 64 bits is the error `Overflow` (3006), not a wrap-around; conversions of `double` to `int` and `uint` outside their ranges, and `duration` and `timestamp` outside theirs, are `ConversionRange` (3007) or `Overflow` ([Values](limits.md#values)). The counter of the cost limit treats a total that does not fit as over every limit.
- **Evidence.** [ArithmeticTests.cs](../tests/Great.Parts.Cel.Tests/Library/ArithmeticTests.cs), [IntArithmeticTests.cs](../tests/Great.Parts.Cel.Tests/Examples/Library/IntArithmeticTests.cs), `CostThatDoesNotFitTheCounter_IsCostLimitExceeded` in [CostLimitTests.cs](../tests/Great.Parts.Cel.Tests/Evaluation/CostLimitTests.cs).

### CWE-362 Race condition

- **Claim.** A built environment and a compiled program never change, and every evaluation has a frame of its own, so one environment and one program are evaluated from any number of threads without synchronization, with the results of sequential calls. The caches of patterns and of compilations are safe for concurrent use and do not change a result or a cost.
- **Evidence.** `ParallelEqualsSequential` in [ConcurrencyTests.cs](../tests/Great.Parts.Cel.Properties/Facade/ConcurrencyTests.cs): 64 threads compile and evaluate the same texts, through the one-step call and a shared compilation cache, while the cache of patterns evicts; [PatternCacheTests.cs](../tests/Great.Parts.Cel.Tests/Regex/PatternCacheTests.cs).
- **Limitation.** Objects of the application that an evaluation reads are the application's: the library does not synchronize them.

### Memory safety

The library is managed code. It has no `unsafe` blocks, no pointers and no native calls (`DllImport`, `LibraryImport`). It uses `Unsafe.As` to pass a value of a generic parameter on without boxing, each time after one of two tests of the generic parameter: an exact comparison with the target type (`typeof(T) == typeof(X)`), or a `switch` on `Type.GetTypeCode(typeof(T))`, used for enums and their underlying type and for integer keys ([ConversionRules.cs](../src/Great.Parts.Cel/Values/ConversionRules.cs), [ConversionRules.Collections.cs](../src/Great.Parts.Cel/Values/ConversionRules.Collections.cs)). `MemoryMarshal` is used in three places: `TryGetMemoryManager` and `TryGetArray` to read the memory behind a `ReadOnlyMemory<byte>` ([CelValue.cs](../src/Great.Parts.Cel/Values/CelValue.cs)) and `Cast<char, ushort>` to view a string as UTF-16 code units ([Strings.cs](../src/Great.Parts.Cel/Library/Strings.cs)). These uses are reviewed as code, not checked by an analyzer.

### Results that depend on the host

- **Claim.** The library does not read the culture, the time zone or the clock of the process: `CultureInfo.CurrentCulture`, `TimeZoneInfo`, `DateTime.Now` and the other members listed under "Current culture" and "Time zones and clocks" in [eng/BannedSymbols.txt](../eng/BannedSymbols.txt) are build errors. Time zones come only from the database that the application gives; a zone it does not have is the error `TimeZoneNotFound` (3022). There is one exception, and the application chooses it: the database `CelTimeZoneDatabase.FromSystem()` reads the zones of the operating system through `TimeZoneInfo`, in one file of the library, and only for a name that passed the check of the form of an IANA identifier. Its offsets depend on the machine: with this database, and only with it, the same expression can give different results on two machines or after an update of the operating system ([Time zone database](recipes/time-zone-database.md#zones-of-the-operating-system)).
- **Evidence.** CI runs every test project on Ubuntu and macOS under JIT, and `Great.Parts.Cel.Tests` and `Great.Parts.Cel.Conformance` also on linux-x64 under Native AOT, for `net10.0` and `net11.0` ([ci.yml](../.github/workflows/ci.yml)); [TimeZoneNameTests.cs](../tests/Great.Parts.Cel.Tests/Examples/Library/TimeZoneNameTests.cs); [SystemTimeZoneTests.cs](../tests/Great.Parts.Cel.Tests/Examples/Values/SystemTimeZoneTests.cs) for the database of the operating system.

### Information exposure through error messages (CWE-209)

- **Claim.** An error message names the code, the stage, the place in the text and, where it helps, the names of a variable, a field or a type; a stack trace is only in `CelError.Exception` of `InternalError` and `ApplicationException`.
- **Evidence.** [Error codes](error-codes.md) lists the codes and the stage of each; `UnexpectedExceptionInsideEvaluation_IsInternalErrorWithTheException` in [StackAndInternalErrorTests.cs](../tests/Great.Parts.Cel.Tests/Examples/Evaluation/StackAndInternalErrorTests.cs) and [ApplicationFunctionTests.cs](../tests/Great.Parts.Cel.Tests/Examples/Evaluation/ApplicationFunctionTests.cs) show that the exception is kept in `CelError.Exception` and not put in the message.
- **Limitation.** Messages may quote parts of the expression and of the data, and `CelError.Exception` holds the exception of application code. An application that shows errors to the author of an expression decides what of them to show.

## What the application must do

The library leaves these to the application; the case above holds only when it does them.

| Duty | Why |
| :- | :- |
| Set `CelEvalOptions.CostLimit` for every evaluation of an untrusted expression | without it an evaluation is not bounded ([SECURITY.md](../SECURITY.md)) |
| Give every application function whose work grows with its arguments a `CelCostFunction` | the library does not measure application code |
| Pass a cancellation token with a deadline when wall-clock time matters | the cost limit bounds work, not time spent in application code |
| Bound the number of compilations per user and set the capacities of `CelCompilationCache` | compilation is not charged |
| Bound the size of data it binds and of environment descriptions it reads from users | the library charges work on data, not its size on entry |
| Decide what of an error message and of `CelError.Exception` to show to the author of an expression | messages may quote data |
| Use the latest release | only the latest release gets fixes ([Supported versions](../SECURITY.md#supported-versions)) |

Outside the scope of the library: timing side channels (no operation is constant-time, so an expression is not a place to compare secrets), access control over the data that the application binds, and the security of application code that expressions call.

## Verification that keeps the case true

| Check | What it keeps true | Where |
| :- | :- | :- |
| The build | warnings are errors, all .NET analyzers, nullable reference types, banned APIs (RS0030), trimming and AOT analyzers, the public API snapshot | [Directory.Build.props](../Directory.Build.props), [Great.Parts.Cel.csproj](../src/Great.Parts.Cel/Great.Parts.Cel.csproj) |
| Unit, property and conformance tests | the claims of this page; the cel-spec conformance suite at a pinned commit for the integrity of results ([Conformance](conformance.md)) | [ci.yml](../.github/workflows/ci.yml) on every pull request and push to `main`, on Ubuntu and macOS under JIT and, for the unit and conformance projects only, linux-x64 under Native AOT, for `net10.0` and `net11.0` |
| Differential run against cel-go | integrity of results on generated expressions; a release waits for a green run of at least two hours and for every difference to be analysed | [difftest-main.yml](../.github/workflows/difftest-main.yml) on every push to `main`, [difftest-nightly.yml](../.github/workflows/difftest-nightly.yml) every night; [known-differences.md](../tests/Great.Parts.Cel.Conformance/Regression/known-differences.md) |
| CodeQL | static analysis of the C# code | [codeql.yml](../.github/workflows/codeql.yml) on every pull request, push to `main` and every week |
| OpenSSF Scorecard | the security practices of the repository | [scorecard.yml](../.github/workflows/scorecard.yml) |
| Dependencies | `Great.Parts.Cel` has no package dependencies and `Great.Parts.Cel.Protobuf` only `Great.Parts.Cel` and `Google.Protobuf`, checked on the built packages; restore uses lock files and is locked in CI; Dependabot proposes updates every week | [eng/packages/Great.Parts.Cel.txt](../eng/packages/Great.Parts.Cel.txt), [eng/packages/Great.Parts.Cel.Protobuf.txt](../eng/packages/Great.Parts.Cel.Protobuf.txt), [dependabot.yml](../.github/dependabot.yml) |
| Release | attestation of origin of every `.nupkg` in the public log of Sigstore, Trusted Publishing to nuget.org with a short-lived token that exists only inside the publishing job, a second attestation of the files as nuget.org gives them, actions pinned by the SHA of a commit | [release.yml](../.github/workflows/release.yml); how to check a package: [README](../README.md#where-the-packages-come-from) |
| Sign-off | every commit of `main` carries `Signed-off-by` | [ci.yml](../.github/workflows/ci.yml); [CONTRIBUTING.md](../CONTRIBUTING.md) |

A change that adds a limit, changes a row of the cost table, adds a boundary or a new kind of input updates this page in the same change.
