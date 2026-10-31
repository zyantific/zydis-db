import { readSourceOrNotFound, type LoadSource } from '../loader';
import type { DiffCounts, DiffModel, DiffNode, DiffPair, ViewDefinition, ViewModel } from '../model';
import { setSearch } from '../search/searchState';
import { store, type AppState, type LoadedDatabase } from '../store';
import { ancestorsOf } from '../tree/rows';

export interface DiffSession {
  base: LoadedDatabase;
  model: DiffModel;
  changesOnly: boolean;
  // An index into changedNodeIdsInOrder, -1 before the first step.
  cursor: number;
}

// The badge of an inner row and the status bar write the counts in this order. U+2212 is the minus sign, as wide as
// the plus sign.
export const COUNT_SIGNS: ReadonlyArray<readonly [keyof DiffCounts, string]> = [
  ['added', '+'],
  ['removed', '\u2212'],
  ['changed', '~'],
  ['moved', '>'],
];

interface DiffIndex {
  // The definition rows with a status other than unchanged, in preorder.
  changedRows: number[];
  // The same without the old rows of moved forms, so a moved form is one step.
  changedNodeIds: number[];
  // The pairs list a removed form after the head forms, so its position in pairs differs from its base index.
  pairOfBase: Map<number, DiffPair>;
  headRowOf: Map<number, number>;
  // The rows at a base path: removed forms and the old rows of moved forms.
  baseRowOf: Map<number, number>;
}

const indexes = new WeakMap<DiffModel, DiffIndex>();

function indexOf(model: DiffModel): DiffIndex {
  const cached = indexes.get(model);
  if (cached !== undefined) {
    return cached;
  }

  const changedRows: number[] = [];
  const pending = [...model.rootIds].reverse();
  for (let nodeId = pending.pop(); nodeId !== undefined; nodeId = pending.pop()) {
    const node = model.nodes[nodeId];
    if (node.definitionIndex !== undefined && node.status !== 'unchanged') {
      changedRows.push(nodeId);
    }
    // Pushed in reverse, so the first child is the next one popped.
    for (let child = node.childIds.length - 1; child >= 0; child--) {
      pending.push(node.childIds[child]);
    }
  }

  const pairOfBase = new Map<number, DiffPair>();
  for (const pair of model.pairs) {
    if (pair.baseIndex !== undefined) {
      pairOfBase.set(pair.baseIndex, pair);
    }
  }

  const headRowOf = new Map<number, number>();
  const baseRowOf = new Map<number, number>();
  for (const node of model.nodes) {
    if (node.headDefinitionIndex !== undefined) {
      headRowOf.set(node.headDefinitionIndex, node.id);
    } else if (node.baseDefinitionIndex !== undefined) {
      baseRowOf.set(node.baseDefinitionIndex, node.id);
    }
  }

  const changedNodeIds = changedRows.filter((nodeId) => !isMovedFrom(model.nodes[nodeId]));
  const index = { changedRows, changedNodeIds, pairOfBase, headRowOf, baseRowOf };
  indexes.set(model, index);
  return index;
}

// The row a moved form leaves at its base path has the status of the pair but no head form.
export function isMovedFrom(node: Partial<Pick<DiffNode, 'headDefinitionIndex' | 'status'>>): boolean {
  return node.status === 'moved' && node.headDefinitionIndex === undefined;
}

// The callers of one model share the returned array.
export function changedNodeIdsInOrder(model: DiffModel): number[] {
  return indexOf(model).changedNodeIds;
}

// Keeps the old rows of moved forms as well, which the steps skip, so the link from the inspector to the old row
// selects a row the grid shows.
export function changesInclude(model: DiffModel): (nodeId: number) => boolean {
  return includeWithAncestors(model, indexOf(model).changedRows);
}

// The search keeps its matches and their ancestors, so a changed row it keeps is a match. The intersection of the two
// sets would also keep an ancestor that leads to a match and to a change but to no changed match, as an inner row
// without children.
export function changedMatchesInclude(model: DiffModel, searchInclude: ReadonlySet<number>):
  (nodeId: number) => boolean {
  return includeWithAncestors(model, indexOf(model).changedRows.filter((nodeId) => searchInclude.has(nodeId)));
}

function includeWithAncestors(model: DiffModel, nodeIds: readonly number[]): (nodeId: number) => boolean {
  const kept = new Set<number>();
  for (const nodeId of nodeIds) {
    // Stops at the first ancestor already kept, since the row that kept it also kept the rest of its chain.
    for (let id: number | null = nodeId; id !== null && !kept.has(id); id = model.nodes[id].parentId) {
      kept.add(id);
    }
  }
  return (nodeId) => kept.has(nodeId);
}

export function definitionFor(session: DiffSession, head: LoadedDatabase, nodeId: number):
  { base: ViewDefinition | null; head: ViewDefinition | null; pair: DiffPair | null } {
  const { model } = session;
  const node = model.nodes[nodeId];
  const pair = node.headDefinitionIndex !== undefined
    ? model.pairs[node.headDefinitionIndex]
    : node.baseDefinitionIndex !== undefined ? indexOf(model).pairOfBase.get(node.baseDefinitionIndex) : undefined;
  if (pair === undefined) {
    return { base: null, head: null, pair: null };
  }

  return {
    base: pair.baseIndex === undefined ? null : session.base.model.definitions[pair.baseIndex],
    head: pair.headIndex === undefined ? null : head.model.definitions[pair.headIndex],
    pair,
  };
}

