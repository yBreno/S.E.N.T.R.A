import { readFile, readdir } from 'node:fs/promises';
import * as nodeModule from 'node:module';
import { transformWithOxc } from 'vite';

// Usa o transformador JSX já instalado pelo Vite, sem servidor ou nova dependência.
async function compileJsx(url) {
  const filename = new URL(url);
  const source = await readFile(filename, 'utf8');
  const { code } = await transformWithOxc(source, filename.pathname, {
    jsx: { runtime: 'automatic' },
  });

  return { format: 'module', source: code, shortCircuit: true };
}

export async function registerJsxLoader() {
  if (!nodeModule.registerHooks) {
    // Compatibilidade com Node 22.12, suportado pelo Vite do projeto.
    nodeModule.register(import.meta.url);
    return;
  }

  const sourceRoot = new URL('../../src/', import.meta.url);
  const files = await readdir(sourceRoot, { recursive: true });
  const modules = new Map(
    await Promise.all(
      files
        .filter((file) => file.endsWith('.jsx'))
        .map(async (file) => {
          const url = new URL(file.replaceAll('\\', '/'), sourceRoot).href;
          return [url, await compileJsx(url)];
        }),
    ),
  );

  nodeModule.registerHooks({
    load(url, context, nextLoad) {
      if (url.endsWith('.css')) return { format: 'module', source: '', shortCircuit: true };
      return modules.get(url) ?? nextLoad(url, context);
    },
  });
}

export async function load(url, context, nextLoad) {
  if (url.endsWith('.css')) return { format: 'module', source: '', shortCircuit: true };
  return url.endsWith('.jsx') ? compileJsx(url) : nextLoad(url, context);
}
