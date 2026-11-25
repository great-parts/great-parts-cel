#!/usr/bin/env node
// Runner of the benchmark rig: builds and runs SpeedBench with the .NET SDK on the machine, then builds the summary.
// The workflow .github/workflows/bench.yml starts it on a GitHub-hosted runner (docs/design/eng-bench.md).
//
//   node bench/run.mjs                    verify, then one measurement run of all cases (Job.Default)
//   node bench/run.mjs --runs 3           verify, then three separate measurement runs one after another
//   node bench/run.mjs --verify-only      only the verification
//   node bench/run.mjs --job Dry          another BenchmarkDotNet job (Dry: one call per case, a check without measurement)
//   node bench/run.mjs --cases E1,E5      only these cases
//   node bench/run.mjs --filter '*Evaluate*'   BenchmarkDotNet filter (default '*')
//   node bench/run.mjs --summary-only     rebuild the summaries from saved runs
//   node bench/run.mjs --no-thresholds    without the measurement of the limits of the speed-ups
//   node bench/run.mjs --thresholds-only  only the measurement of the limits (no verification, no cases of the rig)
//
// After the runs of the rig the script measures the limits of the speed-ups of the library (bench/ThresholdBench): the search of the
// literal prefix of matches around 64 code units, the build of the automaton of the pattern E5, the scan and the buckets of the index
// of distinct and sets at lists of 8 to 64 values; and the diagnostics without a verdict: the search of a substring by the functions of
// strings, the simple entry, CelMap.Create. The job (--job) is the one of the rig. The verdicts and the diagnostics are in the section
// "Limits of the speed-ups" of the summary.
//
// Results go to bench/results/ (ignored by git): results/runs/<k>/ per run, results/summary.md over all runs,
// results/thresholds/ (the reports of BenchmarkDotNet, meta.json and thresholds.json with the verdicts).
// No npm dependencies.

import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const project = join('SpeedBench', 'SpeedBench.csproj');
const results = join(here, 'results');
const runsDir = join(results, 'runs');
const verifyJson = join(results, 'verify.json');
const summaryMd = join(results, 'summary.md');
const thresholdProject = join('ThresholdBench', 'Great.Parts.Cel.ThresholdBench.csproj');
const thresholdsDir = join(results, 'thresholds');
const thresholdsJson = join(thresholdsDir, 'thresholds.json');
const THRESHOLD = 1.1;
// A row has at least MIN_RUNS runs; a row whose runs lie on both sides of the threshold is marked at MAX_RUNS runs.
const MIN_RUNS = 3;
const MAX_RUNS = 5;
// The library whose rows get the closing block of the summary. BENCH_SUBJECT names another one, only to check the summary on saved runs.
// Its variants have short names (BenchmarkDotNet cuts a parameter "library:case" at 20 characters): GreatPartsCel is the variant with
// bindings, GPCel-Input the input of the environment, GPCel-Simple the simple entry.
const SUBJECT = process.env.BENCH_SUBJECT ?? 'GreatPartsCel';
const SUBJECT_VARIANTS = process.env.BENCH_SUBJECT ? [SUBJECT] : [SUBJECT, 'GPCel-Input', 'GPCel-Simple'];
const isSubject = (library) => SUBJECT_VARIANTS.includes(library);
// Limits of the speed-ups that the verdicts of the thresholds check.
const SEARCH_LIMIT = 64;
// The diagnostic of the search of the functions of strings, which has no verdict: contains on (ab)^5000 with a pattern of each length.
const COST_PATTERN_LENGTHS = [64, 256, 1024];
const COST_TEXT_LENGTH = 10000;
const AUTOMATON_LIMIT_NS = 2000;
// The index of distinct and sets: the lengths of the lists at which the scan and the buckets are measured, and the length up to which
// the library scans (ValueIndex.ScanLimit). The measurement states no verdict on it.
const HASH_SIZES = [8, 16, 24, 32, 48, 64];
const HASH_SCAN_LIMIT = 32;
const HASH_EVALUATION_SIZES = [16, 32, 33, 48, 64];

const args = process.argv.slice(2);
const flag = (name) => args.includes(name);
const option = (name) => {
  const i = args.indexOf(name);
  return i >= 0 ? args[i + 1] : undefined;
};

// ---------- the .NET SDK ----------

// The words that start dotnet: BENCH_DOTNET replaces "dotnet" for a check inside a container that has the SDK, for example
// BENCH_DOTNET="docker exec -w /repo/bench <container> dotnet". The paths of the arguments are relative to bench/, which the container
// mounts, so the results appear on the machine; the environment variable BENCH_CASES does not reach the container, so --cases is not
// for this form. The workflow does not set it.
const dotnetWords = (process.env.BENCH_DOTNET ?? 'dotnet').split(/\s+/).filter(Boolean);

// Runs dotnet in the directory of the rig; returns the exit code.
function dotnet(command, env = {}) {
  console.log(`> ${dotnetWords.join(' ')} ${command.join(' ')}`);
  const r = spawnSync(dotnetWords[0], [...dotnetWords.slice(1), ...command], { stdio: 'inherit', cwd: here, env: { ...process.env, ...env } });
  if (r.error) {
    console.error(`cannot start dotnet: ${r.error.message}`);
    process.exit(1);
  }
  return r.status ?? 1;
}

