# cel-spec conformance skip list

Cases of the pinned cel-spec commit (tests/cel-spec/COMMIT) that may fail in the given scope. Every entry has a reason and, before a release, an issue. In release 1.0.0 the list may contain only cases with a defect in the cel-spec test data that no implementation passes under the cel-go conformance rule; such an entry also has a reference to the language definition or to a minimal case.

| Case | Scope | Reason | Reference | Issue |
|---|---|---|---|---|
| network_ext/ip_type/is_ip_cidr_compile_error | * * * | Defect in the cel-spec test data. The case expects an evaluation error for isIP(cidr('192.168.0.0/24')), but the expression is ill-typed: isIP takes a string, so type checking rejects it under any implementation, and the conformance rule counts a type-check error as a failure. | Minimal case: expression isIP(cidr('192.168.0.0/24')), no variables, network extension version 1. Type checking reports no matching overload for 'isIP' applied to '(net.CIDR)', because isIP is declared only for string; the case sets disable_check: false, so the check runs and the expected evaluation error is never reached. | |
