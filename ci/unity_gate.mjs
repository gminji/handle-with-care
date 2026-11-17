// Unity checks for the am commit gate (am-gate.json at the repository root).
//
// Purpose
//   Checks the Unity project one folder above this script with the Unity
//   Editor that has exactly that project open, so a git worktree checks its own
//   copy and never borrows the main checkout's Editor. Exits 0 on pass and 1
//   otherwise, which makes the gate block the commit:
//     - no running Editor has this project open (the gate never launches one),
//     - the Editor is in Play mode, shows a dialog, or does not get ready,
//     - an open scene has unsaved changes (tests only: the test runner would
//       stop on a "save scene?" dialog),
//     - compile errors, or an EditMode test run that failed, ran nothing or
//       did not finish,
//     - an untracked .meta file whose asset is staged or tracked (compile
//       only: the gate's own refresh writes .meta files after `git add`).
//
// CLI
//   node ci/unity_gate.mjs compile   asset refresh + script recompile
//   node ci/unity_gate.mjs tests     every EditMode test
//
//   Needs the Unity CLI (`unity`) on PATH and the com.unity.pipeline package in
//   the project. Node only, no dependencies.

import { spawnSync } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const PROJECT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const BUDGET_S = 450; // am-gate.json allows 480 s per command; stop first and say which step ran out
const DEADLINE = Date.now() + BUDGET_S * 1000;
const STATUS_TIMEOUT_S = 30;
const STATUS_ATTEMPTS = 4;
const READY_TIMEOUT_S = 120;
const COMPILE_TIMEOUT_S = 300;
const TEST_TIMEOUT_S = 300; // copied from the exorcism project's gate (2026-11-17)
const MAX_LISTED = 20;
const MAX_LINES = 36; // gate.mjs shows only the last 40 lines (4000 chars) of a failed command
const MAX_CHARS = 3000;

/** Prints the reason and exits 1. The first line always survives the gate's tail cut. */
function fail(message) {
  const [head, ...rest] = String(message).split('\n');
  const body = rest.slice(-(MAX_LINES - 1)).join('\n').slice(-MAX_CHARS);
  process.stdout.write(`${head}\n${body}`.trimEnd() + '\n');
  process.exit(1);
}

/** Runs the Unity CLI against this project, within both the step's own timeout and the script's budget. */
function unity(step, args, timeoutS) {
  const leftS = Math.floor((DEADLINE - Date.now()) / 1000);
  if (leftS < 5) fail(`검사 시간(${BUDGET_S}초)을 다 써서 '${step}' 단계를 시작하지 못했습니다.`);
  const r = spawnSync('unity', [...args, '--project-path', PROJECT], {
    encoding: 'utf8',
    windowsHide: true,
    timeout: Math.min(timeoutS + 30, leftS) * 1000,
    maxBuffer: 64 * 1024 * 1024,
  });
  if (r.error && r.error.code === 'ETIMEDOUT') {
    fail(`'${step}' 단계가 시간 안에 끝나지 않았습니다. Unity 에디터가 멈췄거나 대화 상자가 떠 있을 수 있습니다.`);
  }
  if (r.error) fail(`Unity CLI(unity)를 실행하지 못했습니다('${step}' 단계): ${r.error.message}`);
  return { step, code: r.status, out: r.stdout || '', err: r.stderr || '' };
}

function tryParse(text) {
  // Callers decide what an unreadable reply means (retry, explain or fail); null is that signal.
  try {
    return JSON.parse(text);
  } catch {
    return null;
  }
}

/** Parsed JSON output, or a gate failure that shows the raw text. */
function parse(r) {
  const value = tryParse(r.out);
  return value !== null ? value : fail(`'${r.step}' 단계의 Unity CLI 출력을 읽지 못했습니다 (종료 코드 ${r.code}).\n${(r.out + r.err).trim()}`);
}

/** Short explanation of a failed CLI call: its error codes and messages, else the raw output. */
function summarize(r) {
  const env = tryParse(r.out);
  const lines = [];
  for (const e of (env && env.errors) || []) lines.push(`${e.code || 'error'}: ${e.message || JSON.stringify(e)}`);
  if (env && env.data && env.data.message) lines.push(env.data.message);
  return lines.length ? lines.join('\n') : (r.out + r.err).trim().slice(-1500);
}

const samePath = (a, b) => path.resolve(String(a)).toLowerCase() === path.resolve(String(b)).toLowerCase();
const DIALOG = 'Unity 에디터에 대화 상자가 떠 있습니다. 닫고 다시 커밋하세요.';