function dotnetOrExit(command, env = {}) {
  const status = dotnet(command, env);
  if (status !== 0) {
    console.error(`dotnet exited with code ${status}`);
    process.exit(status);
  }
}

// The rig runs on .NET 11 under JIT.
const FRAMEWORK = 'net11.0';
const run = (...rest) => ['run', '-c', 'Release', '--no-build', '--project', 'SpeedBench', '-f', FRAMEWORK, '--', ...rest];
const runThresholds = (...rest) => ['run', '-c', 'Release', '--no-build', '--project', 'ThresholdBench', '-f', FRAMEWORK, '--', ...rest];

// ---------- formatting ----------

function fmtTime(ns) {
  if (ns === undefined || ns === null || Number.isNaN(ns)) return '-';
  if (Math.abs(ns) >= 1e6) return `${(ns / 1e6).toFixed(2)} ms`;
  if (Math.abs(ns) >= 1e3) return `${(ns / 1e3).toFixed(2)} us`;
  return `${ns.toFixed(1)} ns`;
}

function fmtBytes(b) {
  if (b === undefined || b === null || Number.isNaN(b)) return '-';
  if (Math.abs(b) >= 1024 * 1024) return `${(b / 1024 / 1024).toFixed(2)} MB`;
  if (Math.abs(b) >= 1024) return `${(b / 1024).toFixed(2)} KB`;
  return `${b} B`;
}

const esc = (s) => s.replaceAll('|', '\\|');
const cut = (s, n = 80) => (s.length > n ? `${s.slice(0, n)}...` : s);

function median(values) {
  const v = [...values].sort((a, b) => a - b);
  const m = Math.floor(v.length / 2);
  return v.length % 2 === 1 ? v[m] : (v[m - 1] + v[m]) / 2;
}

// ---------- loading ----------

// [BenchmarkDotNet method, label, whether the cost of text creation is subtracted]
const OPERATIONS = [
  ['Compile', 'compile', true],
  ['Evaluate', 'evaluate', false],
  ['FullCycle', 'full cycle', true],
];

function loadVerify() {
  return existsSync(verifyJson) ? JSON.parse(readFileSync(verifyJson, 'utf8')) : null;
}

function runNumbers() {
  if (!existsSync(runsDir)) return [];
  return readdirSync(runsDir).filter((d) => /^\d+$/.test(d)).map(Number).sort((a, b) => a - b);
}

function loadRunMeta(k) {
  const f = join(runsDir, String(k), 'run.json');
  return existsSync(f) ? JSON.parse(readFileSync(f, 'utf8')) : { run: k };
}

function loadBdn(bdnDir) {
  const dir = join(bdnDir, 'results');
  if (!existsSync(dir)) return null;
  const out = { host: null, ops: [], text: [] };
  for (const f of readdirSync(dir)) {
    if (!f.endsWith('-report-full.json')) continue;
    const j = JSON.parse(readFileSync(join(dir, f), 'utf8'));
    out.host = out.host ?? j.HostEnvironmentInfo;
    for (const b of j.Benchmarks) {
      const row = {
        method: b.Method,
        param: /(?:Target|Case)=([^\s,]+)/.exec(b.Parameters)?.[1] ?? b.Parameters,
        median: b.Statistics?.Median,
        alloc: b.Memory?.BytesAllocatedPerOperation,
      };
      if (b.Type === 'TextBench') out.text.push(row);
      else out.ops.push(row);
    }
  }
  return out;
}

// One run: per scenario the text row and, per operation, the measured libraries with the ratio
// to the fastest correct library of the same run (decisive column: minus text creation).
function measure(bdn, verify) {
  const scenarios = [...new Set([...bdn.text.map((r) => r.param), ...bdn.ops.map((r) => r.param.split(':')[1])])];
  const order = (verify ?? []).map((r) => r.Scenario);
  scenarios.sort((a, b) => order.indexOf(a) - order.indexOf(b));
  const libraries = [...new Set((verify ?? []).map((r) => r.Library))];
  return scenarios.map((s) => {
    const text = bdn.text.find((r) => r.param === s);
    const operations = [];
    for (const [method, label, withText] of OPERATIONS) {
      const measured = libraries
        .map((l) => ({ l, r: bdn.ops.find((x) => x.method === method && x.param === `${l}:${s}`) }))
        .filter((x) => x.r)
        .map(({ l, r }) => ({
          l,
          raw: r.median,
          rawAlloc: r.alloc,
          time: withText ? r.median - (text?.median ?? NaN) : r.median,
          alloc: withText ? r.alloc - (text?.alloc ?? NaN) : r.alloc,
        }));
      if (measured.length === 0) continue;
      // A variant of the subject is compared with the other libraries and with itself, not with its own other variants.
      for (const m of measured) {
        const pool = isSubject(m.l) ? measured.filter((x) => !isSubject(x.l) || x === m) : measured;
        const fastest = Math.min(...pool.map((x) => x.time));
        m.ratio = m.time / fastest;
        m.fastestLib = pool.find((x) => x.time === fastest)?.l ?? '?';
      }
      operations.push({ method, label, withText, measured });
    }
    return { s, text, operations };
  });
}

