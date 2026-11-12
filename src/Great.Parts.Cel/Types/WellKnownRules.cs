namespace Great.Parts.Cel.Types;

/// <summary>
/// Which full names of protobuf stand for which CEL type: the thirteen well-known messages whose CEL type is not an object
/// type. <c>google.protobuf.Timestamp</c> and <c>google.protobuf.Duration</c> are not among them: those two names are type
/// identifiers of the standard library.
/// </summary>
internal static class WellKnownRules
{
    private static readonly CelType StructType = CelType.MapOf(CelType.String, CelType.Dyn);
    private static readonly CelType ListValueType = CelType.ListOf(CelType.Dyn);

    /// <summary>
    /// The CEL type that a full name of protobuf stands for.
    /// </summary>
    /// <param name="fullName">The full name, without a leading dot.</param>
    /// <returns>The type of the table; <see langword="null"/> for any other name, which is an object type.</returns>
    internal static CelType? TypeOf(string fullName) => fullName switch
    {
        "google.protobuf.Any" => CelType.Any,
        "google.protobuf.Value" => CelType.Dyn,
        "google.protobuf.Struct" => StructType,
        "google.protobuf.ListValue" => ListValueType,
        "google.protobuf.BoolValue" => CelType.WrapperOf(CelType.Bool),
        "google.protobuf.BytesValue" => CelType.WrapperOf(CelType.Bytes),
        "google.protobuf.DoubleValue" or "google.protobuf.FloatValue" => CelType.WrapperOf(CelType.Double),
        "google.protobuf.Int32Value" or "google.protobuf.Int64Value" => CelType.WrapperOf(CelType.Int),
        "google.protobuf.UInt32Value" or "google.protobuf.UInt64Value" => CelType.WrapperOf(CelType.UInt),
        "google.protobuf.StringValue" => CelType.WrapperOf(CelType.String),
        _ => null,
    };
}
