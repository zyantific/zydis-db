import { copyFileSync, existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import os from 'node:os';
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

// The head of the diff check is a copy of the database with a changed operand action in the first cfcmovb form and
// without hlt, compared with the database itself as the base. It lives outside the checkout, since the viewer and its
// checks never write the database. hlt sits at opcode F4 of the primary table, after the subtree of opcode 62 that
// holds cfcmovb, so the merged tree numbers its rows up to the changed one as the head does, and the Close diff check
// sees a selection whose id stays while its tree changes.
const headForms = JSON.parse(readFileSync(database, 'utf8'));
const changedOperand = headForms.find((form) => form.mnemonic === searchText).operands[0];
const baseAction = changedOperand.action;
changedOperand.action = baseAction === 'condwrite' ? 'write' : 'condwrite';
const removedMnemonic = 'hlt';
const removedForms = headForms.filter((form) => form.mnemonic === removedMnemonic).length;

const server = await preview({ root: webRoot, logLevel: 'warn', preview: { port: 4173, strictPort: true } });
let browser;
let scratchDir;
try {
  // Inside the try, so the cleanup removes it whatever fails after it.
  scratchDir = mkdtempSync(path.join(os.tmpdir(), 'zydis-viewer-smoke-'));
  const headFile = path.join(scratchDir, 'instructions-head.json');
  writeFileSync(headFile, JSON.stringify(headForms.filter((form) => form.mnemonic !== removedMnemonic)));

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
      // The compare dialog asks for the git references of the local host, which the preview server does not have.
      if (message.type() === 'error' && message.location().url.endsWith('/api/git/refs')) {
        console.log(`Expected console error: ${message.text()}`);
      } else if (message.type() === 'error') {
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

  // The modified copy becomes the loaded database, the head. The real database is the base the dialog picks.
  const headName = path.basename(headFile);
  const headDefinitions = `${(forms.length - removedForms).toLocaleString('en-US')} definitions`;
  const hashBeforeDiff = new URL(page.url()).hash;
  await page.locator('.toolbar input[type=file]').setInputFiles(headFile);
  await Promise.race([failure, statusBar.filter({ hasText: headDefinitions }).waitFor({ timeout })]);

  // The match mode is still exact from the decision step. The head keeps every cfcmovb form and changes only an operand
  // of one, so the single view and the diff count the same matches.
  const exactMatches = forms.filter((form) => form.mnemonic === searchText).length;
  const exactText = `${exactMatches.toLocaleString('en-US')} ${exactMatches === 1 ? 'match' : 'matches'}`;
  await searchBox.fill(searchText);
  await statusBar.filter({ hasText: exactText }).waitFor({ timeout: 5_000 });

  // The picker would show only after the answer to the refs request, so the check waits for it.
  const refsAnswered = page.waitForResponse((response) => response.url().endsWith('/api/git/refs'));
  await page.locator('.toolbar').getByRole('button', { name: 'Compare', exact: true }).click();
  const dialog = page.locator('dialog.compare-dialog');
  await dialog.waitFor({ timeout: 5_000 });
  await refsAnswered;
  if (await dialog.getByRole('combobox', { name: 'Base reference' }).isVisible()) {
    throw new Error('The compare dialog shows the git reference picker without the local host.');
  }
  await dialog.locator('input[type=file]').setInputFiles(database);
  const compareStarted = performance.now();
  await dialog.getByRole('button', { name: 'Compare', exact: true }).click();
  await Promise.race([failure, dialog.waitFor({ state: 'hidden', timeout })]);
  const compareSeconds = ((performance.now() - compareStarted) / 1000).toFixed(1);

  const counts = await statusBar.locator('.badge').allInnerTexts();
  const expectedCounts = ['+0 added', `\u2212${removedForms} removed`, '~1 changed', '>0 moved'];
  if (JSON.stringify(counts) !== JSON.stringify(expectedCounts)) {
    throw new Error(`The diff counts read ${JSON.stringify(counts)}, expected ${JSON.stringify(expectedCounts)}.`);
  }
  const statusText = (await statusBar.innerText()).replace(/\s+/g, ' ');
  if (!statusText.includes(`A: ${path.basename(database)}`) || !statusText.includes(`B: ${headName}`)) {
    throw new Error(`The status bar of the diff reads "${statusText}", expected the names of A and B.`);
  }
  if (!statusText.includes(exactText)) {
    throw new Error(`The status bar of the diff reads "${statusText}", expected the ${exactText} of the search typed`
      + ' before the comparison.');
  }
  console.log(`Diff after ${compareSeconds} s from the Compare click: ${statusText}`);
  // Expand all stays disabled while a search is active.
  await searchBox.press('Escape');

  await page.getByRole('button', { name: 'Expand all' }).click();
  const expandedRows = await rowCount();
  await page.getByRole('checkbox', { name: 'Changes only' }).check();
  const changesRows = await rowCount();
  if (!(changesRows < 40 && expandedRows > 1000)) {
    throw new Error(`Changes only shows ${changesRows} of ${expandedRows} rows, expected fewer than 40 of more than`
      + ' 1000.');
  }

  // The removed row and the changed row are the two steps.
  const counter = page.locator('.diff-counter');
  await page.getByRole('button', { name: 'Next', exact: true }).click();
  if ((await counter.innerText()) !== '1 / 2') {
    throw new Error(`The step counter reads ${await counter.innerText()} after the first step, expected 1 / 2.`);
  }
  const changedRow = page.locator('.grid-row.status-changed');
  await changedRow.click();
  const changedMnemonic = await changedRow.locator('.grid-cell:nth-child(2)').innerText();
  if (changedMnemonic !== searchText) {
    throw new Error(`The changed row shows ${changedMnemonic}, expected ${searchText}.`);
  }

  // The first operand row of both operand tables is tinted, and its action cell holds the value of each side.
  const operandCells = (column) => page.locator(`.diff-grid .${column} tbody tr.diff-changed`)
    .evaluateAll((rows) => rows.map((row) => [row.cells[0].textContent, row.cells[1].textContent]));
  const tinted = [await operandCells('diff-a'), await operandCells('diff-b')];
  const expectedTint = [[['0', baseAction]], [['0', changedOperand.action]]];
  if (JSON.stringify(tinted) !== JSON.stringify(expectedTint)) {
    throw new Error(`The tinted operand rows read ${JSON.stringify(tinted)}, expected`
      + ` ${JSON.stringify(expectedTint)}.`);
  }
  const mnemonicKey = page.locator('.diff-grid .inspector-key', { hasText: /^mnemonic$/ });
  if (await mnemonicKey.isVisible()) {
    throw new Error('The unchanged mnemonic row shows before Show unchanged is checked.');
  }
  await page.getByRole('checkbox', { name: 'Show unchanged' }).check();
  await mnemonicKey.waitFor({ timeout: 5_000 });
  if (new URL(page.url()).hash !== hashBeforeDiff) {
    throw new Error(`Selecting a diff row changed the hash from "${hashBeforeDiff}" to "${new URL(page.url()).hash}".`);
  }
  console.log(`Changes only: ${changesRows} of ${expandedRows} rows, ${searchText} operand 0 action`
    + ` ${baseAction} in A and ${changedOperand.action} in B`);

  // The changed row is still selected, so the single view selects the row of its head form and opens the path to it.
  // Eleven other cfcmovb forms share the mnemonic, so the form id tells the carried form apart.
  const carriedForm = await inspectorTitle.innerText();
  const carriedHash = `${formPrefix}${encodeURIComponent(carriedForm)}`;
  await page.getByRole('button', { name: 'Close diff' }).click();
  await statusBar.filter({ hasText: headDefinitions }).waitFor({ timeout: 5_000 });
  const reopened = page.locator('.grid-row.selected');
  if ((await reopened.count()) === 0 || (await inspectorTitle.innerText()) !== carriedForm
    || !(await insideViewport(reopened))) {
    throw new Error(`Close diff selected "${await inspectorTitle.innerText()}", expected ${carriedForm} in view.`);
  }
  if (new URL(page.url()).hash !== carriedHash) {
    throw new Error(`Close diff left the hash at "${new URL(page.url()).hash}" with ${carriedForm} selected.`);
  }
  const reopenedRow = await describeRow(reopened);
  const closedRows = await rowCount();

  // A link opens the ancestors of its form and nothing else. The one root of the tree is an ancestor of every form, so
  // after Collapse all the link to the selected form shows the rows Close diff has to leave.
  await page.getByRole('button', { name: 'Collapse all' }).click();
  await page.evaluate((hash) => {
    location.hash = hash;
  }, carriedHash);
  await page.waitForFunction((id) => document.querySelector('.inspector-title')?.textContent === id, carriedForm,
    { timeout: 5_000 });
  const linkRows = await rowCount();
  if (closedRows !== linkRows) {
    throw new Error(`Close diff left ${closedRows} rows, expected the ${linkRows} the link to ${carriedForm} shows.`);
  }
  console.log(`Close diff: "${reopenedRow}" selected in ${closedRows} rows, as the link to ${carriedForm} shows`);

  // A second comparison closes under an active search. The cfcmovo and cfcmovno forms at opcodes 40 and 41 come before
  // the cfcmovb forms at 42, so they put the carried row below the first screen of the result, where the grid has to
  // scroll to show it.
  const familyText = 'cfcmov';
  const familyMatches = forms.filter((form) => form.mnemonic.includes(familyText)).length;
  await page.getByRole('combobox', { name: 'Match mode' }).selectOption('contains');
  await searchBox.fill(familyText);
  await statusBar.filter({ hasText: `${familyMatches.toLocaleString('en-US')} matches` }).waitFor({ timeout: 5_000 });
  await page.locator('.toolbar').getByRole('button', { name: 'Compare', exact: true }).click();
  await dialog.waitFor({ timeout: 5_000 });
  await dialog.locator('input[type=file]').setInputFiles(database);
  await dialog.getByRole('button', { name: 'Compare', exact: true }).click();
  await Promise.race([failure, dialog.waitFor({ state: 'hidden', timeout })]);
  // The search hides the removed hlt row, so the first step selects the changed row.
  await page.getByRole('button', { name: 'Next', exact: true }).click();
  if ((await inspectorTitle.innerText()) !== carriedForm) {
    throw new Error(`The first step under the search selected "${await inspectorTitle.innerText()}", expected`
      + ` ${carriedForm}.`);
  }
  await page.getByRole('button', { name: 'Close diff' }).click();
  await statusBar.filter({ hasText: headDefinitions }).waitFor({ timeout: 5_000 });
  const searchedRow = page.locator('.grid-row.selected');
  if ((await searchedRow.count()) === 0 || (await inspectorTitle.innerText()) !== carriedForm
    || !(await insideViewport(searchedRow))) {
    throw new Error(`Close diff under the search ${familyText} selected "${await inspectorTitle.innerText()}", expected`
      + ` ${carriedForm} in view.`);
  }
  console.log(`Close diff under the search ${familyText}: "${await describeRow(searchedRow)}" selected in view at`
    + ` scroll offset ${await page.locator('.grid-viewport').evaluate((viewport) => viewport.scrollTop)}`);

  if (problems.length > 0) {
    throw new Error(problems.join('\n'));
  }
} catch (error) {
  console.error(error instanceof Error ? error.message : error);
  process.exitCode = 1;
} finally {
  if (scratchDir !== undefined) {
    rmSync(scratchDir, { recursive: true, force: true });
  }
  await browser?.close();
  await server.close();
}
