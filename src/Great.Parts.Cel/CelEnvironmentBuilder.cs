using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel;

/// <summary>
/// Collects the declarations of an environment — variables, functions with their .NET implementations,
/// type sources, extension sets, the time zone database and the name resolution container — and builds
/// an immutable <see cref="CelEnvironment"/> from them.
/// </summary>
/// <remarks>
/// <para>
/// The builder collects declarations as a set. <see cref="Build"/> checks the whole set at once, so the
/// order of the calls changes neither the environment nor the text of an error. Conflicts are build
/// errors, not exceptions, and each names both sources, such as "application" and
/// "extension strings v5": two different variables or types with one name, overloads that are the same or
/// overlap, one extension set in two versions, two different extension sets with one name and version,
/// two different name resolution containers, a declaration that conflicts with the standard library.
/// Nothing is replaced silently. The methods throw only when a documented condition of the call is
/// violated, such as a <see langword="null"/> argument.
/// </para>
/// <para>
/// An environment description read from JSON with <c>CelEnvironmentJson.Apply</c> calls the same methods,
/// so an environment described in JSON is the same as one described by the same calls in code.
/// </para>
/// <para>
/// The builder is mutable and is used from one thread. Build an environment once and share it: the
/// environment and its programs are immutable and thread-safe. <see cref="CelEnvironment.ToBuilder"/>
/// returns a builder with the declarations of an environment, for an environment that adds declarations.
/// </para>
/// </remarks>
public sealed class CelEnvironmentBuilder
{
    private readonly CelStandardLibrary _standardLibrary;
    private readonly List<CelVariableDecl> _variables = [];
    private readonly List<CelFunctionDecl> _functions = [];
    private readonly List<ImplementationEntry> _implementations = [];

    // The non-empty containers, each once.
    private readonly List<string> _containers = [];

    // Declarations whose .NET type is outside the input rows, which Build reports as TypeNotDeclared.
    private readonly List<UnmappedDeclaration> _unmapped = [];

    /// <summary>
    /// Creates a builder for an environment with the given version of the standard library.
    /// </summary>
    /// <remarks>
    /// The version is required: the declarations of a version never change, so an environment keeps its
    /// meaning when the library is updated.
    /// </remarks>
    /// <param name="standardLibrary">The version of the standard library, such as <see cref="CelStandardLibrary.V1"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="standardLibrary"/> is <see langword="null"/>.</exception>
    public CelEnvironmentBuilder(CelStandardLibrary standardLibrary)
    {
        ArgumentNullException.ThrowIfNull(standardLibrary);
        _standardLibrary = standardLibrary;
    }

    /// <summary>
    /// Declares the name resolution container of the environment, such as <c>acme.sales</c>. Without it the
    /// environment has no container.
    /// </summary>
    /// <remarks>
    /// <para>
    /// As in the CEL language definition, a name in an expression is first looked up qualified by the
    /// container and its parents, then as written: in the container <c>acme.sales</c> the name
    /// <c>Order</c> can mean <c>acme.sales.Order</c>, <c>acme.Order</c> or <c>Order</c>, the longest
    /// declared name first.
    /// </para>
    /// <para>
    /// The container is a declaration like the others, and it is never replaced silently. The same value
    /// declared twice, for example in code and in an environment description in JSON, is one declaration; the
    /// empty string declares no container. Two different non-empty values are a build error
    /// <see cref="CelErrorCode.ContainerConflict"/> that names both sources.
    /// </para>
    /// </remarks>
    /// <param name="container">The container: a qualified name, identifiers separated by dots; or the empty string, which declares nothing.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="container"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="container"/> is neither empty nor a qualified name.</exception>
    public CelEnvironmentBuilder UseContainer(string container)
    {
        ArgumentNullException.ThrowIfNull(container);
        if (container.Length == 0)
        {
            return this;
        }

        if (!DeclarationNames.IsQualifiedIdentifier(container))
        {
            throw new ArgumentException("The container '" + container + "' is not a qualified name.", nameof(container));
        }

        if (!_containers.Contains(container))
        {
            _containers.Add(container);
        }

        return this;
    }

    /// <summary>
    /// Declares a variable whose CEL type comes from the .NET type <typeparamref name="T"/> by
    /// the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>,
    /// for example <c>long</c> as <c>int</c>, <c>DateTimeOffset</c> as <c>timestamp</c>,
    /// <c>IReadOnlyList&lt;string&gt;</c> as <c>list(string)</c>, and a .NET type described by a type source added with
    /// <see cref="AddTypes"/> as its object type.
    /// </summary>
    /// <remarks>
    /// A .NET type that maps to no CEL type and that no type source of the environment describes is a
    /// build error <see cref="CelErrorCode.TypeNotDeclared"/> that names the variable. So is
    /// <see cref="decimal"/> on .NET 10, and on .NET 11 when the <c>decimal</c> extension set is not added;
    /// the message names the extension set. A name that is not an identifier or a qualified name is a build
    /// error <see cref="CelErrorCode.InvalidVariableName"/>. The other build errors of a variable are listed
    /// on <see cref="CelVariableDecl"/>.
    /// </remarks>
    /// <typeparam name="T">The .NET type of the values the variable gets in the bindings.</typeparam>
    /// <param name="name">The name expressions use to read the variable.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    public CelEnvironmentBuilder AddVariable<T>(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (ConversionRules.InputType<T>() is not CelType type)
        {
            AddUnmapped(new UnmappedDeclaration(name, null, typeof(T)));
            return this;
        }

