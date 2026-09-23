// Download pinned primary-source fixtures for static analysis only. Never launch them.
import { mkdir, writeFile, readFile } from 'node:fs/promises';
import { createHash } from 'node:crypto';
const dir = new URL('../samples/private/', import.meta.url);
try {
  const manifest = [...JSON.parse(await readFile(new URL('../samples/manifest.json', import.meta.url), 'utf8')), ...JSON.parse(await readFile(new URL('../samples/negative-manifest.json', import.meta.url), 'utf8'))];
  await mkdir(dir, { recursive: true });
  for (const fixture of manifest) {
    if (!/^[a-zA-Z0-9_.-]+$/.test(fixture.name) || !fixture.source.startsWith('https://')) throw Error('Invalid fixture manifest');
    const response = await fetch(fixture.source, { signal: AbortSignal.timeout(120000) });
    if (!response.ok || Number(response.headers.get('content-length')) > 96 * 1024 * 1024) throw Error(`Fixture download rejected: ${fixture.name} (${response.status})`);
    const parts = []; let size = 0;
    for await (const part of response.body) {
      size += part.length;
      if (size > 96 * 1024 * 1024) throw Error('Fixture exceeds 96 MB');
      parts.push(part);
    }
    const bytes = Buffer.concat(parts);
    if (createHash('sha256').update(bytes).digest('hex') !== fixture.sha256) throw Error(`Fixture hash mismatch: ${fixture.name}`);
    const target=fixture.category==='benign'?new URL('../artifacts/negative/',import.meta.url):dir;
    await mkdir(target,{recursive:true});await writeFile(new URL(fixture.name, target), bytes);
    console.log(`Verified ${fixture.name}`);
  }
} catch (error) {
  console.error(error instanceof Error ? error.message : error);
  process.exitCode = 1;
}