// ---------- summary ----------

// Versions of the packages of the rig, from Directory.Packages.props.
function packageVersions() {
  const text = readFileSync(join(here, 'Directory.Packages.props'), 'utf8');
  return [...text.matchAll(/<PackageVersion\s+Include="([^"]+)"\s+Version="([^"]+)"/g)].map((m) => `${m[1]} ${m[2]}`);
}

function conditionsSection() {
  const e = process.env;
  const lines = ['## Conditions\n'];
  lines.push(`- Date of the summary: ${new Date().toISOString()}`);
  lines.push(`- Commit: ${e.GITHUB_SHA ?? 'not a GitHub Actions run'}`);
  if (e.GITHUB_SERVER_URL && e.GITHUB_REPOSITORY && e.GITHUB_RUN_ID) {
    lines.push(`- Run: ${e.GITHUB_SERVER_URL}/${e.GITHUB_REPOSITORY}/actions/runs/${e.GITHUB_RUN_ID}`);
  }
  lines.push(`- Runner: ${e.RUNNER_OS ?? '?'} ${e.RUNNER_ARCH ?? '?'}, image ${e.ImageOS ?? '?'} ${e.ImageVersion ?? '?'}`);
  lines.push(`- Great.Parts.Cel: built from the sources of this commit`);
  lines.push(`- Packages: ${packageVersions().join('; ')}\n`);
  return lines.join('\n');
}

function verifySection(verify) {
  const libraries = [...new Set(verify.map((r) => r.Library))];
  const scenarios = [...new Set(verify.map((r) => r.Scenario))];
  const lines = ['## Verification\n'];
  lines.push('Correct: all three variants gave the expected value: the original text (compile and evaluate), the text with a tail (compile and evaluate), the text with a tail (full cycle).\n');
  lines.push(`| Scenario | Expression | Lines | Expected | ${libraries.join(' | ')} |`);
  lines.push(`|---|---|--:|---|${libraries.map(() => '---').join('|')}|`);
  for (const s of scenarios) {
    const rows = verify.filter((r) => r.Scenario === s);
    const first = rows[0];
    const cells = libraries.map((l) => {
      const r = rows.find((x) => x.Library === l);
      if (!r) return '-';
      if (r.Correct) return 'correct';
      const wrong = r.Variants.filter((v) => !v.Correct);
      return esc(cut(`wrong (${wrong.length} of 3): ${wrong[0].Result}`));
    });
    lines.push(`| ${s} | \`${esc(first.Expression)}\` | ${first.Lines} | ${esc(cut(first.Expected, 40))} | ${cells.join(' | ')} |`);
  }
  const excluded = verify.filter((r) => !r.Correct);
  lines.push('');
  lines.push(excluded.length === 0
    ? 'No library is excluded: every library gave the expected value in every scenario.'
    : `Excluded from the measurement (wrong result): ${excluded.map((r) => `${r.Library} - ${r.Scenario}`).join('; ')}.`);
  lines.push('');
  return lines.join('\n');
}

function environmentSection(host) {
  const h = host ?? {};
  const lines = ['## Measurement environment\n'];
  lines.push(`- BenchmarkDotNet ${h.BenchmarkDotNetVersion ?? '?'}; OS: ${h.OsVersion ?? '?'}; processor: ${h.ProcessorName ?? '?'}`);
  lines.push(`- Cores: ${h.PhysicalCoreCount ?? '?'} physical, ${h.LogicalCoreCount ?? '?'} logical; architecture: ${h.Architecture ?? '?'}`);
  lines.push(`- Runtime: ${h.RuntimeVersion ?? '?'}; SDK: ${h.DotNetCliVersion ?? '?'}; configuration: ${h.Configuration ?? '?'}\n`);
  return lines.join('\n');
}

const runLine = (m) =>
  `- Run ${m.run}: ${m.started ?? '?'} - ${m.finished ?? '?'}; job: ${m.job ?? 'Default'}; cases: ${m.cases ?? 'all'}; filter: ${m.filter ?? '*'}`;

