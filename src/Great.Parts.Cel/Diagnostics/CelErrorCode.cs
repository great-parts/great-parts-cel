using System.Diagnostics.CodeAnalysis;

namespace Great.Parts.Cel.Diagnostics;

/// <summary>
/// The published list of error codes. Every <see cref="CelError"/> has exactly one code.
/// </summary>
/// <remarks>
/// <para>
/// Each code has an explicit number. The number range tells the stage: 1000–1999 parsing,
/// 2000–2999 type checking (including the preparation of constant arguments while the program is
/// planned), 3000–3999 evaluation, 4000–4999 building an environment, including an environment
/// description read from JSON, and 9000–9999 any stage, see <see cref="CelError.Stage"/>. When the
/// same kind of error can occur at two stages, each stage has its own code, for example
/// <see cref="NoMatchingOverload"/> and <see cref="EvalNoMatchingOverload"/>.
/// </para>
/// <para>
/// Adding a code is not a breaking change, so a <see langword="switch"/> over codes needs a default
/// branch. Removing a code or changing its meaning is a breaking change. Numbers are never reused.
/// </para>
/// <para>
/// There is no code with the value 0: <c>default(CelErrorCode)</c> is not a valid code, and no error
/// has it.
/// </para>
/// </remarks>
[SuppressMessage(
    "Design",
    "CA1008:Enums should have zero value",
    Justification = "Published error codes start at 1000 and are grouped by stage, like HTTP status codes; no error has the code 0.")]
public enum CelErrorCode
{
    // Parsing: 1000-1999.

    /// <summary>
    /// The text does not follow the CEL grammar together with the syntax enabled by the environment.
    /// The message says what was expected and what was found. Location: the token at which parsing
    /// could not continue.
    /// </summary>
    SyntaxError = 1000,

    /// <summary>
    /// A literal does not denote a value: an invalid escape sequence, an <c>int</c> or <c>uint</c>
    /// literal out of the 64-bit range, a <c>decimal</c> literal with more than 34 significant digits or
    /// with an exponent out of the decimal128 range. Location: the literal.
    /// </summary>
    InvalidLiteral = 1001,

    /// <summary>
    /// The expression text is longer than the limit on the length of an expression in UTF-8 bytes, given on the
    /// page <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/limits.md">Limits</see>. Location:
    /// the character at which the limit was exceeded.
    /// </summary>
    ExpressionTooLong = 1002,

    /// <summary>
    /// The syntactic nesting or the depth of the syntax tree exceeds its limit, given on the
    /// page <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/limits.md">Limits</see>. Chains of
    /// <c>&amp;&amp;</c> and <c>||</c> do not add depth. Location: the node at which the limit was exceeded.
    /// </summary>
    NestingTooDeep = 1003,

    /// <summary>
    /// Parsing stopped after the maximum number of syntax errors, given on the
    /// page <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/limits.md">Limits</see>; this
    /// error is added after the errors already found. Location: the character at which parsing stopped.
    /// </summary>
    TooManyErrors = 1004,

    /// <summary>
    /// A name is a reserved word of the CEL language definition, such as <c>package</c>, <c>if</c> or
    /// <c>return</c>. A call of a function with such a name on a receiver, such as
    /// <c>a.package()</c>, is allowed. Location: the identifier.
    /// </summary>
    [SuppressMessage(
        "Naming",
        "CA1700:Do not name enum values 'Reserved'",
        Justification = "The code reports an identifier that is a reserved word of CEL; it is not a placeholder for future use.")]
    ReservedIdentifier = 1005,

    /// <summary>
    /// A call has the form of a macro, but its arguments do not fit the macro: for example, the
    /// iteration variable of <c>all</c> is not a simple identifier, or the argument of <c>has</c> is not
    /// a field selection. Location: the call.
    /// </summary>
    InvalidMacroCall = 1006,

    /// <summary>
    /// Syntax that the library supports but the environment has not enabled: <c>.?</c>, <c>[?</c> and
    /// <c>{?</c> without the <c>optional</c> extension; a <c>decimal</c> literal such as <c>12.34m</c> or
    /// <c>100m</c> without the decimal literal option of the environment. The message says what to enable;
    /// on .NET 10 it says that <c>decimal</c> exists only on .NET 11. Location: the token.
    /// </summary>
    SyntaxNotEnabled = 1007,

