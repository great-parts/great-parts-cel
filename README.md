# Reports of the runs

This branch holds the reports of the scheduled and manual runs of the workflows of the repository, and nothing else.
A workflow writes it with the token of its run; the rules of `main` do not apply to it. Nothing here is written by hand.

- `conformance-master/<date>T<time>Z/`: the result of the cel-spec suite on the master branch of cel-spec.
- `bench/<date>T<time>Z/`: the result of the speed comparison with other CEL libraries.
- `<directory>/latest.json`: the `report.json` of the last report of the directory.

A figure links a file at a commit of this branch, never the branch itself, so the link works as long as the figure is published.
