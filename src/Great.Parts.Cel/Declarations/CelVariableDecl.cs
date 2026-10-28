using System;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Declarations;

/// <summary>
/// The declaration of a variable: a name that expressions read, and its CEL type.
/// </summary>
/// <remarks>
/// <para>
/// Type checking uses the declared type, so an expression that uses the variable wrongly fails at
/// compilation, not at evaluation. The value comes from the bindings of each evaluation. A bound value
/// that does not have the declared type is an evaluation error
/// <see cref="CelErrorCode.BindingTypeMismatch"/> that names the variable; a variable without a bound
/// value is an evaluation error <see cref="CelErrorCode.Unbound"/>, raised only when the expression reads
/// the variable.
/// </para>
/// <para>
/// The environment builder takes such a declaration, or a name with a .NET type or a
/// <see cref="CelType"/>, which it turns into the same declaration. The name is an identifier or a
/// qualified name — identifiers separated by dots, such as <c>order</c> or <c>request.auth</c>; another
/// name is a build error <see cref="CelErrorCode.InvalidVariableName"/>. Two different declarations of one
/// name in one environment are a build error <see cref="CelErrorCode.DuplicateVariable"/> that names both
/// sources; a variable with the name of an identifier of the standard library, such as <c>int</c> or
/// <c>list</c>, is a build error <see cref="CelErrorCode.StandardLibraryConflict"/>; a type that no type
/// source or extension set of the environment declares is a build error
/// <see cref="CelErrorCode.TypeNotDeclared"/>.
/// </para>
/// <para>The declaration is immutable and can be shared between threads and environments.</para>
/// </remarks>
public sealed class CelVariableDecl
{
    /// <summary>
    /// Creates the declaration of a variable.
    /// </summary>
    /// <param name="name">The name expressions use to read the variable, such as <c>order</c>.</param>
    /// <param name="type">
    /// The CEL type of the variable, for example <see cref="CelType.String"/> or the type of an object
    /// description; <see cref="CelType.Dyn"/> accepts a value of any type and defers type checks to
    /// evaluation.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="type"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    public CelVariableDecl(string name, CelType type)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// The name of the variable.
    /// </summary>
    public string Name => throw new NotImplementedException();

    /// <summary>
    /// The declared CEL type of the variable.
    /// </summary>
    public CelType Type => throw new NotImplementedException();
}
