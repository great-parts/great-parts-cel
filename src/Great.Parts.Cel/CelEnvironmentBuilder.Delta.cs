using System;
using System.Threading;
using System.Threading.Tasks;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel;

public sealed partial class CelEnvironmentBuilder
{
    /// <summary>
    /// Declares a function with no arguments, called as <c>f()</c>, and implements it with an
    /// asynchronous .NET delegate that returns a <see cref="ValueTask{TResult}"/> and receives the cancellation token of
    /// the evaluation; the CEL types and the overload identifier come from the delegate as for <see cref="AddFunction{TResult}(string, Func{TResult})"/>,
    /// without the token.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An expression that calls the function is evaluated by <see cref="CelProgram{T}.EvaluateAsync(CelBindings, CelEvalOptions, CancellationToken)"/>
    /// and the other asynchronous members; a synchronous evaluation of it is the evaluation error
    /// <see cref="CelErrorCode.AsyncEvaluationRequired"/>. The evaluation runs synchronously until the expression needs the
    /// result of a call whose arguments are known, starts the calls it needs, awaits them all and evaluates again; a call with
    /// the same arguments at the same place of the expression runs once per evaluation. Each pass charges the call as an
    /// application function, and the cost limit bounds the sum of all passes.
    /// </para>
    /// <para>
    /// An exception of the delegate, thrown or in the task, becomes the evaluation error <see cref="CelErrorCode.ApplicationException"/>;
    /// an <see cref="OperationCanceledException"/> for the canceled token of the evaluation ends the evaluation with that
    /// exception. Honor the token: after cancellation the evaluation starts no new call but awaits the calls it started. The
    /// library creates no threads, tasks or timers; wrap the delegate for time limits or retries.
    /// </para>
    /// </remarks>
    /// <typeparam name="TResult">The .NET type of the result of the task.</typeparam>
    /// <param name="name">The name of the function.</param>
    /// <param name="implementation">The implementation; its last parameter is the cancellation token of the evaluation.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="implementation"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    public CelEnvironmentBuilder AddAsyncFunction<TResult>(string name, Func<CancellationToken, ValueTask<TResult>> implementation)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(implementation);
        return AddDelegate(name, implementation, [], ClrResult<TResult>(), null, asyncImplementation: _ => TypedImplementation.OfAsync(implementation));
    }

    /// <summary>
    /// Declares an asynchronous function as <see cref="AddAsyncFunction{TResult}(string, Func{CancellationToken, ValueTask{TResult}})"/> does,
    /// with the options of the overload: its cost, overload identifier and description.
    /// </summary>
    /// <remarks>
    /// The options replace what the delegate form derives, as <see cref="AddFunction{TResult}(string, Func{TResult}, CelFunctionOptions)"/>
    /// describes; the cost function receives the arguments without the token and is charged in each pass that evaluates the call.
    /// </remarks>
    /// <typeparam name="TResult">The .NET type of the result of the task.</typeparam>
    /// <param name="name">The name of the function.</param>
    /// <param name="implementation">The implementation; its last parameter is the cancellation token of the evaluation.</param>
    /// <param name="options">The options of the overload.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/>, <paramref name="implementation"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> or <see cref="CelFunctionOptions.OverloadId"/> is empty.</exception>
    public CelEnvironmentBuilder AddAsyncFunction<TResult>(string name, Func<CancellationToken, ValueTask<TResult>> implementation, CelFunctionOptions options)
    {
        CheckOptions(name, implementation, options);
        return AddDelegate(name, implementation, [], ClrResult<TResult>(), null, options: options, asyncImplementation: _ => TypedImplementation.OfAsync(implementation));
    }

    /// <summary>
    /// Declares a function with one argument, called as <c>f(a)</c>, and implements it with an
    /// asynchronous .NET delegate that returns a <see cref="ValueTask{TResult}"/> and receives the cancellation token of
    /// the evaluation; the CEL types and the overload identifier come from the delegate as for <see cref="AddFunction{T1, TResult}(string, Func{T1, TResult})"/>,
    /// without the token.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An expression that calls the function is evaluated by <see cref="CelProgram{T}.EvaluateAsync(CelBindings, CelEvalOptions, CancellationToken)"/>
    /// and the other asynchronous members; a synchronous evaluation of it is the evaluation error
    /// <see cref="CelErrorCode.AsyncEvaluationRequired"/>. The evaluation runs synchronously until the expression needs the
    /// result of a call whose arguments are known, starts the calls it needs, awaits them all and evaluates again; a call with
    /// the same arguments at the same place of the expression runs once per evaluation. Each pass charges the call as an
    /// application function, and the cost limit bounds the sum of all passes.
    /// </para>
    /// <para>
    /// An exception of the delegate, thrown or in the task, becomes the evaluation error <see cref="CelErrorCode.ApplicationException"/>;
    /// an <see cref="OperationCanceledException"/> for the canceled token of the evaluation ends the evaluation with that
    /// exception. Honor the token: after cancellation the evaluation starts no new call but awaits the calls it started. The
    /// library creates no threads, tasks or timers; wrap the delegate for time limits or retries.
    /// </para>
    /// </remarks>
    /// <typeparam name="T1">The .NET type of the argument.</typeparam>
    /// <typeparam name="TResult">The .NET type of the result of the task.</typeparam>
    /// <param name="name">The name of the function.</param>
    /// <param name="implementation">The implementation; its last parameter is the cancellation token of the evaluation.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="implementation"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    public CelEnvironmentBuilder AddAsyncFunction<T1, TResult>(string name, Func<T1, CancellationToken, ValueTask<TResult>> implementation)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(implementation);
        return AddDelegate(name, implementation, [Clr<T1>()], ClrResult<TResult>(), null, asyncImplementation: id => TypedImplementation.OfAsync(id, implementation));
    }

    /// <summary>
    /// Declares an asynchronous function as <see cref="AddAsyncFunction{T1, TResult}(string, Func{T1, CancellationToken, ValueTask{TResult}})"/> does,
    /// with the options of the overload: its cost, overload identifier and description.
    /// </summary>
    /// <remarks>
    /// The options replace what the delegate form derives, as <see cref="AddFunction{T1, TResult}(string, Func{T1, TResult}, CelFunctionOptions)"/>
    /// describes; the cost function receives the arguments without the token and is charged in each pass that evaluates the call.
    /// </remarks>
    /// <typeparam name="T1">The .NET type of the argument.</typeparam>
    /// <typeparam name="TResult">The .NET type of the result of the task.</typeparam>
    /// <param name="name">The name of the function.</param>
    /// <param name="implementation">The implementation; its last parameter is the cancellation token of the evaluation.</param>
    /// <param name="options">The options of the overload.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/>, <paramref name="implementation"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> or <see cref="CelFunctionOptions.OverloadId"/> is empty.</exception>
    public CelEnvironmentBuilder AddAsyncFunction<T1, TResult>(string name, Func<T1, CancellationToken, ValueTask<TResult>> implementation, CelFunctionOptions options)
    {
        CheckOptions(name, implementation, options);
        return AddDelegate(name, implementation, [Clr<T1>()], ClrResult<TResult>(), null, options: options, asyncImplementation: id => TypedImplementation.OfAsync(id, implementation));
    }

    /// <summary>
    /// Declares a function with two arguments, called as <c>f(a, b)</c>, and implements it with an
    /// asynchronous .NET delegate that returns a <see cref="ValueTask{TResult}"/> and receives the cancellation token of
    /// the evaluation; the CEL types and the overload identifier come from the delegate as for <see cref="AddFunction{T1, T2, TResult}(string, Func{T1, T2, TResult})"/>,
    /// without the token.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An expression that calls the function is evaluated by <see cref="CelProgram{T}.EvaluateAsync(CelBindings, CelEvalOptions, CancellationToken)"/>
    /// and the other asynchronous members; a synchronous evaluation of it is the evaluation error
    /// <see cref="CelErrorCode.AsyncEvaluationRequired"/>. The evaluation runs synchronously until the expression needs the
    /// result of a call whose arguments are known, starts the calls it needs, awaits them all and evaluates again; a call with
    /// the same arguments at the same place of the expression runs once per evaluation. Each pass charges the call as an
    /// application function, and the cost limit bounds the sum of all passes.
    /// </para>
    /// <para>
    /// An exception of the delegate, thrown or in the task, becomes the evaluation error <see cref="CelErrorCode.ApplicationException"/>;
    /// an <see cref="OperationCanceledException"/> for the canceled token of the evaluation ends the evaluation with that
    /// exception. Honor the token: after cancellation the evaluation starts no new call but awaits the calls it started. The
    /// library creates no threads, tasks or timers; wrap the delegate for time limits or retries.
    /// </para>
    /// </remarks>
    /// <typeparam name="T1">The .NET type of the first argument.</typeparam>
    /// <typeparam name="T2">The .NET type of the second argument.</typeparam>
    /// <typeparam name="TResult">The .NET type of the result of the task.</typeparam>
    /// <param name="name">The name of the function.</param>
    /// <param name="implementation">The implementation; its last parameter is the cancellation token of the evaluation.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="implementation"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    public CelEnvironmentBuilder AddAsyncFunction<T1, T2, TResult>(string name, Func<T1, T2, CancellationToken, ValueTask<TResult>> implementation)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(implementation);
        return AddDelegate(name, implementation, [Clr<T1>(), Clr<T2>()], ClrResult<TResult>(), null, asyncImplementation: id => TypedImplementation.OfAsync(id, implementation));
    }

    /// <summary>
    /// Declares an asynchronous function as <see cref="AddAsyncFunction{T1, T2, TResult}(string, Func{T1, T2, CancellationToken, ValueTask{TResult}})"/> does,
    /// with the options of the overload: its cost, overload identifier and description.
    /// </summary>
    /// <remarks>
    /// The options replace what the delegate form derives, as <see cref="AddFunction{T1, T2, TResult}(string, Func{T1, T2, TResult}, CelFunctionOptions)"/>
    /// describes; the cost function receives the arguments without the token and is charged in each pass that evaluates the call.
    /// </remarks>
    /// <typeparam name="T1">The .NET type of the first argument.</typeparam>
    /// <typeparam name="T2">The .NET type of the second argument.</typeparam>
    /// <typeparam name="TResult">The .NET type of the result of the task.</typeparam>
    /// <param name="name">The name of the function.</param>
    /// <param name="implementation">The implementation; its last parameter is the cancellation token of the evaluation.</param>
    /// <param name="options">The options of the overload.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/>, <paramref name="implementation"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> or <see cref="CelFunctionOptions.OverloadId"/> is empty.</exception>
    public CelEnvironmentBuilder AddAsyncFunction<T1, T2, TResult>(string name, Func<T1, T2, CancellationToken, ValueTask<TResult>> implementation, CelFunctionOptions options)
    {
        CheckOptions(name, implementation, options);
        return AddDelegate(name, implementation, [Clr<T1>(), Clr<T2>()], ClrResult<TResult>(), null, options: options, asyncImplementation: id => TypedImplementation.OfAsync(id, implementation));
    }

    /// <summary>
    /// Declares a function with three arguments, called as <c>f(a, b, c)</c>, and implements it with an
    /// asynchronous .NET delegate that returns a <see cref="ValueTask{TResult}"/> and receives the cancellation token of
    /// the evaluation; the CEL types and the overload identifier come from the delegate as for <see cref="AddFunction{T1, T2, T3, TResult}(string, Func{T1, T2, T3, TResult})"/>,
    /// without the token.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An expression that calls the function is evaluated by <see cref="CelProgram{T}.EvaluateAsync(CelBindings, CelEvalOptions, CancellationToken)"/>
    /// and the other asynchronous members; a synchronous evaluation of it is the evaluation error
    /// <see cref="CelErrorCode.AsyncEvaluationRequired"/>. The evaluation runs synchronously until the expression needs the
    /// result of a call whose arguments are known, starts the calls it needs, awaits them all and evaluates again; a call with
    /// the same arguments at the same place of the expression runs once per evaluation. Each pass charges the call as an
    /// application function, and the cost limit bounds the sum of all passes.
    /// </para>
    /// <para>
    /// An exception of the delegate, thrown or in the task, becomes the evaluation error <see cref="CelErrorCode.ApplicationException"/>;
    /// an <see cref="OperationCanceledException"/> for the canceled token of the evaluation ends the evaluation with that
    /// exception. Honor the token: after cancellation the evaluation starts no new call but awaits the calls it started. The
    /// library creates no threads, tasks or timers; wrap the delegate for time limits or retries.
    /// </para>
    /// </remarks>
    /// <typeparam name="T1">The .NET type of the first argument.</typeparam>
    /// <typeparam name="T2">The .NET type of the second argument.</typeparam>
    /// <typeparam name="T3">The .NET type of the third argument.</typeparam>
    /// <typeparam name="TResult">The .NET type of the result of the task.</typeparam>
    /// <param name="name">The name of the function.</param>
    /// <param name="implementation">The implementation; its last parameter is the cancellation token of the evaluation.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="implementation"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    public CelEnvironmentBuilder AddAsyncFunction<T1, T2, T3, TResult>(string name, Func<T1, T2, T3, CancellationToken, ValueTask<TResult>> implementation)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(implementation);
        return AddDelegate(name, implementation, [Clr<T1>(), Clr<T2>(), Clr<T3>()], ClrResult<TResult>(), null, asyncImplementation: id => TypedImplementation.OfAsync(id, implementation));
    }

    /// <summary>
    /// Declares an asynchronous function as <see cref="AddAsyncFunction{T1, T2, T3, TResult}(string, Func{T1, T2, T3, CancellationToken, ValueTask{TResult}})"/> does,
    /// with the options of the overload: its cost, overload identifier and description.
    /// </summary>
    /// <remarks>
    /// The options replace what the delegate form derives, as <see cref="AddFunction{T1, T2, T3, TResult}(string, Func{T1, T2, T3, TResult}, CelFunctionOptions)"/>
    /// describes; the cost function receives the arguments without the token and is charged in each pass that evaluates the call.
    /// </remarks>
    /// <typeparam name="T1">The .NET type of the first argument.</typeparam>
    /// <typeparam name="T2">The .NET type of the second argument.</typeparam>
    /// <typeparam name="T3">The .NET type of the third argument.</typeparam>
    /// <typeparam name="TResult">The .NET type of the result of the task.</typeparam>
    /// <param name="name">The name of the function.</param>
    /// <param name="implementation">The implementation; its last parameter is the cancellation token of the evaluation.</param>
    /// <param name="options">The options of the overload.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/>, <paramref name="implementation"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> or <see cref="CelFunctionOptions.OverloadId"/> is empty.</exception>
    public CelEnvironmentBuilder AddAsyncFunction<T1, T2, T3, TResult>(string name, Func<T1, T2, T3, CancellationToken, ValueTask<TResult>> implementation, CelFunctionOptions options)
    {
        CheckOptions(name, implementation, options);
        return AddDelegate(name, implementation, [Clr<T1>(), Clr<T2>(), Clr<T3>()], ClrResult<TResult>(), null, options: options, asyncImplementation: id => TypedImplementation.OfAsync(id, implementation));
    }

    /// <summary>
    /// Declares a function with four arguments, called as <c>f(a, b, c, d)</c>, and implements it with an
    /// asynchronous .NET delegate that returns a <see cref="ValueTask{TResult}"/> and receives the cancellation token of
    /// the evaluation; the CEL types and the overload identifier come from the delegate as for <see cref="AddFunction{T1, T2, T3, T4, TResult}(string, Func{T1, T2, T3, T4, TResult})"/>,
    /// without the token.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An expression that calls the function is evaluated by <see cref="CelProgram{T}.EvaluateAsync(CelBindings, CelEvalOptions, CancellationToken)"/>
    /// and the other asynchronous members; a synchronous evaluation of it is the evaluation error
    /// <see cref="CelErrorCode.AsyncEvaluationRequired"/>. The evaluation runs synchronously until the expression needs the
    /// result of a call whose arguments are known, starts the calls it needs, awaits them all and evaluates again; a call with
    /// the same arguments at the same place of the expression runs once per evaluation. Each pass charges the call as an
    /// application function, and the cost limit bounds the sum of all passes.
    /// </para>
    /// <para>
    /// An exception of the delegate, thrown or in the task, becomes the evaluation error <see cref="CelErrorCode.ApplicationException"/>;
    /// an <see cref="OperationCanceledException"/> for the canceled token of the evaluation ends the evaluation with that
    /// exception. Honor the token: after cancellation the evaluation starts no new call but awaits the calls it started. The
    /// library creates no threads, tasks or timers; wrap the delegate for time limits or retries.
    /// </para>
    /// </remarks>
    /// <typeparam name="T1">The .NET type of the first argument.</typeparam>
    /// <typeparam name="T2">The .NET type of the second argument.</typeparam>
    /// <typeparam name="T3">The .NET type of the third argument.</typeparam>
    /// <typeparam name="T4">The .NET type of the fourth argument.</typeparam>
    /// <typeparam name="TResult">The .NET type of the result of the task.</typeparam>
    /// <param name="name">The name of the function.</param>
    /// <param name="implementation">The implementation; its last parameter is the cancellation token of the evaluation.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="implementation"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    public CelEnvironmentBuilder AddAsyncFunction<T1, T2, T3, T4, TResult>(string name, Func<T1, T2, T3, T4, CancellationToken, ValueTask<TResult>> implementation)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(implementation);
        return AddDelegate(name, implementation, [Clr<T1>(), Clr<T2>(), Clr<T3>(), Clr<T4>()], ClrResult<TResult>(), null, asyncImplementation: id => TypedImplementation.OfAsync(id, implementation));
    }

    /// <summary>
    /// Declares an asynchronous function as <see cref="AddAsyncFunction{T1, T2, T3, T4, TResult}(string, Func{T1, T2, T3, T4, CancellationToken, ValueTask{TResult}})"/> does,
    /// with the options of the overload: its cost, overload identifier and description.
    /// </summary>
    /// <remarks>
    /// The options replace what the delegate form derives, as <see cref="AddFunction{T1, T2, T3, T4, TResult}(string, Func{T1, T2, T3, T4, TResult}, CelFunctionOptions)"/>
    /// describes; the cost function receives the arguments without the token and is charged in each pass that evaluates the call.
    /// </remarks>
    /// <typeparam name="T1">The .NET type of the first argument.</typeparam>
    /// <typeparam name="T2">The .NET type of the second argument.</typeparam>
    /// <typeparam name="T3">The .NET type of the third argument.</typeparam>
    /// <typeparam name="T4">The .NET type of the fourth argument.</typeparam>
    /// <typeparam name="TResult">The .NET type of the result of the task.</typeparam>
    /// <param name="name">The name of the function.</param>
    /// <param name="implementation">The implementation; its last parameter is the cancellation token of the evaluation.</param>
    /// <param name="options">The options of the overload.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/>, <paramref name="implementation"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> or <see cref="CelFunctionOptions.OverloadId"/> is empty.</exception>
    public CelEnvironmentBuilder AddAsyncFunction<T1, T2, T3, T4, TResult>(string name, Func<T1, T2, T3, T4, CancellationToken, ValueTask<TResult>> implementation, CelFunctionOptions options)
    {
        CheckOptions(name, implementation, options);
        return AddDelegate(name, implementation, [Clr<T1>(), Clr<T2>(), Clr<T3>(), Clr<T4>()], ClrResult<TResult>(), null, options: options, asyncImplementation: id => TypedImplementation.OfAsync(id, implementation));
    }

    /// <summary>
    /// Declares a function called on a receiver with no arguments, as <c>r.f()</c>, and implements it with an
    /// asynchronous .NET delegate that returns a <see cref="ValueTask{TResult}"/> and receives the cancellation token of
    /// the evaluation; the CEL types and the overload identifier come from the delegate as for <see cref="AddMethod{TReceiver, TResult}(string, Func{TReceiver, TResult})"/>,
    /// without the token.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An expression that calls the function is evaluated by <see cref="CelProgram{T}.EvaluateAsync(CelBindings, CelEvalOptions, CancellationToken)"/>
    /// and the other asynchronous members; a synchronous evaluation of it is the evaluation error
    /// <see cref="CelErrorCode.AsyncEvaluationRequired"/>. The evaluation runs synchronously until the expression needs the
    /// result of a call whose arguments are known, starts the calls it needs, awaits them all and evaluates again; a call with
    /// the same arguments at the same place of the expression runs once per evaluation. Each pass charges the call as an
    /// application function, and the cost limit bounds the sum of all passes.
    /// </para>
    /// <para>
    /// An exception of the delegate, thrown or in the task, becomes the evaluation error <see cref="CelErrorCode.ApplicationException"/>;
    /// an <see cref="OperationCanceledException"/> for the canceled token of the evaluation ends the evaluation with that
    /// exception. Honor the token: after cancellation the evaluation starts no new call but awaits the calls it started. The
    /// library creates no threads, tasks or timers; wrap the delegate for time limits or retries.
    /// </para>
    /// </remarks>
    /// <typeparam name="TReceiver">The .NET type of the receiver, the value before the dot.</typeparam>
    /// <typeparam name="TResult">The .NET type of the result of the task.</typeparam>
    /// <param name="name">The name of the function.</param>
    /// <param name="implementation">The implementation; its first parameter is the receiver and its last parameter is the cancellation token of the evaluation.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="implementation"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    public CelEnvironmentBuilder AddAsyncMethod<TReceiver, TResult>(string name, Func<TReceiver, CancellationToken, ValueTask<TResult>> implementation)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(implementation);
        return AddDelegate(name, implementation, [Clr<TReceiver>()], ClrResult<TResult>(), null, isReceiverStyle: true, asyncImplementation: id => TypedImplementation.OfAsync(id, implementation));
    }

    /// <summary>
    /// Declares an asynchronous function as <see cref="AddAsyncMethod{TReceiver, TResult}(string, Func{TReceiver, CancellationToken, ValueTask{TResult}})"/> does,
    /// with the options of the overload: its cost, overload identifier and description.
    /// </summary>
    /// <remarks>
    /// The options replace what the delegate form derives, as <see cref="AddMethod{TReceiver, TResult}(string, Func{TReceiver, TResult}, CelFunctionOptions)"/>
    /// describes; the cost function receives the arguments without the token and is charged in each pass that evaluates the call.
    /// </remarks>
    /// <typeparam name="TReceiver">The .NET type of the receiver, the value before the dot.</typeparam>
    /// <typeparam name="TResult">The .NET type of the result of the task.</typeparam>
    /// <param name="name">The name of the function.</param>
    /// <param name="implementation">The implementation; its first parameter is the receiver and its last parameter is the cancellation token of the evaluation.</param>
    /// <param name="options">The options of the overload.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/>, <paramref name="implementation"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> or <see cref="CelFunctionOptions.OverloadId"/> is empty.</exception>
    public CelEnvironmentBuilder AddAsyncMethod<TReceiver, TResult>(string name, Func<TReceiver, CancellationToken, ValueTask<TResult>> implementation, CelFunctionOptions options)
    {
        CheckOptions(name, implementation, options);
        return AddDelegate(name, implementation, [Clr<TReceiver>()], ClrResult<TResult>(), null, isReceiverStyle: true, options: options, asyncImplementation: id => TypedImplementation.OfAsync(id, implementation));
    }

    /// <summary>
    /// Declares a function called on a receiver with one argument, as <c>r.f(a)</c>, and implements it with an
    /// asynchronous .NET delegate that returns a <see cref="ValueTask{TResult}"/> and receives the cancellation token of
    /// the evaluation; the CEL types and the overload identifier come from the delegate as for <see cref="AddMethod{TReceiver, T1, TResult}(string, Func{TReceiver, T1, TResult})"/>,
    /// without the token.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An expression that calls the function is evaluated by <see cref="CelProgram{T}.EvaluateAsync(CelBindings, CelEvalOptions, CancellationToken)"/>
    /// and the other asynchronous members; a synchronous evaluation of it is the evaluation error
    /// <see cref="CelErrorCode.AsyncEvaluationRequired"/>. The evaluation runs synchronously until the expression needs the
    /// result of a call whose arguments are known, starts the calls it needs, awaits them all and evaluates again; a call with
    /// the same arguments at the same place of the expression runs once per evaluation. Each pass charges the call as an
    /// application function, and the cost limit bounds the sum of all passes.
    /// </para>
    /// <para>
    /// An exception of the delegate, thrown or in the task, becomes the evaluation error <see cref="CelErrorCode.ApplicationException"/>;
    /// an <see cref="OperationCanceledException"/> for the canceled token of the evaluation ends the evaluation with that
    /// exception. Honor the token: after cancellation the evaluation starts no new call but awaits the calls it started. The
    /// library creates no threads, tasks or timers; wrap the delegate for time limits or retries.
    /// </para>
    /// </remarks>
    /// <typeparam name="TReceiver">The .NET type of the receiver, the value before the dot.</typeparam>
    /// <typeparam name="T1">The .NET type of the argument.</typeparam>
    /// <typeparam name="TResult">The .NET type of the result of the task.</typeparam>
    /// <param name="name">The name of the function.</param>
    /// <param name="implementation">The implementation; its first parameter is the receiver and its last parameter is the cancellation token of the evaluation.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="implementation"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    public CelEnvironmentBuilder AddAsyncMethod<TReceiver, T1, TResult>(string name, Func<TReceiver, T1, CancellationToken, ValueTask<TResult>> implementation)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(implementation);
        return AddDelegate(name, implementation, [Clr<TReceiver>(), Clr<T1>()], ClrResult<TResult>(), null, isReceiverStyle: true, asyncImplementation: id => TypedImplementation.OfAsync(id, implementation));
    }

    /// <summary>
    /// Declares an asynchronous function as <see cref="AddAsyncMethod{TReceiver, T1, TResult}(string, Func{TReceiver, T1, CancellationToken, ValueTask{TResult}})"/> does,
    /// with the options of the overload: its cost, overload identifier and description.
    /// </summary>
    /// <remarks>
    /// The options replace what the delegate form derives, as <see cref="AddMethod{TReceiver, T1, TResult}(string, Func{TReceiver, T1, TResult}, CelFunctionOptions)"/>
    /// describes; the cost function receives the arguments without the token and is charged in each pass that evaluates the call.
    /// </remarks>
    /// <typeparam name="TReceiver">The .NET type of the receiver, the value before the dot.</typeparam>
    /// <typeparam name="T1">The .NET type of the argument.</typeparam>
    /// <typeparam name="TResult">The .NET type of the result of the task.</typeparam>
    /// <param name="name">The name of the function.</param>
    /// <param name="implementation">The implementation; its first parameter is the receiver and its last parameter is the cancellation token of the evaluation.</param>
    /// <param name="options">The options of the overload.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/>, <paramref name="implementation"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> or <see cref="CelFunctionOptions.OverloadId"/> is empty.</exception>
    public CelEnvironmentBuilder AddAsyncMethod<TReceiver, T1, TResult>(string name, Func<TReceiver, T1, CancellationToken, ValueTask<TResult>> implementation, CelFunctionOptions options)
    {
        CheckOptions(name, implementation, options);
        return AddDelegate(name, implementation, [Clr<TReceiver>(), Clr<T1>()], ClrResult<TResult>(), null, isReceiverStyle: true, options: options, asyncImplementation: id => TypedImplementation.OfAsync(id, implementation));
    }

    /// <summary>
    /// Declares a function called on a receiver with two arguments, as <c>r.f(a, b)</c>, and implements it with an
    /// asynchronous .NET delegate that returns a <see cref="ValueTask{TResult}"/> and receives the cancellation token of
    /// the evaluation; the CEL types and the overload identifier come from the delegate as for <see cref="AddMethod{TReceiver, T1, T2, TResult}(string, Func{TReceiver, T1, T2, TResult})"/>,
    /// without the token.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An expression that calls the function is evaluated by <see cref="CelProgram{T}.EvaluateAsync(CelBindings, CelEvalOptions, CancellationToken)"/>
    /// and the other asynchronous members; a synchronous evaluation of it is the evaluation error
    /// <see cref="CelErrorCode.AsyncEvaluationRequired"/>. The evaluation runs synchronously until the expression needs the
    /// result of a call whose arguments are known, starts the calls it needs, awaits them all and evaluates again; a call with
    /// the same arguments at the same place of the expression runs once per evaluation. Each pass charges the call as an
    /// application function, and the cost limit bounds the sum of all passes.
    /// </para>
    /// <para>
    /// An exception of the delegate, thrown or in the task, becomes the evaluation error <see cref="CelErrorCode.ApplicationException"/>;
    /// an <see cref="OperationCanceledException"/> for the canceled token of the evaluation ends the evaluation with that
    /// exception. Honor the token: after cancellation the evaluation starts no new call but awaits the calls it started. The
    /// library creates no threads, tasks or timers; wrap the delegate for time limits or retries.
    /// </para>
    /// </remarks>
    /// <typeparam name="TReceiver">The .NET type of the receiver, the value before the dot.</typeparam>
    /// <typeparam name="T1">The .NET type of the first argument.</typeparam>
    /// <typeparam name="T2">The .NET type of the second argument.</typeparam>
    /// <typeparam name="TResult">The .NET type of the result of the task.</typeparam>
    /// <param name="name">The name of the function.</param>
    /// <param name="implementation">The implementation; its first parameter is the receiver and its last parameter is the cancellation token of the evaluation.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="implementation"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    public CelEnvironmentBuilder AddAsyncMethod<TReceiver, T1, T2, TResult>(string name, Func<TReceiver, T1, T2, CancellationToken, ValueTask<TResult>> implementation)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(implementation);
        return AddDelegate(name, implementation, [Clr<TReceiver>(), Clr<T1>(), Clr<T2>()], ClrResult<TResult>(), null, isReceiverStyle: true, asyncImplementation: id => TypedImplementation.OfAsync(id, implementation));
    }

    /// <summary>
    /// Declares an asynchronous function as <see cref="AddAsyncMethod{TReceiver, T1, T2, TResult}(string, Func{TReceiver, T1, T2, CancellationToken, ValueTask{TResult}})"/> does,
    /// with the options of the overload: its cost, overload identifier and description.
    /// </summary>
    /// <remarks>
    /// The options replace what the delegate form derives, as <see cref="AddMethod{TReceiver, T1, T2, TResult}(string, Func{TReceiver, T1, T2, TResult}, CelFunctionOptions)"/>
    /// describes; the cost function receives the arguments without the token and is charged in each pass that evaluates the call.
    /// </remarks>
    /// <typeparam name="TReceiver">The .NET type of the receiver, the value before the dot.</typeparam>
    /// <typeparam name="T1">The .NET type of the first argument.</typeparam>
    /// <typeparam name="T2">The .NET type of the second argument.</typeparam>
    /// <typeparam name="TResult">The .NET type of the result of the task.</typeparam>
    /// <param name="name">The name of the function.</param>
    /// <param name="implementation">The implementation; its first parameter is the receiver and its last parameter is the cancellation token of the evaluation.</param>
    /// <param name="options">The options of the overload.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/>, <paramref name="implementation"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> or <see cref="CelFunctionOptions.OverloadId"/> is empty.</exception>
    public CelEnvironmentBuilder AddAsyncMethod<TReceiver, T1, T2, TResult>(string name, Func<TReceiver, T1, T2, CancellationToken, ValueTask<TResult>> implementation, CelFunctionOptions options)
    {
        CheckOptions(name, implementation, options);
        return AddDelegate(name, implementation, [Clr<TReceiver>(), Clr<T1>(), Clr<T2>()], ClrResult<TResult>(), null, isReceiverStyle: true, options: options, asyncImplementation: id => TypedImplementation.OfAsync(id, implementation));
    }

    /// <summary>
    /// Declares a function called on a receiver with three arguments, as <c>r.f(a, b, c)</c>, and implements it with an
    /// asynchronous .NET delegate that returns a <see cref="ValueTask{TResult}"/> and receives the cancellation token of
    /// the evaluation; the CEL types and the overload identifier come from the delegate as for <see cref="AddMethod{TReceiver, T1, T2, T3, TResult}(string, Func{TReceiver, T1, T2, T3, TResult})"/>,
    /// without the token.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An expression that calls the function is evaluated by <see cref="CelProgram{T}.EvaluateAsync(CelBindings, CelEvalOptions, CancellationToken)"/>
    /// and the other asynchronous members; a synchronous evaluation of it is the evaluation error
    /// <see cref="CelErrorCode.AsyncEvaluationRequired"/>. The evaluation runs synchronously until the expression needs the
    /// result of a call whose arguments are known, starts the calls it needs, awaits them all and evaluates again; a call with
    /// the same arguments at the same place of the expression runs once per evaluation. Each pass charges the call as an
    /// application function, and the cost limit bounds the sum of all passes.
    /// </para>
    /// <para>
    /// An exception of the delegate, thrown or in the task, becomes the evaluation error <see cref="CelErrorCode.ApplicationException"/>;
    /// an <see cref="OperationCanceledException"/> for the canceled token of the evaluation ends the evaluation with that
    /// exception. Honor the token: after cancellation the evaluation starts no new call but awaits the calls it started. The
    /// library creates no threads, tasks or timers; wrap the delegate for time limits or retries.
    /// </para>
    /// </remarks>
    /// <typeparam name="TReceiver">The .NET type of the receiver, the value before the dot.</typeparam>
    /// <typeparam name="T1">The .NET type of the first argument.</typeparam>
    /// <typeparam name="T2">The .NET type of the second argument.</typeparam>
    /// <typeparam name="T3">The .NET type of the third argument.</typeparam>
    /// <typeparam name="TResult">The .NET type of the result of the task.</typeparam>
    /// <param name="name">The name of the function.</param>
    /// <param name="implementation">The implementation; its first parameter is the receiver and its last parameter is the cancellation token of the evaluation.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="implementation"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    public CelEnvironmentBuilder AddAsyncMethod<TReceiver, T1, T2, T3, TResult>(string name, Func<TReceiver, T1, T2, T3, CancellationToken, ValueTask<TResult>> implementation)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(implementation);
        return AddDelegate(name, implementation, [Clr<TReceiver>(), Clr<T1>(), Clr<T2>(), Clr<T3>()], ClrResult<TResult>(), null, isReceiverStyle: true, asyncImplementation: id => TypedImplementation.OfAsync(id, implementation));
    }

    /// <summary>
    /// Declares an asynchronous function as <see cref="AddAsyncMethod{TReceiver, T1, T2, T3, TResult}(string, Func{TReceiver, T1, T2, T3, CancellationToken, ValueTask{TResult}})"/> does,
    /// with the options of the overload: its cost, overload identifier and description.
    /// </summary>
    /// <remarks>
    /// The options replace what the delegate form derives, as <see cref="AddMethod{TReceiver, T1, T2, T3, TResult}(string, Func{TReceiver, T1, T2, T3, TResult}, CelFunctionOptions)"/>
    /// describes; the cost function receives the arguments without the token and is charged in each pass that evaluates the call.
    /// </remarks>
    /// <typeparam name="TReceiver">The .NET type of the receiver, the value before the dot.</typeparam>
    /// <typeparam name="T1">The .NET type of the first argument.</typeparam>
    /// <typeparam name="T2">The .NET type of the second argument.</typeparam>
    /// <typeparam name="T3">The .NET type of the third argument.</typeparam>
    /// <typeparam name="TResult">The .NET type of the result of the task.</typeparam>
    /// <param name="name">The name of the function.</param>
    /// <param name="implementation">The implementation; its first parameter is the receiver and its last parameter is the cancellation token of the evaluation.</param>
    /// <param name="options">The options of the overload.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/>, <paramref name="implementation"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> or <see cref="CelFunctionOptions.OverloadId"/> is empty.</exception>
    public CelEnvironmentBuilder AddAsyncMethod<TReceiver, T1, T2, T3, TResult>(string name, Func<TReceiver, T1, T2, T3, CancellationToken, ValueTask<TResult>> implementation, CelFunctionOptions options)
    {
        CheckOptions(name, implementation, options);
        return AddDelegate(name, implementation, [Clr<TReceiver>(), Clr<T1>(), Clr<T2>(), Clr<T3>()], ClrResult<TResult>(), null, isReceiverStyle: true, options: options, asyncImplementation: id => TypedImplementation.OfAsync(id, implementation));
    }

    /// <summary>
    /// Gives an overload of a declared function an asynchronous implementation, by the overload identifier: for an overload
    /// declared with <see cref="AddFunction(CelFunctionDecl)"/> or in an environment description in JSON, as
    /// <see cref="AddFunctionImplementation(string, CelFunctionImplementation)"/> gives a synchronous one.
    /// </summary>
    /// <remarks>
    /// The arguments are the CEL values of the call, receiver first; they do not change and can be kept. The result must fit the
    /// result type of the overload, otherwise the call is <see cref="CelErrorCode.ImplementationResultMismatch"/>. Evaluation,
    /// cost, exceptions and cancellation are as for <see cref="AddAsyncFunction{T1, TResult}(string, Func{T1, CancellationToken, ValueTask{TResult}})"/>.
    /// An overload with both a synchronous and an asynchronous implementation, or with two implementations, is the build error
    /// <see cref="CelErrorCode.DuplicateImplementation"/>.
    /// </remarks>
    /// <param name="overloadId">The identifier of the overload.</param>
    /// <param name="implementation">The asynchronous implementation.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="overloadId"/> or <paramref name="implementation"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="overloadId"/> is empty.</exception>
    public CelEnvironmentBuilder AddAsyncFunctionImplementation(string overloadId, CelAsyncFunctionImplementation implementation)
    {
        ArgumentException.ThrowIfNullOrEmpty(overloadId);
        ArgumentNullException.ThrowIfNull(implementation);
        return AddImplementation(new ImplementationEntry(overloadId, implementation, TypedImplementation.OfAsync(implementation)));
    }
}