        return AddVariable(name, type);
    }

    /// <summary>
    /// Declares a variable with an explicit CEL type, for example <see cref="CelType.Dyn"/>, or the type of an
    /// object type described at run time, <c>AddVariable("deal", dealType.Type)</c>.
    /// </summary>
    /// <remarks>
    /// A name that is not an identifier or a qualified name is a build error
    /// <see cref="CelErrorCode.InvalidVariableName"/>. The other build errors of a variable are listed on
    /// <see cref="CelVariableDecl"/>.
    /// </remarks>
    /// <param name="name">The name expressions use to read the variable.</param>
    /// <param name="type">The CEL type of the variable.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="type"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    public CelEnvironmentBuilder AddVariable(string name, CelType type) => AddVariable(new CelVariableDecl(name, type));

    /// <summary>
    /// Declares a variable.
    /// </summary>
    /// <remarks>The build errors of a variable are listed on <see cref="CelVariableDecl"/>.</remarks>
    /// <param name="declaration">The declaration of the variable.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="declaration"/> is <see langword="null"/>.</exception>
    public CelEnvironmentBuilder AddVariable(CelVariableDecl declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        if (!_variables.Exists(declaration.SameAs))
        {
            _variables.Add(declaration);
        }

        return this;
    }

    /// <summary>
    /// Declares a function without arguments, called as <c>f()</c>, and implements it with a .NET delegate. The CEL
    /// result type comes from <typeparamref name="TResult"/> by
    /// the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The delegate forms declare one overload called globally. Its identifier is the function name followed by the CEL
    /// type of each parameter in order, each preceded by <c>_</c> and written without spaces as in an environment
    /// description in JSON: <c>isInn_string</c>, <c>sum_list&lt;int&gt;</c>, <c>check_acme.sales.Order</c>; without
    /// parameters the identifier is the function name. Error messages show the identifier, and a different overload
    /// with the same identifier, for example one declared in an environment description in JSON, is a build error
    /// <see cref="CelErrorCode.DuplicateOverloadId"/>. The result enters the expression by the input rows of the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>.
    /// A .NET type that maps to no CEL type and that no type source describes is a build error
    /// <see cref="CelErrorCode.TypeNotDeclared"/>. A call costs 1 cost unit; an overload that declares its cost, an
    /// overload with parameters of type <c>dyn</c> or type parameters, and an overload called on a receiver need
    /// <see cref="AddFunction(CelFunctionDecl)"/>. The rules for the function name are on
    /// <see cref="CelFunctionDecl"/>.
    /// </para>
    /// <para>
    /// The implementation runs synchronously inside evaluation, can be called from several threads at once, and an
    /// exception it throws becomes the evaluation error
    /// <see cref="CelErrorCode.ApplicationException"/>. Do not block in it on asynchronous work — <c>Task.Result</c>, <c>Task.Wait()</c>: a thread blocked on I/O starves the thread pool of ASP.NET Core and hangs in <c>browser-wasm</c>. Load data before the evaluation and pass it in the bindings, as the recipe <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/recipes/application-functions.md">Application functions</see>
    /// shows.
    /// </para>
    /// </remarks>
    /// <typeparam name="TResult">The .NET type of the result.</typeparam>
    /// <param name="name">The name of the function.</param>
    /// <param name="implementation">The implementation.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="implementation"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    public CelEnvironmentBuilder AddFunction<TResult>(string name, Func<TResult> implementation)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(implementation);
        return AddDelegate(name, implementation, [], Clr<TResult>(), _ => TypedImplementation.Of(implementation));
    }

    /// <summary>
    /// Declares a function with one argument, called as <c>f(a)</c>, and implements it with a .NET delegate, for
    /// example <c>AddFunction("isInn", static (string value) =&gt; Inn.IsValid(value))</c>. The CEL types come from the
    /// delegate signature by
    /// the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The delegate forms declare one overload called globally. Its identifier is the function name followed by the CEL
    /// type of each parameter in order, each preceded by <c>_</c> and written without spaces as in an environment
    /// description in JSON: <c>isInn_string</c>, <c>sum_list&lt;int&gt;</c>, <c>check_acme.sales.Order</c>; without
    /// parameters the identifier is the function name. Error messages show the identifier, and a different overload
    /// with the same identifier, for example one declared in an environment description in JSON, is a build error
    /// <see cref="CelErrorCode.DuplicateOverloadId"/>. An argument is passed to the delegate by the result rows of the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>;
    /// a value that does not convert, such as an <c>int</c> out of the range of an
    /// <see cref="int"/> parameter, is the evaluation error <see cref="CelErrorCode.ArgumentConversion"/>. The result enters the expression by the input rows of the type mapping. A .NET type that maps to no CEL type and that no type source describes is a build error <see cref="CelErrorCode.TypeNotDeclared"/>. A call costs 1 cost unit, plus the units of each argument that is copied to convert it for the delegate: a list of n elements 1 + n, a map of n pairs 1 + 2n and ⌈L/10⌉ for each string key of length L, a <c>byte[]</c> of L bytes 1 + ⌈L/10⌉, as the page <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/cost.md">Cost</see>
    /// gives; an overload that declares its cost, an overload with parameters of type <c>dyn</c> or type parameters,
    /// and an overload called on a receiver need <see cref="AddFunction(CelFunctionDecl)"/>. The rules for the function
    /// name are on <see cref="CelFunctionDecl"/>.
    /// </para>
    /// <para>
    /// The implementation runs synchronously inside evaluation, can be called from several threads at once, and an
    /// exception it throws becomes the evaluation error
    /// <see cref="CelErrorCode.ApplicationException"/>. Do not block in it on asynchronous work — <c>Task.Result</c>, <c>Task.Wait()</c>: a thread blocked on I/O starves the thread pool of ASP.NET Core and hangs in <c>browser-wasm</c>. Load data before the evaluation and pass it in the bindings, as the recipe <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/recipes/application-functions.md">Application functions</see>
    /// shows.
    /// </para>
    /// </remarks>
    /// <typeparam name="T1">The .NET type of the argument.</typeparam>
    /// <typeparam name="TResult">The .NET type of the result.</typeparam>
    /// <param name="name">The name of the function.</param>
    /// <param name="implementation">The implementation.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="implementation"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    public CelEnvironmentBuilder AddFunction<T1, TResult>(string name, Func<T1, TResult> implementation)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(implementation);
        return AddDelegate(name, implementation, [Clr<T1>()], Clr<TResult>(), id => TypedImplementation.Of(id, implementation));
    }

    /// <summary>
    /// Declares a function with two arguments, called as <c>f(a, b)</c>, and implements it with a .NET delegate. The
    /// CEL types come from the delegate signature by
    /// the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The delegate forms declare one overload called globally. Its identifier is the function name followed by the CEL
    /// type of each parameter in order, each preceded by <c>_</c> and written without spaces as in an environment
    /// description in JSON: <c>isInn_string</c>, <c>sum_list&lt;int&gt;</c>, <c>check_acme.sales.Order</c>; without
    /// parameters the identifier is the function name. Error messages show the identifier, and a different overload
    /// with the same identifier, for example one declared in an environment description in JSON, is a build error
    /// <see cref="CelErrorCode.DuplicateOverloadId"/>. Arguments are passed to the delegate by the result rows of the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>;
    /// a value that does not convert is the evaluation error
    /// <see cref="CelErrorCode.ArgumentConversion"/>. The result enters the expression by the input rows of the type mapping. A .NET type that maps to no CEL type and that no type source describes is a build error <see cref="CelErrorCode.TypeNotDeclared"/>. A call costs 1 cost unit, plus the units of each argument that is copied to convert it for the delegate: a list of n elements 1 + n, a map of n pairs 1 + 2n and ⌈L/10⌉ for each string key of length L, a <c>byte[]</c> of L bytes 1 + ⌈L/10⌉, as the page <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/cost.md">Cost</see>
    /// gives; other overloads need <see cref="AddFunction(CelFunctionDecl)"/>. The rules for the function name are on
    /// <see cref="CelFunctionDecl"/>.
    /// </para>
    /// <para>
    /// The implementation runs synchronously inside evaluation, can be called from several threads at once, and an
    /// exception it throws becomes the evaluation error
    /// <see cref="CelErrorCode.ApplicationException"/>. Do not block in it on asynchronous work — <c>Task.Result</c>, <c>Task.Wait()</c>: a thread blocked on I/O starves the thread pool of ASP.NET Core and hangs in <c>browser-wasm</c>. Load data before the evaluation and pass it in the bindings, as the recipe <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/recipes/application-functions.md">Application functions</see>
    /// shows.
    /// </para>
    /// </remarks>
    /// <typeparam name="T1">The .NET type of the first argument.</typeparam>
    /// <typeparam name="T2">The .NET type of the second argument.</typeparam>
    /// <typeparam name="TResult">The .NET type of the result.</typeparam>
    /// <param name="name">The name of the function.</param>
    /// <param name="implementation">The implementation.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="implementation"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    public CelEnvironmentBuilder AddFunction<T1, T2, TResult>(string name, Func<T1, T2, TResult> implementation)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(implementation);
        return AddDelegate(name, implementation, [Clr<T1>(), Clr<T2>()], Clr<TResult>(), id => TypedImplementation.Of(id, implementation));
    }

    /// <summary>
    /// Declares a function with three arguments, called as <c>f(a, b, c)</c>, and implements it with a .NET delegate.
    /// The CEL types come from the delegate signature by
    /// the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The delegate forms declare one overload called globally. Its identifier is the function name followed by the CEL
    /// type of each parameter in order, each preceded by <c>_</c> and written without spaces as in an environment
    /// description in JSON: <c>isInn_string</c>, <c>sum_list&lt;int&gt;</c>, <c>check_acme.sales.Order</c>; without
    /// parameters the identifier is the function name. Error messages show the identifier, and a different overload
    /// with the same identifier, for example one declared in an environment description in JSON, is a build error
    /// <see cref="CelErrorCode.DuplicateOverloadId"/>. Arguments are passed to the delegate by the result rows of the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>;
    /// a value that does not convert is the evaluation error
    /// <see cref="CelErrorCode.ArgumentConversion"/>. The result enters the expression by the input rows of the type mapping. A .NET type that maps to no CEL type and that no type source describes is a build error <see cref="CelErrorCode.TypeNotDeclared"/>. A call costs 1 cost unit, plus the units of each argument that is copied to convert it for the delegate: a list of n elements 1 + n, a map of n pairs 1 + 2n and ⌈L/10⌉ for each string key of length L, a <c>byte[]</c> of L bytes 1 + ⌈L/10⌉, as the page <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/cost.md">Cost</see>
    /// gives; other overloads need <see cref="AddFunction(CelFunctionDecl)"/>. The rules for the function name are on
    /// <see cref="CelFunctionDecl"/>.
    /// </para>
    /// <para>
    /// The implementation runs synchronously inside evaluation, can be called from several threads at once, and an
    /// exception it throws becomes the evaluation error
    /// <see cref="CelErrorCode.ApplicationException"/>. Do not block in it on asynchronous work — <c>Task.Result</c>, <c>Task.Wait()</c>: a thread blocked on I/O starves the thread pool of ASP.NET Core and hangs in <c>browser-wasm</c>. Load data before the evaluation and pass it in the bindings, as the recipe <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/recipes/application-functions.md">Application functions</see>
    /// shows.
    /// </para>
    /// </remarks>
    /// <typeparam name="T1">The .NET type of the first argument.</typeparam>
    /// <typeparam name="T2">The .NET type of the second argument.</typeparam>
    /// <typeparam name="T3">The .NET type of the third argument.</typeparam>
    /// <typeparam name="TResult">The .NET type of the result.</typeparam>
    /// <param name="name">The name of the function.</param>
    /// <param name="implementation">The implementation.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="implementation"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    public CelEnvironmentBuilder AddFunction<T1, T2, T3, TResult>(string name, Func<T1, T2, T3, TResult> implementation)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(implementation);
        return AddDelegate(name, implementation, [Clr<T1>(), Clr<T2>(), Clr<T3>()], Clr<TResult>(), id => TypedImplementation.Of(id, implementation));
    }

    /// <summary>
    /// Declares a function with four arguments, called as <c>f(a, b, c, d)</c>, and implements it with a .NET delegate.
    /// The CEL types come from the delegate signature by
    /// the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The delegate forms declare one overload called globally. Its identifier is the function name followed by the CEL
    /// type of each parameter in order, each preceded by <c>_</c> and written without spaces as in an environment
    /// description in JSON: <c>isInn_string</c>, <c>sum_list&lt;int&gt;</c>, <c>check_acme.sales.Order</c>; without
    /// parameters the identifier is the function name. Error messages show the identifier, and a different overload
    /// with the same identifier, for example one declared in an environment description in JSON, is a build error
    /// <see cref="CelErrorCode.DuplicateOverloadId"/>. Arguments are passed to the delegate by the result rows of the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>;
    /// a value that does not convert is the evaluation error
    /// <see cref="CelErrorCode.ArgumentConversion"/>. The result enters the expression by the input rows of the type mapping. A .NET type that maps to no CEL type and that no type source describes is a build error <see cref="CelErrorCode.TypeNotDeclared"/>. A call costs 1 cost unit, plus the units of each argument that is copied to convert it for the delegate: a list of n elements 1 + n, a map of n pairs 1 + 2n and ⌈L/10⌉ for each string key of length L, a <c>byte[]</c> of L bytes 1 + ⌈L/10⌉, as the page <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/cost.md">Cost</see>
    /// gives; functions with more arguments and other overloads need <see cref="AddFunction(CelFunctionDecl)"/>. The
    /// rules for the function name are on <see cref="CelFunctionDecl"/>.
    /// </para>
    /// <para>
    /// The implementation runs synchronously inside evaluation, can be called from several threads at once, and an
    /// exception it throws becomes the evaluation error
    /// <see cref="CelErrorCode.ApplicationException"/>. Do not block in it on asynchronous work — <c>Task.Result</c>, <c>Task.Wait()</c>: a thread blocked on I/O starves the thread pool of ASP.NET Core and hangs in <c>browser-wasm</c>. Load data before the evaluation and pass it in the bindings, as the recipe <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/recipes/application-functions.md">Application functions</see>
    /// shows.
    /// </para>
    /// </remarks>
    /// <typeparam name="T1">The .NET type of the first argument.</typeparam>
    /// <typeparam name="T2">The .NET type of the second argument.</typeparam>
    /// <typeparam name="T3">The .NET type of the third argument.</typeparam>
    /// <typeparam name="T4">The .NET type of the fourth argument.</typeparam>
    /// <typeparam name="TResult">The .NET type of the result.</typeparam>
    /// <param name="name">The name of the function.</param>
    /// <param name="implementation">The implementation.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="implementation"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    public CelEnvironmentBuilder AddFunction<T1, T2, T3, T4, TResult>(string name, Func<T1, T2, T3, T4, TResult> implementation)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(implementation);
        return AddDelegate(name, implementation, [Clr<T1>(), Clr<T2>(), Clr<T3>(), Clr<T4>()], Clr<TResult>(), id => TypedImplementation.Of(id, implementation));
    }

    /// <summary>
    /// Declares a function by its full declaration: for overloads with parameters of type <c>dyn</c>,
    /// generic overloads, overloads called on a receiver, overloads that declare their cost, and functions
    /// whose implementations are registered separately, as for an environment description read from JSON.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each overload is implemented by the implementation registered under its identifier with
    /// <see cref="AddFunctionImplementation(string, CelFunctionImplementation)"/> or a typed overload of that
    /// method; the order of the two calls does not matter. An overload without an implementation is a
    /// build error <see cref="CelErrorCode.MissingImplementation"/>. The other build errors are listed on
    /// <see cref="CelFunctionDecl"/> and <see cref="CelOverloadDecl"/>.
    /// </para>
    /// <para>
    /// The implementations run synchronously inside evaluation. Do not block in them on asynchronous work —
    /// <c>Task.Result</c>, <c>Task.Wait()</c>: a thread blocked on I/O starves the thread pool of ASP.NET Core and
    /// hangs in <c>browser-wasm</c>. Load data before the evaluation and pass it in the bindings, as the
    /// recipe <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/recipes/application-functions.md">Application functions</see>
    /// shows.
    /// </para>
    /// </remarks>
    /// <param name="declaration">The declaration of the function.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="declaration"/> is <see langword="null"/>.</exception>
    public CelEnvironmentBuilder AddFunction(CelFunctionDecl declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        if (!_functions.Contains(declaration))
        {
            _functions.Add(declaration);
        }

        return this;
    }

    /// <summary>
    /// Registers the implementation of a declared overload by its identifier; the implementation receives
    /// the CEL values of the arguments as they are.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An implementation whose identifier no declared overload has is a build error
    /// <see cref="CelErrorCode.UnusedImplementation"/>, which catches a misspelled identifier; two
    /// implementations with one identifier are a build error <see cref="CelErrorCode.DuplicateImplementation"/>.
    /// </para>
    /// <para>
    /// The implementation runs synchronously inside evaluation. Do not block in it on asynchronous work —
    /// <c>Task.Result</c>, <c>Task.Wait()</c>: a thread blocked on I/O starves the thread pool of ASP.NET Core and
    /// hangs in <c>browser-wasm</c>. Load data before the evaluation and pass it in the bindings; see
    /// <see cref="CelFunctionImplementation"/> and the recipe <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/recipes/application-functions.md">Application functions</see>.
    /// </para>
    /// </remarks>
    /// <param name="overloadId">The identifier of the overload.</param>
    /// <param name="implementation">The implementation.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="overloadId"/> or <paramref name="implementation"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="overloadId"/> is empty.</exception>
    public CelEnvironmentBuilder AddFunctionImplementation(string overloadId, CelFunctionImplementation implementation)
    {
        ArgumentException.ThrowIfNullOrEmpty(overloadId);
        ArgumentNullException.ThrowIfNull(implementation);
        return AddImplementation(new ImplementationEntry(overloadId, implementation, TypedImplementation.Of(implementation)));
    }

    /// <summary>
    /// Registers a typed .NET delegate as the implementation of a declared overload without arguments.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The CEL types of the delegate, by
    /// the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>,
    /// must fit the declared overload; otherwise it is a build error
    /// <see cref="CelErrorCode.ImplementationSignatureMismatch"/>. An implementation whose identifier no declared
    /// overload has is a build error <see cref="CelErrorCode.UnusedImplementation"/>; two implementations with one
    /// identifier are a build error <see cref="CelErrorCode.DuplicateImplementation"/>.
    /// </para>
    /// <para>
    /// The implementation runs synchronously inside evaluation. Do not block in it on asynchronous work —
    /// <c>Task.Result</c>, <c>Task.Wait()</c>: a thread blocked on I/O starves the thread pool of ASP.NET Core and
    /// hangs in <c>browser-wasm</c>. Load data before the evaluation and pass it in the bindings, as the
    /// recipe <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/recipes/application-functions.md">Application functions</see>
    /// shows.
    /// </para>
    /// </remarks>
    /// <typeparam name="TResult">The .NET type of the result.</typeparam>
    /// <param name="overloadId">The identifier of the overload.</param>
    /// <param name="implementation">The implementation.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="overloadId"/> or <paramref name="implementation"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="overloadId"/> is empty.</exception>
    public CelEnvironmentBuilder AddFunctionImplementation<TResult>(string overloadId, Func<TResult> implementation)
    {
        ArgumentException.ThrowIfNullOrEmpty(overloadId);
        ArgumentNullException.ThrowIfNull(implementation);
        return AddTyped(
            overloadId,
            implementation,
            TypedImplementation.Of(implementation),
            [],
            ConversionRules.InputType<TResult>(),
            [],
            ConversionRules.TakesNullReference<TResult>());
    }

    /// <summary>
    /// Registers a typed .NET delegate as the implementation of a declared overload with one argument, for
    /// example <c>AddFunctionImplementation("isInn_string", static (string s) =&gt; Inn.IsValid(s))</c>. For an
    /// overload called on a receiver, the receiver is the argument.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The CEL types of the delegate, by
    /// the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>,
    /// must fit the declared overload; otherwise it is a build error
    /// <see cref="CelErrorCode.ImplementationSignatureMismatch"/>. An argument that does not convert to the parameter
    /// type at evaluation is the evaluation error <see cref="CelErrorCode.ArgumentConversion"/>. An implementation
    /// whose identifier no declared overload has is a build error <see cref="CelErrorCode.UnusedImplementation"/>; two
    /// implementations with one identifier are a build error <see cref="CelErrorCode.DuplicateImplementation"/>.
    /// </para>
    /// <para>
    /// The implementation runs synchronously inside evaluation. Do not block in it on asynchronous work —
    /// <c>Task.Result</c>, <c>Task.Wait()</c>: a thread blocked on I/O starves the thread pool of ASP.NET Core and
    /// hangs in <c>browser-wasm</c>. Load data before the evaluation and pass it in the bindings, as the
    /// recipe <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/recipes/application-functions.md">Application functions</see>
    /// shows.
    /// </para>
    /// </remarks>
    /// <typeparam name="T1">The .NET type of the argument.</typeparam>
    /// <typeparam name="TResult">The .NET type of the result.</typeparam>
    /// <param name="overloadId">The identifier of the overload.</param>
    /// <param name="implementation">The implementation.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="overloadId"/> or <paramref name="implementation"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="overloadId"/> is empty.</exception>
    public CelEnvironmentBuilder AddFunctionImplementation<T1, TResult>(string overloadId, Func<T1, TResult> implementation)
    {
        ArgumentException.ThrowIfNullOrEmpty(overloadId);
        ArgumentNullException.ThrowIfNull(implementation);
        return AddTyped(
            overloadId,
            implementation,
            TypedImplementation.Of(overloadId, implementation),
            [ConversionRules.ParameterType<T1>()],
            ConversionRules.InputType<TResult>(),
            [ConversionRules.TakesNullReference<T1>()],
            ConversionRules.TakesNullReference<TResult>());
    }

    /// <summary>
    /// Registers a typed .NET delegate as the implementation of a declared overload with two arguments. For
    /// an overload called on a receiver, the receiver is the first argument.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The CEL types of the delegate, by
    /// the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>,
    /// must fit the declared overload; otherwise it is a build error
    /// <see cref="CelErrorCode.ImplementationSignatureMismatch"/>. An argument that does not convert to the parameter
    /// type at evaluation is the evaluation error <see cref="CelErrorCode.ArgumentConversion"/>. An implementation
    /// whose identifier no declared overload has is a build error <see cref="CelErrorCode.UnusedImplementation"/>; two
    /// implementations with one identifier are a build error <see cref="CelErrorCode.DuplicateImplementation"/>.
    /// </para>
    /// <para>
    /// The implementation runs synchronously inside evaluation. Do not block in it on asynchronous work —
    /// <c>Task.Result</c>, <c>Task.Wait()</c>: a thread blocked on I/O starves the thread pool of ASP.NET Core and
    /// hangs in <c>browser-wasm</c>. Load data before the evaluation and pass it in the bindings, as the
    /// recipe <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/recipes/application-functions.md">Application functions</see>
    /// shows.
    /// </para>
    /// </remarks>
    /// <typeparam name="T1">The .NET type of the first argument.</typeparam>
    /// <typeparam name="T2">The .NET type of the second argument.</typeparam>
    /// <typeparam name="TResult">The .NET type of the result.</typeparam>
    /// <param name="overloadId">The identifier of the overload.</param>
    /// <param name="implementation">The implementation.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="overloadId"/> or <paramref name="implementation"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="overloadId"/> is empty.</exception>
    public CelEnvironmentBuilder AddFunctionImplementation<T1, T2, TResult>(string overloadId, Func<T1, T2, TResult> implementation)
    {
        ArgumentException.ThrowIfNullOrEmpty(overloadId);
        ArgumentNullException.ThrowIfNull(implementation);
        return AddTyped(
            overloadId,
            implementation,
            TypedImplementation.Of(overloadId, implementation),
            [ConversionRules.ParameterType<T1>(), ConversionRules.ParameterType<T2>()],
            ConversionRules.InputType<TResult>(),
            [ConversionRules.TakesNullReference<T1>(), ConversionRules.TakesNullReference<T2>()],
            ConversionRules.TakesNullReference<TResult>());
    }

    /// <summary>
    /// Registers a typed .NET delegate as the implementation of a declared overload with three arguments.
    /// For an overload called on a receiver, the receiver is the first argument.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The CEL types of the delegate, by
    /// the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>,
    /// must fit the declared overload; otherwise it is a build error
    /// <see cref="CelErrorCode.ImplementationSignatureMismatch"/>. An argument that does not convert to the parameter
    /// type at evaluation is the evaluation error <see cref="CelErrorCode.ArgumentConversion"/>. An implementation
    /// whose identifier no declared overload has is a build error <see cref="CelErrorCode.UnusedImplementation"/>; two
    /// implementations with one identifier are a build error <see cref="CelErrorCode.DuplicateImplementation"/>.
    /// </para>
    /// <para>
    /// The implementation runs synchronously inside evaluation. Do not block in it on asynchronous work —
    /// <c>Task.Result</c>, <c>Task.Wait()</c>: a thread blocked on I/O starves the thread pool of ASP.NET Core and
    /// hangs in <c>browser-wasm</c>. Load data before the evaluation and pass it in the bindings, as the
    /// recipe <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/recipes/application-functions.md">Application functions</see>
    /// shows.
    /// </para>
    /// </remarks>
    /// <typeparam name="T1">The .NET type of the first argument.</typeparam>
    /// <typeparam name="T2">The .NET type of the second argument.</typeparam>
    /// <typeparam name="T3">The .NET type of the third argument.</typeparam>
    /// <typeparam name="TResult">The .NET type of the result.</typeparam>
    /// <param name="overloadId">The identifier of the overload.</param>
    /// <param name="implementation">The implementation.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="overloadId"/> or <paramref name="implementation"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="overloadId"/> is empty.</exception>
    public CelEnvironmentBuilder AddFunctionImplementation<T1, T2, T3, TResult>(string overloadId, Func<T1, T2, T3, TResult> implementation)
    {
        ArgumentException.ThrowIfNullOrEmpty(overloadId);
        ArgumentNullException.ThrowIfNull(implementation);
        return AddTyped(
            overloadId,
            implementation,
            TypedImplementation.Of(overloadId, implementation),
            [ConversionRules.ParameterType<T1>(), ConversionRules.ParameterType<T2>(), ConversionRules.ParameterType<T3>()],
            ConversionRules.InputType<TResult>(),
            [ConversionRules.TakesNullReference<T1>(), ConversionRules.TakesNullReference<T2>(), ConversionRules.TakesNullReference<T3>()],
            ConversionRules.TakesNullReference<TResult>());
    }

    /// <summary>
    /// Registers a typed .NET delegate as the implementation of a declared overload with four arguments.
    /// For an overload called on a receiver, the receiver is the first argument.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The CEL types of the delegate, by
    /// the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>,
    /// must fit the declared overload; otherwise it is a build error
    /// <see cref="CelErrorCode.ImplementationSignatureMismatch"/>. An argument that does not convert to the parameter
    /// type at evaluation is the evaluation error <see cref="CelErrorCode.ArgumentConversion"/>. An implementation
    /// whose identifier no declared overload has is a build error <see cref="CelErrorCode.UnusedImplementation"/>; two
    /// implementations with one identifier are a build error <see cref="CelErrorCode.DuplicateImplementation"/>.
    /// </para>
    /// <para>
    /// The implementation runs synchronously inside evaluation. Do not block in it on asynchronous work —
    /// <c>Task.Result</c>, <c>Task.Wait()</c>: a thread blocked on I/O starves the thread pool of ASP.NET Core and
    /// hangs in <c>browser-wasm</c>. Load data before the evaluation and pass it in the bindings, as the
    /// recipe <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/recipes/application-functions.md">Application functions</see>
    /// shows.
    /// </para>
    /// </remarks>
    /// <typeparam name="T1">The .NET type of the first argument.</typeparam>
    /// <typeparam name="T2">The .NET type of the second argument.</typeparam>
    /// <typeparam name="T3">The .NET type of the third argument.</typeparam>
    /// <typeparam name="T4">The .NET type of the fourth argument.</typeparam>
    /// <typeparam name="TResult">The .NET type of the result.</typeparam>
    /// <param name="overloadId">The identifier of the overload.</param>
    /// <param name="implementation">The implementation.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="overloadId"/> or <paramref name="implementation"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="overloadId"/> is empty.</exception>
    public CelEnvironmentBuilder AddFunctionImplementation<T1, T2, T3, T4, TResult>(string overloadId, Func<T1, T2, T3, T4, TResult> implementation)
    {
        ArgumentException.ThrowIfNullOrEmpty(overloadId);
        ArgumentNullException.ThrowIfNull(implementation);
        return AddTyped(
            overloadId,
            implementation,
            TypedImplementation.Of(overloadId, implementation),
            [ConversionRules.ParameterType<T1>(), ConversionRules.ParameterType<T2>(), ConversionRules.ParameterType<T3>(), ConversionRules.ParameterType<T4>()],
            ConversionRules.InputType<TResult>(),
            [
                ConversionRules.TakesNullReference<T1>(),
                ConversionRules.TakesNullReference<T2>(),
                ConversionRules.TakesNullReference<T3>(),
                ConversionRules.TakesNullReference<T4>(),
            ],
            ConversionRules.TakesNullReference<TResult>());
    }

    /// <summary>
    /// Adds a source of object and enumeration types: a type context completed by the source generator,
    /// types described at run time, or the protobuf message types of the protobuf package.
    /// </summary>
    /// <remarks>
    /// Expressions can read only the types of the sources added to the environment. All sources are checked
    /// together when the environment is built, and build errors name a source by
    /// <see cref="CelTypeProvider.SourceName"/>; the rules and build errors are on
    /// <see cref="CelTypeProvider"/>.
    /// </remarks>
    /// <param name="provider">The type source, for example the <c>Default</c> instance of a type context.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <see langword="null"/>.</exception>
    public CelEnvironmentBuilder AddTypes(CelTypeProvider provider)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Adds an extension set with its explicit version, such as <c>CelExtensions.Strings(version: 5)</c>.
    /// </summary>
    /// <remarks>
    /// The rules and build errors of extension sets are on <see cref="CelExtension"/>.
    /// </remarks>
    /// <param name="extension">The extension set.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="extension"/> is <see langword="null"/>.</exception>
    public CelEnvironmentBuilder AddExtension(CelExtension extension)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Adds the IANA time zone database that functions taking a time zone, such as
    /// <c>getHours('Europe/Moscow')</c>, use in this environment.
    /// </summary>
    /// <remarks>
    /// Without a database only <c>UTC</c> and fixed offsets resolve; any other zone name is the evaluation
    /// error <see cref="CelErrorCode.TimeZoneNotFound"/>. A different database added to the same environment
    /// is a build error <see cref="CelErrorCode.TimeZoneDatabaseConflict"/>: a database is never replaced
    /// silently. An environment description in JSON does not carry the database; add it in code.
    /// </remarks>
    /// <param name="database">The time zone database.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="database"/> is <see langword="null"/>.</exception>
    public CelEnvironmentBuilder UseTimeZones(CelTimeZoneDatabase database)
    {
        throw new NotImplementedException();
    }

