import { COUNT_SIGNS, isMovedFrom } from '../diff/diffState';
import type { DiffNode, NodeKind, ViewDefinition, ViewNode } from '../model';
import { store, type AppState } from '../store';
import { rowFilter, sourceOf } from './gridSource';
import { ancestorsOf, computeVisibleRows, innerNodeIds, type Row } from './rows';

const ROW_HEIGHT = 22;
const OVERSCAN = 10;
const INDENT = 16;
const MIN_COLUMN_WIDTH = 40;
const COLUMNS_KEY = 'zydis-viewer.columns';
const COLUMN_TITLES = ['Item', 'Mnemonic', 'Operands', 'Comment'];

const KIND_ICONS: Record<NodeKind, string> = {
  conflict: '!',
  decision: '◇',
  definition: '•',
  opcode: '#',
  reference: '↪',
  slot: '›',
  switch: '⇄',
  table: '▤',
};

type GridNode = ViewNode & Partial<Pick<DiffNode, 'counts' | 'headDefinitionIndex' | 'status'>>;

// The rows of the single view or of a diff. The node ids of a tree index its nodes.
export interface GridSource {
  tree: { rootIds: number[]; nodes: GridNode[] };
  definitionOf(nodeId: number): ViewDefinition | undefined;
}

// Returns the viewport, the element that holds the keyboard focus of the grid.
export function mountGrid(container: HTMLElement): HTMLElement {
  const headerRow = element('div', 'grid-header-row');
  COLUMN_TITLES.forEach((title, column) => {
    const cell = element('div', 'grid-cell', title);
    const resizer = element('span', 'column-resizer');
    resizer.addEventListener('pointerdown', (event) => startResize(event, resizer, column));
    cell.append(resizer);
    headerRow.append(cell);
  });
  const header = element('div', 'grid-header');
  header.append(headerRow);

  const canvas = element('div', 'grid-canvas');
  const viewport = element('div', 'grid-viewport');
  viewport.tabIndex = 0;
  viewport.append(canvas);

  const grid = element('div', 'grid');
  // The offsets below are multiples of ROW_HEIGHT, so the stylesheet takes the row height from here.
  grid.style.setProperty('--row-height', `${ROW_HEIGHT}px`);
  grid.append(header, viewport);
  container.append(grid);

  const stored = readColumnWidths();
  if (stored !== null) {
    store.set({ columnWidths: stored });
  }

  let source: GridSource | null = null;
  let expanded: ReadonlySet<number> | null = null;
  // The kept rows of the filters. They show open whatever expanded holds, and a collapse would hide the path to a
  // kept row, so Enter, the glyph, Left, Right and * leave the expansion alone until the filters clear.
  let include: ((nodeId: number) => boolean) | null = null;
  // Shown when changes only keeps no row, so the user can tell an empty result from a grid that failed to load.
  let hint: string | null = null;
  let selectedNodeId: number | null = null;
  let widths: number[] | null = null;
  let rows: Row[] = [];
  let renderedFirst = -1;
  let renderedLast = -1;

  const isOpen = (index: number): boolean => index + 1 < rows.length && rows[index + 1].depth > rows[index].depth;

  const renderRow = (rowSource: GridSource, index: number): HTMLElement => {
    const row = rows[index];
    const node = rowSource.tree.nodes[row.nodeId];
    const rowElement = element('div', 'grid-row');
    rowElement.dataset.index = String(index);
    rowElement.style.top = `${index * ROW_HEIGHT}px`;
    rowElement.classList.toggle('conflict', node.conflict);
    rowElement.classList.toggle('selected', node.id === selectedNodeId);
    if (node.status !== undefined && node.status !== 'unchanged') {
      rowElement.classList.add(`status-${node.status}`);
      rowElement.classList.toggle('moved-from', isMovedFrom(node));
    }

    const expander = element('span', 'expander', row.hasChildren ? (isOpen(index) ? '▾' : '▸') : '');
    expander.style.marginLeft = `${row.depth * INDENT}px`;
    const item = element('div', 'grid-cell');
    item.append(expander, element('span', `icon icon-${node.kind}`, KIND_ICONS[node.kind]),
      element('span', 'label', node.label));
    const detail = detailOf(node);
    if (detail !== undefined) {
      item.append(element('span', 'detail', detail));
    }
    if (node.counts !== undefined && node.kind !== 'definition') {
      item.append(...countBadges(node.counts));
    }

    const definition = rowSource.definitionOf(node.id);
    rowElement.append(
      item,
      element('div', 'grid-cell', definition?.mnemonic),
      element('div', 'grid-cell', definition?.operandSummary),
      element('div', 'grid-cell', definition?.commentText?.split(/\r?\n/, 1)[0]),
    );

    return rowElement;
  };

  // A scroll rebuilds only when the rows in view leave the rendered range, so the overscan absorbs small scrolls. A
  // change of the rows or the selection always rebuilds.
  const renderRows = (force: boolean): void => {
    const top = viewport.scrollTop;
    const visibleFirst = Math.floor(top / ROW_HEIGHT);
    const visibleLast = Math.min(rows.length, Math.ceil((top + viewport.clientHeight) / ROW_HEIGHT));
    if (!force && visibleFirst >= renderedFirst && visibleLast <= renderedLast) {
      return;
    }
    renderedFirst = Math.max(0, visibleFirst - OVERSCAN);
    renderedLast = Math.min(rows.length, visibleLast + OVERSCAN);

    const elements: HTMLElement[] = [];
    if (source !== null) {
      for (let index = renderedFirst; index < renderedLast; index++) {
        elements.push(renderRow(source, index));
      }
    }
    if (hint !== null) {
      elements.push(element('div', 'grid-hint', hint));
    }
    canvas.replaceChildren(...elements);
  };

  const scrollSelectionIntoView = (): void => {
    const index = rows.findIndex((row) => row.nodeId === selectedNodeId);
    if (index < 0) {
      return;
    }

    const top = index * ROW_HEIGHT;
    if (top < viewport.scrollTop) {
      viewport.scrollTop = top;
    } else if (top + ROW_HEIGHT > viewport.scrollTop + viewport.clientHeight) {
      viewport.scrollTop = top + ROW_HEIGHT - viewport.clientHeight;
    }
  };

  const update = (state: AppState): void => {
    const nextSource = sourceOf(state);
    const nextInclude = rowFilter(state);
    grid.hidden = nextSource === null;

    let changed = false;
    const filtersCleared = include !== null && nextInclude === null;
    if (nextSource !== source || state.expanded !== expanded || nextInclude !== include) {
      // A filter turned on after a deep scroll would otherwise open at the clamped end of the shorter row list.
      if (nextSource?.tree !== source?.tree || (include === null && nextInclude !== null)) {
        viewport.scrollTop = 0;
      }
      source = nextSource;
      expanded = state.expanded;
      include = nextInclude;
      rows = source === null ? [] : computeVisibleRows(source.tree, expanded, include ?? undefined);
      canvas.style.height = `${rows.length * ROW_HEIGHT}px`;
      hint = rows.length > 0 || state.diff?.changesOnly !== true
        ? null
        : state.searchInclude === null ? 'No changed forms' : 'No changed form matches the search';
      changed = true;
    }
    if (state.columnWidths !== widths) {
      widths = state.columnWidths;
      widths.forEach((width, column) => grid.style.setProperty(`--column-${column}`, `${width}px`));
      grid.style.setProperty('--grid-width', `${widths.reduce((sum, width) => sum + width, 0)}px`);
    }
    // Clearing the filters moves the selected row to a new index, so it scrolls into view as a new selection does.
    if (state.selectedNodeId !== selectedNodeId || filtersCleared) {
      selectedNodeId = state.selectedNodeId;
      // Runs after the row list update, so a selection that arrives with new rows finds its index.
      scrollSelectionIntoView();
      changed = true;
    }
    if (changed) {
      renderRows(true);
    }
  };

  const select = (index: number): void => {
    if (rows.length > 0) {
      store.set({ selectedNodeId: rows[Math.min(Math.max(index, 0), rows.length - 1)].nodeId });
    }
  };

  const toggle = (index: number): void => {
    if (include === null && rows[index].hasChildren) {
      setOpen(rows[index].nodeId, !isOpen(index));
    }
  };

  const parentIndex = (index: number): number => {
    let parent = index - 1;
    while (parent >= 0 && rows[parent].depth >= rows[index].depth) {
      parent--;
    }
    return parent;
  };

  canvas.addEventListener('click', (event) => {
    const { target } = event;
    if (!(target instanceof Element)) {
      return;
    }
    const rowElement = target.closest<HTMLElement>('.grid-row');
    if (rowElement === null) {
      return;
    }

    const index = Number(rowElement.dataset.index);
    viewport.focus({ preventScroll: true });
    if (target.classList.contains('expander') && rows[index].hasChildren) {
      toggle(index);
    } else {
      select(index);
    }
  });

  viewport.addEventListener('keydown', (event) => {
    // Leaves Ctrl+F and the other browser and application shortcuts to the handlers above the grid.
    if (event.altKey || event.ctrlKey || event.metaKey) {
      return;
    }

    const index = rows.findIndex((row) => row.nodeId === selectedNodeId);
    const page = Math.max(1, Math.floor(viewport.clientHeight / ROW_HEIGHT));
    switch (event.key) {
      case 'ArrowDown':
        select(index + 1);
        break;
      case 'ArrowUp':
        select(index - 1);
        break;
      case 'PageDown':
        select(index + page);
        break;
      case 'PageUp':
        select(index - page);
        break;
      case 'Home':
        select(0);
        break;
      case 'End':
        select(rows.length - 1);
        break;
      case 'ArrowLeft':
        if (index >= 0 && isOpen(index) && include === null) {
          setOpen(rows[index].nodeId, false);
        } else if (index >= 0 && parentIndex(index) >= 0) {
          select(parentIndex(index));
        }
        break;
      case 'ArrowRight':
        if (index >= 0 && isOpen(index)) {
          select(index + 1);
        } else if (index >= 0 && rows[index].hasChildren && include === null) {
          setOpen(rows[index].nodeId, true);
        }
        break;
      case 'Enter':
        if (index >= 0) {
          toggle(index);
        }
        break;
      case '*':
        if (include === null) {
          expandSubtree();
        }
        break;
      default:
        return;
    }
    // Keeps the viewport from scrolling on its own, since the selection scrolls it.
    event.preventDefault();
  });

  viewport.addEventListener('scroll', () => {
    headerRow.style.transform = `translateX(${-viewport.scrollLeft}px)`;
    renderRows(false);
  }, { passive: true });
  new ResizeObserver(() => renderRows(false)).observe(viewport);

  store.subscribe(update);
  update(store.state);
  return viewport;
}

