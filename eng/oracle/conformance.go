package main

import (
	"errors"
	"fmt"
	"io"
	"os"
	"strings"

	"cel.dev/cel-go/cel"
	"cel.dev/cel-go/common"
	"cel.dev/cel-go/common/types"
	"cel.dev/cel-go/common/types/ref"
	"cel.dev/cel-go/ext"
	celpb "cel.dev/expr"
	test2pb "cel.dev/expr/conformance/proto2"
	test3pb "cel.dev/expr/conformance/proto3"
	testpb "cel.dev/expr/conformance/test"
	"google.golang.org/protobuf/encoding/prototext"
	"google.golang.org/protobuf/proto"
	"google.golang.org/protobuf/types/known/anypb"
)

// conformanceEnvironments are the two environments of the cel-go conformance runner: stdOpts of
// conformance/conformance_test.go of v0.32.0 without the test-only block macros, and with the sets
// that the files outside the runner need, sets and network (docs/design/eng-oracle.md, "One-time run
// of test files").
type conformanceEnvironments struct {
	withMacros *cel.Env
	noMacros   *cel.Env
}

func newConformanceEnvironments() (conformanceEnvironments, error) {
	noMacros, err := cel.NewCustomEnv(
		cel.StdLib(),
		cel.ClearMacros(),
		cel.OptionalTypes(),
		cel.EagerlyValidateDeclarations(true),
		cel.EnableErrorOnBadPresenceTest(true),
		cel.Types(&test2pb.TestAllTypes{}, &test2pb.Proto2ExtensionScopedMessage{}, &test3pb.TestAllTypes{}),
		ext.Bindings(),
		ext.Encoders(),
		ext.Lists(),
		ext.Math(),
		ext.Protos(),
		ext.Strings(),
		ext.TwoVarComprehensions(),
		ext.Sets(),
		ext.Network(),
		cel.EnableIdentifierEscapeSyntax())
	if err != nil {
		return conformanceEnvironments{}, err
	}
	withMacros, err := noMacros.Extend(cel.Macros(cel.StandardMacros...))
	if err != nil {
		return conformanceEnvironments{}, err
	}
	return conformanceEnvironments{withMacros: withMacros, noMacros: noMacros}, nil
}

// tally counts the cases of one file or of the whole run.
type tally struct {
	cases, passed, failed int
}

func (t *tally) add(other tally) {
	t.cases += other.cases
	t.passed += other.passed
	t.failed += other.failed
}

// runConformance runs cel-spec test files by the conformance rule and prints one line per case;
// it returns 0 when every case passes, 1 when a case fails and 2 when a file cannot be read.
func runConformance(paths []string, stdout, stderr io.Writer) int {
	environments, err := newConformanceEnvironments()
	if err != nil {
		fmt.Fprintf(stderr, "oracle: the conformance environment: %v\n", err)
		return 2
	}
	files := make([]*testpb.SimpleTestFile, 0, len(paths))
	for _, path := range paths {
		data, err := os.ReadFile(path)
		if err != nil {
			fmt.Fprintf(stderr, "oracle: %v\n", err)
			return 2
		}
		file := &testpb.SimpleTestFile{}
		if err := prototext.Unmarshal(data, file); err != nil {
			fmt.Fprintf(stderr, "oracle: %s is not a SimpleTestFile: %v\n", path, err)
			return 2
		}
		files = append(files, file)
	}
	var total tally
	for i, file := range files {
		var counts tally
		for _, section := range file.GetSection() {
			for _, test := range section.GetTest() {
				name := fmt.Sprintf("%s/%s/%s", file.GetName(), section.GetName(), test.GetName())
				counts.cases++
				if reason := conformanceCase(environments, name, test); reason == "" {
					counts.passed++
					fmt.Fprintf(stdout, "PASS %s\n", name)
				} else {
					counts.failed++
					fmt.Fprintf(stdout, "FAIL %s: %s; expr: %q\n", name, oneLine(reason), test.GetExpr())
				}
			}
		}
		fmt.Fprintf(stdout, "== %s: %d cases, %d PASS, %d FAIL\n", paths[i], counts.cases, counts.passed, counts.failed)
		total.add(counts)
	}
	fmt.Fprintf(stdout, "== total: %d cases, %d PASS, %d FAIL\n", total.cases, total.passed, total.failed)
	if total.failed != 0 {
		return 1
	}
	return 0
}