function requireEditor() {
  const first = unity('status', ['status', '--format', 'json'], STATUS_TIMEOUT_S);
  const status = parse(first);
  const rows = (status.data && status.data.instances) || [];
  const instance = rows.find((i) => samePath(i.project, PROJECT));
  const code = status.errors && status.errors[0] && status.errors[0].code;
  if (!instance && (rows.length > 0 || code === 'STATUS_NO_INSTANCES')) {
    fail(`이 프로젝트를 연 Unity 에디터가 없습니다: ${PROJECT}\n에디터를 켜고 다시 커밋하세요. 이 검사는 Unity를 따로 띄우지 않습니다.`);
  }
  if (instance && instance.state === 'blocked_by_dialog') fail(DIALOG);

  // Right after a compile the Editor reloads its scripts and times out requests until it settles
  // (measured: 6-8 s waiting here against a 30 s timeout on the first editor_status otherwise).
  // Starting, loading the package and Safe Mode also land here instead of "no Editor".
  // --project-path only filters by substring, so the ready Editor must also be this exact project.
  const ready = unity('until-ready', ['status', '--until-ready', '--timeout', String(READY_TIMEOUT_S), '--format', 'json'], READY_TIMEOUT_S);
  const readyData = ((ready.code === 0 && tryParse(ready.out)) || {}).data;
  if (!((readyData && readyData.instances) || []).some((i) => samePath(i.project, PROJECT) && i.state === 'ready')) {
    if (/blocked_by_dialog/.test(ready.out)) fail(DIALOG);
    const safeMode = /PIPELINE_LOAD_PENDING/.test(first.out + ready.out) ? '\n컴파일 오류로 에디터가 Safe Mode로 열렸을 수 있습니다. 오류를 고치고 에디터를 다시 여세요.' : '';
    fail(`Unity 에디터가 ${READY_TIMEOUT_S}초 안에 준비되지 않았습니다.${safeMode}\n${summarize(code ? first : ready)}`);
  }

  // A reload can still start between the two calls; editor_status only reads, so asking again is safe.
  let reply = null;
  let editor = {};
  for (let attempt = 0; attempt < STATUS_ATTEMPTS && typeof editor.playMode !== 'string'; attempt += 1) {
    reply = unity('editor_status', ['command', 'editor_status', '--result-only', '--timeout', String(STATUS_TIMEOUT_S)], STATUS_TIMEOUT_S);
    editor = tryParse(reply.out) || {};
  }
  if (typeof editor.playMode !== 'string') fail(`Unity 에디터 상태를 읽지 못했습니다(${STATUS_ATTEMPTS}번 시도).\n${summarize(reply)}`);
  if (!samePath(editor.projectPath, PROJECT)) fail(`다른 프로젝트의 에디터가 응답했습니다: ${editor.projectPath} (검사할 프로젝트: ${PROJECT})`);
  if (editor.playMode !== 'stopped') fail('Unity 에디터가 Play 모드입니다. Play를 끄고 다시 커밋하세요.');
}

/** Paths under Assets and Packages from a git listing command, relative to PROJECT. */
function gitPaths(args) {
  const r = spawnSync('git', ['-C', PROJECT, ...args, '-z', '--', 'Assets', 'Packages'], {
    encoding: 'utf8',
    windowsHide: true,
    maxBuffer: 64 * 1024 * 1024,
  });
  if (r.error || r.status !== 0) fail(`git 파일 목록을 읽지 못했습니다.\n${r.error ? r.error.message : r.stderr.trim()}`);
  return r.stdout.split('\0').filter(Boolean);
}

const shown = (f) => `  ${path.basename(PROJECT)}/${f}`;

/** At most `max` lines of paths, plus a count of the rest. */
function listed(files, max) {
  const lines = files.slice(0, max).map(shown);
  if (files.length > max) lines.push(`  외 ${files.length - max}개`);
  return lines.join('\n');
}

/**
 * The refresh in `recompile` writes a .meta for every new asset, and in a commit that happens
 * after `git add`. A commit without them gives the asset a new GUID in every other clone,
 * which breaks its references there.
 *
 * Limit: the hook runs before the whole command line, so in `git add X && git commit` X is not
 * in the index yet and its .meta is not asked for. Blocking every new .meta instead would also
 * block am:check on each change that adds an asset, so the message asks for a separate add.
 */
