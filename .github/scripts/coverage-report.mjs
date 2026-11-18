#!/usr/bin/env node
// Turns the Cobertura report of Coverlet into the summary of the workflow coverage.yml (docs/design/eng-coverage.md).
//
//   node .github/scripts/coverage-report.mjs --input <coverage.cobertura.xml> --output <directory> [--threshold <percent>]
//
// The directory gets summary.md (the table of the projects and the files with the least coverage; the workflow adds it to the
// summary of the run), uncovered.md (every file that has an uncovered line, with the line ranges) and summary.json (the numbers).
// A statement is a line of code that has a sequence point, which is what Coverlet counts as a line. The threshold (80 by default) only
// sets the verdict of the text: the exit code is 0 for any coverage, 1 for a report that cannot be read. No npm dependencies.

import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const args = process.argv.slice(2);
const option = (name) => {
  const i = args.indexOf(name);
  return i >= 0 ? args[i + 1] : undefined;
};

const inputPath = option('--input');
const outputPath = option('--output');
const threshold = Number(option('--threshold') ?? '80');
if (!inputPath || !outputPath || !Number.isFinite(threshold)) {
  console.error('Usage: coverage-report.mjs --input <coverage.cobertura.xml> --output <directory> [--threshold <percent>]');
  process.exit(2);
}

mkdirSync(outputPath, { recursive: true });

const fail = (message) => {
  console.error(message);
  writeFileSync(join(outputPath, 'summary.md'), `### Coverage\n\nNo coverage report: ${message}\n`);
  process.exit(1);
};

if (!existsSync(inputPath)) {
  fail(`${inputPath} does not exist, so no test run produced a report.`);
}
const xml = readFileSync(inputPath, 'utf8');

const attributes = (text) => {
  const result = {};
  for (const m of text.matchAll(/([A-Za-z-]+)="([^"]*)"/g)) {
    result[m[1]] = m[2];
  }
  return result;
};

