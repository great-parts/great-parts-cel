# cel-policy master

- Date and time of the report (UTC): 2026-11-27 11:28:41
- Commit of cel-policy (master): [`8e4cf0378e3f0d3406b537afbcc6f59562652ad6`](https://github.com/cel-expr/cel-policy/commit/8e4cf0378e3f0d3406b537afbcc6f59562652ad6)
- Commit of great-parts/great-parts-cel: [`0e7d21173aab3d5a6eff66b7932d7622e470afc5`](https://github.com/great-parts/great-parts-cel/commit/0e7d21173aab3d5a6eff66b7932d7622e470afc5)
- Cell: jit, linux-x64, net11.0
- Result: 85 of 91 cases pass; the skip list of the pinned commit is not applied
- Run: https://github.com/great-parts/great-parts-cel/actions/runs/45990620647 (GitHub keeps a run for 90 days; this report does not depend on it)

```text
cel-policy master 8e4cf03: 85/91 passed, 2026-11-27 (jit, linux-x64, net11.0)
```

## Cases that fail: 6

```text
tests/cel-policy/compile_errors/aggregate_false_condition/tests.yaml(20,11): error ENG801: case 'compile_errors/aggregate_false_condition/compile/false_condition' failed: no error contains 'condition is always false'; the errors are 'Condition is always false' (jit, linux-x64, net11.0)
tests/cel-policy/compile_errors/aggregate_heterogeneous_outputs/tests.yaml(20,11): error ENG801: case 'compile_errors/aggregate_heterogeneous_outputs/compile/incompatible_types' failed: no error contains 'incompatible output types: block has output type map(string, string), but previous outputs have type int'; the errors are 'incompatible output types: block has output type int, but previous outputs have type string' (jit, linux-x64, net11.0)
tests/cel-policy/compile_errors/syntax/tests.yaml(20,11): error ENG801: case 'compile_errors/syntax/compile/syntax_error' failed: no error contains 'mismatched input \'resource\''; the errors are 'syntax error: expected \')\', found \'resource\''; 'syntax error: expected \'}\', found \'2\''; 'syntax error: expected the end of the text, found \']\'' (jit, linux-x64, net11.0)
tests/cel-policy/context_pb/tests.yaml(19,15): error ENG801: case 'context_pb/valid/good spec' failed: the policy does not compile: 'undeclared reference to \'single_int32\' (in container \'cel.expr.conformance.proto3\')'; 'undeclared reference to \'single_int32\' (in container \'cel.expr.conformance.proto3\')'; 'undeclared reference to \'standalone_enum\' (in container \'cel.expr.conformance.proto3\')'; 'undeclared reference to \'repeated_nested_enum\' (in container \'cel.expr.conformance.proto3\')' (jit, linux-x64, net11.0)
tests/cel-policy/context_pb/tests.yaml(25,15): error ENG801: case 'context_pb/invalid/bad spec' failed: the policy does not compile: 'undeclared reference to \'single_int32\' (in container \'cel.expr.conformance.proto3\')'; 'undeclared reference to \'single_int32\' (in container \'cel.expr.conformance.proto3\')'; 'undeclared reference to \'standalone_enum\' (in container \'cel.expr.conformance.proto3\')'; 'undeclared reference to \'repeated_nested_enum\' (in container \'cel.expr.conformance.proto3\')' (jit, linux-x64, net11.0)
tests/cel-policy/k8s/tests.yaml(19,15): error ENG801: case 'k8s/invalid/restricted_container' failed: the policy does not compile: 'unsupported policy tag: kind'; 'unsupported policy tag: metadata'; 'unsupported policy tag: spec' (jit, linux-x64, net11.0)
```