function requireMetaFiles() {
  const untracked = gitPaths(['ls-files', '--others', '--exclude-standard']).filter((f) => f.endsWith('.meta'));
  if (!untracked.length) return;
  // `git add -N` entries are in the index but stay out of the commit.
  const intentToAdd = new Set(gitPaths(['diff', '--name-only', '--relative', '--diff-filter=A']));
  const indexed = new Set();
  for (const file of gitPaths(['ls-files'])) {
    if (intentToAdd.has(file)) continue;
    // Folders count too: a folder's .meta belongs with the files staged inside it.
    for (let p = file; p.includes('/'); p = p.slice(0, p.lastIndexOf('/'))) indexed.add(p);
    indexed.add(file);
  }
  const missing = untracked.filter((f) => indexed.has(f.slice(0, -'.meta'.length)));
  if (!missing.length) return;
  // A file moved with `git mv` but without its .meta: the refresh drops the old .meta and writes a new one.
  const gone = gitPaths(['ls-files', '--deleted']).filter((f) => f.endsWith('.meta'));
  const moved = gone.length
    ? `\nUnity가 지운 .meta(git에는 남아 있음):\n${listed(gone, 5)}\n옮기거나 이름을 바꾼 파일이면 새 .meta를 올리지 마세요. 원래 .meta를 git restore로 되살려 git mv로 함께 옮기세요. 새 .meta는 GUID가 달라 연결이 끊깁니다.`
    : '';
  fail(
    `git에 있는 에셋의 .meta 파일 ${missing.length}개가 git에 없습니다:\n${listed(missing, MAX_LISTED)}` +
      `\n.meta가 빠지면 다른 PC에서 이 에셋을 가리키던 연결이 끊깁니다. git add로 올리고 다시 커밋하세요.` +
      `\ngit add는 커밋과 따로, 먼저 실행하세요. 이 검사는 명령 줄 전체보다 먼저 돌아서 "git add … && git commit"의 add를 보지 못합니다.${moved}`,
  );
}

function compile() {
  const r = unity('recompile', ['recompile', '--format', 'json', '--timeout', String(COMPILE_TIMEOUT_S)], COMPILE_TIMEOUT_S);
  if (r.code === 0) {
    requireMetaFiles();
    process.stdout.write('compile: ok\n');
    return;
  }
  const report = tryParse(r.out) || {};
  const errors = ((report.data && report.data.errors) || []).filter((e) => e && e.file);
  if (r.code === 6 && errors.length) {
    const lines = errors.slice(0, MAX_LISTED).map((e) => `  ${e.file}(${e.line || '?'}): ${e.message}`);
    fail(`컴파일 오류 ${errors.length}개:\n${lines.join('\n')}`);
  }
  // Measured 2026-10-06: for about a minute after the Editor starts, `unity status` already says
  // ready while the Editor is still importing, and recompile is not accepted within 30 s (exit 7).
  const busy = r.code === 7
    ? ' 에디터에 연결하지 못했습니다. 코드 문제가 아니니 파일을 고치지 말고 1분쯤 뒤 다시 커밋하세요(에디터를 켠 직후 약 1분은 이렇게 됩니다). 계속되면 에디터가 멈췄거나 대화 상자가 떠 있는지 확인하세요.'
    : '';
  fail(`컴파일을 확인하지 못했습니다 (종료 코드 ${r.code}).${busy}\n${summarize(r)}`);
}

/** The test runner asks "save scene?" before an EditMode run, which would stall an unattended check. */
function requireSavedScenes() {
  const r = unity('list_open_scenes', ['command', 'list_open_scenes', '--result-only', '--timeout', String(STATUS_TIMEOUT_S)], STATUS_TIMEOUT_S);
  const scenes = (tryParse(r.out) || {}).scenes;
  if (!Array.isArray(scenes)) fail(`열린 씬 상태를 읽지 못했습니다.\n${summarize(r)}`);
  const dirty = scenes.filter((s) => s.isDirty).map((s) => s.path || s.name || '(이름 없는 씬)');
  if (dirty.length) {
    fail(`저장하지 않은 씬이 있습니다: ${dirty.join(', ')}\n저장하고 다시 커밋하세요. 그대로 테스트를 시작하면 Unity가 저장 여부를 묻는 창을 띄워 검사가 멈춥니다.`);
  }
}

function tests() {
  requireSavedScenes();
  const r = unity('run_tests', ['command', 'run_tests', '--mode', 'editor', '--result-only', '--timeout', String(TEST_TIMEOUT_S)], TEST_TIMEOUT_S);
  if (r.code !== 0) fail(`EditMode 테스트를 끝까지 돌리지 못했습니다 (종료 코드 ${r.code}).\n${summarize(r)}`);
  // run_tests reports a failed run in its body ("success", "error") and still exits 0.
  const result = parse(r);
  if (result.success === false) {
    fail(`EditMode 테스트를 끝까지 돌리지 못했습니다.\n${[result.error, result.errorDetails].filter(Boolean).join('\n')}`);
  }
  const summary = result.Summary || {};
  const failed = (result.Results || []).filter((t) => t.Status === 'Failed');
  if (!summary.Total) fail('EditMode 테스트가 하나도 돌지 않았습니다.');
  if (summary.Failed > 0 || failed.length > 0) {
    const lines = failed.slice(0, MAX_LISTED).map((t) => `  ${t.FullName}: ${String(t.Message || '').split('\n')[0]}`);
    fail(`EditMode 테스트 실패 ${summary.Failed}/${summary.Total}:\n${lines.join('\n')}`);
  }
  process.stdout.write(`tests: ${summary.Passed}/${summary.Total} passed (${Math.round(result.Duration || 0)}s)\n`);
}

const step = process.argv[2];
if (step !== 'compile' && step !== 'tests') fail('사용법: node unity_gate.mjs compile|tests');
requireEditor();
if (step === 'compile') compile();
else tests();