// Selects the first definition row of the grid while no row is selected, so Enter in the search box lands on the first
// match.
export function selectFirstDefinition(): void {
  const { expanded, selectedNodeId } = store.state;
  const source = sourceOf(store.state);
  if (source === null || selectedNodeId !== null) {
    return;
  }

  const { nodes } = source.tree;
  const first = computeVisibleRows(source.tree, expanded, rowFilter(store.state) ?? undefined)
    .find((row) => nodes[row.nodeId].kind === 'definition');
  if (first !== undefined) {
    store.set({ selectedNodeId: first.nodeId });
  }
}

export function expandAll(): void {
  const tree = sourceOf(store.state)?.tree;
  if (tree !== undefined) {
    store.set({ expanded: new Set(innerNodeIds(tree, tree.rootIds)) });
  }
}

export function collapseAll(): void {
  const next = new Set<number>();
  store.set({ expanded: next, selectedNodeId: selectionAfterCollapse(next) });
}

export function expandSubtree(): void {
  setSubtreeOpen(true);
}

export function collapseSubtree(): void {
  setSubtreeOpen(false);
}

function setSubtreeOpen(open: boolean): void {
  const { expanded, selectedNodeId } = store.state;
  const tree = sourceOf(store.state)?.tree;
  if (tree === undefined || selectedNodeId === null) {
    return;
  }

  const next = new Set(expanded);
  for (const nodeId of innerNodeIds(tree, [selectedNodeId])) {
    if (open) {
      next.add(nodeId);
    } else {
      next.delete(nodeId);
    }
  }
  store.set({ expanded: next });
}

