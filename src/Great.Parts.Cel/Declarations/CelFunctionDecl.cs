using System;
using System.Collections.Immutable;
using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel.Declarations;

/// <summary>
/// The declaration of a function: its name and its overloads.
/// </summary>
/// <remarks>
/// <para>
/// This is the full form of a function declaration. It covers what the environment builder cannot infer
/// from the signature of a .NET delegate: parameters of type <c>dyn</c>, generic overloads with type
/// parameters, and overloads called on a receiver. Each overload is implemented by the implementation
/// registered in the environment under its identifier.
/// </para>
/// <para>
/// The name is an identifier or a qualified name — identifiers separated by dots, such as
/// <c>acme.isInn</c>. Other names, including the names of operators and internal functions such as
/// <c>_+_</c> or <c>@in</c>, are a build error <see cref="CelErrorCode.InvalidFunctionName"/>: an
/// application does not overload operators. An application can add overloads to a function of the
/// standard library, such as <c>size</c>, when they do not overlap the standard overloads; a function
/// with the name and the form of a macro, such as <c>all</c> called on a receiver with two arguments, is
/// a build error <see cref="CelErrorCode.StandardLibraryConflict"/>. Declarations of one function from
/// several sources are merged by overload; the rules for overloads are on <see cref="CelOverloadDecl"/>.
/// </para>
/// <para>The declaration is immutable and can be shared between threads and environments.</para>
/// </remarks>
public sealed class CelFunctionDecl
{
    /// <summary>
    /// Creates the declaration of a function.
    /// </summary>
    /// <param name="name">The name of the function, such as <c>isInn</c> or <c>acme.isInn</c>.</param>
    /// <param name="overloads">The overloads of the function, at least one.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or an element of <paramref name="overloads"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty, or <paramref name="overloads"/> is empty.</exception>
    public CelFunctionDecl(string name, params ReadOnlySpan<CelOverloadDecl> overloads)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// The name of the function.
    /// </summary>
    public string Name => throw new NotImplementedException();

    /// <summary>
    /// The overloads of the function, in the order given to the constructor.
    /// </summary>
    public ImmutableArray<CelOverloadDecl> Overloads => throw new NotImplementedException();
}
