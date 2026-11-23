package main

import (
	"bufio"
	"bytes"
	"encoding/base64"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"runtime"
	"runtime/debug"
	"strconv"
	"time"

	"cel.dev/cel-go/cel"
	"cel.dev/cel-go/common/ast"
	"cel.dev/cel-go/common/types"
	"cel.dev/cel-go/common/types/ref"
	celpb "cel.dev/expr"
	testpb "cel.dev/expr/conformance/test"
	exprpb "google.golang.org/genproto/googleapis/api/expr/v1alpha1"
	"google.golang.org/protobuf/encoding/protojson"
	"google.golang.org/protobuf/encoding/prototext"
	"google.golang.org/protobuf/proto"
)

// celGoModule is the module whose version the handshake names.
const celGoModule = "cel.dev/cel-go"

// request is one line of standard input (docs/design/eng-oracle.md, "Protocol").
type request struct {
	ID          *int64                     `json:"id"`
	Op          string                     `json:"op"`
	Expr        *string                    `json:"expr"`
	Bindings    map[string]json.RawMessage `json:"bindings"`
	Name        string                     `json:"name"`
	Description string                     `json:"description"`
	Zone        *string                    `json:"zone"`
	Instant     *int64                     `json:"instant"`
	Checked     *string                    `json:"checked"`
	Patterns    []attributeJSON            `json:"patterns"`
}

// attributeJSON is an attribute of the op partial: a pattern of unknown data in a request, a trail of an unknown in an answer. It is
// a variable and the qualifiers under it.
type attributeJSON struct {
	Variable   string          `json:"variable"`
	Qualifiers []qualifierJSON `json:"qualifiers"`
}

// qualifierJSON is one qualifier of an attribute: kind is string, int, uint, bool, or any for a pattern that matches every qualifier;
// value is the text of the qualifier, empty for any.
type qualifierJSON struct {
	Kind  string `json:"kind"`
	Value string `json:"value"`
}

type handshake struct {
	Oracle string `json:"oracle"`
	Go     string `json:"go"`
}

// answer is one line of standard output.
type answer struct {
	ID        int64            `json:"id"`
	Outcome   string           `json:"outcome,omitempty"`
	Stage     string           `json:"stage,omitempty"`
	Message   *string          `json:"message,omitempty"`
	Value     json.RawMessage  `json:"value,omitempty"`
	Textproto *string          `json:"textproto,omitempty"`
	Seconds   *int64           `json:"seconds,omitempty"`
	Checked   *string          `json:"checked,omitempty"`
	Trails    *[]attributeJSON `json:"trails,omitempty"`
}

// outcome is the result of cel-go for one expression.
type outcome struct {
	kind    string // compile-error, eval-error, value or unknown
	stage   string // parse, check or program, for compile-error
	message string
	value   *celpb.Value
	trails  []attributeJSON
	parsed  *cel.Ast // nil when parsing failed
}

// serve writes the handshake and answers requests until standard input ends.
func serve(environment *cel.Env, stdin io.Reader, stdout io.Writer) error {
	out := bufio.NewWriter(stdout)
	encoder := json.NewEncoder(out)
	encoder.SetEscapeHTML(false)
	if err := encoder.Encode(handshake{Oracle: celGoModule + " " + celGoVersion(), Go: runtime.Version()}); err != nil {
		return err
	}
	if err := out.Flush(); err != nil {
		return err
	}
	in := bufio.NewReader(stdin)
	for {
		line, err := in.ReadBytes('\n')
		if len(line) == 0 && errors.Is(err, io.EOF) {
			return nil
		}
		if err != nil && !errors.Is(err, io.EOF) {
			return err
		}
		reply, failure := handle(environment, bytes.TrimRight(line, "\r\n"))
		if failure != nil {
			return failure
		}
		if err := encoder.Encode(reply); err != nil {
			return err
		}
		if err := out.Flush(); err != nil {
			return err
		}
	}
}

// celGoVersion is the version of cel-go that the oracle was built with.
func celGoVersion() string {
	if info, ok := debug.ReadBuildInfo(); ok {
		for _, module := range info.Deps {
			if module.Path == celGoModule {
				return module.Version
			}
		}
	}
	return "(unknown)"
}

