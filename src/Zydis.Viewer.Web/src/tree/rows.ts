export interface Row {
  nodeId: number;
  depth: number;
  hasChildren: boolean;
}

// ViewModel and DiffModel both satisfy NodeSource, so the diff view reuses the grid unchanged. The functions below use
// a node id as an index into nodes, so a source must number its nodes by position.
export interface NodeSource {
  rootIds: number[];
  nodes: Array<{ id: number; childIds: number[] }>;
}

// Under include, a kept inner node opens regardless of `expanded`, so a search that keeps its matches and their
// ancestors shows every path to a match.
export function computeVisibleRows(
  source: NodeSource,
  expanded: ReadonlySet<number>,
  include?: (nodeId: number) => boolean,
): Row[] {
  const rows: Row[] = [];

  const visit = (nodeId: number, depth: number): void => {
    if (include !== undefined && !include(nodeId)) {
      return;
    }

    const { childIds } = source.nodes[nodeId];
    rows.push({ nodeId, depth, hasChildren: childIds.length > 0 });
    if (include !== undefined || expanded.has(nodeId)) {
      for (const childId of childIds) {
        visit(childId, depth + 1);
      }
    }
  };

  for (const rootId of source.rootIds) {
    visit(rootId, 0);
  }

  return rows;
}

export function ancestorsOf(
  source: NodeSource & { nodes: Array<{ parentId: number | null }> },
  nodeId: number,
): number[] {
  const ancestors: number[] = [];
  for (let parentId = source.nodes[nodeId].parentId; parentId !== null; parentId = source.nodes[parentId].parentId) {
    ancestors.push(parentId);
  }

  return ancestors.reverse();
}

// Returns the nodes with children in the subtrees of startIds, the start nodes included, for the expand actions.
export function innerNodeIds(source: NodeSource, startIds: readonly number[]): number[] {
  const inner: number[] = [];
  const pending = [...startIds];
  for (let nodeId = pending.pop(); nodeId !== undefined; nodeId = pending.pop()) {
    const { childIds } = source.nodes[nodeId];
    if (childIds.length > 0) {
      inner.push(nodeId);
      pending.push(...childIds);
    }
  }

  return inner;
}