// The path of a file as the repository has it: from src/ on, whether the report keeps the absolute path of the runner or the
// /_/ of a deterministic build.
const repositoryPath = (file) => {
  const normal = file.replaceAll('\\', '/');
  const i = normal.search(/(^|\/)src\//);
  return i >= 0 ? normal.slice(i).replace(/^\//, '') : normal;
};

// Coverlet writes the file of a class relative to the common directory of the files of its assembly, and lists those directories as
// the sources of the report; with several assemblies a name alone does not say which source it belongs to. The directory of a project
// of src/ is named as its assembly, so the path is found from the name of the package.
const sources = [...xml.matchAll(/<source>([^<]*)<\/source>/g)].map((m) => m[1].replaceAll('\\', '/'));
const filePath = (packageName, filename) => {
  const normal = filename.replaceAll('\\', '/');
  if (normal.startsWith('/') || /^[A-Za-z]:\//.test(normal)) return repositoryPath(normal);
  if (sources.some((s) => s.endsWith(`/src/${packageName}/`))) return `src/${packageName}/${normal}`;
  return repositoryPath(`${sources[0] ?? ''}${normal}`);
};

// Per package (assembly): the lines of each file, the highest hit count of a line over its classes.
const projects = new Map();
for (const p of xml.matchAll(/<package\b([^>]*)>([\s\S]*?)<\/package>/g)) {
  const name = attributes(p[1]).name ?? '(no name)';
  const files = projects.get(name) ?? new Map();
  projects.set(name, files);
  for (const c of p[2].matchAll(/<class\b([^>]*)>([\s\S]*?)<\/class>/g)) {
    const filename = attributes(c[1]).filename;
    if (!filename) continue;
    const file = filePath(name, filename);
    const lines = files.get(file) ?? new Map();
    files.set(file, lines);
    // The lines of the class stand after its methods; the methods repeat them.
    const classLines = c[2].replace(/<methods>[\s\S]*?<\/methods>/g, '');
    for (const l of classLines.matchAll(/<line\b([^>]*?)\/?>/g)) {
      const a = attributes(l[1]);
      const number = Number(a.number);
      const hits = Number(a.hits);
      if (!Number.isInteger(number) || !Number.isFinite(hits)) continue;
      lines.set(number, Math.max(lines.get(number) ?? 0, hits));
    }
  }
}

if (projects.size === 0) {
  fail('the report has no package.');
}

// Ranges of uncovered lines: a run of uncovered lines with no covered line of code between them.
const ranges = (lines) => {
  const result = [];
  let start = null;
  let last = null;
  for (const number of [...lines.keys()].sort((a, b) => a - b)) {
    if (lines.get(number) === 0) {
      start ??= number;
      last = number;
    } else if (start !== null) {
      result.push(start === last ? `${start}` : `${start}-${last}`);
      start = null;
    }
  }
  if (start !== null) result.push(start === last ? `${start}` : `${start}-${last}`);
  return result;
};

const percent = (covered, valid) => (valid === 0 ? 100 : (covered * 100) / valid);
const format = (value) => `${value.toFixed(2)} %`;

const rows = [];
const fileRows = [];
let totalCovered = 0;
let totalValid = 0;
for (const [name, files] of [...projects].sort((a, b) => a[0].localeCompare(b[0]))) {
  let covered = 0;
  let valid = 0;
  for (const [file, lines] of files) {
    const fileValid = lines.size;
    const fileCovered = [...lines.values()].filter((h) => h > 0).length;
    covered += fileCovered;
    valid += fileValid;
    if (fileCovered < fileValid) {
      fileRows.push({ project: name, file, valid: fileValid, covered: fileCovered, uncovered: fileValid - fileCovered, ranges: ranges(lines) });
    }
  }
  totalCovered += covered;
  totalValid += valid;
  rows.push({ project: name, files: files.size, valid, covered, percent: percent(covered, valid) });
}
const total = percent(totalCovered, totalValid);

// Branch coverage is informational: the criterion is the coverage of statements.
const root = attributes((/<coverage\b([^>]*)>/.exec(xml) ?? [, ''])[1]);
const branchValid = Number(root['branches-valid'] ?? 0);
const branchCovered = Number(root['branches-covered'] ?? 0);

fileRows.sort((a, b) => b.uncovered - a.uncovered || a.file.localeCompare(b.file));

const verdict = total >= threshold
  ? `The statement coverage ${format(total)} is at least ${threshold} %.`
  : `The statement coverage ${format(total)} is below ${threshold} %: ${totalValid - totalCovered} statements of ${totalValid} are not covered.`;

const md = [
  '### Coverage of src/ (Coverlet)',
  '',
  `${verdict} Branch coverage (not a criterion): ${branchValid === 0 ? 'no branches' : format(percent(branchCovered, branchValid))}.`,
  '',
  '| Project | Files | Statements | Covered | Coverage |',
  '| :- | -: | -: | -: | -: |',
  ...rows.map((r) => `| ${r.project} | ${r.files} | ${r.valid} | ${r.covered} | ${format(r.percent)} |`),
  `| **Total** | ${rows.reduce((s, r) => s + r.files, 0)} | ${totalValid} | ${totalCovered} | **${format(total)}** |`,
  '',
  `The ${Math.min(30, fileRows.length)} files with the most uncovered statements (all ${fileRows.length}: uncovered.md in the artifact coverage):`,
  '',
  '| File | Uncovered | Statements | Coverage | Uncovered lines |',
  '| :- | -: | -: | -: | :- |',
  ...fileRows.slice(0, 30).map((f) => {
    const shown = f.ranges.slice(0, 12).join(', ') + (f.ranges.length > 12 ? `, … (${f.ranges.length - 12} more ranges)` : '');
    return `| \`${f.file}\` | ${f.uncovered} | ${f.valid} | ${format(percent(f.covered, f.valid))} | ${shown} |`;
  }),
  '',
].join('\n');

const uncovered = [
  '# Uncovered statements',
  '',
  `${verdict}`,
  '',
  '| Project | File | Uncovered | Statements | Coverage | Uncovered lines |',
  '| :- | :- | -: | -: | -: | :- |',
  ...fileRows.map((f) => `| ${f.project} | \`${f.file}\` | ${f.uncovered} | ${f.valid} | ${format(percent(f.covered, f.valid))} | ${f.ranges.join(', ')} |`),
  '',
].join('\n');

writeFileSync(join(outputPath, 'summary.md'), md);
writeFileSync(join(outputPath, 'uncovered.md'), uncovered);
writeFileSync(
  join(outputPath, 'summary.json'),
  JSON.stringify(
    {
      schema: 1,
      threshold,
      statements: { valid: totalValid, covered: totalCovered, percent: Number(total.toFixed(2)) },
      branches: { valid: branchValid, covered: branchCovered },
      projects: rows.map((r) => ({ ...r, percent: Number(r.percent.toFixed(2)) })),
      files: fileRows,
    },
    null,
    2,
  ) + '\n',
);

console.log(verdict);
for (const r of rows) console.log(`${r.project}: ${r.covered}/${r.valid} ${format(r.percent)}`);