// handle answers one request; an error ends the process with exit code 1.
func handle(environment *cel.Env, line []byte) (answer, error) {
	decoder := json.NewDecoder(bytes.NewReader(line))
	decoder.DisallowUnknownFields()
	var req request
	if err := decoder.Decode(&req); err != nil {
		return answer{}, fmt.Errorf("malformed request %q: %v", line, err)
	}
	if decoder.More() {
		return answer{}, fmt.Errorf("malformed request %q: more than one JSON value", line)
	}
	if req.ID == nil {
		return answer{}, fmt.Errorf("malformed request %q: no id", line)
	}
	if req.Op == "offset" {
		return handleOffset(req, line)
	}
	if req.Zone != nil || req.Instant != nil {
		return answer{}, fmt.Errorf("malformed request %q: zone and instant belong to the op offset", line)
	}
	if req.Op == "checked" {
		return handleChecked(environment, req, line)
	}
	if req.Checked != nil {
		return answer{}, fmt.Errorf("malformed request %q: checked belongs to the op checked", line)
	}
	if req.Expr == nil {
		return answer{}, fmt.Errorf("malformed request %q: no expr", line)
	}
	if req.Op != "eval" && req.Op != "case" && req.Op != "partial" {
		return answer{}, fmt.Errorf("malformed request %q: unknown op %q", line, req.Op)
	}
	if req.Op != "case" && (req.Name != "" || req.Description != "") {
		return answer{}, fmt.Errorf("malformed request %q: name and description belong to the op case", line)
	}
	if req.Op == "partial" && req.Patterns == nil {
		return answer{}, fmt.Errorf("malformed request %q: the op partial needs patterns", line)
	}
	if req.Op != "partial" && req.Patterns != nil {
		return answer{}, fmt.Errorf("malformed request %q: patterns belong to the op partial", line)
	}
	bindings, err := readBindings(req, line)
	if err != nil {
		return answer{}, err
	}
	var result outcome
	if req.Op == "partial" {
		result, err = evaluatePartial(environment, *req.Expr, bindings, req.Patterns)
	} else {
		result, err = evaluate(environment, *req.Expr, bindings)
	}
	if err != nil {
		return answer{}, fmt.Errorf("request %d: %v", *req.ID, err)
	}
	if req.Op == "case" {
		text, err := simpleTest(environment, req, bindings, result)
		if err != nil {
			return answer{}, fmt.Errorf("request %d: %v", *req.ID, err)
		}
		return answer{ID: *req.ID, Textproto: &text}, nil
	}
	return outcomeAnswer(*req.ID, result)
}

// readBindings decodes the bindings of a request: name -> cel.expr.Value in the JSON mapping of protobuf.
func readBindings(req request, line []byte) (map[string]*celpb.Value, error) {
	bindings := make(map[string]*celpb.Value, len(req.Bindings))
	for name, raw := range req.Bindings {
		value := &celpb.Value{}
		if err := protojson.Unmarshal(raw, value); err != nil {
			return nil, fmt.Errorf("malformed request %q: binding %q: %v", line, name, err)
		}
		bindings[name] = value
	}
	return bindings, nil
}

// outcomeAnswer is the answer to a request that evaluates: the outcome, with its stage, message or value.
func outcomeAnswer(id int64, result outcome) (answer, error) {
	reply := answer{ID: id, Outcome: result.kind, Stage: result.stage}
	if result.kind == "value" {
		raw, err := protojson.Marshal(result.value)
		if err != nil {
			return answer{}, fmt.Errorf("request %d: %v", id, err)
		}
		reply.Value = raw
	} else if result.kind == "unknown" {
		trails := result.trails
		reply.Trails = &trails
	} else {
		message := result.message
		reply.Message = &message
	}
	return reply, nil
}

// handleChecked answers the op checked (docs/design/ast-protobuf.md, "Exchange with cel-go"). With expr, it parses and checks
// the expression and answers the bytes of cel.AstToCheckedExpr, base64; a parse or check failure is the compile-error of
// the op eval. With checked, it loads the bytes with cel.CheckedExprToAst, plans them in the environment and evaluates on
// the bindings, as the op eval does with text.
func handleChecked(environment *cel.Env, req request, line []byte) (answer, error) {
	if req.Name != "" || req.Description != "" {
		return answer{}, fmt.Errorf("malformed request %q: name and description belong to the op case", line)
	}
	if (req.Expr == nil) == (req.Checked == nil) {
		return answer{}, fmt.Errorf("malformed request %q: the op checked has expr or checked, one of them", line)
	}
	if req.Expr != nil {
		if len(req.Bindings) != 0 {
			return answer{}, fmt.Errorf("malformed request %q: the op checked with expr has no bindings", line)
		}
		return checkedBytes(environment, *req.ID, *req.Expr)
	}
	data, err := base64.StdEncoding.DecodeString(*req.Checked)
	if err != nil {
		return answer{}, fmt.Errorf("malformed request %q: checked is not base64: %v", line, err)
	}
	bindings, err := readBindings(req, line)
	if err != nil {
		return answer{}, err
	}
	result, err := evaluateChecked(environment, data, bindings)
	if err != nil {
		return answer{}, fmt.Errorf("request %d: %v", *req.ID, err)
	}
	return outcomeAnswer(*req.ID, result)
}

