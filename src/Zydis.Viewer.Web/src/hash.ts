import type { DiffModel, ViewModel } from './model';
import { store } from './store';
import { ancestorsOf } from './tree/rows';

const FORM_PREFIX = '#form=';

// Selects and reveals the form the location hash names. An id that names no form of the loaded database is ignored,
// and so is the hash during a diff, whose rows have other node ids.
export function revealHashForm(): void {
  const { database, diff, expanded, searchInclude } = store.state;
  const formId = hashFormId(location.hash);
  if (database === null || diff !== null || formId === null) {
    return;
  }

  const { model } = database;
  const index = model.definitions.findIndex((definition) => definition.formId === formId);
  if (index < 0) {
    return;
  }
  const nodeId = model.nodes.findIndex((node) => node.definitionIndex === index);
  // An active search shows only the paths to its matches, and a form outside them would be selected out of sight.
  if (nodeId < 0 || (searchInclude !== null && !searchInclude.has(nodeId))) {
    return;
  }

  // The grid scrolls a new selection into view after it lays out the rows the expansion opens.
  store.set({ expanded: new Set([...expanded, ...ancestorsOf(model, nodeId)]), selectedNodeId: nodeId });
}

// Keeps the hash on the selected form, so the address bar holds a link to it, and clears it while another kind of row
// is selected. A load clears the selection before revealHashForm reads the hash, so no selection leaves it alone. A
// diff leaves it alone as well, so the link of the single view survives a comparison.
export function trackSelectionInHash(): void {
  // The merged tree of a diff numbers its rows as the single view does up to the first row only the base has, so the
  // tree tells two selections with the same id apart.
  let tree: DiffModel | ViewModel | null = null;
  let selectedNodeId: number | null = null;
  store.subscribe((state) => {
    const selectedTree = state.diff?.model ?? state.database?.model ?? null;
    if (state.selectedNodeId === selectedNodeId && selectedTree === tree) {
      return;
    }
    tree = selectedTree;
    selectedNodeId = state.selectedNodeId;

    const model = state.database?.model;
    if (model === undefined || state.diff !== null || selectedNodeId === null) {
      return;
    }

    const index = model.nodes[selectedNodeId].definitionIndex;
    const hash = index === undefined ? '' : `${FORM_PREFIX}${encodeURIComponent(model.definitions[index].formId)}`;
    if (location.hash !== hash) {
      // A replace, so the back button leaves the viewer instead of stepping through every row clicked.
      history.replaceState(history.state, '', hash === '' ? location.pathname + location.search : hash);
    }
  });
}

function hashFormId(hash: string): string | null {
  if (!hash.startsWith(FORM_PREFIX)) {
    return null;
  }

  try {
    return decodeURIComponent(hash.slice(FORM_PREFIX.length));
  } catch {
    // A malformed escape names no form.
    return null;
  }
}
