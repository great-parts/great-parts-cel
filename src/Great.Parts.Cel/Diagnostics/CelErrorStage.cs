namespace Great.Parts.Cel.Diagnostics;

/// <summary>
/// The stage at which an error was found: building the environment, parsing, type checking or evaluation.
/// </summary>
/// <remarks>
/// <para>
/// Parse and check errors are compilation errors: compilation reports all of them at once, each with
/// its own position. Evaluation returns one error: inside evaluation an error is a value that logical
/// operators and some macros can absorb, and when several parts of an expression fail, the language
/// definition does not specify which error is reported. A one-step evaluation, which compiles and
/// evaluates in a single call, returns either the compilation errors or one evaluation error; the
/// stage of its errors tells which of the two failed.
/// </para>
/// <para>
/// Each <see cref="CelErrorCode"/> belongs to the stage of its number range, except the codes
/// 9000–9999 (<see cref="CelErrorCode.InsufficientStack"/>, <see cref="CelErrorCode.InternalError"/>),
/// which occur at any stage; for them this value is the only indication of where the error happened.
/// </para>
/// <para>The numeric values are part of the public contract and do not change.</para>
/// </remarks>
public enum CelErrorStage
{
    /// <summary>
    /// Building an environment: conflicting declarations, an unknown extension version, a missing
    /// extension dependency, a declaration without an implementation, and errors of an environment
    /// description read from JSON. Codes 4000–4999. Such errors have no position in an expression.
    /// </summary>
    Build = 0,

    /// <summary>
    /// Parsing the expression text and expanding macros. Codes 1000–1999.
    /// </summary>
    Parse = 1,

    /// <summary>
    /// Type checking the parsed expression in the environment, including the preparation of constant
    /// arguments while the program is planned, such as compiling a constant <c>matches</c> pattern.
    /// Codes 2000–2999.
    /// </summary>
    Check = 2,

    /// <summary>
    /// Evaluating a compiled program on bindings. Codes 3000–3999.
    /// </summary>
    Evaluate = 3,
}