function setOpen(nodeId: number, open: boolean): void {
  const next = new Set(store.state.expanded);
  if (open) {
    next.add(nodeId);
    store.set({ expanded: next });
  } else {
    next.delete(nodeId);
    store.set({ expanded: next, selectedNodeId: selectionAfterCollapse(next) });
  }
}

// A collapse that hides the selected row moves the selection to its deepest ancestor still on screen, so the keyboard
// and the subtree actions keep a visible row to act on.
function selectionAfterCollapse(next: ReadonlySet<number>): number | null {
  const { selectedNodeId } = store.state;
  const tree = sourceOf(store.state)?.tree;
  if (tree === undefined || selectedNodeId === null) {
    return selectedNodeId;
  }

  // The roots always show and an open node shows its children, so the first closed ancestor, root first, is the
  // deepest one left in the row list.
  return ancestorsOf(tree, selectedNodeId).find((ancestorId) => !next.has(ancestorId)) ?? selectedNodeId;
}

// The operand summary, the detail of a definition row, has its own column. On the rows that count forms the count
// takes the place of the detail.
function detailOf(node: ViewNode): string | undefined {
  switch (node.kind) {
    case 'conflict':
    case 'decision':
    case 'opcode':
      return String(node.formCount);
    case 'definition':
      return undefined;
    default:
      return node.detail;
  }
}