// checkedBytes parses and checks an expression and gives the bytes of its CheckedExpr; an error means that cel-go has no
// message for a checked expression it checked.
func checkedBytes(environment *cel.Env, id int64, expr string) (answer, error) {
	parsed, issues := environment.Parse(expr)
	if issues.Err() != nil {
		message := issues.Err().Error()
		return answer{ID: id, Outcome: "compile-error", Stage: "parse", Message: &message}, nil
	}
	checked, issues := environment.Check(parsed)
	if issues.Err() != nil {
		message := issues.Err().Error()
		return answer{ID: id, Outcome: "compile-error", Stage: "check", Message: &message}, nil
	}
	message, err := cel.AstToCheckedExpr(checked)
	if err != nil {
		return answer{}, fmt.Errorf("request %d: AstToCheckedExpr: %v", id, err)
	}
	data, err := proto.MarshalOptions{Deterministic: true}.Marshal(message)
	if err != nil {
		return answer{}, fmt.Errorf("request %d: the checked expression does not marshal: %v", id, err)
	}
	encoded := base64.StdEncoding.EncodeToString(data)
	return answer{ID: id, Outcome: "checked", Checked: &encoded}, nil
}

// evaluateChecked loads the bytes of a CheckedExpr, as an application that stored one does, and runs it. Bytes that do not read
// as the message, or that cel-go refuses to load, and a program that it cannot plan are a compile error, stage load or program:
// the bytes are the other side's, so a divergence is the finding.
func evaluateChecked(environment *cel.Env, data []byte, bindings map[string]*celpb.Value) (outcome, error) {
	message := &exprpb.CheckedExpr{}
	if err := proto.Unmarshal(data, message); err != nil {
		return outcome{kind: "compile-error", stage: "load", message: err.Error()}, nil
	}
	loaded, err := cel.CheckedExprToAstWithSource(message, nil)
	if err != nil {
		return outcome{kind: "compile-error", stage: "load", message: err.Error()}, nil
	}
	program, err := environment.Program(loaded)
	if err != nil {
		return outcome{kind: "compile-error", stage: "program", message: err.Error()}, nil
	}
	return runProgram(environment, program, bindings, nil, false, nil)
}

// evaluate runs an expression through the stages of cel-go; an error means the result has no
// cel.expr.Value form or a binding has no CEL value.
func evaluate(environment *cel.Env, expr string, bindings map[string]*celpb.Value) (outcome, error) {
	return evaluateWith(environment, expr, bindings, false, nil)
}

// evaluatePartial runs an expression with cel.OptPartialEval and the patterns of the request as the unknown data
// (docs/design/eng-oracle.md, "Protocol"): an unknown result is the outcome unknown with its attribute trails.
func evaluatePartial(environment *cel.Env, expr string, bindings map[string]*celpb.Value, patterns []attributeJSON) (outcome, error) {
	unknowns, err := attributePatterns(patterns)
	if err != nil {
		return outcome{}, err
	}
	return evaluateWith(environment, expr, bindings, true, unknowns)
}

// attributePatterns makes the cel-go patterns of the unknown data of a request.
func attributePatterns(patterns []attributeJSON) ([]*cel.AttributePatternType, error) {
	unknowns := make([]*cel.AttributePatternType, 0, len(patterns))
	for _, pattern := range patterns {
		unknown := cel.AttributePattern(pattern.Variable)
		for _, qualifier := range pattern.Qualifiers {
			switch qualifier.Kind {
			case "string":
				unknown = unknown.QualString(qualifier.Value)
			case "int":
				number, err := strconv.ParseInt(qualifier.Value, 10, 64)
				if err != nil {
					return nil, fmt.Errorf("pattern %q: %v", pattern.Variable, err)
				}
				unknown = unknown.QualInt(number)
			case "uint":
				number, err := strconv.ParseUint(qualifier.Value, 10, 64)
				if err != nil {
					return nil, fmt.Errorf("pattern %q: %v", pattern.Variable, err)
				}
				unknown = unknown.QualUint(number)
			case "bool":
				flag, err := strconv.ParseBool(qualifier.Value)
				if err != nil {
					return nil, fmt.Errorf("pattern %q: %v", pattern.Variable, err)
				}
				unknown = unknown.QualBool(flag)
			case "any":
				unknown = unknown.Wildcard()
			default:
				return nil, fmt.Errorf("pattern %q: unknown qualifier kind %q", pattern.Variable, qualifier.Kind)
			}
		}
		unknowns = append(unknowns, unknown)
	}
	return unknowns, nil
}

