namespace Great.Parts.Cel.Library.Unicode;

// What a name of \p{…} stands for: the number of its table, the number of the table that adds the code points of the case folding
// under (?i), or -1 when there is none, and whether the class is the complement of the table.
internal readonly struct TableName(int table, int foldTable, bool isComplement)
{
    internal int Table { get; } = table;

    internal int FoldTable { get; } = foldTable;

    internal bool IsComplement { get; } = isComplement;
}
