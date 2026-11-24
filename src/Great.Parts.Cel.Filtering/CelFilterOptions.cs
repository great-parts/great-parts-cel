using System;
using System.Collections.Immutable;

namespace Great.Parts.Cel.Filtering;

/// <summary>
/// The options of a <see cref="CelFilterCompiler"/>: which integer fields are enumerations, and which string fields a value
/// without a field is searched in.
/// </summary>
/// <remarks>
/// <para>
/// Both options are empty by default; a filter then compares enumeration fields only with integers and full names of
/// enumeration constants, and a value without a field is the error
/// <see cref="Diagnostics.CelErrorCode.InvalidFilterRestriction"/>. The compiler checks the options against its environment
/// when it is created.
/// </para>
/// <para>The options are immutable after initialization and can be shared between compilers and threads.</para>
/// </remarks>
public sealed class CelFilterOptions
{
    private readonly ImmutableDictionary<string, string> _enumTypes = ImmutableDictionary<string, string>.Empty;
    private readonly ImmutableArray<string> _searchFields = [];

    /// <summary>
    /// The fields of type <c>int</c> that hold an enumeration, and the full name of the enumeration type of each.
    /// </summary>
    /// <remarks>
    /// <para>
    /// CEL gives an enumeration field the type <c>int</c>, so the type of the field does not tell that it is an enumeration.
    /// A key names the field: the name of a variable of the environment, such as <c>state</c> for an environment with an
    /// input whose member is <c>state</c>, or the full name of an object type, a dot and the name of its field, such as
    /// <c>acme.Book.state</c>. A value is the full name of an enumeration type of the environment, such as
    /// <c>acme.State</c>.
    /// </para>
    /// <para>
    /// A filter compares such a field with the name of a constant, case-sensitively, quoted or not (<c>state = ACTIVE</c>),
    /// and allows only <c>=</c>, <c>!=</c> and <c>:</c>, as AIP-160 prescribes for enumerations.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Set to <see langword="null"/>.</exception>
    public ImmutableDictionary<string, string> EnumTypes
    {
        get => _enumTypes;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            _enumTypes = value;
        }
    }

    /// <summary>
    /// The fields of type <c>string</c> in which a value written without a field, such as <c>Dune</c> in the filter
    /// <c>Dune</c>, is searched.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A field is a path from a variable of the environment through fields of object types, such as <c>title</c> or
    /// <c>author.name</c>; its type is <c>string</c> or the wrapper of <c>string</c>. A value matches an item when one of
    /// the fields contains it, case-sensitively: the filter <c>Dune</c> becomes
    /// <c>title.contains("Dune") || author != null &amp;&amp; author.name.contains("Dune")</c>.
    /// </para>
    /// <para>
    /// Empty by default: AIP-160 lets a service restrict the filter, and without search fields a value without a field is
    /// the error <see cref="Diagnostics.CelErrorCode.InvalidFilterRestriction"/>. An array that was never set, the default
    /// value of <see cref="ImmutableArray{T}"/>, is the same as an empty one.
    /// </para>
    /// </remarks>
    public ImmutableArray<string> SearchFields
    {
        get => _searchFields;
        init => _searchFields = value.IsDefault ? [] : value;
    }
}