// unknownTrails lists the attribute trails of an unknown result, in the order of the ids of its nodes. The ids themselves are not sent: the
// comparison does not use them.
func unknownTrails(unknown *types.Unknown) []attributeJSON {
	trails := []attributeJSON{}
	for _, id := range unknown.IDs() {
		found, _ := unknown.GetAttributeTrails(id)
		for _, trail := range found {
			qualifiers := []qualifierJSON{}
			for _, qualifier := range trail.QualifierPath() {
				qualifiers = append(qualifiers, trailQualifier(qualifier))
			}
			trails = append(trails, attributeJSON{Variable: trail.Variable(), Qualifiers: qualifiers})
		}
	}
	return trails
}

// trailQualifier writes a qualifier of an attribute trail: cel-go keeps the kinds string, int64, uint64 and bool.
func trailQualifier(qualifier any) qualifierJSON {
	switch value := qualifier.(type) {
	case string:
		return qualifierJSON{Kind: "string", Value: value}
	case int64:
		return qualifierJSON{Kind: "int", Value: strconv.FormatInt(value, 10)}
	case uint64:
		return qualifierJSON{Kind: "uint", Value: strconv.FormatUint(value, 10)}
	case bool:
		return qualifierJSON{Kind: "bool", Value: strconv.FormatBool(value)}
	}
	return qualifierJSON{Kind: "string", Value: fmt.Sprint(qualifier)}
}

// evaluateWith is evaluate with the partial mode: the program of cel.OptPartialEval, and the activation of PartialVars with the
// unknown patterns.
func evaluateWith(environment *cel.Env, expr string, bindings map[string]*celpb.Value, partial bool, unknowns []*cel.AttributePatternType) (outcome, error) {
	parsed, issues := environment.Parse(expr)
	if issues.Err() != nil {
		return outcome{kind: "compile-error", stage: "parse", message: issues.Err().Error()}, nil
	}
	checked, issues := environment.Check(parsed)
	if issues.Err() != nil {
		return outcome{kind: "compile-error", stage: "check", message: issues.Err().Error(), parsed: parsed}, nil
	}
	var programOptions []cel.ProgramOption
	if partial {
		programOptions = append(programOptions, cel.EvalOptions(cel.OptPartialEval))
	}
	program, err := environment.Program(checked, programOptions...)
	if err != nil {
		return outcome{kind: "compile-error", stage: "program", message: err.Error(), parsed: parsed}, nil
	}
	return runProgram(environment, program, bindings, parsed, partial, unknowns)
}

// runProgram evaluates a program on the bindings; parsed is the parsed form of the text, nil for a loaded expression. In the partial
// mode the activation is PartialVars with the unknown patterns, and an unknown result is the outcome unknown.
func runProgram(environment *cel.Env, program cel.Program, bindings map[string]*celpb.Value, parsed *cel.Ast, partial bool, unknowns []*cel.AttributePatternType) (outcome, error) {
	activation := make(map[string]any, len(bindings))
	for name, value := range bindings {
		converted, err := cel.ProtoAsValue(environment.CELTypeAdapter(), value)
		if err != nil {
			return outcome{}, fmt.Errorf("binding %q: %v", name, err)
		}
		activation[name] = converted
	}
	var input any = activation
	if partial {
		partialInput, err := cel.PartialVars(activation, unknowns...)
		if err != nil {
			return outcome{}, fmt.Errorf("partial variables: %v", err)
		}
		input = partialInput
	}
	result, _, err := program.Eval(input)
	if err != nil {
		return outcome{kind: "eval-error", message: err.Error(), parsed: parsed}, nil
	}
	if types.IsError(result) {
		return outcome{kind: "eval-error", message: errorMessage(result), parsed: parsed}, nil
	}
	if unknown, isUnknown := result.(*types.Unknown); isUnknown {
		if !partial {
			return outcome{}, fmt.Errorf("the result is unknown and has no cel.expr.Value form")
		}
		return outcome{kind: "unknown", trails: unknownTrails(unknown), parsed: parsed}, nil
	}
	// An optional as the whole result has no cel.expr.Value form either: cel-go gives one for an expression that its checker types as
	// another type, such as a presence test over an optional, so the case is an evaluation error with that message and the comparison
	// names the divergence, where an exit of the oracle would end the run.
	if optional, isOptional := result.(*types.Optional); isOptional {
		return outcome{kind: "eval-error", message: "the result is an optional and has no cel.expr.Value form: " + optional.String(), parsed: parsed}, nil
	}
	value, err := cel.ValueAsProto(result)
	if err != nil {
		return outcome{}, fmt.Errorf("the result of type %s has no cel.expr.Value form: %v", result.Type().TypeName(), err)
	}
	return outcome{kind: "value", value: value, parsed: parsed}, nil
}

