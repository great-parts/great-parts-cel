<!--
Thanks for the pull request. CONTRIBUTING.md describes how a change is made.
Do not describe a vulnerability or its fix in a public pull request: report it as SECURITY.md describes.
-->

## Summary

<!-- What the change does and why. -->

## Related issue

<!-- For example: Fixes #123. A change of the public API is discussed in an issue before the pull request. -->

## Checklist

- [ ] `docker compose -f eng/compose.yaml run --rm test` (`eng/test`) passes: the last line is `test: passed`.
- [ ] If the change touches `README.md`, `docs/` or the XML documentation of `src/`: `docker compose -f eng/compose.yaml run --rm docs` (`eng/docs`) passes.
- [ ] Every change of behavior comes with a test that fails without it. A difference from cel-spec or cel-go is shown by the expression, its variables and the expected result.
- [ ] A change of the public API was discussed in the related issue, and the `PublicAPI` files of the package projects are updated.
- [ ] A change that users of the package can see has a line under `Unreleased` in `CHANGELOG.md`; a change of the result or of the error of an expression that was valid before is under `Behavior changes`.
- [ ] Every commit has a `Signed-off-by` line (`git commit -s`), certifying the Developer Certificate of Origin 1.1.

<!--
The maintainer merges the branch locally without rebasing it, so your commits keep their author and sign-off.
-->