    // Type checking: 2000-2999.

    /// <summary>
    /// A name is not declared as a variable, function or type of the environment, taking the name
    /// resolution container into account. Location: the identifier.
    /// </summary>
    UndeclaredReference = 2000,

    /// <summary>
    /// No overload of a function fits the static types of the arguments; also an ordering comparison
    /// of lists, maps or <c>null</c>. Location: the call.
    /// </summary>
    NoMatchingOverload = 2001,

    /// <summary>
    /// The type of a part of the expression is not assignable to the type the construct requires, for
    /// example a field value in a message literal and the type of the field. Location: the part of the
    /// expression.
    /// </summary>
    TypeMismatch = 2002,

    /// <summary>
    /// The type of the result is not assignable to the expected CEL type by the type checking rules of
    /// CEL. The expected type comes from the requested .NET type or is set in the compilation options.
    /// Location: the root of the expression.
    /// </summary>
    ExpectedTypeMismatch = 2003,

    /// <summary>
    /// The object type has no field with this name, or the type does not support field selection.
    /// The message names the field and the type. Location: the field selection.
    /// </summary>
    NoSuchField = 2004,

    /// <summary>
    /// The range of a comprehension macro is not a <c>list</c>, a <c>map</c> or <c>dyn</c>.
    /// Location: the range.
    /// </summary>
    InvalidComprehensionRange = 2005,

    /// <summary>
    /// A name in the position of a type name — a message literal or a type literal — does not denote a
    /// type. Location: the name.
    /// </summary>
    NotAType = 2006,

    /// <summary>
    /// A member of an application type is not available to expressions: the source generator excluded
    /// it (its kind of .NET type is not read, or it is marked obsolete or experimental, among other
    /// reasons), or it is described as <c>decimal</c> and the <c>decimal</c> extension is not added to
    /// the environment. The message gives the reason; for <c>decimal</c> it names the extension.
    /// Location: the field selection.
    /// </summary>
    FieldNotAccessible = 2007,

    /// <summary>
    /// An object literal for a .NET type or for a type described at run time; object literals are
    /// supported only for protobuf messages. Location: the literal.
    /// </summary>
    TypeNotConstructible = 2008,

    /// <summary>
    /// A constant <c>matches</c> pattern is invalid: an RE2 syntax error, a construct that RE2 does not support, or a
    /// pattern over a limit given on the
    /// page <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/limits.md#pattern-of-matches">Limits</see>.
    /// The message gives the offset in the pattern and the kind of error; the syntax is on the page
    /// <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/standard-library.md#matches">Standard library</see>.
    /// The pattern is compiled while the program is planned. Location: the pattern literal.
    /// </summary>
    InvalidRegex = 2009,

    /// <summary>
    /// A call of <c>format</c> of the <c>strings</c> extension whose format string is a string constant and
    /// whose argument is a list literal is invalid: an unknown format specifier, a precision above 100, a
    /// specifier without a list element, an element whose type does not fit its specifier, or more elements
    /// than specifiers. A call with a format string or a list that is not a literal is not checked at
    /// compilation; a format error found during evaluation is <see cref="InvalidArgument"/>. Location: the
    /// call of <c>format</c>; for an element whose type does not fit its specifier, that element of the list literal.
    /// </summary>
    InvalidFormatString = 2010,

    // Evaluation: 3000-3999.

    /// <summary>
    /// Among the overloads that type checking accepted, none fits the types of the argument values
    /// (an operand of type <c>dyn</c>); also an ordering comparison of lists, maps or <c>null</c>, and a
    /// <c>decimal</c> with another numeric type. The runtime error <c>no_matching_overload</c> of the
    /// language definition. Location: the call.
    /// </summary>
    EvalNoMatchingOverload = 3000,

    /// <summary>
    /// A map or a message has no such field when selecting <c>e.f</c> or testing <c>has(e.f)</c>, or a
    /// value of type <c>dyn</c> turned out to be an object without such a field. The runtime error
    /// <c>no_such_field</c> of the language definition. Location: the field selection.
    /// </summary>
    EvalNoSuchField = 3001,

