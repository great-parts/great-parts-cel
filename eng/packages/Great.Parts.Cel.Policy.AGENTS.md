# Great.Parts.Cel.Policy: instructions for AI coding agents

This file is for an agent that helps a developer use the package `Great.Parts.Cel.Policy` in an application. It is not for the contributors of the library; they have `AGENTS.md` in the repository. Read the `AGENTS.md` of `Great.Parts.Cel` first: a policy compiles into an ordinary program of a CEL environment. Versions 1.x of the library are not supported; this package exists from 2.0.

## What it is

- `CelPolicy.Parse(text, sourceName)` reads a CEL Policy document in YAML and returns the policy or all errors of the document;
- `policy.Compile<T>(environment)` compiles it into one `CelProgram<T>`, which evaluates as any program;
- every error has its line and column in the document in `CelError.SourceLocation`.

## Rules for the agent

- Give the environment the extension sets `optional` and `bindings`: the composed policy uses `optional.of`, `optional.none()` and `cel.@block`.
- The result type: the type of the outputs; `CelOptional<T>` when not every path gives an output; a list for `aggregate`.
- An expression written as a YAML block scalar (`|` or `>`) keeps its indentation and line breaks, as in cel-go: a multiline string literal in it contains the indentation.
- Do not read files in library code: pass the text of the document and its name.
