import { createCompareDialog } from './diff/compareDialog';
import { changedNodeIdsInOrder, closeDiff, COUNT_SIGNS, setChangesOnly, stepChange } from './diff/diffState';
import { revealHashForm, trackSelectionInHash } from './hash';
import { createDiffPanel } from './inspector/diffInspector';
import { mountInspector } from './inspector/inspector';
import { describeLoadError, readSource, type LoadSource } from './loader';
import type { ViewModel, ViewNode } from './model';
import { startRuntime } from './runtime';
import { createSearchBox } from './search/searchBox';
import { setSearch } from './search/searchState';
import { store, type AppState } from './store';
import { collapseAll, collapseSubtree, expandAll, expandSubtree, mountGrid } from './tree/grid';

export function mountApp(root: HTMLElement): void {
  const fileInput = document.createElement('input');
  fileInput.type = 'file';
  fileInput.accept = '.json,application/json';
  fileInput.hidden = true;

  const openButton = document.createElement('button');
  openButton.type = 'button';
  openButton.textContent = 'Open';

  const treeButtons = [toolbarButton('Expand all', expandAll), toolbarButton('Collapse all', collapseAll)];
  const subtreeButtons = [
    toolbarButton('Expand subtree', expandSubtree),
    toolbarButton('Collapse subtree', collapseSubtree),
  ];
  const compareDialog = createCompareDialog();
  const compareButton = toolbarButton('Compare', () => compareDialog.open());
  const diffControls = createDiffControls();

  const dropZone = document.createElement('div');
  dropZone.className = 'drop-zone';
  dropZone.textContent = 'Drop an instructions.json file anywhere on the window, or pick one with Open.';

  const content = document.createElement('main');
  content.className = 'content';
  content.append(dropZone);
  const gridViewport = mountGrid(content);
  mountInspector(content, createDiffPanel());

  const toolbar = document.createElement('header');
  toolbar.className = 'toolbar';
  const search = createSearchBox(() => gridViewport.focus());
  toolbar.append(openButton, ...search, ...treeButtons, ...subtreeButtons, compareButton, diffControls.element,
    fileInput);

  const statusBar = document.createElement('footer');
  statusBar.className = 'status-bar';
  statusBar.setAttribute('role', 'status');

  root.replaceChildren(toolbar, content, statusBar, compareDialog.element);

  const render = (state: AppState): void => {
    dropZone.hidden = state.database !== null;
    // The grid shows the kept rows of a search or of changes only open whatever expanded holds, so these would act
    // unseen until the filters clear.
    const filtered = state.searchInclude !== null || state.diff?.changesOnly === true;
    for (const button of treeButtons) {
      button.disabled = state.database === null || filtered;
    }
    for (const button of subtreeButtons) {
      button.disabled = state.database === null || filtered || state.selectedNodeId === null;
    }
    compareButton.disabled = state.database === null;
    diffControls.render(state);
    renderStatus(statusBar, state);
  };
  store.subscribe(render);
  render(store.state);
  trackSelectionInHash();

  const runtimeReady = startRuntime().then((runtime) => {
    store.set({ runtime });
    return runtime;
  });

  // Counts the loads started, so a slow load, such as the start-up fetch, cannot replace a file dropped after it.
  let latestLoad = 0;

  const load = async (source: LoadSource): Promise<void> => {
    const ticket = ++latestLoad;
    try {
      const runtime = await runtimeReady;
      if (ticket !== latestLoad) {
        return;
      }

      // Set before the read starts, so the text paints while the file downloads and stays up during the parse.
      store.set({ loading: source.kind === 'url' ? source.name : source.file.name });
      const read = await readSource(source);
      if (ticket !== latestLoad) {
        return;
      }
      if (read === null) {
        store.set({ loading: null });
        return;
      }

      const handle = runtime.loadDatabase(read.json);
      let model: ViewModel;
      try {
        model = runtime.getViewModel(handle);
      } catch (error) {
        runtime.release(handle);
        throw error;
      }

      const previous = store.state.database;
      const previousDiff = store.state.diff;
      try {
        store.set({
          database: { name: read.name, handle, model },
          // The comparison was with the database this one replaces.
          diff: null,
          error: null,
          loading: null,
          // The node ids of the previous database name other nodes in this one, so expansion, selection and the
          // search result start over.
          expanded: new Set(model.rootIds),
          searchInclude: null,
          selectedNodeId: null,
        });
      } finally {
        // A subscriber that throws must not keep the old database alive in the runtime heap.
        if (previous !== null) {
          runtime.release(previous.handle);
        }
        if (previousDiff !== null) {
          runtime.release(previousDiff.base.handle);
        }
      }
      // The text in the search box stays, so the search runs again on the new tree.
      setSearch(store.state.search);
      revealHashForm();
    } catch (error) {
      if (ticket === latestLoad) {
        store.set({ error: describeLoadError(error), loading: null });
      }
    }
  };

  openButton.addEventListener('click', () => fileInput.click());
  fileInput.addEventListener('change', () => {
    const file = fileInput.files?.[0];
    // Cleared so that picking the same file again still fires a change.
    fileInput.value = '';
    if (file !== undefined) {
      void load({ kind: 'file', file });
    }
  });

  const carriesFiles = (event: DragEvent): boolean => event.dataTransfer?.types.includes('Files') ?? false;

  window.addEventListener('dragover', (event) => {
    if (!carriesFiles(event)) {
      return;
    }
    // Without this the window is no drop target, and the browser opens the dropped file in place of the app.
    event.preventDefault();
    dropZone.classList.add('dragging');
  });
  window.addEventListener('dragleave', (event) => {
    // Only a drag that leaves the window has no related target.
    if (event.relatedTarget === null) {
      dropZone.classList.remove('dragging');
    }
  });
  window.addEventListener('drop', (event) => {
    dropZone.classList.remove('dragging');
    const file = event.dataTransfer?.files[0];
    if (file === undefined) {
      return;
    }
    event.preventDefault();
    if (!compareDialog.takeFile(file)) {
      void load({ kind: 'file', file });
    }
  });

  // A pasted link that differs only in the hash changes the hash without a reload.
  window.addEventListener('hashchange', revealHashForm);

  void load({ kind: 'url', url: 'datafiles/instructions.json', name: 'instructions.json' });
}