    /// <summary>
    /// A map is indexed with a key it does not contain. Location: the index.
    /// </summary>
    NoSuchKey = 3002,

    /// <summary>
    /// A list index is out of bounds, or an index or an offset of a function of the <c>strings</c> set is. Location: the index of a list, the call of a function of the set.
    /// </summary>
    IndexOutOfRange = 3003,

    /// <summary>
    /// Division of an <c>int</c>, <c>uint</c> or <c>decimal</c> by zero. Location: the division.
    /// </summary>
    DivisionByZero = 3004,

    /// <summary>
    /// The remainder of an <c>int</c> or <c>uint</c> by zero. Location: the modulus.
    /// </summary>
    ModulusByZero = 3005,

    /// <summary>
    /// The result of arithmetic is out of the range of its type: <c>int</c>, <c>uint</c>, <c>duration</c>,
    /// <c>timestamp</c> or <c>decimal</c>. The ranges are on the
    /// page <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/limits.md">Limits</see>. Location:
    /// the call.
    /// </summary>
    Overflow = 3006,

    /// <summary>
    /// A conversion whose result is out of the range of the target type: <c>int(double)</c>,
    /// <c>uint(int)</c>, <c>int(decimal)</c>, <c>timestamp(int)</c> and others, and a time string that
    /// is well formed but denotes a <c>timestamp</c> or <c>duration</c> out of range; a number of a
    /// <c>JsonElement</c> that does not fit a finite <c>double</c>; a value of application data out of
    /// the range of its CEL type. Location: the call or the node that read the value.
    /// </summary>
    ConversionRange = 3007,

    /// <summary>
    /// An argument of the right type with an invalid value: a string that does not parse as
    /// <c>int</c>, <c>double</c>, <c>decimal</c>, <c>timestamp</c> or <c>duration</c>, including a
    /// constant such as <c>duration('…')</c>, which is reported at evaluation; an invalid argument of
    /// an extension function. A time string that parses but is out of range is
    /// <see cref="ConversionRange"/>. Location: the call.
    /// </summary>
    InvalidArgument = 3008,

    /// <summary>
    /// A variable declared in the environment has no value in the bindings. The error occurs only when
    /// the expression reads the variable. Location: the identifier.
    /// </summary>
    Unbound = 3009,

    /// <summary>
    /// A map literal repeats a key; a <c>JsonElement</c> object repeats a property name, reported by
    /// any operation that touches that name; the macro <c>transformMapEntry</c> of the extension set of two-variable
    /// comprehensions puts a key into its result that an earlier step put in. Location: the literal, the operation or the call
    /// of the macro.
    /// </summary>
    DuplicateMapKey = 3010,

    /// <summary>
    /// The value bound to a variable does not have the type declared for the variable. The message
    /// names the variable. Location: the identifier.
    /// </summary>
    BindingTypeMismatch = 3011,

    /// <summary>
    /// A value has a .NET type that no type source of the environment describes, where the type is
    /// needed: an operation needs the description of an object — to select a field or test its presence
    /// by name, for <c>type</c>, or to compare two objects with <c>==</c>, <c>!=</c> or <c>in</c> — or a
    /// <c>dyn</c> position holds a .NET collection — an instance of a type that implements
    /// <c>IEnumerable</c>, other than <c>string</c>, a byte array,
    /// <c>CelList</c> and <c>CelMap</c>, which no type source describes by its
    /// type or a base class; an instance that a source describes is an object even if it is enumerable, and
    /// an element of a collection of the application is described only where its description is needed — or
    /// a <c>System.Decimal</c> or <c>Decimal128</c> value
    /// when the environment has no <c>decimal</c> extension: on .NET 11 without the extension and on
    /// .NET 10. Reading or passing an object on needs no description, and checking a value against a
    /// declared type never gives this code: an instance without a description that does not belong to
    /// the declared type is a mismatch of that place, such as <see cref="BindingTypeMismatch"/>. The
    /// message names the .NET type. Location: for an object, the node that needs its description; for a
    /// collection or a decimal value, the node that read the value.
    /// </summary>
    TypeNotAccessible = 3012,