#if NET11_0_OR_GREATER
    /// <summary>
    /// Enables <c>decimal</c> literals in expressions: a number in decimal notation with the suffix
    /// <c>m</c> or <c>M</c>, with a fraction or without one, such as <c>12.34m</c>, <c>100m</c>, <c>.5m</c>,
    /// <c>1e3m</c> or <c>2.5E+3M</c>. Only on .NET 11.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A literal keeps its exact digits and exponent: <c>string(1.50m)</c> is <c>"1.50"</c>, and
    /// <c>string(100m)</c> is <c>"100"</c>. A literal with more than 34 significant digits or an exponent out
    /// of the decimal128 range is the parse error <see cref="CelErrorCode.InvalidLiteral"/>. There is no
    /// hexadecimal form: <c>0x10m</c> is a syntax error. A minus sign is the unary operator, as for other
    /// numbers.
    /// </para>
    /// <para>
    /// The literals need the <c>decimal</c> extension set, <c>CelExtensions.Decimal(version: 1)</c>: the
    /// option without it is a build error <see cref="CelErrorCode.MissingExtensionDependency"/>. Without the
    /// option, a decimal literal is the parse error <see cref="CelErrorCode.SyntaxNotEnabled"/>, which names
    /// the option; the function <c>decimal('12.34')</c> needs only the extension set.
    /// </para>
    /// </remarks>
    /// <returns>This builder.</returns>
    public CelEnvironmentBuilder EnableDecimalLiterals()
    {
        throw new NotImplementedException();
    }
