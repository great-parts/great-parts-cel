using System.Collections.Generic;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Syntax;

// The kind that the oneof expr_kind of an Expr message holds.
internal enum ExprCase
{
    None = 0,
    Constant,
    Ident,
    Select,
    Call,
    List,
    Struct,
    Comprehension,
}

// An Expr message as read, before it is checked and turned into a node. The name is the name of an identifier, the field of a
// selection, the function of a call, the type name of a message literal or the iteration variable of a comprehension.
internal sealed class ExprData
{
    internal long Id { get; set; }

    internal ExprCase Case { get; set; }

    internal CelValue? Value { get; set; }

    internal string Name { get; set; } = string.Empty;

    internal string SecondName { get; set; } = string.Empty;

    internal string AccuName { get; set; } = string.Empty;

    internal bool TestOnly { get; set; }

    // The operand of a selection, the target of a call, the range of a comprehension.
    internal ExprData? First { get; set; }

    internal ExprData? AccuInit { get; set; }

    internal ExprData? Condition { get; set; }

    internal ExprData? Step { get; set; }

    internal ExprData? Result { get; set; }

    // The arguments of a call, the elements of a list.
    internal List<ExprData> Items { get; } = [];

    internal List<int> OptionalIndices { get; } = [];

    internal List<EntryData> Entries { get; } = [];

    // Forgets what an earlier member of the oneof set: a later member replaces it.
    internal void Reset()
    {
        Case = ExprCase.None;
        Value = null;
        Name = string.Empty;
        SecondName = string.Empty;
        AccuName = string.Empty;
        TestOnly = false;
        First = null;
        AccuInit = null;
        Condition = null;
        Step = null;
        Result = null;
        Items.Clear();
        OptionalIndices.Clear();
        Entries.Clear();
    }
}

// An entry of a CreateStruct message: a map entry with a key expression or a message field with a name.
internal sealed class EntryData
{
    internal long Id { get; set; }

    internal string? FieldKey { get; set; }

    internal ExprData? MapKey { get; set; }

    internal ExprData? Value { get; set; }

    internal bool IsOptional { get; set; }
}

// The SourceInfo message as read: the location, the starts of the lines, the position of each id and the macro calls by the id of
// the expansion.
internal sealed class SourceData
{
    internal string? Location { get; set; }

    internal List<int> LineOffsets { get; } = [];

    internal Dictionary<long, int> Positions { get; } = [];

    internal Dictionary<long, ExprData> MacroCalls { get; } = [];
}
