import {
  ArrowLeftRight, Braces, Check, ChevronLeft, ChevronRight, ChevronsDownUp, ChevronsUpDown, Copy, CornerDownRight,
  CornerUpRight, Cpu, Diamond, Diff, Ellipsis, Flag, FoldVertical, FolderOpen, Funnel, GitBranch, GitCompareArrows,
  Grid2x2, Hash, Info, List, MessageSquare, Monitor, Moon, MoveRight, Search, SlidersHorizontal, StickyNote, Sun,
  Table2, Tag, TriangleAlert, UnfoldVertical, X, createElement, type IconNode,
} from 'lucide';

import type { NodeKind } from './model';

// A filled disc reads as a leaf at 14 px, where the stroked circle of the icon set turns into a thin ring.
const DISC: IconNode = [['circle', { cx: 12, cy: 12, r: 4.5, fill: 'currentColor', stroke: 'none' }]];

export const KIND_ICONS: Record<NodeKind, IconNode> = {
  conflict: TriangleAlert,
  decision: Diamond,
  definition: DISC,
  opcode: Hash,
  reference: CornerUpRight,
  slot: CornerDownRight,
  switch: ArrowLeftRight,
  table: Table2,
};

export const ACTION_ICONS = {
  check: Check,
  close: X,
  collapseAll: ChevronsDownUp,
  collapseSubtree: FoldVertical,
  compare: GitCompareArrows,
  copy: Copy,
  expandAll: ChevronsUpDown,
  expandSubtree: UnfoldVertical,
  expander: ChevronRight,
  next: ChevronRight,
  open: FolderOpen,
  previous: ChevronLeft,
  search: Search,
  themeDark: Moon,
  themeLight: Sun,
  themeSystem: Monitor,
} satisfies Record<string, IconNode>;

export type ActionIcon = keyof typeof ACTION_ICONS;

// The inspector sections by title, shared by the single view and the diff panel.
export const SECTION_ICONS: Record<string, IconNode> = {
  'Affected flags': Flag,
  Annotations: StickyNote,
  Changes: Diff,
  Comment: MessageSquare,
  Definition: Tag,
  'Encoding info': Cpu,
  Filters: Funnel,
  Flags: SlidersHorizontal,
  'Meta info': Info,
  Move: MoveRight,
  Node: GitBranch,
  Operands: List,
  Other: Ellipsis,
  Raw: Braces,
  Slots: Grid2x2,
};

// Icons are decoration next to a label, so they stay out of the accessibility tree.
export function icon(node: IconNode, className = ''): SVGElement {
  const svg = createElement(node, { 'aria-hidden': 'true', 'stroke-width': 1.75 });
  svg.classList.add('icon-svg');
  if (className !== '') {
    svg.classList.add(className);
  }
  return svg;
}

export function actionIcon(name: ActionIcon, className = ''): SVGElement {
  return icon(ACTION_ICONS[name], className);
}

// The glyph of a node kind in its kind color, as the tree rows and the inspector header show it.
export function kindGlyph(kind: NodeKind): HTMLElement {
  const span = document.createElement('span');
  span.className = `icon icon-${kind}`;
  span.append(icon(KIND_ICONS[kind]));
  return span;
}
