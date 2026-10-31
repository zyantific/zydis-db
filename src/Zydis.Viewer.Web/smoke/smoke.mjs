import { copyFileSync, existsSync, mkdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { chromium } from 'playwright';
import { preview } from 'vite';

const webRoot = fileURLToPath(new URL('..', import.meta.url));
const distDir = path.join(webRoot, 'dist');
const database = path.join(webRoot, '..', '..', 'datafiles', 'instructions.json');
// Covers the runtime download and the parse of the whole database.
const timeout = 60_000;

if (!existsSync(path.join(distDir, '_framework', 'dotnet.js'))) {
  console.error('dist/_framework/dotnet.js is missing. Run npm run build first.');
  process.exit(1);
}

// Taken from the file so the checks follow the database as it changes.
const forms = JSON.parse(readFileSync(database, 'utf8'));
const expected = `${forms.length.toLocaleString('en-US')} definitions`;
const searchText = 'cfcmovb';
const searchMatches = forms.filter((form) => form.mnemonic.includes(searchText)).length;

mkdirSync(path.join(distDir, 'datafiles'), { recursive: true });
copyFileSync(database, path.join(distDir, 'datafiles', 'instructions.json'));

const server = await preview({ root: webRoot, logLevel: 'warn', preview: { port: 4173, strictPort: true } });
let browser;
try {
  browser = await chromium.launch();
  const page = await browser.newPage();

  // The list catches an error that arrives after the status bar check. The promise ends a failed start without
  // waiting for the timeout.
  const problems = [];
  const failure = new Promise((_, reject) => {
    const fail = (problem) => {
      problems.push(problem);
      reject(new Error(problem));
    };
    page.on('pageerror', (error) => fail(`Page error: ${error.message}`));
    page.on('console', (message) => {
      if (message.type() === 'error') {
        fail(`Console error: ${message.text()}`);
      } else if (message.type() === 'warning') {
        console.log(`Console warning: ${message.text()}`);
      }
    });
  });
  // A failed goto skips the race, and a later rejection without a handler would end the process before the cleanup.
  failure.catch(() => {});

  const statusBar = page.locator('.status-bar');
  const started = performance.now();
  await page.goto(server.resolvedUrls.local[0]);
  await Promise.race([
    failure,
    statusBar.filter({ hasText: expected }).waitFor({ timeout }),
    statusBar.locator('.error').waitFor({ timeout }).then(async () => {
      throw new Error(`Load error: ${await statusBar.locator('.error').innerText()}`);
    }),
  ]);
  const seconds = ((performance.now() - started) / 1000).toFixed(1);

  console.log(`Status bar after ${seconds} s: ${(await statusBar.innerText()).replace(/\s+/g, ' ')}`);

  // Only the rows in view exist in the DOM, so the count comes from the height of the scroll canvas.
  const rowCount = () => page.locator('.grid-canvas').evaluate((canvas) => canvas.offsetHeight / 22);
  const firstRow = page.locator('.grid-row[data-index="0"]');
  const primaryLabel = await firstRow.locator('.label').innerText();
  if (primaryLabel !== 'PRIMARY') {
    throw new Error(`The first row is ${primaryLabel}, expected PRIMARY.`);
  }

  // A load opens the root rows, so the glyph click closes PRIMARY before the second click opens it again.
  const loaded = await rowCount();
  await firstRow.locator('.expander').click();
  const collapsed = await rowCount();
  if (collapsed !== 1) {
    throw new Error(`Collapsing PRIMARY left ${collapsed} rows, expected 1.`);
  }
  await firstRow.locator('.expander').click();
  await page.locator('.grid-row[data-index="1"]').waitFor({ timeout: 5_000 });

  await firstRow.locator('.label').click();
  await page.keyboard.press('*');
  const subtree = await rowCount();
  if (!(subtree > Math.max(100, loaded))) {
    throw new Error(`* on PRIMARY shows ${subtree} rows, expected more than 100 and more than ${loaded}.`);
  }
  console.log(`Grid rows: ${loaded} after load, ${collapsed} with PRIMARY collapsed, ${subtree} after * on PRIMARY`);

  // With everything collapsed the result sits in a branch that is closed before the search, so only the clear can
  // bring it back into the tree.
  await page.getByRole('button', { name: 'Collapse all' }).click();
  const searchBox = page.getByRole('searchbox', { name: 'Search' });
  await page.keyboard.press('Control+f');
  if (!(await searchBox.evaluate((box) => box === document.activeElement))) {
    throw new Error('Ctrl+F did not focus the search box.');
  }
  await page.keyboard.type(searchText);
  const matchesText = `${searchMatches.toLocaleString('en-US')} ${searchMatches === 1 ? 'match' : 'matches'}`;
  await statusBar.filter({ hasText: matchesText }).waitFor({ timeout: 5_000 });
  const result = page.locator('.grid-row')
    .filter({ has: page.locator('.grid-cell:nth-child(2)', { hasText: new RegExp(`^${searchText}$`) }) })
    .first();
  await result.waitFor({ timeout: 5_000 });
  const searched = await rowCount();
  if (!(await page.getByRole('button', { name: 'Expand all' }).isDisabled())) {
    throw new Error('Expand all stays enabled during a search.');
  }

  // An invalid pattern shows the error and keeps the rows of the last valid one.
  await page.getByRole('combobox', { name: 'Match mode' }).selectOption('regex');
  await searchBox.press('(');
  await page.locator('.search-error').waitFor({ timeout: 5_000 });
  if ((await rowCount()) !== searched) {
    throw new Error(`An invalid pattern changed the grid from ${searched} to ${await rowCount()} rows.`);
  }
  await searchBox.press('Backspace');
  await page.locator('.search-error').waitFor({ state: 'hidden', timeout: 5_000 });

  // Every definition row carries the same label, so the depth and the cell texts tell the picked form apart.
  const describeRow = (row) => row.evaluate((element) =>
    `${element.querySelector('.expander').style.marginLeft} ${element.innerText.replace(/\s+/g, ' ')}`);
  const picked = await describeRow(result);
  await result.click();
  await searchBox.press('Escape');
  const selected = page.locator('.grid-row.selected');
  await selected.waitFor({ timeout: 5_000 });
  const kept = await describeRow(selected);
  if (kept !== picked) {
    throw new Error(`After the clear "${kept}" is selected, expected "${picked}".`);
  }
  // The grid scrolls by clientHeight, which rounds the fractional height of the viewport, so a row at the bottom
  // edge can overhang it by less than a pixel.
  const insideViewport = async (row) => {
    const rowBox = await row.boundingBox();
    const viewportBox = await page.locator('.grid-viewport').boundingBox();
    return rowBox.y >= viewportBox.y && rowBox.y + rowBox.height <= viewportBox.y + viewportBox.height + 1;
  };
  if (!(await insideViewport(selected))) {
    throw new Error('The selected row is outside the viewport after the clear.');
  }
  if (!(await page.locator('.grid-viewport').evaluate((viewport) => viewport === document.activeElement))) {
    throw new Error('Escape did not return the focus to the grid.');
  }
  if (/\bmatch(es)?\b/.test(await statusBar.innerText())) {
    throw new Error('The status bar still counts matches after the clear.');
  }
  const restored = await rowCount();
  console.log(`Search ${searchText}: ${searchMatches} matches in ${searched} rows, ${restored} rows after the clear`
    + ` with "${kept}" selected`);

  // The selection wrote the form id of the picked row from the view model into the hash.
  const formPrefix = '#form=';
  const hash = new URL(page.url()).hash;
  if (!hash.startsWith(formPrefix)) {
    throw new Error(`Selecting ${searchText} left the hash at "${hash}".`);
  }
  const formId = decodeURIComponent(hash.slice(formPrefix.length));
  const inspectorTitle = page.locator('.inspector-title');
  if ((await inspectorTitle.innerText()) !== formId) {
    throw new Error(`The inspector header shows "${await inspectorTitle.innerText()}", expected ${formId}.`);
  }

  // A change of the hash alone does not reload the page, so the blank page in between makes the link a fresh load.
  await page.goto('about:blank');
  await page.goto(`${server.resolvedUrls.local[0]}${formPrefix}${encodeURIComponent(formId)}`);
  await Promise.race([failure, statusBar.filter({ hasText: expected }).waitFor({ timeout })]);
  await page.waitForFunction((id) => document.querySelector('.inspector-title')?.textContent === id, formId,
    { timeout: 5_000 });
  const linked = page.locator('.grid-row.selected');
  await linked.waitFor({ timeout: 5_000 });
  const linkedMnemonic = await linked.locator('.grid-cell:nth-child(2)').innerText();
  if (linkedMnemonic !== searchText) {
    throw new Error(`The link ${formPrefix}${formId} selected a ${linkedMnemonic} row, expected ${searchText}.`);
  }
  if (!(await insideViewport(linked))) {
    throw new Error(`The row the link ${formPrefix}${formId} selected is outside the viewport.`);
  }
  console.log(`Link ${formPrefix}${formId}: inspector header "${await inspectorTitle.innerText()}", selected`
    + ` "${await describeRow(linked)}"`);

  // The aaa form tests mode, so the decision row above it lists the slots of that filter. The row and its child labels
  // come from the loaded tree, whichever form of the mode node the tree builder chose.
  const decisionText = 'aaa';
  await page.getByRole('combobox', { name: 'Match mode' }).selectOption('exact');
  await searchBox.fill(decisionText);
  const decisionForm = page.locator('.grid-row')
    .filter({ has: page.locator('.grid-cell:nth-child(2)', { hasText: new RegExp(`^${decisionText}$`) }) })
    .first();
  await decisionForm.waitFor({ timeout: 5_000 });
  await decisionForm.click();
  await searchBox.press('Escape');
  // Left selects the parent row or closes an open one, so a few presses reach the decision row from the form.
  const selectedRow = page.locator('.grid-row.selected');
  for (let press = 0; press < 4 && (await selectedRow.locator('.icon-decision').count()) === 0; press++) {
    await page.keyboard.press('ArrowLeft');
  }
  const decision = await selectedRow.evaluate((row) => {
    const depthOf = (element) => parseFloat(element.querySelector('.expander').style.marginLeft);
    const children = [];
    let childDepth;
    for (let next = row.nextElementSibling; next !== null && depthOf(next) > depthOf(row);
      next = next.nextElementSibling) {
      childDepth ??= depthOf(next);
      if (depthOf(next) === childDepth) {
        children.push(next.querySelector('.label').textContent);
      }
    }
    return { isDecision: row.querySelector('.icon-decision') !== null, label: row.querySelector('.label').textContent,
      children };
  });
  if (!decision.isDecision || decision.children.length === 0) {
    throw new Error(`Left from ${decisionText} stopped at ${decision.label} with children ${decision.children}.`);
  }
  if (new URL(page.url()).hash !== '') {
    throw new Error(`Selecting the decision row ${decision.label} left the hash at ${new URL(page.url()).hash}.`);
  }
  const slots = await page.locator('.inspector-section')
    .filter({ has: page.locator('.inspector-section-title', { hasText: /^Slots$/ }) })
    .evaluate((section) => Array.from(section.querySelectorAll('.inspector-key'),
      (key) => [key.textContent, key.nextElementSibling.textContent]));
  const occupied = new Set(decision.children);
  const misread = slots.filter(([key, value]) => value !== (occupied.has(key) ? 'occupied' : 'empty'));
  // The inspector appends a child label it could not rebuild from filterSlots after else, which means the slot label
  // format of the tree builder changed.
  if (slots.length === 0 || slots[slots.length - 1][0] !== 'else' || misread.length > 0) {
    throw new Error(`The slots of ${decision.label} read ${JSON.stringify(slots)}, its child rows are`
      + ` ${JSON.stringify(decision.children)}.`);
  }
  console.log(`Decision ${decision.label} above ${decisionText}: `
    + `${slots.map(([key, value]) => `${key} ${value}`).join(', ')}`);

  if (problems.length > 0) {
    throw new Error(problems.join('\n'));
  }
} catch (error) {
  console.error(error instanceof Error ? error.message : error);
  process.exitCode = 1;
} finally {
  await browser?.close();
  await server.close();
}
