import { describeLoadError, readSource, type LoadSource } from './loader';
import type { ViewModel } from './model';
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

  const dropZone = document.createElement('div');
  dropZone.className = 'drop-zone';
  dropZone.textContent = 'Drop an instructions.json file anywhere on the window, or pick one with Open.';

  const content = document.createElement('main');
  content.className = 'content';
  content.append(dropZone);
  const gridViewport = mountGrid(content);

  const toolbar = document.createElement('header');
  toolbar.className = 'toolbar';
  const search = createSearchBox(() => gridViewport.focus());
  toolbar.append(openButton, ...search, ...treeButtons, ...subtreeButtons, fileInput);

  const statusBar = document.createElement('footer');
  statusBar.className = 'status-bar';
  statusBar.setAttribute('role', 'status');

  root.replaceChildren(toolbar, content, statusBar);

  const render = (state: AppState): void => {
    dropZone.hidden = state.database !== null;
    // The grid shows the kept rows of a search open whatever expanded holds, so these would act unseen until it clears.
    const searching = state.searchInclude !== null;
    for (const button of treeButtons) {
      button.disabled = state.database === null || searching;
    }
    for (const button of subtreeButtons) {
      button.disabled = state.database === null || searching || state.selectedNodeId === null;
    }
    renderStatus(statusBar, state);
  };
  store.subscribe(render);
  render(store.state);

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
      try {
        store.set({
          database: { name: read.name, handle, model },
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
      }
      // The text in the search box stays, so the search runs again on the new tree.
      setSearch(store.state.search);
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
    void load({ kind: 'file', file });
  });

  void load({ kind: 'url', url: 'datafiles/instructions.json', name: 'instructions.json' });
}

function renderStatus(statusBar: HTMLElement, state: AppState): void {
  const items: HTMLElement[] = [];
  const { database, error, loading } = state;

  if (database !== null) {
    const { formIdError, schema, stats } = database.model;
    items.push(
      statusItem(database.name),
      statusItem(`${formatCount(stats.definitions)} definitions`),
      statusItem(`${formatCount(stats.nodes)} nodes`),
      statusItem(`${formatCount(stats.conflicts)} conflicts`),
    );
    if (state.searchInclude !== null) {
      const matches = countMatches(database.model, state.searchInclude);
      items.push(statusItem(`${formatCount(matches)} ${matches === 1 ? 'match' : 'matches'}`));
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

// The include set also holds the ancestors of the matches, and of its nodes only the matches are definition rows.
function countMatches(model: ViewModel, include: ReadonlySet<number>): number {
  let matches = 0;
  for (const nodeId of include) {
    if (model.nodes[nodeId].definitionIndex !== undefined) {
      matches++;
    }
  }
  return matches;
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
