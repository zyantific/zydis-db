import { spawnSync } from 'node:child_process';
import { cpSync, rmSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const webRoot = fileURLToPath(new URL('..', import.meta.url));
const wasmProject = path.join(webRoot, '..', 'Zydis.Viewer.Wasm');
const publishDir = path.join(webRoot, '.publish', 'Zydis.Viewer.Wasm');
const frameworkDir = path.join(webRoot, 'public', '_framework');

// A publish into a folder that holds an earlier output leaves the stale hashed files next to the new ones, and the
// copy below would ship them.
rmSync(publishDir, { recursive: true, force: true });

const publish = spawnSync('dotnet', ['publish', wasmProject, '-c', 'Debug', '-o', publishDir], { stdio: 'inherit' });
if (publish.error !== undefined) {
  throw publish.error;
}
if (publish.status !== 0) {
  process.exit(publish.status ?? 1);
}

rmSync(frameworkDir, { recursive: true, force: true });
cpSync(path.join(publishDir, 'wwwroot', '_framework'), frameworkDir, { recursive: true });
