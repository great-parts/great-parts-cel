using System;
using System.Collections.Generic;
using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel;

// Abbreviations of qualified names, the "imports" of a CEL policy and cel.Abbrevs of cel-go. A separate part of the class, so that the
// members of other features do not meet it in one file.
public sealed partial class CelEnvironmentBuilder
{
    // The qualified names of AddAbbreviation, in the order of the calls; Build checks them.
    private readonly List<string> _abbreviations = [];

    /// <summary>
    /// Adds an abbreviation: the last part of a qualified name, such as <c>Document</c> for
    /// <c>dev.cel.example.Document</c>, refers in expressions to the qualified name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A name in an expression whose first part is the abbreviation resolves only to the qualified name with the rest of the
    /// name appended, before the candidates of the container (<see cref="UseContainer(string)"/>): <c>Document{title: "a"}</c>
    /// creates a <c>dev.cel.example.Document</c>, and <c>Document.Section</c> names <c>dev.cel.example.Document.Section</c>.
    /// A name written with a leading dot is not abbreviated. Abbreviations apply to identifiers, selections, function names and
    /// message names, as in cel-go.
    /// </para>
    /// <para>
    /// Surrounding white space of the name is dropped, as in cel-go, and the name without it is the name in
    /// <see cref="CelError.Subject"/> and in the message.
    /// A name that is not a qualified identifier with at least two parts, a name with a leading dot, a second abbreviation with
    /// the same last part (even with the same name), and an abbreviation whose last part equals the container or begins the container
    /// followed by a dot are the error
    /// <see cref="CelErrorCode.InvalidAbbreviation"/> of <see cref="Build"/>, with the name in <see cref="CelError.Subject"/>.
    /// A container <c>x.T</c> with the abbreviation <c>a.b.T</c> is valid, as in cel-go.
    /// </para>
    /// </remarks>
    /// <param name="qualifiedName">The qualified name, for example of a message type.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="qualifiedName"/> is <see langword="null"/>.</exception>
    public CelEnvironmentBuilder AddAbbreviation(string qualifiedName)
    {
        ArgumentNullException.ThrowIfNull(qualifiedName);
        _abbreviations.Add(qualifiedName);
        return this;
    }
}