// Table of one run (the per-run summary, results/runs/<k>/summary.md).
function runSection(k, bdn, verify) {
  const meta = loadRunMeta(k);
  const lines = [`## Run ${k}: scenario x operation\n`, runLine(meta), ''];
  lines.push('Median time and allocated memory per operation. The decisive column is "minus text": for compile and full cycle the median of the text creation (the row "text creation") is subtracted, evaluation creates no text. The ratio is to the fastest library with a correct result in this run by the decisive column (for a variant of ${SUBJECT}: to the fastest other library or to itself); "> 1.1" marks a ratio above the threshold in this run. The summary over all runs is in `results/summary.md`.\n');
  lines.push('| Scenario | Operation | Library | Time minus text | Time | Memory minus text | Memory | Ratio | > 1.1 |');
  lines.push('|---|---|---|--:|--:|--:|--:|--:|---|');
  for (const { s, text, operations } of measure(bdn, verify)) {
    lines.push(`| ${s} | text creation | - | - | ${fmtTime(text?.median)} | - | ${fmtBytes(text?.alloc)} | - | |`);
    for (const { label, withText, measured } of operations) {
      for (const m of measured) {
        lines.push(`| ${s} | ${label} | ${m.l} | ${fmtTime(m.time)} | ${withText ? fmtTime(m.raw) : '-'} | ${fmtBytes(m.alloc)} | ${withText ? fmtBytes(m.rawAlloc) : '-'} | ${m.ratio.toFixed(2)} | ${m.ratio > THRESHOLD ? 'yes' : ''} |`);
      }
      for (const r of (verify ?? []).filter((x) => x.Scenario === s && !x.Correct)) {
        lines.push(`| ${s} | ${label} | ${r.Library} | - | - | - | - | excluded | |`);
      }
    }
  }
  lines.push('');
  return lines.join('\n');
}

// Summary over all runs: per row "scenario, operation, library" the ratios of the runs that measured it, R = median of the ratios,
// spread = max / min ratio, both sides = some ratio <= 1.1 and some > 1.1.
function overRunsSection(runs, verify) {
  const rows = new Map();
  for (const { k, bdn } of runs) {
    for (const { s, operations } of measure(bdn, verify)) {
      for (const { label, measured } of operations) {
        for (const m of measured) {
          const key = `${s}|${label}|${m.l}`;
          if (!rows.has(key)) rows.set(key, { s, label, l: m.l, per: [] });
          rows.get(key).per.push({ k, ratio: m.ratio, time: m.time, alloc: m.alloc, fastestLib: m.fastestLib });
        }
      }
    }
  }
  const lines = ['## Summary over the runs\n'];
  lines.push(`The ratio of a row in a run is to the fastest library with a correct result in the same run (for a variant of ${SUBJECT}: to the fastest other library or to itself, not to its other variants). R is the median of the ratios over the runs; the column "R > 1.1" is filled when the row has ${MIN_RUNS} runs or more; "both sides" means that the ratio is not above 1.1 in some runs and above it in others. "Allocated" is the median over the runs of the bytes allocated per operation that BenchmarkDotNet reports (MemoryDiagnoser), less the allocation of making the text for compile and full cycle, as the time is.\n`);
  lines.push('| Scenario | Operation | Library | Runs | Ratios by run | R | Spread | Fastest by run | Median time over the runs | Allocated | R > 1.1 | Both sides of 1.1 |');
  lines.push('|---|---|---|--:|---|--:|--:|---|--:|--:|---|---|');
  const over = [];
  const subject = { seen: false, over: [], both: [], short: [] };
  for (const r of rows.values()) {
    const ratios = r.per.map((p) => p.ratio);
    const R = median(ratios);
    // Non-positive ratios only come from a job without a real measurement (Dry): no spread then.
    const spread = ratios.every((x) => x > 0) ? Math.max(...ratios) / Math.min(...ratios) : NaN;
    const both = ratios.some((x) => x <= THRESHOLD) && ratios.some((x) => x > THRESHOLD);
    const enough = ratios.length >= MIN_RUNS;
    const fastest = [...new Set(r.per.map((p) => p.fastestLib))].join(', ');
    const allocs = r.per.map((p) => p.alloc).filter((x) => Number.isFinite(x));
    const where = `${r.s}, ${r.label}, ${r.l}`;
    if (enough && R > THRESHOLD) over.push(`${where}: ${R.toFixed(2)}`);
    if (isSubject(r.l)) {
      subject.seen = true;
      if (!enough) subject.short.push(`${where} (${ratios.length})`);
      else {
        if (R > THRESHOLD) subject.over.push(`${where}: ${R.toFixed(2)}`);
        if (both) subject.both.push(`${where}: ${R.toFixed(2)}`);
      }
    }
    const sideMark = !both ? '' : ratios.length >= MAX_RUNS ? 'yes, at the threshold' : 'yes';
    const flagOver = !enough ? `no result (runs: ${ratios.length})` : R > THRESHOLD ? 'yes' : 'no';
    lines.push(`| ${r.s} | ${r.label} | ${r.l} | ${ratios.length} | ${r.per.map((p) => `${p.k}: ${p.ratio.toFixed(2)}`).join('; ')} | ${R.toFixed(2)} | ${Number.isNaN(spread) ? '-' : spread.toFixed(2)} | ${fastest} | ${fmtTime(median(r.per.map((p) => p.time)))} | ${allocs.length === 0 ? '-' : fmtBytes(Math.round(median(allocs)))} | ${flagOver} | ${sideMark} |`);
  }
  lines.push('');
  lines.push(over.length === 0 ? 'R above 1.1 (all libraries): none.' : `R above 1.1 (all libraries): ${over.join('; ')}.`);
  lines.push('');
  if (!subject.seen) {
    lines.push(`No rows of ${SUBJECT} in the measurement.`);
  } else {
    lines.push(`**Rows of ${SUBJECT}.**\n`);
    lines.push(subject.over.length === 0 ? '- R above 1.1: none.' : `- R above 1.1: ${subject.over.join('; ')}.`);
    lines.push(subject.both.length === 0 ? '- Ratios on both sides of 1.1: none.' : `- Ratios on both sides of 1.1: ${subject.both.join('; ')}.`);
    if (subject.short.length > 0) lines.push(`- Fewer than ${MIN_RUNS} runs, no R above 1.1 is stated: ${subject.short.join('; ')}.`);
  }
  lines.push('');
  return lines.join('\n');
}

