using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel.Syntax;

// The failure of reading a message: the exception of an unsuccessful result with one error of the stage Parse, thrown at the first
// fault, which stops the read. The loader catches it and returns the error; no exception leaves the library.
internal static class ProtoErrors
{
    /// <summary>
    /// Creates the exception that stops the read with an error.
    /// </summary>
    /// <param name="code">The code of the error: <see cref="CelErrorCode.InvalidProtobuf"/>, <see cref="CelErrorCode.InvalidSyntaxTree"/> or <see cref="CelErrorCode.NestingTooDeep"/>.</param>
    /// <param name="message">The message.</param>
    /// <param name="location">The place of the node in the text, or <c>default</c> for none.</param>
    /// <returns>The exception to throw.</returns>
    internal static CelException Stop(CelErrorCode code, string message, CelSourceLocation location = default) =>
        CelException.FromErrors([new CelError(code, CelErrorStage.Parse, message, location, sourceLocation: null, exception: null)]);
}
