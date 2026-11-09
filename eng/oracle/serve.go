package main

import (
	"bufio"
	"bytes"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"runtime"
	"runtime/debug"
	"time"

	"cel.dev/cel-go/cel"
	"cel.dev/cel-go/common/ast"
	"cel.dev/cel-go/common/types"
	"cel.dev/cel-go/common/types/ref"
	celpb "cel.dev/expr"
	testpb "cel.dev/expr/conformance/test"
	"google.golang.org/protobuf/encoding/protojson"
	"google.golang.org/protobuf/encoding/prototext"
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
}

type handshake struct {
	Oracle string `json:"oracle"`
	Go     string `json:"go"`
}

// answer is one line of standard output.
type answer struct {
	ID        int64           `json:"id"`
	Outcome   string          `json:"outcome,omitempty"`
	Stage     string          `json:"stage,omitempty"`
	Message   *string         `json:"message,omitempty"`
	Value     json.RawMessage `json:"value,omitempty"`
	Textproto *string         `json:"textproto,omitempty"`
	Seconds   *int64          `json:"seconds,omitempty"`
}

// outcome is the result of cel-go for one expression.
type outcome struct {
	kind    string // compile-error, eval-error or value
	stage   string // parse, check or program, for compile-error
	message string
	value   *celpb.Value
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
	if req.Expr == nil {
		return answer{}, fmt.Errorf("malformed request %q: no expr", line)
	}
	if req.Op != "eval" && req.Op != "case" {
		return answer{}, fmt.Errorf("malformed request %q: unknown op %q", line, req.Op)
	}
	if req.Op == "eval" && (req.Name != "" || req.Description != "") {
		return answer{}, fmt.Errorf("malformed request %q: name and description belong to the op case", line)
	}
	bindings := make(map[string]*celpb.Value, len(req.Bindings))
	for name, raw := range req.Bindings {
		value := &celpb.Value{}
		if err := protojson.Unmarshal(raw, value); err != nil {
			return answer{}, fmt.Errorf("malformed request %q: binding %q: %v", line, name, err)
		}
		bindings[name] = value
	}
	result, err := evaluate(environment, *req.Expr, bindings)
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
	reply := answer{ID: *req.ID, Outcome: result.kind, Stage: result.stage}
	if result.kind == "value" {
		raw, err := protojson.Marshal(result.value)
		if err != nil {
			return answer{}, fmt.Errorf("request %d: %v", *req.ID, err)
		}
		reply.Value = raw
	} else {
		message := result.message
		reply.Message = &message
	}
	return reply, nil
}

// evaluate runs an expression through the stages of cel-go; an error means the result has no
// cel.expr.Value form or a binding has no CEL value.
func evaluate(environment *cel.Env, expr string, bindings map[string]*celpb.Value) (outcome, error) {
	parsed, issues := environment.Parse(expr)
	if issues.Err() != nil {
		return outcome{kind: "compile-error", stage: "parse", message: issues.Err().Error()}, nil
	}
	checked, issues := environment.Check(parsed)
	if issues.Err() != nil {
		return outcome{kind: "compile-error", stage: "check", message: issues.Err().Error(), parsed: parsed}, nil
	}
	program, err := environment.Program(checked)
	if err != nil {
		return outcome{kind: "compile-error", stage: "program", message: err.Error(), parsed: parsed}, nil
	}
	activation := make(map[string]any, len(bindings))
	for name, value := range bindings {
		converted, err := cel.ProtoAsValue(environment.CELTypeAdapter(), value)
		if err != nil {
			return outcome{}, fmt.Errorf("binding %q: %v", name, err)
		}
		activation[name] = converted
	}
	result, _, err := program.Eval(activation)
	if err != nil {
		return outcome{kind: "eval-error", message: err.Error(), parsed: parsed}, nil
	}
	if types.IsError(result) {
		return outcome{kind: "eval-error", message: errorMessage(result), parsed: parsed}, nil
	}
	if types.IsUnknown(result) {
		return outcome{}, fmt.Errorf("the result is unknown and has no cel.expr.Value form")
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
	if req.Expr != nil || len(req.Bindings) != 0 || req.Name != "" || req.Description != "" {
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