// ---------- the limits of the speed-ups ----------

// The rows of the full reports of BenchmarkDotNet of ThresholdBench: class, method, parameters, median (ns), allocated bytes.
function loadThresholdRows() {
  const dir = join(thresholdsDir, 'bdn', 'results');
  if (!existsSync(dir)) return null;
  const rows = [];
  for (const f of readdirSync(dir)) {
    if (!f.endsWith('-report-full.json')) continue;
    const j = JSON.parse(readFileSync(join(dir, f), 'utf8'));
    for (const b of j.Benchmarks) {
      const parameters = {};
      for (const m of (b.Parameters ?? '').matchAll(/(\w+)=([^,]+)/g)) parameters[m[1]] = m[2].trim();
      rows.push({ type: b.Type, method: b.Method, parameters, median: b.Statistics?.Median, alloc: b.Memory?.BytesAllocatedPerOperation });
    }
  }
  return rows;
}

function loadThresholdMeta() {
  const f = join(thresholdsDir, 'meta.json');
  return existsSync(f) ? JSON.parse(readFileSync(f, 'utf8')) : { job: 'Default' };
}

const times = (x) => (x === undefined || x === null || !(x > 0) ? undefined : x);

// The three limits, each with its measurement, its rule and its verdict. A Dry job has no measurement and no verdict.
function thresholdVerdicts(rows, meta) {
  const dry = meta.job === 'Dry';
  const at = (type, method, key, value) => times(rows.find((r) => r.type === type && r.method === method && r.parameters[key] === String(value))?.median);
  const verdicts = [];

  // The hybrid search is the search of the literal prefix of matches only: the functions of strings search by SearchValues<string> and
  // the ordinal search, and their time is the diagnostic below.
  const search = { limit: `hybrid search of the literal prefix of matches, ${SEARCH_LIMIT} code units`, rule: `.NET at M = ${SEARCH_LIMIT} is not slower than Knuth-Morris-Pratt; otherwise the limit is 32`, rows: [] };
  for (const m of [48, 64, 65, 80]) {
    search.rows.push({ m, kmp: at('SubstringBenchmarks', 'Kmp', 'M', m), net: at('SubstringBenchmarks', 'Net', 'M', m), hybrid: at('SubstringBenchmarks', 'Hybrid', 'M', m) });
  }
  const s64 = search.rows.find((r) => r.m === 64);
  search.verdict = dry ? 'not measured (Dry)' : s64?.kmp === undefined || s64?.net === undefined ? 'no result' : s64.net <= s64.kmp ? `holds: ${SEARCH_LIMIT}` : 'does not hold: 32';
  verdicts.push(search);

  const automaton = { limit: 'build of the automaton of the pattern E5', rule: `the median of the build is at most ${AUTOMATON_LIMIT_NS} ns`, rows: [] };
  for (const name of ['E5', 'UUID']) {
    automaton.rows.push({ name, program: at('AutomatonBenchmarks', 'ParseAndBuildProgram', 'Name', name), automaton: at('AutomatonBenchmarks', 'BuildAutomaton', 'Name', name) });
  }
  const e5 = automaton.rows.find((r) => r.name === 'E5');
  automaton.verdict = dry ? 'not measured (Dry)' : e5?.automaton === undefined ? 'no result' : e5.automaton <= AUTOMATON_LIMIT_NS ? `holds: ${e5.automaton.toFixed(0)} ns` : `does not hold: ${e5.automaton.toFixed(0)} ns`;
  verdicts.push(automaton);

  // The index of the values (distinct, sets): the scan and the buckets are forced at every N. No verdict: the rule below gives the
  // threshold that is the input of the decision on the limit of the library (ValueIndex.ScanLimit, now HASH_SCAN_LIMIT).
  const hashing = {
    limit: `index of the values of a list (distinct, sets): scan or buckets, the limit of the library is ${HASH_SCAN_LIMIT} values`,
    rule: `a diagnostic without a verdict: the threshold by the rule is the largest N at which, and at every smaller N measured (${HASH_SIZES.join(', ')}), scan / buckets is at most 1.0 for distinct and for sets.contains; 8 when the scan is slower already at N = 8`,
    rows: [],
    evaluation: [],
  };
  for (const n of HASH_SIZES) {
    const op = (name) => ({
      library: at('HashingBenchmarks', `${name}Default`, 'N', n),
      scan: at('HashingBenchmarks', `${name}Scan`, 'N', n),
      buckets: at('HashingBenchmarks', `${name}Buckets`, 'N', n),
      constantHash: at('HashingBenchmarks', `${name}ConstantHash`, 'N', n),
      pairwise: at('HashingBenchmarks', `${name}Pairwise`, 'N', n),
    });
    hashing.rows.push({ n, distinct: op('Distinct'), contains: op('Contains') });
  }
  for (const n of HASH_EVALUATION_SIZES) {
    hashing.evaluation.push({ n, distinct: at('HashingEvaluationBenchmarks', 'Distinct', 'N', n), contains: at('HashingEvaluationBenchmarks', 'SetsContains', 'N', n) });
  }
  let threshold = HASH_SIZES[0];
  let missing = false;
  let reached = true;
  for (const r of hashing.rows) {
    for (const op of ['distinct', 'contains']) {
      const { scan, buckets } = r[op];
      if (scan === undefined || buckets === undefined) missing = true;
      else if (scan > buckets) reached = false;
    }
    if (reached) threshold = r.n;
  }
  hashing.threshold = missing || dry ? undefined : threshold;
  hashing.verdict = dry
    ? 'not measured (Dry)'
    : missing
      ? 'no result'
      : `no verdict; threshold by the rule: ${threshold} (the limit of the library: ${HASH_SCAN_LIMIT})`;
  verdicts.push(hashing);

  // The time of contains per unit of its cost (docs/cost.md: 1 + ceil((n + m) / 10) * q, q = max(1, ceil(m / 64)), without the reads of
  // the variables), for a literal pattern (SearchValues<string>) and a pattern from the data (the ordinal search). No verdict: a rise of the
  // time per unit above two times from the shortest pattern to the longest is the input of the decision on the divisor 64 of q.
  const cost = { limit: 'substring search: time per cost unit', rule: 'a diagnostic without a verdict: a rise of the time per unit above 2 times from M = 64 to M = 1024 is the input of the decision on the divisor 64', rows: [] };
  for (const m of COST_PATTERN_LENGTHS) {
    const units = 1 + Math.ceil((COST_TEXT_LENGTH + m) / 10) * Math.max(1, Math.ceil(m / 64));
    const literal = at('SubstringCostBenchmarks', 'Literal', 'M', m);
    const data = at('SubstringCostBenchmarks', 'FromData', 'M', m);
    cost.rows.push({ m, units, literal, data, literalPerUnit: literal === undefined ? undefined : literal / units, dataPerUnit: data === undefined ? undefined : data / units });
  }
  const rise = (key) => {
    const first = cost.rows[0][key];
    const last = cost.rows[cost.rows.length - 1][key];
    return first === undefined || last === undefined ? undefined : last / first;
  };
  cost.rise = { literal: rise('literalPerUnit'), data: rise('dataPerUnit') };
  cost.verdict = dry
    ? 'not measured (Dry)'
    : cost.rise.literal === undefined || cost.rise.data === undefined
      ? 'no result'
      : `no verdict; time per unit from M = 64 to M = 1024: ${cost.rise.literal.toFixed(2)} times for a literal, ${cost.rise.data.toFixed(2)} times for the data`;
  verdicts.push(cost);

  // The simple entry: the call on data that were built before the measurement, and the building of the data as the variant of the rig
  // builds them. No verdict: the rows show where the time of the variant of the rig goes.
  const allocation = (type, method, key, value) => rows.find((r) => r.type === type && r.method === method && r.parameters[key] === String(value))?.alloc;
  const simple = { limit: 'simple entry: evaluate and data', rule: 'a diagnostic without a verdict: the time of the call on a prepared anonymous object, and the time of building that object', rows: [] };
  for (const text of ['scalars', 'lines']) {
    simple.rows.push({
      text,
      evaluate: at('SimpleEntryBenchmarks', 'Evaluate', 'Text', text),
      evaluateAllocated: allocation('SimpleEntryBenchmarks', 'Evaluate', 'Text', text),
      data: at('SimpleEntryBenchmarks', 'Data', 'Text', text),
      dataAllocated: allocation('SimpleEntryBenchmarks', 'Data', 'Text', text),
    });
  }
  simple.verdict = dry
    ? 'not measured (Dry)'
    : simple.rows.some((r) => r.evaluate === undefined || r.data === undefined)
      ? 'no result'
      : `no verdict; ${simple.rows.map((r) => `${r.text}: evaluate ${fmtTime(r.evaluate)}, data ${fmtTime(r.data)}`).join('; ')}`;
  verdicts.push(simple);

  // CelMap.Create on the row of an order, a map of three entries made with a collection expression.
  const celMap = { limit: 'CelMap.Create of the row of an order', rule: 'a diagnostic without a verdict: the time and the allocation of a map of three entries made with a collection expression', rows: [] };
  const lineMap = rows.find((r) => r.type === 'CelMapBenchmarks' && r.method === 'LineMap');
  celMap.rows.push({ time: times(lineMap?.median), allocated: lineMap?.alloc });
  celMap.verdict = dry ? 'not measured (Dry)' : celMap.rows[0].time === undefined ? 'no result' : `no verdict; ${fmtTime(celMap.rows[0].time)}, ${fmtBytes(celMap.rows[0].allocated)}`;
  verdicts.push(celMap);
  return verdicts;
}

