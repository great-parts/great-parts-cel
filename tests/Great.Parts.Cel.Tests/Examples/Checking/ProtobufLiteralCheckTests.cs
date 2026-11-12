using System.Collections.Generic;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Tests.Examples.Library;
using Great.Parts.Cel.Tests.Examples.Values;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Checking;

// Type checking of the literal of a protobuf message, M{f: v} (the type mapping page, the rule of the literal of the table
// of protobuf messages): a name that the message has not is NoSuchField (2004), "undefined field '<name>' in type
// '<type>'", and a value that is not assignable to its field TypeMismatch (2002), "expected type '<field type>' but found
// '<type of the value>'"; both at the ":" of the field in the literal, the place that cel-go v0.32.0 gives too. The check
// goes on after an error, so every faulty field is an error, in the order of the text. The value of a field is assignable
// by the rules of the language: an int is not a uint nor a double, null fits a field of a message type and a wrapper,
// and dyn fits every field (its value is refused at the evaluation when it does not fit, the rows of
// ProtobufLiteralTests). The type of the literal is the message, and a literal of a type that is not a protobuf message is
// TypeNotConstructible at its "{" next to it in the same environment. Rows are those of ExtensionRows, "stage |
// expression | codes | messages": "^" marks the place of an error, "-" no check of the text.
[Trait("Requirement", "F-2")]
[Trait("Requirement", "F-4")]
public sealed class ProtobufLiteralCheckTests
{
    // The text of the rows below is written out: a row has no placeholders, since the braces of a literal would end one.
    private const string Errors = """
        Check | TestAllTypes{nope^: 1} | NoSuchField | undefined field 'nope' in type 'cel.expr.conformance.proto3.TestAllTypes'
        Check | TestAllTypes{single_int32: 1, Single_Int32^: 2} | NoSuchField | undefined field 'Single_Int32' in type 'cel.expr.conformance.proto3.TestAllTypes'
        Check | TestAllTypes{single_int32^: 'a'} | TypeMismatch | expected type 'int' but found 'string'
        Check | TestAllTypes{single_int32^: 1.5} | TypeMismatch | expected type 'int' but found 'double'
        Check | TestAllTypes{single_int32^: 1u} | TypeMismatch | expected type 'int' but found 'uint'
        Check | TestAllTypes{single_uint32^: 1} | TypeMismatch | expected type 'uint' but found 'int'
        Check | TestAllTypes{single_double^: 1} | TypeMismatch | expected type 'double' but found 'int'
        Check | TestAllTypes{single_float^: 1} | TypeMismatch | expected type 'double' but found 'int'
        Check | TestAllTypes{single_bytes^: 'a'} | TypeMismatch | expected type 'bytes' but found 'string'
        Check | TestAllTypes{single_string^: b'a'} | TypeMismatch | expected type 'string' but found 'bytes'
        Check | TestAllTypes{single_bool^: 1} | TypeMismatch | expected type 'bool' but found 'int'
        Check | TestAllTypes{standalone_enum^: 'a'} | TypeMismatch | expected type 'int' but found 'string'
        Check | TestAllTypes{single_int32^: null} | TypeMismatch | expected type 'int' but found 'null_type'
        Check | TestAllTypes{single_string^: null} | TypeMismatch | expected type 'string' but found 'null_type'
        Check | TestAllTypes{repeated_int32^: null} | TypeMismatch | -
        Check | TestAllTypes{repeated_int32^: ['a']} | TypeMismatch | -
        Check | TestAllTypes{repeated_nested_message^: [1]} | TypeMismatch | -
        Check | TestAllTypes{map_string_int64^: {1: 2}} | TypeMismatch | -
        Check | TestAllTypes{map_string_int64^: {'a': 'b'}} | TypeMismatch | -
        Check | TestAllTypes{map_int64_nested_type^: {1: TestAllTypes{}}} | TypeMismatch | -
        Check | TestAllTypes{standalone_message^: 1} | TypeMismatch | -
        Check | TestAllTypes{standalone_message^: TestAllTypes{}} | TypeMismatch | -
        Check | TestAllTypes{single_int64_wrapper^: 'a'} | TypeMismatch | -
        Check | TestAllTypes{single_double_wrapper^: 1} | TypeMismatch | -
        Check | TestAllTypes{single_timestamp^: 1} | TypeMismatch | -
        Check | TestAllTypes{single_struct^: {1: 2}} | TypeMismatch | -
        Check | TestAllTypes{standalone_message: TestAllTypes.NestedMessage{bb^: 'a'}} | TypeMismatch | expected type 'int' but found 'string'
        Check | TestAllTypes{nope^: 1, single_int32^: 'a', single_bool^: 1} | NoSuchField TypeMismatch TypeMismatch | undefined field 'nope' in type 'cel.expr.conformance.proto3.TestAllTypes' ;; expected type 'int' but found 'string' ;; expected type 'bool' but found 'int'
        """;

