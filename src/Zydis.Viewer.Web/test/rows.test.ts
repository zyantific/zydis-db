import { describe, expect, it } from 'vitest';

import { ancestorsOf, computeVisibleRows } from '../src/tree/rows';

// 0
//   1
//     2
//   3
// 4
//   5
//     6
const source = {
  rootIds: [0, 4],
  nodes: [
    { id: 0, parentId: null, childIds: [1, 3] },
    { id: 1, parentId: 0, childIds: [2] },
    { id: 2, parentId: 1, childIds: [] },
    { id: 3, parentId: 0, childIds: [] },
    { id: 4, parentId: null, childIds: [5] },
    { id: 5, parentId: 4, childIds: [6] },
    { id: 6, parentId: 5, childIds: [] },
  ],
};

describe('computeVisibleRows', () => {
  it('lists only roots when nothing is expanded', () => {
    expect(computeVisibleRows(source, new Set())).toEqual([
      { nodeId: 0, depth: 0, hasChildren: true },
      { nodeId: 4, depth: 0, hasChildren: true },
    ]);
  });

  it('lists children of expanded nodes in preorder with depth', () => {
    // Node 5 is expanded below the collapsed root 4, so its child stays hidden.
    expect(computeVisibleRows(source, new Set([0, 1, 5]))).toEqual([
      { nodeId: 0, depth: 0, hasChildren: true },
      { nodeId: 1, depth: 1, hasChildren: true },
      { nodeId: 2, depth: 2, hasChildren: false },
      { nodeId: 3, depth: 1, hasChildren: false },
      { nodeId: 4, depth: 0, hasChildren: true },
    ]);
  });

  it('forces expansion of kept inner nodes under an include predicate', () => {
    // Node 6 is kept below the excluded nodes 4 and 5, so it stays hidden with them.
    const kept = new Set([0, 1, 2, 6]);
    expect(computeVisibleRows(source, new Set(), (nodeId) => kept.has(nodeId))).toEqual([
      { nodeId: 0, depth: 0, hasChildren: true },
      { nodeId: 1, depth: 1, hasChildren: true },
      { nodeId: 2, depth: 2, hasChildren: false },
    ]);
  });
});

describe('ancestorsOf', () => {
  it('ancestorsOf returns the parent chain root first', () => {
    expect(ancestorsOf(source, 6)).toEqual([4, 5]);
  });
});