function countBadges(counts: NonNullable<GridNode['counts']>): HTMLElement[] {
  return COUNT_SIGNS
    .filter(([status]) => counts[status] > 0)
    .map(([status, sign]) => element('span', `badge status-${status}`, `${sign}${counts[status]}`));
}

function startResize(event: PointerEvent, handle: HTMLElement, column: number): void {
  if (event.button !== 0) {
    return;
  }
  // Keeps the drag from selecting the header text.
  event.preventDefault();
  handle.setPointerCapture(event.pointerId);

  const startX = event.clientX;
  const startWidth = store.state.columnWidths[column];
  const move = (moveEvent: PointerEvent): void => {
    const widths = [...store.state.columnWidths];
    widths[column] = Math.max(MIN_COLUMN_WIDTH, Math.round(startWidth + moveEvent.clientX - startX));
    store.set({ columnWidths: widths });
  };

  handle.addEventListener('pointermove', move);
  handle.addEventListener('lostpointercapture', () => {
    handle.removeEventListener('pointermove', move);
    saveColumnWidths(store.state.columnWidths);
  }, { once: true });
}

function readColumnWidths(): number[] | null {
  try {
    const value: unknown = JSON.parse(localStorage.getItem(COLUMNS_KEY) ?? 'null');
    const valid = Array.isArray(value) && value.length === COLUMN_TITLES.length
      && value.every((width) => typeof width === 'number' && Number.isFinite(width) && width > 0);
    return valid ? (value as number[]) : null;
  } catch {
    // Blocked storage or stored text that is not JSON falls back to the default widths.
    return null;
  }
}

function saveColumnWidths(widths: number[]): void {
  try {
    localStorage.setItem(COLUMNS_KEY, JSON.stringify(widths));
  } catch {
    // The widths are a convenience, so blocked or full storage only costs the next visit its layout.
  }
}

function element<K extends keyof HTMLElementTagNameMap>(
  tag: K,
  className: string,
  text?: string,
): HTMLElementTagNameMap[K] {
  const created = document.createElement(tag);
  created.className = className;
  if (text !== undefined) {
    created.textContent = text;
  }
  return created;
}
