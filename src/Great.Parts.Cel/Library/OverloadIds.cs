namespace Great.Parts.Cel.Library;

// The overload identifiers of the standard library, letter for letter as in cel-go, so that the overload
// lists of the library and of the reference implementation compare without exceptions. An identifier of
// ordering is the prefix of its relation and the suffix of its kinds, such as less_int64_double.
internal static class OverloadIds
{
    internal const string Conditional = "conditional";
    internal const string LogicalAnd = "logical_and";
    internal const string LogicalOr = "logical_or";
    internal const string LogicalNot = "logical_not";
    internal const string NotStrictlyFalse = "not_strictly_false";

    internal const string EqualsId = "equals";
    internal const string NotEquals = "not_equals";

    internal const string AddBytes = "add_bytes";
    internal const string AddDouble = "add_double";
    internal const string AddInt64 = "add_int64";
    internal const string AddList = "add_list";
    internal const string AddString = "add_string";
    internal const string AddUInt64 = "add_uint64";
    internal const string DivideDouble = "divide_double";
    internal const string DivideInt64 = "divide_int64";
    internal const string DivideUInt64 = "divide_uint64";
    internal const string ModuloInt64 = "modulo_int64";
    internal const string ModuloUInt64 = "modulo_uint64";
    internal const string MultiplyDouble = "multiply_double";
    internal const string MultiplyInt64 = "multiply_int64";
    internal const string MultiplyUInt64 = "multiply_uint64";
    internal const string NegateDouble = "negate_double";
    internal const string NegateInt64 = "negate_int64";
    internal const string SubtractDouble = "subtract_double";
    internal const string SubtractInt64 = "subtract_int64";
    internal const string SubtractUInt64 = "subtract_uint64";

    internal const string Less = "less";
    internal const string LessEquals = "less_equals";
    internal const string Greater = "greater";
    internal const string GreaterEquals = "greater_equals";

    internal const string IndexList = "index_list";
    internal const string IndexMap = "index_map";
    internal const string InList = "in_list";
    internal const string InMap = "in_map";

    internal const string SizeBytes = "size_bytes";
    internal const string BytesSize = "bytes_size";
    internal const string SizeList = "size_list";
    internal const string ListSize = "list_size";
    internal const string SizeMap = "size_map";
    internal const string MapSize = "map_size";
    internal const string SizeString = "size_string";
    internal const string StringSize = "string_size";

    internal const string Type = "type";
    internal const string ToDyn = "to_dyn";
    internal const string BoolToBool = "bool_to_bool";
    internal const string StringToBool = "string_to_bool";
    internal const string BytesToBytes = "bytes_to_bytes";
    internal const string StringToBytes = "string_to_bytes";
    internal const string DoubleToDouble = "double_to_double";
    internal const string Int64ToDouble = "int64_to_double";
    internal const string StringToDouble = "string_to_double";
    internal const string UInt64ToDouble = "uint64_to_double";
    internal const string Int64ToInt64 = "int64_to_int64";
    internal const string DoubleToInt64 = "double_to_int64";
    internal const string StringToInt64 = "string_to_int64";
    internal const string UInt64ToInt64 = "uint64_to_int64";
    internal const string StringToString = "string_to_string";
    internal const string BoolToString = "bool_to_string";
    internal const string BytesToString = "bytes_to_string";
    internal const string DoubleToString = "double_to_string";
    internal const string Int64ToString = "int64_to_string";
    internal const string UInt64ToString = "uint64_to_string";
    internal const string UInt64ToUInt64 = "uint64_to_uint64";
    internal const string DoubleToUInt64 = "double_to_uint64";
    internal const string Int64ToUInt64 = "int64_to_uint64";
    internal const string StringToUInt64 = "string_to_uint64";

    internal const string ContainsString = "contains_string";
    internal const string EndsWithString = "ends_with_string";
    internal const string StartsWithString = "starts_with_string";

    internal const string Matches = "matches";
    internal const string MatchesString = "matches_string";
}
