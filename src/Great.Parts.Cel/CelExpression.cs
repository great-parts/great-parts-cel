using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel;

/// <summary>
/// The simple entry: evaluates an expression in one call, without building an environment, on data passed as an anonymous
/// object or as a dictionary of names and values. Variables are declared from the types of the data.
/// </summary>
/// <remarks>
/// <para>
/// <c>CelExpression.Evaluate&lt;long&gt;("(x + 2) / 3", new { x = 40L })</c> returns 14. The call builds a hidden environment
/// with the standard library version 1, without extension sets, without a time zone database (only <c>UTC</c> and fixed
/// offsets resolve) and without a name resolution container, declares one variable per property of the anonymous object or
/// per key of the dictionary, compiles the expression and evaluates it.
/// </para>
/// <para>
/// The simple entry reads only values that the
/// <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see> converts to CEL
/// without a type source: <see cref="bool"/>, integers, <see cref="double"/>, <see cref="string"/>, <c>byte[]</c> and
/// <c>ReadOnlyMemory&lt;byte&gt;</c>, <see cref="DateTimeOffset"/>, a <see cref="DateTime"/> of the kind
/// <see cref="DateTimeKind.Utc"/>, <see cref="TimeSpan"/>, <see cref="Guid"/>, <see cref="System.Text.Json.JsonElement"/>,
/// <see cref="Values.CelValue"/>, <see cref="Nullable{T}"/> of them, and lists and dictionaries with <see cref="string"/>
/// keys of these values. A class, a structure or an interface of the application is read as a property of an anonymous
/// object, <c>new { order }</c>, and so are an array, a <c>List&lt;X&gt;</c>, an <c>IReadOnlyList&lt;X&gt;</c>, an
/// <c>ImmutableArray&lt;X&gt;</c>, a <c>Dictionary&lt;string, X&gt;</c> and an <c>IReadOnlyDictionary&lt;string, X&gt;</c> of
/// such a type <c>X</c>: the source generator of the package describes the type and the types its members reach when the
/// application is built, with the rules of a type context, and the object is read by reference, where the expression reads it.
/// An object of the application as the data itself, as a value of a dictionary or in a place of the type
/// <see cref="object"/>, a <see cref="decimal"/> and a protobuf message need a type source or an extension set: in the simple
/// entry such a value is the error <see cref="CelErrorCode.TypeNotDeclared"/> with the name of the .NET type; build a
/// <see cref="CelEnvironment"/> for them. So is a list of objects of the application as a value of a dictionary, such as a
/// <c>List&lt;Order&gt;</c>: a list of values of the type <see cref="object"/> is read only when its element type is
/// exactly <see cref="object"/>.
/// </para>
/// <para>
/// Errors are thrown, unlike in the compilation and the evaluation of an environment, which return them in results: every error of
/// building, parsing, type checking and evaluation, including <see cref="CelErrorCode.CostLimitExceeded"/>, is a
/// <see cref="CelException"/> with all errors. The relation is that of <see cref="int.Parse(string)"/> to
/// <see cref="int.TryParse(string, out int)"/>.
/// </para>
/// <para>
/// Every call has a cost limit: by default <see cref="DefaultCostLimit"/>, at least 100 times the cost of each scenario of the
/// performance scenario set of the library. The limit bounds both the time and the memory of a call; it cannot be turned
/// off, but a call can pass any limit, up to <see cref="ulong.MaxValue"/>. The cost is always counted, so the simple entry
/// is not faster than an environment built once with programs compiled once.
/// </para>
/// <para>
/// The calls can be made from any number of threads at once. The entry keeps compiled environments and programs in a
/// hidden cache of the process: at most 64 environments, one per shape of the data, and at most 1,024 programs with a
/// total expression length of at most 100,000 UTF-16 code units; a longer text is compiled but not kept, and evicting an
/// environment removes its programs. The cache creates no threads and no timers and has no public members; an application
/// that needs control over caching uses <see cref="CelCompilationCache"/>. The page
/// <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/limits.md">Limits</see> gives the numbers and
/// how they were measured. A call finds the environment of an anonymous type and a program whose text is the same string
/// instance as in an earlier call (a string literal at the call site is one instance) without a lock and without hashing the
/// text; a text equal to a cached one in another string instance is found by its value, so the result of a call never depends on
/// which way it was found.
/// </para>
/// <para>
/// The registry of anonymous types and the cache hold the types that they have seen by strong references, so an assembly whose
/// code calls the simple entry with an anonymous object is not unloaded from a collectible
/// <see cref="System.Runtime.Loader.AssemblyLoadContext"/>, and neither is an assembly of a class that such an object has as a
/// property. For plug-ins that are unloaded, build a <see cref="CelEnvironment"/> and use a <see cref="CelCompilationCache"/>.
/// </para>
/// </remarks>
public static class CelExpression
{
    /// <summary>
    /// The cost limit of a call of the simple entry that passes no limit: 1,000,000 cost units of the
    /// <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/cost.md">cost model</see>.
    /// </summary>
    /// <remarks>
    /// One cost unit allocates at most 64 bytes, so a call within this limit allocates at most about 64 MB. The value is part
    /// of the compatibility contract of the cost: it changes only in a major version.
    /// </remarks>
    public const ulong DefaultCostLimit = 1_000_000;