    /// <summary>
    /// Charging the cost of the next operation would exceed the cost limit of the evaluation options; the operation is
    /// not performed. Costs are given on the
    /// page <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/cost.md">Cost</see>. Location: the
    /// node of the operation.
    /// </summary>
    CostLimitExceeded = 3013,

    /// <summary>
    /// Application code called during evaluation threw an exception: a function implementation, a
    /// member getter of an application object, a delegate of a type described at run time, or a
    /// <c>JsonElement</c> whose document the application has already disposed. The original exception
    /// is in <see cref="CelError.Exception"/>. Location: the call or the field selection.
    /// </summary>
    /// <remarks>
    /// <see cref="System.OperationCanceledException"/> of the canceled token of the call and
    /// <see cref="System.OutOfMemoryException"/> are not caught.
    /// </remarks>
    ApplicationException = 3014,

    /// <summary>
    /// The value of the result cannot be converted to the requested .NET type by
    /// the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>,
    /// for example a <c>dyn</c> result that is a string when <c>bool</c> was requested, or a <c>decimal</c> out of the
    /// range of <see cref="decimal"/>. A list or a map that does not fit the type that <c>T</c> expects, at any depth, is
    /// refused before a lazy view of it is built; the message names the type of the first element, key or value that does
    /// not fit, such as <c>list(int)</c>, and a <c>null</c> in a position of <c>string</c>, <c>byte[]</c>,
    /// <see cref="Great.Parts.Cel.Values.CelList"/>, <see cref="Great.Parts.Cel.Values.CelMap"/> or
    /// <see cref="Great.Parts.Cel.Types.CelType"/> is not a mismatch and is not in the type. Location: the root of the expression.
    /// </summary>
    ResultConversion = 3015,

    /// <summary>
    /// The value of an empty <c>optional</c> is read, as in <c>optional.none().value()</c>.
    /// Location: the call.
    /// </summary>
    OptionalHasNoValue = 3016,

    /// <summary>
    /// A member of a reference type annotated as not nullable turned out to be <see langword="null"/>.
    /// The message names the type and the member. Location: the field selection.
    /// </summary>
    UnexpectedNull = 3017,

    /// <summary>
    /// A field of a type described at run time is read while it has no value. Location: the field
    /// selection.
    /// </summary>
    FieldNotSet = 3018,

    /// <summary>
    /// The value of a field of a type described at run time does not have the declared type of the
    /// field. Location: the field selection.
    /// </summary>
    FieldTypeMismatch = 3019,

    /// <summary>
    /// A <c>google.protobuf.Any</c> has an unknown type URL or content that cannot be parsed.
    /// Location: the node that unpacked the value.
    /// </summary>
    InvalidAny = 3020,

    /// <summary>
    /// The same as <see cref="InvalidRegex"/> for a <c>matches</c> pattern that is not a constant and is
    /// compiled during evaluation; a refused pattern is refused at every evaluation, from the cache of patterns of the
    /// environment as well. Location: the call of <c>matches</c>.
    /// </summary>
    EvalInvalidRegex = 3021,

    /// <summary>
    /// The time zone name is not in the time zone database of the environment, including the names
    /// <c>""</c> and <c>Local</c>; if the environment has no database, the message says so. The error
    /// occurs at evaluation even for a constant time zone name. Location: the call.
    /// </summary>
    TimeZoneNotFound = 3022,

    /// <summary>
    /// A time zone name that contains <c>:</c> is not a fixed offset <c>[+-]HH:MM</c> with hours 00–23 and
    /// minutes 00–59. Location: the call.
    /// </summary>
    InvalidTimeZone = 3023,

    /// <summary>
    /// A value cannot be passed to a parameter of the .NET delegate that implements an application function, by the
    /// result rows of
    /// the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>:
    /// for example, an <c>int</c> that does not fit an <see cref="int"/> parameter. The message names the function, the
    /// overload identifier and the parameter. Location: the call.
    /// </summary>
    ArgumentConversion = 3024,

