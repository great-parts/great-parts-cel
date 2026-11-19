using System;

namespace Great.Parts.Cel.Values;

/// <summary>
/// The description of an object type whose instances are of the .NET type <typeparamref name="TInstance"/>, read without
/// boxing: the library reads a field of the input of the environment
/// (<see cref="CelEnvironmentBuilder.UseInput{TInput}"/>) through <see cref="GetField(in TInstance, int)"/> with the instance
/// passed by reference, so a structure is not copied to the heap.
/// </summary>
/// <typeparam name="TInstance">The .NET type of the instances: a class, a record or a structure.</typeparam>
/// <remarks>
/// <para>
/// The source generator (<c>CelTypeContext</c>) and <see cref="CelObjectTypeBuilder{TCarrier}"/> derive their descriptions
/// from this class. A description written by the application may derive from it or from <see cref="CelObjectType"/>;
/// with the latter, an input structure is boxed once for each evaluation.
/// </para>
/// <para>
/// The members that take the instance as <see cref="object"/> are sealed here and call the typed members, so a derived
/// description implements reading once. An instance that is not a <typeparamref name="TInstance"/> is a violation of the
/// calling contract of the library, which only passes instances that belong to the description.
/// </para>
/// </remarks>
public abstract class CelObjectType<TInstance> : CelObjectType
{
    /// <summary>
    /// Initializes a description with the CEL name of the type and its fields; the .NET type is
    /// <typeparamref name="TInstance"/>.
    /// </summary>
    /// <param name="name">The qualified CEL name of the type.</param>
    /// <param name="fields">The fields in the order of their numbers.</param>
    /// <exception cref="ArgumentException">The same conditions as the constructor of <see cref="CelObjectType"/>.</exception>
    protected CelObjectType(string name, ReadOnlySpan<CelObjectField> fields)
        : base(name, fields, typeof(TInstance))
    {
    }

    // A description of a type described at run time over a carrier class: the .NET type of the instances is none, so a value is
    // never found by its type; the builder of such types derives from this class to read its carrier without boxing.
    internal CelObjectType(string name, ReadOnlySpan<CelObjectField> fields, Type? clrType)
        : base(name, fields, clrType)
    {
    }

    /// <summary>
    /// Reads a field of an instance passed by reference: the same contract as
    /// <see cref="CelObjectType.GetField(object, int)"/>, without boxing a structure.
    /// </summary>
    /// <param name="instance">The instance; not <see langword="null"/>.</param>
    /// <param name="number">The number of a field of this description.</param>
    /// <returns>The value of the field, or an error value created by <see cref="CelObjectType.FieldError"/>.</returns>
    protected internal abstract CelValue GetField(in TInstance instance, int number);

    /// <summary>
    /// The units of cost that reading a field adds beyond the selection of a field: the same contract as
    /// <see cref="CelObjectType.FieldReadCost(object, int)"/>, without boxing a structure. The default is 0.
    /// </summary>
    /// <param name="instance">The instance; not <see langword="null"/>.</param>
    /// <param name="number">The number of a field of this description.</param>
    /// <returns>The units of cost.</returns>
    protected internal virtual long FieldReadCost(in TInstance instance, int number) => 0;

    /// <summary>Calls <see cref="GetField(in TInstance, int)"/> with the instance unboxed.</summary>
    /// <param name="instance">An instance of <typeparamref name="TInstance"/>.</param>
    /// <param name="number">The number of a field.</param>
    /// <returns>The value of the field.</returns>
    protected internal sealed override CelValue GetField(object instance, int number)
    {
        var typed = (TInstance)instance;
        return GetField(in typed, number);
    }

    /// <summary>Calls <see cref="FieldReadCost(in TInstance, int)"/> with the instance unboxed.</summary>
    /// <param name="instance">An instance of <typeparamref name="TInstance"/>.</param>
    /// <param name="number">The number of a field.</param>
    /// <returns>The units of cost.</returns>
    protected internal sealed override long FieldReadCost(object instance, int number)
    {
        var typed = (TInstance)instance;
        return FieldReadCost(in typed, number);
    }
}
