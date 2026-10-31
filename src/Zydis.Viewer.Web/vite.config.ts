import { defineConfig } from 'vitest/config';

export default defineConfig({
  // Without the single-page fallback a missing datafiles/instructions.json answers 404, so the app shows its empty
  // state instead of trying to load index.html as a database.
  appType: 'mpa',
  // Relative URLs let the built app run from any sub-path.
  base: './',
});