    /// <summary>
    /// A field selection <c>e.f</c> or a test <c>has(e.f)</c> on a value that is not a map, a message or an
    /// object with described fields, found during evaluation because the operand has the type <c>dyn</c>.
    /// At type checking the same case is <see cref="NoSuchField"/>. Location: the field selection.
    /// </summary>
    FieldSelectionNotSupported = 3025,

    /// <summary>
    /// The range of a comprehension macro — a standard macro, a two-variable macro or <c>sortBy</c> — is
    /// not a list or a map, found during evaluation because the range has the type <c>dyn</c>. The
    /// evaluation counterpart of <see cref="InvalidComprehensionRange"/>. Location: the range.
    /// </summary>
    EvalInvalidComprehensionRange = 3026,

    /// <summary>
    /// A key of a map literal evaluates to a value that is not an <c>int</c>, a <c>uint</c>, a <c>bool</c> or
    /// a <c>string</c>, such as a <c>double</c>, <c>null</c> or a <c>decimal</c>: map keys can have only these
    /// types. Type checking does not reject such a literal. Location: the key of the literal.
    /// </summary>
    UnsupportedMapKeyType = 3027,

    /// <summary>
    /// The implementation of an application function returned a value whose kind does not fit the declared
    /// result type of the overload. A result of type <c>dyn</c> is not checked; an error value that the
    /// implementation returns is propagated as an error, not reported with this code. The message names the
    /// function and the overload identifier. Location: the call.
    /// </summary>
    ImplementationResultMismatch = 3028,

    // Building an environment: 4000-4999. Not every build error is a conflict: a message about a
    // conflict names both conflicting sources, while others, such as UnknownExtensionVersion (4004) or
    // InvalidVariableName (4016), concern one declaration.

    /// <summary>
    /// Two different variable declarations have the same name. The message names both sources.
    /// </summary>
    DuplicateVariable = 4000,

    /// <summary>
    /// Two different descriptions have the same CEL type name, or one .NET type is described by two
    /// type sources. Identical descriptions from two generated type contexts are one declaration, not an
    /// error. The message names both sources.
    /// </summary>
    DuplicateType = 4001,

    /// <summary>
    /// The signatures of two overloads of a function are the same or overlap after the erasure of
    /// type parameters. The message names both overloads and their sources.
    /// </summary>
    OverlappingOverloads = 4002,

    /// <summary>
    /// One extension is added in two versions. Adding the same version twice is one declaration, not
    /// an error.
    /// </summary>
    ExtensionVersionConflict = 4003,

    /// <summary>
    /// The extension has no such version. The message lists the available versions.
    /// </summary>
    UnknownExtensionVersion = 4004,

    /// <summary>
    /// A declaration requires an extension that is not added to the environment: an extension that
    /// depends on another one, or the decimal literal option without the <c>decimal</c> extension.
    /// Dependencies are never added implicitly. The message names both. A variable of a .NET
    /// <see cref="decimal"/> type without the <c>decimal</c> extension is <see cref="TypeNotDeclared"/>.
    /// </summary>
    MissingExtensionDependency = 4005,

    /// <summary>
    /// A declaration of the application conflicts with the selected version of the standard library:
    /// an overload that is the same as or overlaps a standard overload, a variable or type with the
    /// name of a standard identifier, or a function with the name and form of a macro.
    /// </summary>
    StandardLibraryConflict = 4006,

    /// <summary>
    /// An overload is declared without an implementation; implementations are bound by overload
    /// identifier, as for declarations read from an environment description in JSON.
    /// </summary>
    MissingImplementation = 4007,

    /// <summary>
    /// The type of a variable, field or overload — a .NET type or a CEL type name — is not declared by
    /// any type source or extension added to the environment. For a .NET <see cref="decimal"/> type the
    /// message names the <c>decimal</c> extension; on .NET 10 it says that <c>decimal</c> exists only on
    /// .NET 11. The type name <c>any</c> of an environment description needs the protobuf type provider.
    /// </summary>
    TypeNotDeclared = 4008,

    /// <summary>
    /// An environment description in JSON cannot be read: it is not valid JSON, or it has an unknown or
    /// repeated key, a value of the wrong kind, an extension version <c>latest</c> or an empty version,
    /// or a key of the cel-go format that this library does not support, which the message names.
    /// The position in the JSON text is in <see cref="CelError.SourceLocation"/>; the builder is not
    /// changed.
    /// </summary>
    InvalidConfiguration = 4009,

