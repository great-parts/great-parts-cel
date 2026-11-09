using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Configuration;

// The keys of the JSON form of the environment configuration of cel-go that the format does not have (the page
// of the description, "Mapping onto builder calls"): imports, stdlib, context_variable, validators, features
// and limits at the top level, and is_type_param in a variable. Each is an error that names the key, at the
// key; what is inside the value is not read, so it adds no second error. They stay errors: the format is
// complete. In the marked documents each '|' is the place of one expected error (see JsonExample).
[Trait("Requirement", "F-7")]
[Trait("Requirement", "F-4")]
public sealed class EnvironmentJsonUnsupportedKeyTests
{
    [Theory]
    [InlineData("""{|"imports": [{"name": "acme.crm.Deal"}]}""", "imports")]
    [InlineData("""{|"imports": []}""", "imports")]
    [InlineData("""{|"stdlib": {"disabled": true, "disable_macros": false, "include_macros": ["has"], "exclude_functions": [{"name": "size"}]}}""", "stdlib")]
    [InlineData("""{|"stdlib": {}}""", "stdlib")]
    [InlineData("""{|"context_variable": {"type_name": "acme.crm.Request"}}""", "context_variable")]
    [InlineData("""{|"validators": [{"name": "cel.validator.duration"}, {"name": "cel.validator.regex_program_size_limit", "config": {"limit": 100}}]}""", "validators")]
    [InlineData("""{|"features": [{"name": "cel.feature.macro_call_tracking", "enabled": true}]}""", "features")]
    [InlineData("""{|"limits": [{"name": "cel.limit.expression_code_points", "value": 100}]}""", "limits")]
    public void KeyOfCelGoOutsideTheFormat_IsAnErrorAtTheKeyThatNamesIt(string marked, string key) =>
        JsonExample.AssertError(marked, mentions: key);

    // Next to the keys of the format they are the only error, and the keys of the format are read.
    [Fact]
    public void KeyOfCelGoOutsideTheFormat_NextToRightKeys_IsTheOnlyError() =>
        JsonExample.AssertError(
            """{"name": "m", "variables": [{"name": "x", "type": "int"}], |"stdlib": {"disabled": true}, "container": "acme"}""",
            "environment JSON 'm'",
            mentions: "stdlib");

    // Every one of them is reported, in the order of the text.
    [Fact]
    public void EveryKeyOfCelGoOutsideTheFormat_IsReported() =>
        JsonExample.AssertErrors(
            """{|"imports": [], |"stdlib": {}, |"context_variable": {}, |"validators": [], |"features": [], |"limits": []}""");

    // is_type_param is an error at the key; a type parameter is written ~T in a record, where an overload takes it.
    [Theory]
    [InlineData("""{"variables": [{"name": "x", "type_name": "T", |"is_type_param": true}]}""")]
    [InlineData("""{"variables": [{"name": "x", "type": "int", |"is_type_param": false}]}""")]
    public void IsTypeParam_IsAnErrorAtTheKey(string marked) =>
        JsonExample.AssertError(marked, mentions: "is_type_param");

    // The object form of a record, with the keys type_name, params and is_type_param, is outside the format
    // wherever a record stands; the keys of the object are not read.
    [Theory]
    [InlineData("""{"variables": [{"name": "x", "type": |{"type_name": "list", "params": [{"type_name": "int"}]}}]}""")]
    [InlineData("""{"functions": [{"name": "f", "overloads": [{"id": "a", "return": |{"type_name": "int", "is_type_param": false}}]}]}""")]
    [InlineData("""{"functions": [{"name": "f", "overloads": [{"id": "a", "args": [|{"type_name": "int"}], "return": "int"}]}]}""")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": "a.A", "fields": [{"name": "x", "type": |{"type_name": "int"}}]}]}}""")]
    public void ObjectFormOfARecord_IsAnErrorAtTheValue(string marked) =>
        JsonExample.AssertError(marked);

    // The spelling of the keys is that of cel-go v0.32.0: camelCase is another key.
    [Theory]
    [InlineData("""{|"contextVariable": {}}""")]
    [InlineData("""{"variables": [{"name": "x", "type": "int", |"typeName": "int"}]}""")]
    public void CamelCaseKey_IsAnUnknownKey(string marked) =>
        JsonExample.AssertErrors(marked);
}