function thresholdsSection() {
  const rows = loadThresholdRows();
  if (!rows) return null;
  const meta = loadThresholdMeta();
  const verdicts = thresholdVerdicts(rows, meta);
  const [search, automaton, hashing, cost, simple, celMap] = verdicts;
  const ratio = (a, b) => (a === undefined || b === undefined ? '-' : (a / b).toFixed(2));
  const lines = ['## Limits of the speed-ups\n'];
  lines.push(`Job: ${meta.job ?? 'Default'}; started ${meta.started ?? '?'}, finished ${meta.finished ?? '?'}. Medians of BenchmarkDotNet on the code of this commit; the reports are in \`results/thresholds/bdn/results\`.\n`);
  lines.push('| Limit | Rule | Verdict |');
  lines.push('|---|---|---|');
  for (const v of verdicts) lines.push(`| ${v.limit} | ${v.rule} | ${v.verdict} |`);
  lines.push('');

  lines.push(`### Search of the literal prefix of matches, the text (ab)^5000\n`);
  lines.push('M is the length of the pattern, the slowest case for the search of .NET. "Library" is the search as it runs: .NET up to the limit, Knuth-Morris-Pratt above. Net / KMP below 1 means .NET is faster.\n');
  lines.push('| M | KMP | .NET | Library | Net / KMP |');
  lines.push('|--:|--:|--:|--:|--:|');
  for (const r of search.rows) lines.push(`| ${r.m} | ${fmtTime(r.kmp)} | ${fmtTime(r.net)} | ${fmtTime(r.hybrid)} | ${ratio(r.net, r.kmp)} |`);
  lines.push('');

  lines.push('### Search of a substring by the functions of strings: time per cost unit\n');
  lines.push(`contains on the text (ab)^5000 (${COST_TEXT_LENGTH} code units) and a pattern of M code units, the slowest case for the search of .NET. "Literal" is a pattern that is a literal of the expression (SearchValues<string>), "data" a pattern read from a variable (the ordinal search); the time is that of the whole evaluation. The cost is 1 + ceil((n + M) / 10) * q, q = max(1, ceil(M / 64)), without the reads of the variables. No verdict.\n`);
  lines.push('| M | Cost | Literal | Literal per unit | Data | Data per unit |');
  lines.push('|--:|--:|--:|--:|--:|--:|');
  for (const r of cost.rows) lines.push(`| ${r.m} | ${r.units} | ${fmtTime(r.literal)} | ${fmtTime(r.literalPerUnit)} | ${fmtTime(r.data)} | ${fmtTime(r.dataPerUnit)} |`);
  lines.push('');

  lines.push('### Build of the automaton\n');
  lines.push('| Pattern | Parse and program | Automaton |');
  lines.push('|---|--:|--:|');
  for (const r of automaton.rows) lines.push(`| ${r.name} | ${fmtTime(r.program)} | ${fmtTime(r.automaton)} |`);
  lines.push('');

  lines.push('### Index of the values: the lists of N integers\n');
  lines.push(`"Library" is the index as the library makes it (the scan up to ${HASH_SCAN_LIMIT} values, the buckets above); "scan" and "buckets" force that way at any N; "one hash" gives every value the same hash; "pairwise" are integers of 2^53 and more, compared with each other in order. Scan / buckets below 1 means the scan is faster. No verdict: ${hashing.threshold === undefined ? 'the threshold by the rule is not stated (no result or no measurement)' : `the threshold by the rule is ${hashing.threshold}`}; the limit of the library is set by a decision on this table.\n`);
  lines.push('| Operation | N | Library | Scan | Buckets | One hash | Pairwise | Scan / buckets |');
  lines.push('|---|--:|--:|--:|--:|--:|--:|--:|');
  for (const op of ['distinct', 'contains']) {
    for (const r of hashing.rows) {
      const x = r[op];
      lines.push(`| ${op === 'distinct' ? 'distinct' : 'sets.contains'} | ${r.n} | ${fmtTime(x.library)} | ${fmtTime(x.scan)} | ${fmtTime(x.buckets)} | ${fmtTime(x.constantHash)} | ${fmtTime(x.pairwise)} | ${ratio(x.scan, x.buckets)} |`);
    }
  }
  lines.push('');

  lines.push('### distinct and sets.contains through the public API\n');
  lines.push('The evaluation of the program on a list of N integers; the time per element shows a jump at the limit, if there is one.\n');
  lines.push('| N | distinct | per element | sets.contains | per element |');
  lines.push('|--:|--:|--:|--:|--:|');
  const perElement = (x, n) => (x === undefined ? '-' : fmtTime(x / n));
  for (const r of hashing.evaluation) lines.push(`| ${r.n} | ${fmtTime(r.distinct)} | ${perElement(r.distinct, r.n)} | ${fmtTime(r.contains)} | ${perElement(r.contains, r.n)} |`);
  lines.push('');

  lines.push('### Simple entry: evaluate and data\n');
  lines.push('The call `CelExpression.Evaluate` on an anonymous object with the scalars of the order, the customer and the lines as properties, as the variant of the rig passes them. "Evaluate" is the call on an object that was built before the measurement, with the text of the variant with the input of the environment ("scalars" is the text of E1, "lines" the text of E2 over 10 lines); "data" is the building of that object for one call. No verdict.\n');
  lines.push('| Text | Evaluate | Allocated | Data | Allocated |');
  lines.push('|---|--:|--:|--:|--:|');
  for (const r of simple.rows) lines.push(`| ${r.text} | ${fmtTime(r.evaluate)} | ${fmtBytes(r.evaluateAllocated)} | ${fmtTime(r.data)} | ${fmtBytes(r.dataAllocated)} |`);
  lines.push('');

  lines.push('### CelMap.Create of the row of an order\n');
  lines.push('A map of three entries (`sku`, `qty`, `price`) made with a collection expression. No verdict.\n');
  lines.push('| Time | Allocated |');
  lines.push('|--:|--:|');
  for (const r of celMap.rows) lines.push(`| ${fmtTime(r.time)} | ${fmtBytes(r.allocated)} |`);
  lines.push('');

  mkdirSync(thresholdsDir, { recursive: true });
  writeFileSync(thresholdsJson, `${JSON.stringify({ job: meta.job ?? 'Default', verdicts }, null, 2)}\n`);
  return lines.join('\n');
}

