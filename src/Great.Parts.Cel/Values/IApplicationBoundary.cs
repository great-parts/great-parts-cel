using System;

namespace Great.Parts.Cel.Values;

/// <summary>
/// The exception boundary of application code and the event counter of one evaluation, as the value rules that read
/// fields of objects outside a field selection — the equality of objects — reach them.
/// </summary>
internal interface IApplicationBoundary
{
    /// <summary>
    /// Counts an event before it begins: a read or a presence test of a field of an object.
    /// </summary>
    void Event();

    /// <summary>
    /// Tells whether an exception that application code threw becomes <c>ApplicationException</c>: every exception but
    /// <see cref="OutOfMemoryException"/> and the <see cref="OperationCanceledException"/> of the canceled token of the
    /// evaluation, which pass through.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns><see langword="true"/> when the exception becomes an error value.</returns>
    bool IsApplicationException(Exception exception);

    /// <summary>
    /// The error value <c>ApplicationException</c> of a field read at the node whose operation runs, which keeps the
    /// exception.
    /// </summary>
    /// <param name="exception">The exception of application code.</param>
    /// <param name="owner">The description whose field was read.</param>
    /// <param name="number">The number of the field.</param>
    /// <returns>The error value.</returns>
    CelValue FieldException(Exception exception, CelObjectType owner, int number);

    /// <summary>
    /// The error value <c>ApplicationException</c> of the equality of two instances of a description that compares them
    /// itself, at the node whose operation runs, which keeps the exception.
    /// </summary>
    /// <param name="exception">The exception of application code.</param>
    /// <param name="owner">The description whose equality threw.</param>
    /// <returns>The error value.</returns>
    CelValue EqualityException(Exception exception, CelObjectType owner);

    /// <summary>
    /// The error value <c>InternalError</c> at the node whose operation runs, which keeps the exception.
    /// </summary>
    /// <param name="exception">The exception that describes the failure.</param>
    /// <returns>The error value.</returns>
    CelValue InternalFailure(Exception exception);
}
