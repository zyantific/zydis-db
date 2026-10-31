import { describe, expect, it } from 'vitest';

import type { NodeKind, ViewDefinition, ViewModel, ViewNode } from '../src/model';
import { createMatcher, matchingNodeIds, type MatchMode } from '../src/search/matcher';
import { searchUpdate } from '../src/search/searchState';
import type { AppState } from '../src/store';

function node(id: number, parentId: number | null, kind: NodeKind, childIds: number[], definitionIndex?: number):
  ViewNode {
  return { id, parentId, kind, label: String(id), childIds, definitionIndex, conflict: false, formCount: 0 };
}

function definition(index: number, mnemonic: string): ViewDefinition {
  return { index, formId: mnemonic, mnemonic, operandSummary: '', visibleOperandCount: 0, path: '', raw: {} };
}

// 0 PRIMARY
//   1 opcode 37
//     2 aaa
//   3 opcode 42
//     4 decision
//       5 cfcmovb
//       6 cmovb
//   7 opcode 46
//     8 cfcmovbe
const model: ViewModel = {
  schema: 'array',
  stats: { definitions: 4, nodes: 9, conflicts: 0 },
  rootIds: [0],
  nodes: [
    node(0, null, 'table', [1, 3, 7]),
    node(1, 0, 'opcode', [2]),
    node(2, 1, 'definition', [], 0),
    node(3, 0, 'opcode', [4]),
    node(4, 3, 'decision', [5, 6]),
    node(5, 4, 'definition', [], 1),
    node(6, 4, 'definition', [], 2),
    node(7, 0, 'opcode', [8]),
    node(8, 7, 'definition', [], 3),
  ],
  definitions: [definition(0, 'aaa'), definition(1, 'cfcmovb'), definition(2, 'cmovb'), definition(3, 'cfcmovbe')],
  filterSlots: {},
};

type SearchFields = Pick<AppState, 'expanded' | 'search' | 'searchError' | 'searchInclude' | 'selectedNodeId'>;

function start(expanded: number[]): SearchFields {
  return {
    expanded: new Set(expanded),
    search: { text: '', mode: 'contains', scope: 'mnemonic' },
    searchError: null,
    searchInclude: null,
    selectedNodeId: null,
  };
}

// Merges the update the way store.set does.
function type(state: SearchFields, text: string, mode: MatchMode = 'contains'): SearchFields {
  return { ...state, ...searchUpdate(state, model, { text, mode, scope: 'mnemonic' }) };
}

describe('search rows', () => {
  it('keeps ancestors of a match and nothing else', () => {
    const contains = createMatcher('contains', 'cfcmovb');
    if ('error' in contains) {
      throw new Error(contains.error);
    }
    expect(matchingNodeIds(model, 'mnemonic', contains)).toEqual(new Set([0, 3, 4, 5, 7, 8]));
  });

  it('invalid regular expression keeps the last result', () => {
    const found = type(start([0]), 'cfcmovb$', 'regex');
    expect(found.searchInclude).toEqual(new Set([0, 3, 4, 5]));

    const broken = type(found, 'cfcmovb$(', 'regex');
    expect(broken.searchInclude).toBe(found.searchInclude);
    expect(broken.searchError).toMatch(/^Invalid regular expression/);
  });

  it('clearing leaves the expansion as it was and adds the ancestors of the selection', () => {
    const searching = type(start([0, 1]), 'cfcmovb');
    const picked = { ...searching, selectedNodeId: 5 };

    const cleared = type(picked, '');
    expect(cleared.expanded).toEqual(new Set([0, 1, 3, 4]));
  });

  it('clearing keeps the selected result visible when its branch was collapsed before the search', () => {
    const searching = type(start([0]), 'cfcmovb');
    const picked = { ...searching, selectedNodeId: 5 };

    const cleared = type(picked, '');
    expect(cleared.selectedNodeId).toBe(5);
    expect(cleared.expanded).toEqual(new Set([0, 3, 4]));
  });
});
