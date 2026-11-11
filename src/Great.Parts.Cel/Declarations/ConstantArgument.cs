namespace Great.Parts.Cel.Declarations;

// The argument of an overload that is prepared once when a call has a string literal there: the pattern of matches and
// matches_string. Checking and Evaluation refer to Declarations but not to Library, so the library fills the point in.
internal sealed class ConstantArgument(int index, ConstantPreparer prepare)
{
    // The position of the argument, the receiver counting as 0: 1 for matches and matches_string.
    internal int Index { get; } = index;

    internal ConstantPreparer Prepare { get; } = prepare;
}
