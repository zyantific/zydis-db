import { changedMatchesInclude, changesInclude, definitionFor } from '../diff/diffState';
import type { DiffModel } from '../model';
import type { AppState, LoadedDatabase } from '../store';
import type { GridSource } from './grid';

// Both functions return the object of the last call while their inputs stay, so the grid tells a change by identity
// and rebuilds neither the source nor the filter on an expansion or a column resize.
let sourceKey: readonly [LoadedDatabase | null, DiffModel | null] = [null, null];
let source: GridSource | null = null;
let filterKey: readonly [ReadonlySet<number> | null, DiffModel | null] = [null, null];
let filter: ((nodeId: number) => boolean) | null = null;

// Returns the merged tree during a diff and the view model of the loaded database otherwise. A diff row shows the
// head form at a head path and the base form at a base path.
export function sourceOf(state: AppState): GridSource | null {
  const { database, diff } = state;
  const diffModel = diff?.model ?? null;
  if (database === sourceKey[0] && diffModel === sourceKey[1]) {
    return source;
  }

  sourceKey = [database, diffModel];
  if (database === null) {
    source = null;
  } else if (diff !== null) {
    source = {
      tree: diff.model,
      definitionOf: (nodeId) => {
        const { base, head } = definitionFor(diff, database, nodeId);
        return (diff.model.nodes[nodeId].headDefinitionIndex === undefined ? base : head) ?? undefined;
      },
    };
  } else {
    const { model } = database;
    source = {
      tree: model,
      definitionOf: (nodeId) => {
        const index = model.nodes[nodeId].definitionIndex;
        return index === undefined ? undefined : model.definitions[index];
      },
    };
  }
  return source;
}

// Returns the kept rows of an active search, of changes only, or of both, and null while neither is active.
export function rowFilter(state: AppState): ((nodeId: number) => boolean) | null {
  const { diff, searchInclude } = state;
  const changesOf = diff?.changesOnly === true ? diff.model : null;
  if (searchInclude === filterKey[0] && changesOf === filterKey[1]) {
    return filter;
  }

  filterKey = [searchInclude, changesOf];
  if (changesOf !== null) {
    filter = searchInclude === null ? changesInclude(changesOf) : changedMatchesInclude(changesOf, searchInclude);
  } else {
    filter = searchInclude === null ? null : (nodeId) => searchInclude.has(nodeId);
  }
  return filter;
}
