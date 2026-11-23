namespace Great.Parts.Cel.Values;

// What an object literal costs beyond the unit of the literal and one unit for each field it sets, by the kind of description:
// the length of the strings and bytes it writes, the elements and pairs that the created code copies into collection members, or
// nothing.
internal enum LiteralCostRule
{
    // A message of protobuf, and a description written by the application over CelObjectType: one unit for each ten units or bytes
    // of the strings and bytes the literal writes directly.
    WrittenLength,

    // A description over a .NET type, which the generator creates: one unit for each element of a list and each pair of a map that the
    // literal sets, which the created code copies into a member.
    CopiedElements,

    // A description over a carrier, which keeps the values as they are: nothing.
    None,
}
