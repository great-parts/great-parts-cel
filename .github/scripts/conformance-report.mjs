#!/usr/bin/env node
// Builds the report of a run of conformance-master.yml for the branch `reports` (docs/design/eng-release.md, "Reports of the runs").
//
//   node .github/scripts/conformance-report.mjs --log <log of the suite> --commit-file <file with the commit of cel-spec> --output <directory>
//
// The directory gets <stamp>/report.json and <stamp>/report.md, the stamp being the date and the time of the report in UTC
// (yyyy-MM-ddTHHmmssZ). The workflow publish-report.yml writes the directory to the branch. No npm dependencies.

import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const args = process.argv.slice(2);
const option = (name) => {
  const i = args.indexOf(name);
  return i >= 0 ? args[i + 1] : undefined;
};

const logPath = option('--log');
const commitPath = option('--commit-file');
const outputPath = option('--output');
if (!logPath || !commitPath || !outputPath) {
  console.error('Usage: conformance-report.mjs --log <file> --commit-file <file> --output <directory>');
  process.exit(2);
}

const log = readFileSync(logPath, 'utf8');
const celSpecCommit = readFileSync(commitPath, 'utf8').trim();
if (!/^[0-9a-f]{40}$/.test(celSpecCommit)) {
  console.error(`The commit of cel-spec is not a full SHA: ${celSpecCommit}`);
  process.exit(1);
}

// "cel-spec <SHA>: N/M passed, <date> (<cell>)", the last one of the log.
const summaries = [...log.matchAll(/cel-spec ([0-9a-f]{7}): (\d+)\/(\d+) passed, (\d{4}-\d{2}-\d{2}) \(([^)]*)\)/g)];
if (summaries.length === 0) {
  console.error('The log has no summary line.');
  process.exit(1);
}
const [line, shortCommit, passed, total, runDate, cell] = summaries[summaries.length - 1];
if (!celSpecCommit.startsWith(shortCommit)) {
  console.error(`The summary line names ${shortCommit}, the file names ${celSpecCommit}.`);
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
  kind: 'conformance-master',
  generatedAtUtc: generatedAt,
  celSpecRepository: 'cel-expr/cel-spec',
  celSpecCommit,
  greatPartsCelCommit: commit,
  cell,
  runDate,
  passed: Number(passed),
  total: Number(total),
  failed: Number(total) - Number(passed),
  summary: line.replace('cel-spec ', 'cel-spec master '),
  failedCasesReported: failures.length,
  failures,
  run,
};

const shown = failures.slice(0, 500);
const md = [
  '# cel-spec master',
  '',
  `- Date and time of the report (UTC): ${generatedAt.replace('T', ' ').replace('Z', '')}`,
  `- Commit of cel-spec (master): [\`${celSpecCommit}\`](https://github.com/cel-expr/cel-spec/commit/${celSpecCommit})`,
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
