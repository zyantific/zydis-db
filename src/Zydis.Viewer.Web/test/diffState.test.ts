import { describe, expect, it } from 'vitest';

import { changedNodeIdsInOrder, changesInclude, definitionFor, type DiffSession } from '../src/diff/diffState';
import type { DiffModel, DiffNode, DiffPair, DiffStatus, NodeKind, ViewDefinition } from '../src/model';
import type { LoadedDatabase } from '../src/store';

interface Indices {
  base?: number;
  head?: number;
}

function node(id: number, parentId: number | null, kind: NodeKind, status: DiffStatus, childIds: number[],
  indices: Indices = {}): DiffNode {
  return {
    id, parentId, kind, label: String(id), childIds, conflict: false, formCount: 0, status,
    counts: { added: 0, removed: 0, changed: 0, moved: 0 },
    definitionIndex: indices.head ?? indices.base, baseDefinitionIndex: indices.base, headDefinitionIndex: indices.head,
  };
}

function diffModel(nodes: DiffNode[], pairs: DiffPair[] = []): DiffModel {
  return { stats: { added: 0, removed: 0, changed: 0, moved: 0, unchanged: 0 }, rootIds: [0], nodes, pairs };
}

function definition(index: number, formId: string): ViewDefinition {
  return { index, formId, mnemonic: formId, operandSummary: '', visibleOperandCount: 0, path: '', raw: {} };
}

function database(name: string, definitions: ViewDefinition[]): LoadedDatabase {
  return {
    name,
    handle: 0,
    model: {
      schema: 'array', stats: { definitions: 0, nodes: 0, conflicts: 0 }, rootIds: [], nodes: [], definitions,
      filterSlots: {},
    },
  };
}

// 0 PRIMARY
//   1 opcode 37
//     2 decision
//       3 changed
//     4 unchanged
//     5 added
//   6 opcode 40, only in the base
//     7 removed
//     8 moved, the old row
//   9 opcode 41
//     10 unchanged
//     11 moved
//   12 opcode 42
//     13 unchanged
const tree = diffModel([
  node(0, null, 'table', 'unchanged', [1, 6, 9, 12]),
  node(1, 0, 'opcode', 'unchanged', [2, 4, 5]),
  node(2, 1, 'decision', 'unchanged', [3]),
  node(3, 2, 'definition', 'changed', [], { base: 0, head: 0 }),
  node(4, 1, 'definition', 'unchanged', [], { base: 1, head: 1 }),
  node(5, 1, 'definition', 'added', [], { head: 2 }),
  node(6, 0, 'opcode', 'removed', [7, 8]),
  node(7, 6, 'definition', 'removed', [], { base: 2 }),
  node(8, 6, 'definition', 'moved', [], { base: 3 }),
  node(9, 0, 'opcode', 'unchanged', [10, 11]),
  node(10, 9, 'definition', 'unchanged', [], { base: 4, head: 3 }),
  node(11, 9, 'definition', 'moved', [], { base: 3, head: 4 }),
  node(12, 0, 'opcode', 'unchanged', [13]),
  node(13, 12, 'definition', 'unchanged', [], { base: 5, head: 5 }),
]);

describe('diff state', () => {
  it('changedNodeIdsInOrder follows preorder and skips unchanged', () => {
    // A level-order walk gives 5, 7, 11, 3, a stack walk that pushes the children in order gives 11, 7, 5, 3, a
    // status filter that ignores the kind adds the opcode row 6, and a step onto the old row of the moved form adds 8.
    expect(changedNodeIdsInOrder(tree)).toEqual([3, 5, 7, 11]);
  });

  it('changesInclude keeps ancestors of changed rows', () => {
    // The old row 8 of the moved form stays, so the inspector link to it selects a row on screen.
    const include = changesInclude(tree);
    expect([0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13].filter(include)).toEqual([0, 1, 2, 3, 5, 6, 7, 8, 9, 11]);
  });

  it('definitionFor resolves base and head through the pair', () => {
    // The pair at a base index is a different pair here, so a lookup of pairs[baseDefinitionIndex] finds the added
    // form for the base row of the moved form.
    const pairs: DiffPair[] = [
      { baseIndex: 1, headIndex: 0, status: 'moved', basePath: 'old', headPath: 'new', changedPaths: [] },
      { headIndex: 1, status: 'added', headPath: 'other', changedPaths: [] },
      { baseIndex: 0, status: 'removed', basePath: 'gone', changedPaths: [] },
    ];
    const model = diffModel([
      node(0, null, 'table', 'unchanged', [1, 2, 3, 4]),
      node(1, 0, 'definition', 'moved', [], { base: 1, head: 0 }),
      node(2, 0, 'definition', 'added', [], { head: 1 }),
      node(3, 0, 'definition', 'moved', [], { base: 1 }),
      node(4, 0, 'definition', 'removed', [], { base: 0 }),
    ], pairs);
    const base = database('base', [definition(0, 'BASE0'), definition(1, 'BASE1')]);
    const head = database('head', [definition(0, 'HEAD0'), definition(1, 'HEAD1')]);
    const session: DiffSession = { base, model, changesOnly: false, cursor: -1 };

    const moved = { base: base.model.definitions[1], head: head.model.definitions[0], pair: pairs[0] };
    expect(definitionFor(session, head, 3)).toEqual(moved);
    expect(definitionFor(session, head, 1)).toEqual(moved);
    expect(definitionFor(session, head, 2)).toEqual({ base: null, head: head.model.definitions[1], pair: pairs[1] });
    expect(definitionFor(session, head, 4)).toEqual({ base: base.model.definitions[0], head: null, pair: pairs[2] });
  });
});
