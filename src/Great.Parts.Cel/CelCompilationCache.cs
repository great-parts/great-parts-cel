using System;
using System.Threading;
using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel;

/// <summary>
/// A bounded, thread-safe cache of compilations in one environment, for applications that evaluate
/// expression text received at run time, such as rules stored in a database.
/// </summary>
/// <remarks>
/// <para>
/// The application creates the cache explicitly from an environment and chooses its capacity: the
/// environment itself caches no compilations. The key of an entry is the expression text, the result
/// type <c>T</c> and the values of the compilation options. Unsuccessful compilations are cached too, so
/// untrusted text with an error is not compiled again and again. Entries are evicted when a new entry is
/// added, so that both capacities hold; which entries are evicted is not specified. A text longer than the
/// capacity for text length is compiled but not kept. The cache creates no threads and no timers.
/// </para>
/// <para>
/// A result from the cache is the same as the result of the environment for the same text, type and
/// options: the cache changes neither results, nor errors, nor evaluation costs. All members can be
/// called from any number of threads at once.
/// </para>
/// </remarks>
public sealed class CelCompilationCache
{
    /// <summary>
    /// Creates an empty cache for compilations in <paramref name="environment"/>.
    /// </summary>
    /// <param name="environment">The environment in which the cache compiles expressions.</param>
    /// <param name="maxEntryCount">The largest number of entries the cache keeps, 1 or more.</param>
    /// <param name="maxTotalTextLength">
    /// The largest total length of the expression texts the cache keeps, in UTF-16 code units, as
    /// <see cref="string.Length"/> counts them; 1 or more.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="environment"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxEntryCount"/> or <paramref name="maxTotalTextLength"/> is less than 1.</exception>
    public CelCompilationCache(CelEnvironment environment, int maxEntryCount, int maxTotalTextLength)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Returns the cached compilation of an expression, or compiles it as
    /// <see cref="CelEnvironment.Compile{T}"/> does and caches the result.
    /// </summary>
    /// <typeparam name="T">The .NET type in which the program returns its result.</typeparam>
    /// <param name="expression">The text of the expression.</param>
    /// <param name="options">The compilation options, or <see langword="null"/> for none.</param>
    /// <returns>The program or the compilation errors.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="expression"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <typeparamref name="T"/> is not in the result column of the type mapping table, or the expected type
    /// of <paramref name="options"/> cannot be converted to <typeparamref name="T"/>.
    /// </exception>
    public CelCompileResult<T> GetOrCompile<T>(string expression, CelCompileOptions? options = null)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Evaluates an expression on the bindings in one call through the cache: the same result and errors as
    /// <see cref="CelEnvironment.Evaluate{T}"/>, compiling the text only when the cache does not hold it.
    /// </summary>
    /// <remarks>
    /// The result holds either the compilation errors or the one evaluation error;
    /// <see cref="CelError.Stage"/> tells which step failed. The token is checked before the compilation
    /// and between the compilation and the evaluation, and during evaluation as
    /// <see cref="CelProgram{T}.Evaluate"/> describes.
    /// </remarks>
    /// <typeparam name="T">The .NET type in which the result is returned.</typeparam>
    /// <param name="expression">The text of the expression.</param>
    /// <param name="bindings">The values of the variables.</param>
    /// <param name="compileOptions">The compilation options, or <see langword="null"/> for none.</param>
    /// <param name="evalOptions">The evaluation options, such as the cost limit, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The result converted to <typeparamref name="T"/>, or the errors.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="expression"/> or <paramref name="bindings"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <typeparamref name="T"/> is not in the result column of the type mapping table, or the expected type
    /// of <paramref name="compileOptions"/> cannot be converted to <typeparamref name="T"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public CelEvalResult<T> Evaluate<T>(
        string expression,
        CelBindings bindings,
        CelCompileOptions? compileOptions = null,
        CelEvalOptions? evalOptions = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