function writeSummary() {
  const verify = loadVerify();
  const runs = runNumbers()
    .map((k) => ({ k, bdn: loadBdn(join(runsDir, String(k), 'bdn')) }))
    .filter((r) => r.bdn);
  for (const { k, bdn } of runs) {
    writeFileSync(join(runsDir, String(k), 'summary.md'), runSection(k, bdn, verify));
  }
  const parts = [conditionsSection()];
  if (verify) parts.push(verifySection(verify));
  if (runs.length > 0) {
    parts.push(environmentSection(runs[0].bdn.host));
    parts.push(['## Runs\n', ...runs.map(({ k }) => runLine(loadRunMeta(k))), '', 'The table of each run is `results/runs/<k>/summary.md`.\n'].join('\n'));
    parts.push(overRunsSection(runs, verify));
  }
  const thresholds = thresholdsSection();
  if (thresholds) parts.push(thresholds);
  const text = parts.join('\n');
  mkdirSync(results, { recursive: true });
  writeFileSync(summaryMd, text);
  console.log(`\nSummary written to ${summaryMd}\n`);
  console.log(text);
}

// ---------- main ----------

if (flag('--summary-only')) {
  writeSummary();
  process.exit(0);
}

const thresholdsOnly = flag('--thresholds-only');
const runsOption = option('--runs') ?? '1';
if (!/^[1-9]\d*$/.test(runsOption)) {
  console.error(`--runs needs a positive integer, got '${runsOption}'`);
  process.exit(2);
}