// conformanceCase runs one case as conformanceTest of the cel-go runner does and gives the reason
// of a failure, or an empty string when the case passes.
func conformanceCase(environments conformanceEnvironments, name string, test *testpb.SimpleTest) string {
	matcher := test.GetResultMatcher()
	if matcher == nil {
		// A case without a result matcher expects true (TestConformance of the runner).
		matcher = &testpb.SimpleTest_Value{Value: &celpb.Value{Kind: &celpb.Value_BoolValue{BoolValue: true}}}
	}
	environment := environments.withMacros
	if test.GetDisableMacros() {
		environment = environments.noMacros
	}
	compiled, issues := environment.ParseSource(common.NewStringSource(test.GetExpr(), name))
	if err := issues.Err(); err != nil {
		return "parse: " + err.Error()
	}
	var options []cel.EnvOption
	if test.GetContainer() != "" {
		options = append(options, cel.Container(test.GetContainer()))
	}
	for _, declaration := range test.GetTypeEnv() {
		option, err := cel.ProtoAsDeclaration(declaration)
		if err != nil {
			return "environment: " + err.Error()
		}
		options = append(options, option)
	}
	environment, err := environment.Extend(options...)
	if err != nil {
		return "environment: " + err.Error()
	}
	if !test.GetDisableCheck() {
		compiled, issues = environment.Check(compiled)
		if err := issues.Err(); err != nil {
			return "check: " + err.Error()
		}
	}
	if test.GetCheckOnly() {
		typed, ok := matcher.(*testpb.SimpleTest_TypedResult)
		if !ok {
			return fmt.Sprintf("check_only: the matcher is %T, not typed_result", matcher)
		}
		return typeReason(typed.TypedResult.GetDeducedType(), compiled.OutputType())
	}
	program, err := environment.Program(compiled)
	if err != nil {
		return "program: " + err.Error()
	}
	activation := make(map[string]any, len(test.GetBindings()))
	for variable, binding := range test.GetBindings() {
		value, err := bindingValue(environment.CELTypeAdapter(), binding)
		if err != nil {
			return fmt.Sprintf("binding %q: %v", variable, err)
		}
		activation[variable] = value
	}
	result, _, err := program.Eval(activation)
	switch m := matcher.(type) {
	case *testpb.SimpleTest_Value:
		return valueReason(m.Value, result, err)
	case *testpb.SimpleTest_TypedResult:
		if reason := valueReason(m.TypedResult.GetResult(), result, err); reason != "" {
			return reason
		}
		return typeReason(m.TypedResult.GetDeducedType(), compiled.OutputType())
	case *testpb.SimpleTest_EvalError:
		if err == nil && types.IsError(result) {
			err = result.(*types.Err).Unwrap()
		}
		if err == nil {
			return fmt.Sprintf("eval: got %s, want an error %s", describe(result), text(m.EvalError))
		}
		return ""
	default:
		return fmt.Sprintf("the matcher %T is not supported by the rule", matcher)
	}
}

// bindingValue converts a binding as exprValueToRefValue of the cel-go runner does; it is adapted from that
// function (see NOTICE).
func bindingValue(adapter types.Adapter, binding *celpb.ExprValue) (ref.Val, error) {
	switch binding.GetKind().(type) {
	case *celpb.ExprValue_Value:
		return cel.ProtoAsValue(adapter, binding.GetValue())
	case *celpb.ExprValue_Error:
		return types.NewErr("XXX add details later"), nil
	case *celpb.ExprValue_Unknown:
		var unknown *types.Unknown
		for _, id := range binding.GetUnknown().GetExprs() {
			if unknown == nil {
				unknown = types.NewUnknown(id, nil)
			}
			unknown = types.MergeUnknowns(types.NewUnknown(id, nil), unknown)
		}
		return unknown, nil
	}
	return nil, errors.New("unknown ExprValue kind")
}