#endif

    /// <summary>
    /// Builds an immutable environment from the declarations collected so far and checks them all at once.
    /// </summary>
    /// <remarks>
    /// The result holds either the environment or all build errors found, each with the stage
    /// <see cref="CelErrorStage.Build"/>; an error about a conflict names both conflicting sources.
    /// Building does not change the builder.
    /// </remarks>
    /// <returns>The environment or the build errors.</returns>
    public CelBuildResult Build()
    {
        var declarations = new DeclarationSet([.. _variables], [.. _functions], [.. _implementations], [.. _containers], [.. _unmapped]);
        LibraryDeclarations library = _standardLibrary.Declarations;
        ImmutableArray<CelError> errors = DeclarationChecks.Check(
            declarations,
            library,
            out ImmutableArray<CelVariableDecl> variables,
            out ImmutableArray<OverloadBinding> overloads,
            out string container);
        if (!errors.IsEmpty)
        {
            return new(errors);
        }

        DeclarationIndex index = DeclarationIndex.Create([.. library.Overloads, .. overloads], variables, library.TypeIdentifiers, container);
        return new(new CelEnvironment(_standardLibrary, declarations, index));
    }

    // A builder with the declarations of an environment.
    internal CelEnvironmentBuilder(CelStandardLibrary standardLibrary, DeclarationSet declarations)
        : this(standardLibrary)
    {
        _variables.AddRange(declarations.Variables);
        _functions.AddRange(declarations.Functions);
        _implementations.AddRange(declarations.Implementations);
        _containers.AddRange(declarations.Containers);
        _unmapped.AddRange(declarations.Unmapped);
    }

    // The declarations of variables collected so far, in the order of the calls, each once.
    internal ImmutableArray<CelVariableDecl> VariableDeclarations => [.. _variables];

    // The CEL type that the input rows give a .NET type, or null outside them, with the .NET type; for a parameter also
    // the CEL type that the expected-type table gives it, which the build compares with the inferred one.
    private static (CelType? Type, Type ClrType, CelType? ParameterType) Clr<T>() =>
        (ConversionRules.InputType<T>(), typeof(T), ConversionRules.ParameterType<T>());

    // The global overload of a delegate: its identifier from the name and the CEL types of the parameters,
    // and the delegate as its implementation.
    private CelEnvironmentBuilder AddDelegate(
        string name,
        Delegate original,
        ReadOnlySpan<(CelType? Type, Type ClrType, CelType? ParameterType)> parameters,
        (CelType? Type, Type ClrType, CelType? ParameterType) result,
        Func<string, OverloadImplementation> implementation)
    {
        var parameterTypes = new CelType[parameters.Length];
        var parts = new string[parameters.Length];
        var expectedTypes = new CelType?[parameters.Length];
        Type? unmapped = null;
        for (int i = 0; i < parameters.Length; i++)
        {
            (CelType? type, Type clrType, expectedTypes[i]) = parameters[i];
            if (type is null)
            {
                parts[i] = clrType.ToString();
                unmapped ??= clrType;
            }
            else
            {
                parts[i] = DeclarationText.Json(type);
                parameterTypes[i] = type;
            }
        }

        string overloadId = DeclarationText.DerivedOverloadId(name, parts);
        if (unmapped is not null || result.Type is not CelType resultType)
        {
            AddUnmapped(new UnmappedDeclaration(overloadId, name, unmapped ?? result.ClrType));
            return this;
        }

        // The typed delegate is checked against the inferred overload like a typed implementation.
        _functions.Add(new CelFunctionDecl(name, CelOverloadDecl.Global(overloadId, parameterTypes, resultType)));
        return AddImplementation(
            new ImplementationEntry(overloadId, original, implementation(overloadId), [.. expectedTypes], resultType, default, false));
    }

    private CelEnvironmentBuilder AddTyped(
        string overloadId,
        Delegate original,
        OverloadImplementation implementation,
        ImmutableArray<CelType?> parameterTypes,
        CelType? resultType,
        ImmutableArray<bool> parameterTakesNull,
        bool resultTakesNull) =>
        AddImplementation(new ImplementationEntry(overloadId, original, implementation, parameterTypes, resultType, parameterTakesNull, resultTakesNull));

    private CelEnvironmentBuilder AddImplementation(ImplementationEntry implementation)
    {
        if (!_implementations.Exists(implementation.SameAs))
        {
            _implementations.Add(implementation);
        }

        return this;
    }

    private void AddUnmapped(UnmappedDeclaration declaration)
    {
        if (!_unmapped.Contains(declaration))
        {
            _unmapped.Add(declaration);
        }
    }
}