func errorMessage(value ref.Val) string {
	if err, ok := value.Value().(error); ok {
		return err.Error()
	}
	return fmt.Sprint(value)
}

// simpleTest writes the case of the regression corpus: the outcome of cel-go is the expectation,
// and a compile error goes into eval_error.
func simpleTest(environment *cel.Env, req request, bindings map[string]*celpb.Value, result outcome) (string, error) {
	typeEnv, err := readVariables(environment, result.parsed)
	if err != nil {
		return "", err
	}
	test := &testpb.SimpleTest{
		Name:        req.Name,
		Description: req.Description,
		Expr:        *req.Expr,
		TypeEnv:     typeEnv,
	}
	if len(bindings) != 0 {
		test.Bindings = make(map[string]*celpb.ExprValue, len(bindings))
		for name, value := range bindings {
			test.Bindings[name] = &celpb.ExprValue{Kind: &celpb.ExprValue_Value{Value: value}}
		}
	}
	if result.kind == "value" {
		test.ResultMatcher = &testpb.SimpleTest_Value{Value: result.value}
	} else {
		test.ResultMatcher = &testpb.SimpleTest_EvalError{
			EvalError: &celpb.ErrorSet{Errors: []*celpb.Status{{Message: result.message}}},
		}
	}
	text, err := prototext.MarshalOptions{Multiline: true, Indent: "  "}.Marshal(test)
	if err != nil {
		return "", err
	}
	return string(text), nil
}

// readVariables gives the declarations of the manifest variables that a parsed expression reads,
// in the order of the manifest; an expression that does not parse reads none.
func readVariables(environment *cel.Env, parsed *cel.Ast) ([]*celpb.Decl, error) {
	if parsed == nil {
		return nil, nil
	}
	names := map[string]bool{}
	ast.PreOrderVisit(ast.NavigateAST(parsed.NativeRep()), ast.NewExprVisitor(func(e ast.Expr) {
		if e.Kind() == ast.IdentKind {
			names[e.AsIdent()] = true
		}
	}))
	var declarations []*celpb.Decl
	for _, variable := range environment.Variables() {
		if !names[variable.Name()] {
			continue
		}
		celType, err := types.TypeToProto(variable.Type())
		if err != nil {
			return nil, fmt.Errorf("variable %q: %v", variable.Name(), err)
		}
		declarations = append(declarations, &celpb.Decl{
			Name:     variable.Name(),
			DeclKind: &celpb.Decl_Ident{Ident: &celpb.Decl_IdentDecl{Type: celType}},
		})
	}
	return declarations, nil
}

// locations holds the zones that the oracle has loaded: loading one reads the archive of ZONEINFO.
var locations = map[string]*time.Location{}

// handleOffset answers the request offset: the offset in seconds that time.LoadLocation gives for a zone at an instant, in whole
// seconds since 1970-01-01T00:00:00Z (docs/design/eng-oracle.md, "Protocol"). A zone that does not load is the outcome
// zone-error with the message of Go.
func handleOffset(req request, line []byte) (answer, error) {
	if req.Zone == nil || req.Instant == nil {
		return answer{}, fmt.Errorf("malformed request %q: the op offset needs zone and instant", line)
	}
	if req.Expr != nil || req.Checked != nil || len(req.Bindings) != 0 || req.Name != "" || req.Description != "" {
		return answer{}, fmt.Errorf("malformed request %q: the op offset has only zone and instant", line)
	}
	location, ok := locations[*req.Zone]
	if !ok {
		loaded, err := time.LoadLocation(*req.Zone)
		if err != nil {
			message := err.Error()
			return answer{ID: *req.ID, Outcome: "zone-error", Message: &message}, nil
		}
		location = loaded
		locations[*req.Zone] = location
	}
	_, offset := time.Unix(*req.Instant, 0).In(location).Zone()
	seconds := int64(offset)
	return answer{ID: *req.ID, Outcome: "offset", Seconds: &seconds}, nil
}