    private static readonly CelObjectType Deal = CelObjectTypeBuilder.ForDictionary("acme.crm.Deal")
        .AddField("amount", CelType.Int)
        .Build();

    private readonly CelEnvironment environment = ProtobufExample.Builder3()
        .AddTypes(CelTypeProvider.Create("crm", Deal))
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public void FieldThatIsNotInTheMessage_AndValueThatDoesNotFit_AreErrorsAtTheColon()
    {
        Assert.Empty(ExtensionRows.CheckErrors(environment, Errors));
    }

    // null fits a field of a message type and a wrapper, a value of dyn fits any field, and the literal has the type of
    // the message; a name written in full is the same type.
    [Fact]
    public void ValueThatFits_IsNotAnError()
    {
        var types = new (string Expression, CelType Type)[]
        {
            ("TestAllTypes{}", CelType.ObjectNamed(ProtobufExample.TypeName3)),
            ("cel.expr.conformance.proto3.TestAllTypes{}", CelType.ObjectNamed(ProtobufExample.TypeName3)),
            ("TestAllTypes{standalone_message: null}", CelType.ObjectNamed(ProtobufExample.TypeName3)),
            ("TestAllTypes{single_int64_wrapper: null, single_string_wrapper: null}", CelType.ObjectNamed(ProtobufExample.TypeName3)),
            ("TestAllTypes{single_any: null, single_value: null}", CelType.ObjectNamed(ProtobufExample.TypeName3)),
            ("TestAllTypes{single_int32: dyn('a'), repeated_int32: dyn(['a']), standalone_message: dyn(1)}", CelType.ObjectNamed(ProtobufExample.TypeName3)),
            ("TestAllTypes{single_int32: 1}.single_int32", CelType.Int),
            ("TestAllTypes{standalone_message: TestAllTypes.NestedMessage{bb: 1}}.standalone_message", CelType.ObjectNamed(ProtobufExample.NestedName3)),
            ("type(TestAllTypes{}) == TestAllTypes", CelType.Bool),
        };

        var failures = new List<string>();
        foreach ((string expression, CelType type) in types)
        {
            CelCompileResult<CelValue> compiled = environment.Compile<CelValue>(expression);
            if (!compiled.IsSuccess)
            {
                failures.Add(expression + ": " + compiled.Errors[0].Code + " " + compiled.Errors[0].Message);
            }
            else if (!compiled.GetProgramOrThrow().ResultType.Equals(type))
            {
                failures.Add(expression + ": " + compiled.GetProgramOrThrow().ResultType);
            }
        }

        Assert.Empty(failures);
    }

    // A type described at run time cannot be created: its literal is an error at the "{", and the literal of a message in the
    // same environment is not.
    [Theory]
    [InlineData("acme.crm.Deal{}", 13)]
    [InlineData("acme.crm.Deal{amount: 1}", 13)]
    [InlineData("acme.crm.Deal{nope: 'a'}", 13)]
    public void LiteralOfATypeThatIsNotAMessage_IsTypeNotConstructible(string expression, int offset)
    {
        CelError error = Assert.Single(environment.Compile<CelValue>(expression).Errors);

        Assert.Equal((CelErrorCode.TypeNotConstructible, CelErrorStage.Check, offset), (error.Code, error.Stage, error.Location.Offset));
        Assert.Equal(
            "type 'acme.crm.Deal' cannot be created by an expression: object literals are supported only for protobuf messages",
            error.Message);
        Assert.True(environment.Compile<CelValue>("TestAllTypes{single_int32: 1}").IsSuccess);
    }
}