// Returns the other row of a moved form: the row at its base path for the row at its head path, and the reverse.
export function counterpartOf(model: DiffModel, nodeId: number): number | null {
  const node = model.nodes[nodeId];
  const { baseRowOf, headRowOf, pairOfBase } = indexOf(model);
  if (node.headDefinitionIndex !== undefined) {
    const pair = model.pairs[node.headDefinitionIndex];
    return pair.status === 'moved' && pair.baseIndex !== undefined ? baseRowOf.get(pair.baseIndex) ?? null : null;
  }
  if (node.baseDefinitionIndex !== undefined) {
    const headIndex = pairOfBase.get(node.baseDefinitionIndex)?.headIndex;
    return headIndex === undefined ? null : headRowOf.get(headIndex) ?? null;
  }
  return null;
}

// Loads the base from source and compares it with the loaded database, which stays the head.
export async function compareWith(source: LoadSource): Promise<void> {
  const { runtime } = store.state;
  if (runtime === null) {
    throw new Error('The .NET runtime has not started yet.');
  }

  const read = await readSourceOrNotFound(source);
  if ('notFound' in read) {
    // The local host answers 404 with the cause in the body when git fails, times out or does not start, so a missing
    // file is one cause among several.
    const cause = read.notFound.trim();
    throw new Error(cause !== ''
      ? cause
      : `No datafiles/instructions.json at ${source.kind === 'url' ? source.name : source.file.name}.`);
  }
  // Read after the wait, since a file opened meanwhile replaces the head.
  const head = store.state.database;
  if (head === null) {
    throw new Error('No database is loaded to compare with.');
  }

  const handle = runtime.loadDatabase(read.json);
  let baseModel: ViewModel;
  let model: DiffModel;
  try {
    baseModel = runtime.getViewModel(handle);
    model = runtime.diff(handle, head.handle);
  } catch (error) {
    runtime.release(handle);
    throw error;
  }

  const previous = store.state.diff;
  try {
    store.set({
      diff: { base: { name: read.name, handle, model: baseModel }, model, changesOnly: false, cursor: -1 },
      // The node ids of the merged tree name other rows than those of the single view, so the expansion, the selection
      // and the search result start over.
      expanded: new Set(model.rootIds),
      selectedNodeId: null,
      searchInclude: null,
    });
  } finally {
    // A subscriber that throws must not keep the old base alive in the runtime heap.
    if (previous !== null) {
      runtime.release(previous.base.handle);
    }
  }
  // The text in the search box stays, so the search runs again on the merged tree.
  setSearch(store.state.search);
}

export function closeDiff(): void {
  const { database, diff, runtime, selectedNodeId } = store.state;
  if (diff === null) {
    return;
  }

  const model = database?.model;
  const nodeId = model === undefined || selectedNodeId === null ? null : headFormRow(diff.model, model, selectedNodeId);
  // Opens the path to the selection the way revealHashForm does, so the form stays in view.
  const opened = model === undefined || nodeId === null ? [] : ancestorsOf(model, nodeId);
  try {
    store.set({
      diff: null,
      expanded: new Set([...(model?.rootIds ?? []), ...opened]),
      searchInclude: null,
      selectedNodeId: nodeId,
    });
  } finally {
    runtime?.release(diff.base.handle);
  }
  // The text in the search box stays, so the search runs again on the single view.
  setSearch(store.state.search);
  // The search of a diff also matches a row by its base form, which the search of the single view can leave out. Like
  // revealHashForm, the close selects no form out of sight.
  const { searchInclude } = store.state;
  if (nodeId !== null && searchInclude !== null && !searchInclude.has(nodeId)) {
    store.set({ selectedNodeId: null });
  }
}

// The node ids of the merged tree name other rows than those of the single view, so the selection carries over through
// the head form of the selected row. A row without one, such as a removed form or an inner row, carries nothing over.
function headFormRow(model: DiffModel, head: ViewModel, nodeId: number): number | null {
  const headIndex = model.nodes[nodeId].headDefinitionIndex;
  if (headIndex === undefined) {
    return null;
  }
  const row = head.nodes.findIndex((node) => node.definitionIndex === headIndex);
  return row < 0 ? null : row;
}

export function setChangesOnly(changesOnly: boolean): void {
  const { diff } = store.state;
  if (diff !== null) {
    store.set({ diff: { ...diff, changesOnly } });
  }
}

// Under an active search the step skips the changed rows the search hides, so it selects no row out of sight.
export function stepChange(direction: 1 | -1): void {
  const { diff, searchInclude } = store.state;
  if (diff === null) {
    return;
  }

  const changed = changedNodeIdsInOrder(diff.model);
  for (let cursor = diff.cursor + direction; cursor >= 0 && cursor < changed.length; cursor += direction) {
    if (searchInclude === null || searchInclude.has(changed[cursor])) {
      store.set({ ...reveal(store.state, diff.model, changed[cursor]), diff: { ...diff, cursor } });
      return;
    }
  }
}

export function selectDiffRow(nodeId: number): void {
  const { diff } = store.state;
  if (diff !== null) {
    store.set(reveal(store.state, diff.model, nodeId));
  }
}

// Opens the ancestors of the row as well, so it shows in a collapsed tree once changes only is off.
function reveal(state: AppState, model: DiffModel, nodeId: number): Partial<AppState> {
  return { expanded: new Set([...state.expanded, ...ancestorsOf(model, nodeId)]), selectedNodeId: nodeId };
}
