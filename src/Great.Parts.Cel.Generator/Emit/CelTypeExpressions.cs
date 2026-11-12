namespace Great.Parts.Cel.Generator;

// The CelType expression of a field: the tree of the model written as the calls of CelType. `CelType.Decimal` is written without
// a directive: the model has a decimal type only for a compilation that has Decimal128.
internal static class CelTypeExpressions
{
    private const string TypeClass = "global::Great.Parts.Cel.Types.CelType";

    internal static string Of(CelTypeModel type) => type.Kind switch
    {
        CelTypeKind.Int => TypeClass + ".Int",
        CelTypeKind.UInt => TypeClass + ".UInt",
        CelTypeKind.Double => TypeClass + ".Double",
        CelTypeKind.Bool => TypeClass + ".Bool",
        CelTypeKind.String => TypeClass + ".String",
        CelTypeKind.Bytes => TypeClass + ".Bytes",
        CelTypeKind.Timestamp => TypeClass + ".Timestamp",
        CelTypeKind.Duration => TypeClass + ".Duration",
        CelTypeKind.Decimal => TypeClass + ".Decimal",
        CelTypeKind.Wrapper => TypeClass + ".WrapperOf(" + Of(type.First!) + ")",
        CelTypeKind.List => TypeClass + ".ListOf(" + Of(type.First!) + ")",
        CelTypeKind.Map => TypeClass + ".MapOf(" + Of(type.First!) + ", " + Of(type.Second!) + ")",
        CelTypeKind.Object => TypeClass + ".ObjectNamed(" + Literals.Quote(type.Name) + ")",

        // The name is a C# expression, the full name of the descriptor of the message.
        CelTypeKind.Message => TypeClass + ".ObjectNamed(" + type.Name + ")",
        _ => TypeClass + ".Dyn",
    };
}