mkdirSync(results, { recursive: true });
if (!thresholdsOnly) {
  dotnetOrExit(['build', project, '-c', 'Release', '-f', FRAMEWORK, '--nologo', '-v', 'q']);
  dotnetOrExit(run('verify', 'results/verify.json'));
}

let failure = 0;
if (!flag('--verify-only') && !thresholdsOnly) {
  if (existsSync(runsDir)) rmSync(runsDir, { recursive: true, force: true });
  for (let k = 1; k <= Number(runsOption) && failure === 0; k++) {
    const dir = join(runsDir, String(k));
    mkdirSync(dir, { recursive: true });
    const meta = { run: k, job: option('--job') ?? 'Default', cases: option('--cases') ?? null, filter: option('--filter') ?? '*', started: new Date().toISOString() };
    const bdnArgs = ['bench', '--filter', meta.filter, '--artifacts', `results/runs/${k}/bdn`];
    if (option('--job')) bdnArgs.push('--job', option('--job'));
    failure = dotnet(run(...bdnArgs), option('--cases') ? { BENCH_CASES: option('--cases') } : {});
    meta.finished = new Date().toISOString();
    writeFileSync(join(dir, 'run.json'), `${JSON.stringify(meta, null, 2)}\n`);
  }
}

// The limits of the speed-ups: after the runs of the rig, in the same job, on the same machine. A failed run of the rig does not skip them.
if (!flag('--verify-only') && !flag('--no-thresholds')) {
  if (existsSync(thresholdsDir)) rmSync(thresholdsDir, { recursive: true, force: true });
  mkdirSync(thresholdsDir, { recursive: true });
  dotnetOrExit(['build', thresholdProject, '-c', 'Release', '-f', FRAMEWORK, '--nologo', '-v', 'q']);
  const meta = { job: option('--job') ?? 'Default', started: new Date().toISOString() };
  const bdnArgs = ['--filter', '*', '--artifacts', 'results/thresholds/bdn'];
  if (option('--job')) bdnArgs.push('--job', option('--job'));
  const status = dotnet(runThresholds(...bdnArgs));
  meta.finished = new Date().toISOString();
  writeFileSync(join(thresholdsDir, 'meta.json'), `${JSON.stringify(meta, null, 2)}\n`);
  failure = failure || status;
}

// The summary is written after a failed run too: the runs that ended are in it.
writeSummary();
if (failure !== 0) {
  console.error(`dotnet exited with code ${failure}`);
  process.exit(failure);
}