// valueReason compares the result of an evaluation with the expected value.
func valueReason(want *celpb.Value, result ref.Val, err error) string {
	if err != nil {
		return fmt.Sprintf("eval: got the error %q, want %s", err.Error(), text(want))
	}
	if types.IsUnknown(result) {
		return fmt.Sprintf("eval: got an unknown, want %s", text(want))
	}
	got, err := cel.ValueAsProto(result)
	if err != nil {
		if types.IsError(result) {
			return fmt.Sprintf("eval: got the error value %q, want %s", errorMessage(result), text(want))
		}
		return fmt.Sprintf("eval: the result of type %s has no cel.expr.Value form: %v", result.Type().TypeName(), err)
	}
	if !valuesEqual(want, got) {
		return fmt.Sprintf("eval: got %s, want %s", text(got), text(want))
	}
	return ""
}

// typeReason compares the deduced type with the expected one, as diffType of the runner does.
func typeReason(want *celpb.Type, deduced *cel.Type) string {
	got, err := types.TypeToProto(deduced)
	if err != nil {
		return fmt.Sprintf("check: the deduced type %s has no cel.expr.Type form: %v", deduced, err)
	}
	if !proto.Equal(want, got) {
		return fmt.Sprintf("check: deduced type %s, want %s", text(got), text(want))
	}
	return ""
}

// valuesEqual compares values as diffValue of the runner does: field by field, scalars with Go ==,
// map entries in any order, an Any of a known type by its message.
func valuesEqual(want, got *celpb.Value) bool {
	switch w := want.GetKind().(type) {
	case *celpb.Value_DoubleValue:
		g, ok := got.GetKind().(*celpb.Value_DoubleValue)
		return ok && w.DoubleValue == g.DoubleValue
	case *celpb.Value_ListValue:
		g, ok := got.GetKind().(*celpb.Value_ListValue)
		if !ok || len(w.ListValue.GetValues()) != len(g.ListValue.GetValues()) {
			return false
		}
		for i, element := range w.ListValue.GetValues() {
			if !valuesEqual(element, g.ListValue.GetValues()[i]) {
				return false
			}
		}
		return true
	case *celpb.Value_MapValue:
		g, ok := got.GetKind().(*celpb.Value_MapValue)
		if !ok || len(w.MapValue.GetEntries()) != len(g.MapValue.GetEntries()) {
			return false
		}
		used := make([]bool, len(g.MapValue.GetEntries()))
		for _, entry := range w.MapValue.GetEntries() {
			found := false
			for i, candidate := range g.MapValue.GetEntries() {
				if !used[i] && valuesEqual(entry.GetKey(), candidate.GetKey()) && valuesEqual(entry.GetValue(), candidate.GetValue()) {
					used[i], found = true, true
					break
				}
			}
			if !found {
				return false
			}
		}
		return true
	case *celpb.Value_ObjectValue:
		g, ok := got.GetKind().(*celpb.Value_ObjectValue)
		return ok && anyEqual(w.ObjectValue, g.ObjectValue)
	default:
		return proto.Equal(want, got)
	}
}

// anyEqual compares two Any messages by their messages when the type is registered, otherwise by
// the type URL and the bytes.
func anyEqual(want, got *anypb.Any) bool {
	if want.GetTypeUrl() != got.GetTypeUrl() {
		return false
	}
	wantMessage, wantErr := want.UnmarshalNew()
	gotMessage, gotErr := got.UnmarshalNew()
	if wantErr != nil || gotErr != nil {
		return proto.Equal(want, got)
	}
	return proto.Equal(wantMessage, gotMessage)
}

func describe(result ref.Val) string {
	if types.IsUnknown(result) {
		return "an unknown"
	}
	if value, err := cel.ValueAsProto(result); err == nil {
		return text(value)
	}
	return fmt.Sprintf("a value of type %s", result.Type().TypeName())
}

func text(message proto.Message) string {
	return "{" + strings.TrimSpace(prototext.MarshalOptions{}.Format(message)) + "}"
}

// oneLine keeps a reason on the line of its case.
func oneLine(reason string) string {
	return strings.ReplaceAll(strings.TrimRight(reason, "\n"), "\n", " | ")
}
