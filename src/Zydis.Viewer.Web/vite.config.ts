import { createReadStream, existsSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import type { Plugin } from 'vite';
import { defineConfig } from 'vitest/config';

const datafiles = fileURLToPath(new URL('../../datafiles', import.meta.url));

// Gives npm run dev the datafiles route of the local host, so the dev server loads the database of the checkout.
// configureServer runs for the dev server only, which keeps the route out of the build and of vite preview.
function serveDatafiles(): Plugin {
  return {
    name: 'serve-datafiles',
    configureServer(server) {
      server.middlewares.use('/datafiles', (request, response, next) => {
        // The pattern admits plain file names only, so the path stays inside the datafiles folder. The query string
        // stays out of the match, as it does on the host.
        const name = (request.url ?? '').split('?')[0].slice(1);
        const file = path.join(datafiles, name);
        if (request.method !== 'GET' || !/^[A-Za-z0-9_-]+\.json$/.test(name) || !existsSync(file)) {
          next();
          return;
        }
        response.setHeader('Content-Type', 'application/json');
        createReadStream(file).on('error', next).pipe(response);
      });
    },
  };
}

export default defineConfig({
  // Without the single-page fallback a missing datafiles/instructions.json answers 404, so the app shows its empty
  // state instead of trying to load index.html as a database.
  appType: 'mpa',
  // Relative URLs let the built app run from any sub-path.
  base: './',
  plugins: [serveDatafiles()],
});