    /// <summary>
    /// An implementation is registered for an overload identifier that no declaration has.
    /// </summary>
    UnusedImplementation = 4010,

    /// <summary>
    /// The signature of an implementation does not fit the declaration of its overload: the number of parameters differs,
    /// or the CEL type of a result or of a parameter that the type mapping gives for its .NET type is not the declared one
    /// after the type parameters are erased, as with <c>IReadOnlyList&lt;long&gt;</c> for <c>list(int)</c>, which fits,
    /// and <see cref="Great.Parts.Cel.Values.CelList"/> for it, which does not. A .NET type that has no CEL type as a
    /// parameter, such as <c>Guid</c>, <c>short</c>, an array, an enumeration or a typed collection deeper than
    /// one step, does not fit either. A declared <c>wrapper(string)</c> or <c>wrapper(bytes)</c> also takes
    /// <c>string</c> and <c>byte[]</c>, for a parameter and for a result.
    /// </summary>
    ImplementationSignatureMismatch = 4011,

    /// <summary>
    /// A different time zone database is added to an environment that already has one; the database is
    /// never replaced silently.
    /// </summary>
    TimeZoneDatabaseConflict = 4012,

    /// <summary>
    /// One overload identifier is used by two different overloads. An identifier is unique in the whole
    /// environment: the declarations of the application, an environment description in JSON, the
    /// extension sets and the selected version of the standard library. The same overload declared twice —
    /// the same function, identifier and signature — is one declaration, not an error. The message names
    /// both sources.
    /// </summary>
    DuplicateOverloadId = 4013,

    /// <summary>
    /// More than one implementation is registered for one overload identifier.
    /// </summary>
    DuplicateImplementation = 4014,

    /// <summary>
    /// The name of a function that the application declares — directly, in an extension set it builds, or
    /// in an environment description in JSON — is not an identifier or a qualified name of identifiers
    /// separated by dots. Operators and internal functions, such as <c>_+_</c>, <c>_[_]</c> or <c>@in</c>,
    /// cannot be declared: CEL does not allow overloading operators.
    /// </summary>
    InvalidFunctionName = 4015,

    /// <summary>
    /// The name of a variable that the application declares — in code or in an environment description in
    /// JSON — is not an identifier or a qualified name of identifiers separated by dots, by the same rule as
    /// <see cref="InvalidFunctionName"/>. An empty name in code is an invalid argument, and in JSON it is
    /// <see cref="InvalidConfiguration"/>.
    /// </summary>
    InvalidVariableName = 4016,

    /// <summary>
    /// The name resolution container is set to two different non-empty values, for example one in code and
    /// another in an environment description in JSON; the container is never replaced silently. The same
    /// value set twice is one declaration, and the empty string declares no container. The message names
    /// both sources.
    /// </summary>
    ContainerConflict = 4017,

    /// <summary>
    /// Two different extension sets with the same name and version are added: a set of the catalog and a set
    /// the application built, or two sets the application built. The same set — a set of the catalog in the
    /// same version, or the same instance — added twice is one set; one set in two versions is
    /// <see cref="ExtensionVersionConflict"/>. The message names both sources.
    /// </summary>
    DuplicateExtension = 4018,

    // Any stage: 9000-9999.

    /// <summary>
    /// The thread has too little stack left for the next recursive step of parsing, macro expansion,
    /// type checking, planning, evaluation or comparison of nested values. The library checks the stack
    /// before each such step instead of failing with a stack overflow, which cannot be caught.
    /// Location: the node at which the step was not performed.
    /// </summary>
    InsufficientStack = 9000,

    /// <summary>
    /// An unexpected exception inside the library, other than <see cref="System.OutOfMemoryException"/>.
    /// It is reported as an error of the stage at which it happened, so a defect of the library does not
    /// escape as an exception; the original exception is in <see cref="CelError.Exception"/>.
    /// Location: during parsing, type checking and evaluation, the node that was being processed when the
    /// exception was thrown; errors of the environment build have no position in an expression.
    /// </summary>
    InternalError = 9001,
}
