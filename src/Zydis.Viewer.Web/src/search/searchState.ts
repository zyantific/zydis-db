import type { DiffSession } from '../diff/diffState';
import type { ViewModel } from '../model';
import { store, type AppState } from '../store';
import { ancestorsOf } from '../tree/rows';
import { createMatcher, diffMatchingNodeIds, matchingNodeIds } from './matcher';

export type Search = AppState['search'];

// Returns the store fields that change when the search settings become `search`. During a diff, model is the view
// model of the head and the node ids are those of the merged tree.
export function searchUpdate(
  state: Pick<AppState, 'expanded' | 'search' | 'selectedNodeId'>,
  model: ViewModel,
  search: Search,
  diff: Pick<DiffSession, 'base' | 'model'> | null = null,
): Partial<AppState> {
  if (search.text === '') {
    const cleared = { search, searchError: null, searchInclude: null };
    if (state.search.text === '' || state.selectedNodeId === null) {
      return cleared;
    }
    // The grid opens the kept rows of a search by itself, so expanded still holds the state from before the search.
    // Opening the ancestors of the selection keeps a result picked from a branch that was collapsed then visible.
    const tree = diff?.model ?? model;
    return { ...cleared, expanded: new Set([...state.expanded, ...ancestorsOf(tree, state.selectedNodeId)]) };
  }

  const matcher = createMatcher(search.mode, search.text);
  if ('error' in matcher) {
    // Leaves searchInclude out, so the grid keeps showing the last valid result while the user edits the pattern.
    return { search, searchError: matcher.error };
  }

  const searchInclude = diff === null
    ? matchingNodeIds(model, search.scope, matcher)
    : diffMatchingNodeIds(diff.model, diff.base.model, model, search.scope, matcher);
  return { search, searchError: null, searchInclude };
}

export function setSearch(search: Search): void {
  const { database, diff } = store.state;
  if (database !== null) {
    store.set(searchUpdate(store.state, database.model, search, diff));
  }
}