function createDiffControls(): { element: HTMLElement; render(state: AppState): void } {
  const changesOnly = document.createElement('input');
  changesOnly.type = 'checkbox';
  changesOnly.addEventListener('change', () => setChangesOnly(changesOnly.checked));
  const changesOnlyLabel = document.createElement('label');
  changesOnlyLabel.append(changesOnly, ' Changes only');

  const previous = toolbarButton('Previous', () => stepChange(-1));
  const next = toolbarButton('Next', () => stepChange(1));
  const counter = document.createElement('span');
  counter.className = 'diff-counter';

  const element = document.createElement('div');
  element.className = 'diff-controls';
  element.append(changesOnlyLabel, previous, counter, next, toolbarButton('Close diff', closeDiff));

  return {
    element,
    render({ diff }) {
      element.hidden = diff === null;
      if (diff === null) {
        return;
      }
      const changed = changedNodeIdsInOrder(diff.model).length;
      changesOnly.checked = diff.changesOnly;
      previous.disabled = diff.cursor <= 0;
      next.disabled = diff.cursor >= changed - 1;
      counter.textContent = `${diff.cursor + 1} / ${changed}`;
    },
  };
}

function renderStatus(statusBar: HTMLElement, state: AppState): void {
  const items: HTMLElement[] = [];
  const { database, diff, error, loading } = state;

  if (database !== null && diff !== null) {
    const { formIdError, stats } = diff.model;
    items.push(statusItem(`A: ${diff.base.name}`), statusItem(`B: ${database.name}`));
    // Styled as the badges of the inner rows, so the status bar explains their signs.
    items.push(...COUNT_SIGNS.map(([status, sign]) =>
      statusItem(`${sign}${formatCount(stats[status])} ${status}`, `badge status-${status}`)));
    if (state.searchInclude !== null) {
      items.push(matchesItem(diff.model, state.searchInclude));
    }
    if (formIdError !== undefined) {
      items.push(statusItem(`form ids by position: ${formIdError}`, 'warning'));
    }
  } else if (database !== null) {
    const { formIdError, schema, stats } = database.model;
    items.push(
      statusItem(database.name),
      statusItem(`${formatCount(stats.definitions)} definitions`),
      statusItem(`${formatCount(stats.nodes)} nodes`),
      statusItem(`${formatCount(stats.conflicts)} conflicts`),
    );
    if (state.searchInclude !== null) {
      items.push(matchesItem(database.model, state.searchInclude));
    }
    if (schema === 'object') {
      items.push(statusItem('filters: object form, file order'));
    }
    if (formIdError !== undefined) {
      items.push(statusItem(`form ids by position: ${formIdError}`, 'warning'));
    }
  }

  if (loading !== null) {
    items.push(statusItem(`Loading ${loading}`));
  } else if (error !== null) {
    items.push(statusItem(error, 'error'));
  } else if (database === null) {
    items.push(statusItem(state.runtime === null ? 'Starting the .NET runtime' : 'No database loaded'));
  }

  statusBar.replaceChildren(...items);
}

// The include set also holds the ancestors of the matches, and of its nodes only the matches are definition rows. In a
// diff a moved form that matches counts once for each of its two rows.
function matchesItem(tree: { nodes: ViewNode[] }, include: ReadonlySet<number>): HTMLElement {
  let matches = 0;
  for (const nodeId of include) {
    if (tree.nodes[nodeId].definitionIndex !== undefined) {
      matches++;
    }
  }
  return statusItem(`${formatCount(matches)} ${matches === 1 ? 'match' : 'matches'}`);
}

function toolbarButton(text: string, action: () => void): HTMLButtonElement {
  const button = document.createElement('button');
  button.type = 'button';
  button.textContent = text;
  button.addEventListener('click', action);
  return button;
}

function statusItem(text: string, className?: string): HTMLElement {
  const item = document.createElement('span');
  item.textContent = text;
  if (className !== undefined) {
    item.className = className;
  }
  return item;
}

function formatCount(value: number): string {
  return value.toLocaleString('en-US');
}
