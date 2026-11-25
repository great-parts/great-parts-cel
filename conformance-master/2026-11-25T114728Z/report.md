# cel-spec master

- Date and time of the report (UTC): 2026-11-25 11:47:28
- Commit of cel-spec (master): [`3dd8162270505540492e4198a35717c173e1506a`](https://github.com/cel-expr/cel-spec/commit/3dd8162270505540492e4198a35717c173e1506a)
- Commit of great-parts/great-parts-cel: [`8fae68504e5bb35086ecfbe4fc8c6f5f685aba9b`](https://github.com/great-parts/great-parts-cel/commit/8fae68504e5bb35086ecfbe4fc8c6f5f685aba9b)
- Cell: jit, linux-x64, net11.0
- Result: 2755 of 2771 cases pass; the skip list of the pinned commit is not applied
- Run: https://github.com/great-parts/great-parts-cel/actions/runs/45607553676 (GitHub keeps a run for 90 days; this report does not depend on it)

```text
cel-spec master 3dd8162: 2755/2771 passed, 2026-11-25 (jit, linux-x64, net11.0)
```

## Cases that fail: 16

```text
tests/cel-spec/tests/simple/testdata/network_ext.textproto(103,3): error ENG801: case 'network_ext/ip_type/is_ip_cidr_compile_error' failed: check failed: 'found no matching overload for \'isIP\' applied to \'(net.CIDR)\'' (jit, linux-x64, net11.0)
tests/cel-spec/tests/simple/testdata/wrappers.textproto(348,3): error ENG801: case 'wrappers/duration/max_value' failed: check failed: '\'google.protobuf.Duration\' is not a type' (jit, linux-x64, net11.0)
tests/cel-spec/tests/simple/testdata/wrappers.textproto(360,3): error ENG801: case 'wrappers/duration/min_value' failed: check failed: '\'google.protobuf.Duration\' is not a type' (jit, linux-x64, net11.0)
tests/cel-spec/tests/simple/testdata/wrappers.textproto(372,3): error ENG801: case 'wrappers/duration/seconds_over' failed: check failed: '\'google.protobuf.Duration\' is not a type' (jit, linux-x64, net11.0)
tests/cel-spec/tests/simple/testdata/wrappers.textproto(381,3): error ENG801: case 'wrappers/duration/seconds_under' failed: check failed: '\'google.protobuf.Duration\' is not a type' (jit, linux-x64, net11.0)
tests/cel-spec/tests/simple/testdata/wrappers.textproto(390,3): error ENG801: case 'wrappers/duration/nanos_over' failed: check failed: '\'google.protobuf.Duration\' is not a type' (jit, linux-x64, net11.0)
tests/cel-spec/tests/simple/testdata/wrappers.textproto(399,3): error ENG801: case 'wrappers/duration/nanos_under' failed: check failed: '\'google.protobuf.Duration\' is not a type' (jit, linux-x64, net11.0)
tests/cel-spec/tests/simple/testdata/wrappers.textproto(408,3): error ENG801: case 'wrappers/duration/positive_seconds_negative_nanos' failed: check failed: '\'google.protobuf.Duration\' is not a type' (jit, linux-x64, net11.0)
tests/cel-spec/tests/simple/testdata/wrappers.textproto(417,3): error ENG801: case 'wrappers/duration/negative_seconds_positive_nanos' failed: check failed: '\'google.protobuf.Duration\' is not a type' (jit, linux-x64, net11.0)
tests/cel-spec/tests/simple/testdata/wrappers.textproto(439,3): error ENG801: case 'wrappers/timestamp/max_value' failed: check failed: '\'google.protobuf.Timestamp\' is not a type' (jit, linux-x64, net11.0)
tests/cel-spec/tests/simple/testdata/wrappers.textproto(451,3): error ENG801: case 'wrappers/timestamp/min_value' failed: check failed: '\'google.protobuf.Timestamp\' is not a type' (jit, linux-x64, net11.0)
tests/cel-spec/tests/simple/testdata/wrappers.textproto(462,3): error ENG801: case 'wrappers/timestamp/seconds_over' failed: check failed: '\'google.protobuf.Timestamp\' is not a type' (jit, linux-x64, net11.0)
tests/cel-spec/tests/simple/testdata/wrappers.textproto(471,3): error ENG801: case 'wrappers/timestamp/seconds_under' failed: check failed: '\'google.protobuf.Timestamp\' is not a type' (jit, linux-x64, net11.0)
tests/cel-spec/tests/simple/testdata/wrappers.textproto(480,3): error ENG801: case 'wrappers/timestamp/nanos_over' failed: check failed: '\'google.protobuf.Timestamp\' is not a type' (jit, linux-x64, net11.0)
tests/cel-spec/tests/simple/testdata/wrappers.textproto(489,3): error ENG801: case 'wrappers/timestamp/nanos_under' failed: check failed: '\'google.protobuf.Timestamp\' is not a type' (jit, linux-x64, net11.0)
tests/cel-spec/tests/simple/testdata/wrappers.textproto(498,3): error ENG801: case 'wrappers/timestamp/negative_seconds_negative_nanos' failed: check failed: '\'google.protobuf.Timestamp\' is not a type' (jit, linux-x64, net11.0)
```
