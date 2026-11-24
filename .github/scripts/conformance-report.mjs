#!/usr/bin/env node
// Builds the report of a run of conformance-master.yml for the branch `reports` (docs/design/eng-release.md, "Reports of the runs").
//
//   node .github/scripts/conformance-report.mjs [--suite cel-spec|cel-policy] --log <log of the suite> --commit-file <file with the commit> --output <directory>
//
// The suite is cel-spec (the default) or cel-policy. The log is the output of the suite; of its summary lines only those of the named
// suite are read, so a log with the lines of both suites gives the report of the one that is asked for. The commit file holds the full
// SHA of the commit of master on its first line: tests/cel-spec/COMMIT, or tests/cel-policy/VERSION, whose second line is the date.
// The directory gets <stamp>/report.json and <stamp>/report.md, the stamp being the date and the time of the report in UTC
// (yyyy-MM-ddTHHmmssZ). The workflow publish-report.yml writes the directory to the branch. No npm dependencies.

import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const args = process.argv.slice(2);
const option = (name) => {
  const i = args.indexOf(name);
  return i >= 0 ? args[i + 1] : undefined;
};

// What differs between the suites: the name in the summary line, the repository of the data, the kind of the report and the names of
// the fields of report.json. The fields of cel-spec are those of the reports already published.
// The line of the other suite never matches: the name is followed by a blank and a SHA.
const suites = {
  'cel-spec': {
    repository: 'cel-expr/cel-spec',
    kind: 'conformance-master',
    repositoryField: 'celSpecRepository',
    commitField: 'celSpecCommit',
  },
  'cel-policy': {
    repository: 'cel-expr/cel-policy',
    kind: 'conformance-master-policy',
    repositoryField: 'celPolicyRepository',
    commitField: 'celPolicyCommit',
  },
};

const suiteName = option('--suite') ?? 'cel-spec';
const suite = suites[suiteName];
const logPath = option('--log');
const commitPath = option('--commit-file');
const outputPath = option('--output');
if (!suite || !logPath || !commitPath || !outputPath) {
  console.error('Usage: conformance-report.mjs[--suite cel-spec|cel-policy] --log <file> --commit-file <file> --output <directory>');
  process.exit(2);
}

const log = readFileSync(logPath, 'utf8');
const masterCommit = readFileSync(commitPath, 'utf8').split(/\r?\n/)[0].trim();
if (!/^[0-9a-f]{40}$/.test(masterCommit)) {
  console.error(`The commit of ${suiteName} is not a full SHA: ${masterCommit}`);
  process.exit(1);
}

// "<suite> <SHA>: N/M passed, <date> (<cell>)", the last one of the log. The name of the suite has only letters and a hyphen.
const summaries = [...log.matchAll(new RegExp(`${suiteName} ([0-9a-f]{7}): (\\d+)/(\\d+) passed, (\\d{4}-\\d{2}-\\d{2}) \\(([^)]*)\\)`, 'g'))];
if (summaries.length === 0) {
  console.error('The log has no summary line.');
  process.exit(1);
}
const [line, shortCommit, passed, total, runDate, cell] = summaries[summaries.length - 1];
if (!masterCommit.startsWith(shortCommit)) {
  console.error(`The summary line names ${shortCommit}, the file names ${masterCommit}.`);
  process.exit(1);
}

// One line per case that fails: "<file>(<line>,<column>): error ENG801: <message>".
const failures = log.split(/\r?\n/).map((l) => /[^ ]+\(\d+,\d+\): error ENG801: .*/.exec(l)?.[0]).filter(Boolean);

const e = process.env;
const server = e.GITHUB_SERVER_URL ?? 'https://github.com';
const repository = e.GITHUB_REPOSITORY ?? 'great-parts/great-parts-cel';
const run = e.GITHUB_RUN_ID
  ? { id: Number(e.GITHUB_RUN_ID), attempt: Number(e.GITHUB_RUN_ATTEMPT ?? 1), event: e.GITHUB_EVENT_NAME ?? null, url: `${server}/${repository}/actions/runs/${e.GITHUB_RUN_ID}` }
  : null;
const commit = e.GITHUB_SHA ?? null;

const generatedAt = new Date().toISOString().slice(0, 19) + 'Z';
const stamp = `${generatedAt.slice(0, 10)}T${generatedAt.slice(11, 19).replaceAll(':', '')}Z`;

const report = {
  schema: 1,
  kind: suite.kind,
  generatedAtUtc: generatedAt,
  [suite.repositoryField]: suite.repository,
  [suite.commitField]: masterCommit,
  greatPartsCelCommit: commit,
  cell,
  runDate,
  passed: Number(passed),
  total: Number(total),
  failed: Number(total) - Number(passed),
  summary: line.replace(`${suiteName} `, `${suiteName} master `),
  failedCasesReported: failures.length,
  failures,
  run,
};

const shown = failures.slice(0, 500);
const md = [
  `# ${suiteName} master`,
  '',
  `- Date and time of the report (UTC): ${generatedAt.replace('T', ' ').replace('Z', '')}`,
  `- Commit of ${suiteName} (master): [\`${masterCommit}\`](https://github.com/${suite.repository}/commit/${masterCommit})`,
  commit ? `- Commit of ${repository}: [\`${commit}\`](${server}/${repository}/commit/${commit})` : '- Commit of the repository: not a GitHub Actions run',
  `- Cell: ${cell}`,
  `- Result: ${passed} of ${total} cases pass; the skip list of the pinned commit is not applied`,
  run ? `- Run: ${run.url} (GitHub keeps a run for 90 days; this report does not depend on it)` : '- Run: not a GitHub Actions run',
  '',
  '```text',
  report.summary,
  '```',
  '',
  `## Cases that fail: ${failures.length}`,
  '',
  '```text',
  ...shown,
  ...(failures.length > shown.length ? [`... and ${failures.length - shown.length} more, see report.json`] : []),
  '```',
  '',
].join('\n');

const directory = join(outputPath, stamp);
mkdirSync(directory, { recursive: true });
writeFileSync(join(directory, 'report.json'), JSON.stringify(report, null, 2) + '\n');
writeFileSync(join(directory, 'report.md'), md);
console.log(`Report ${stamp}: ${report.summary}; ${failures.length} failing cases listed`);
