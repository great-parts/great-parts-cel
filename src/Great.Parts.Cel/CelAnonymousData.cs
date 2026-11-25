using System;
using System.ComponentModel;

namespace Great.Parts.Cel;

/// <summary>
/// The registry of anonymous types for <see cref="CelExpression.Evaluate{T}(string, object, ulong, System.Threading.CancellationToken)"/>:
/// the source generator of the package registers here each anonymous type that the application passes to the simple entry.
/// Application code does not call it.
/// </summary>
/// <remarks>
/// <para>
/// For each anonymous type in a call of the simple entry, the generator writes a module initializer into the application
/// assembly that calls <see cref="Register{TData}"/> with an instance of the same anonymous type, the declarations of its
/// properties and a delegate that binds their values. In one compilation the same property names and types in the same
/// order are one anonymous type, so the delegate reads the properties directly: no reflection, and the call works under
/// Native AOT.
/// </para>
/// <para>
/// The registry maps a <see cref="Type"/> to its declarations and reader and is looked up by the run-time type of the data,
/// in constant time and without a lock: a registration replaces the dictionary that readers use by a copy that has one more
/// entry. It is filled when the modules of the application load and only grows: its size is the number of anonymous types in the
/// calls of the simple entry of the application, known at build time, not a quantity that depends on data at run time, so it
/// has no limit. Registering a type that is already registered changes nothing. All members can be called from any number
/// of threads at once.
/// </para>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class CelAnonymousData
{
    /// <summary>
    /// Registers an anonymous type: the variables its properties declare and the delegate that binds their values. Called by
    /// generated code from a module initializer.
    /// </summary>
    /// <remarks>
    /// The registration is keyed by <c>typeof(TData)</c>. A second registration of the same type keeps the first one. The
    /// delegates are called only when the simple entry evaluates data of this type: <paramref name="declare"/> once per hidden
    /// environment, <paramref name="bind"/> once per call; an exception they throw is not caught and reaches the caller of the
    /// simple entry unchanged.
    /// </remarks>
    /// <typeparam name="TData">The anonymous type.</typeparam>
    /// <param name="example">
    /// An instance of the anonymous type with default values, which only fixes <typeparamref name="TData"/>: the name of an
    /// anonymous type cannot be written in C#.
    /// </param>
    /// <param name="declare">Declares one variable per property, with the CEL type of the property.</param>
    /// <param name="bind">Binds the value of each property of an instance to its variable.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="example"/>, <paramref name="declare"/> or <paramref name="bind"/> is <see langword="null"/>.
    /// </exception>
    public static void Register<TData>(
        TData example,
        Action<CelEnvironmentBuilder> declare,
        Action<TData, CelBindings> bind)
        where TData : class
    {
        ArgumentNullException.ThrowIfNull(example);
        ArgumentNullException.ThrowIfNull(declare);
        ArgumentNullException.ThrowIfNull(bind);
        AnonymousRegistry.Register(declare, bind);
    }
}