    /// <summary>
    /// Evaluates an expression on variables given as a dictionary of names and values and returns the result converted to
    /// <typeparamref name="T"/>; every error is thrown.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each key is a variable whose CEL type comes from the run-time type of its value by the type mapping, so
    /// <c>{ ["x"] = 40L }</c> declares <c>x</c> as <c>int</c>. A value of the static type <see cref="object"/> that is
    /// <see langword="null"/> declares a variable of the type <c>dyn</c> bound to <c>null</c>. Without variables only
    /// literals and functions of the standard library are available.
    /// </para>
    /// <para>
    /// The hidden environment of a call is found by the ordered list of the names and CEL types of the variables, and the
    /// program by that environment, the text and <typeparamref name="T"/>: the same shape of data and text compiles once
    /// while the program stays in the cache of the simple entry.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">
    /// The .NET type of the result: a type of the result column of the
    /// <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>,
    /// <see cref="Values.CelValue"/> or <see cref="object"/>. It is also the expected type of the expression, as in
    /// <see cref="CelEnvironment.Compile{T}"/>.
    /// </typeparam>
    /// <param name="expression">The text of the expression.</param>
    /// <param name="variables">The values of the variables by name; <see langword="null"/> for none.</param>
    /// <param name="costLimit">
    /// The cost limit of the evaluation in cost units, <see cref="DefaultCostLimit"/> unless given; any value is allowed,
    /// including <see cref="ulong.MaxValue"/>.
    /// </param>
    /// <param name="cancellationToken">The token that cancels the evaluation.</param>
    /// <returns>The result converted to <typeparamref name="T"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="expression"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <typeparamref name="T"/> is not in the result column of the type mapping, or a key of
    /// <paramref name="variables"/> is empty, or a value is a <see cref="DateTime"/> whose <see cref="DateTime.Kind"/> is
    /// not <see cref="DateTimeKind.Utc"/>.
    /// </exception>
    /// <exception cref="CelException">
    /// A key of <paramref name="variables"/> is not the name of a variable (<see cref="CelErrorCode.InvalidVariableName"/>),
    /// the expression does not compile, a value has a type the simple entry does not read
    /// (<see cref="CelErrorCode.TypeNotDeclared"/> with the name of the .NET type), or the evaluation fails, including
    /// <see cref="CelErrorCode.CostLimitExceeded"/>. <see cref="CelException.Errors"/> holds all errors, and
    /// <see cref="Exception.InnerException"/> holds the exception of application code, if one caused an error.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    [SuppressMessage(
        "ApiDesign",
        "RS0026:Do not add multiple public overloads with optional parameters",
        Justification = "The overloads differ by the form of the data, and the cost limit and the token are optional in both; a dictionary argument binds to this overload by the better conversion.")]
    public static T Evaluate<T>(
        string expression,
        IReadOnlyDictionary<string, object?>? variables = null,
        ulong costLimit = DefaultCostLimit,
        CancellationToken cancellationToken = default)
    {
        return SimpleEntryCall.Evaluate<T>(SimpleEntryCache.Shared, expression, variables, costLimit, cancellationToken);
    }

    /// <summary>
    /// Evaluates an expression on the properties of an anonymous object and returns the result converted to
    /// <typeparamref name="T"/>; every error is thrown.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>CelExpression.Evaluate&lt;bool&gt;("total &gt; limit", new { total = 120L, limit = 100L })</c> declares the
    /// variables <c>total</c> and <c>limit</c> of the type <c>int</c>. The source generator of the package finds the call at
    /// build time and registers the anonymous type with its declarations and its reader in the application assembly, so the
    /// call reads the properties without reflection and works under Native AOT.
    /// </para>
    /// <para>
    /// The generator sees the static type of the argument. An anonymous object passed through a generic method or a
    /// variable of the type <see cref="object"/> is the error <see cref="CelErrorCode.TypeNotDeclared"/> with the name of
    /// the type only when the type is registered at no call site of the application and is not one of the dictionaries
    /// of the type mapping; the generator warns at build time. Anonymous types belong to one assembly, so the call works
    /// when an anonymous object of the same property names, types and order is passed directly to this method elsewhere
    /// in the same assembly. The name in the error is the one the compiler gave the anonymous type: its properties cannot
    /// be listed without reflection. The generator finds calls by the method, so <c>using static</c> and aliases are
    /// recognized, but not a call through a delegate to this method. A dictionary passed here is read as by the
    /// overload with a dictionary. A class or a structure of the application is not read as the data itself: pass it as a
    /// property of the anonymous object, <c>new { order }</c>, or build an environment with a type source and
    /// <see cref="CelEnvironmentBuilder.UseInput{TInput}"/>.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The .NET type of the result, as for the overload with a dictionary.</typeparam>
    /// <param name="expression">The text of the expression.</param>
    /// <param name="data">The anonymous object whose properties are the variables, or a dictionary of names and values.</param>
    /// <param name="costLimit">
    /// The cost limit of the evaluation in cost units, <see cref="DefaultCostLimit"/> unless given; any value is allowed,
    /// including <see cref="ulong.MaxValue"/>.
    /// </param>
    /// <param name="cancellationToken">The token that cancels the evaluation.</param>
    /// <returns>The result converted to <typeparamref name="T"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="expression"/> or <paramref name="data"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <typeparamref name="T"/> is not in the result column of the type mapping, or a property of
    /// <paramref name="data"/> is a <see cref="DateTime"/> whose <see cref="DateTime.Kind"/> is not
    /// <see cref="DateTimeKind.Utc"/>.
    /// </exception>
    /// <exception cref="CelException">
    /// The type of <paramref name="data"/> is not registered or a property has a type the simple entry does not read
    /// (<see cref="CelErrorCode.TypeNotDeclared"/> with the name of the .NET type), the expression does not compile, or the
    /// evaluation fails, including <see cref="CelErrorCode.CostLimitExceeded"/>. <see cref="CelException.Errors"/> holds all
    /// errors.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    [SuppressMessage(
        "ApiDesign",
        "RS0026:Do not add multiple public overloads with optional parameters",
        Justification = "The overloads differ by the form of the data, and the cost limit and the token are optional in both; the data parameter is required here.")]
    public static T Evaluate<T>(
        string expression,
        object data,
        ulong costLimit = DefaultCostLimit,
        CancellationToken cancellationToken = default)
    {
        return SimpleEntryCall.Evaluate<T>(SimpleEntryCache.Shared, expression, data, costLimit, cancellationToken);
    }
}
