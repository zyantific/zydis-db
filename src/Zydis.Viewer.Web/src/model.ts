// The shapes of the JSON documents Zydis.Viewer.Core writes. The property names must match its serializer output.

export type NodeKind = 'table' | 'opcode' | 'switch' | 'decision' | 'slot' | 'definition' | 'conflict' | 'reference';

export interface ViewModel {
  schema: 'array' | 'object' | 'none';
  stats: { definitions: number; nodes: number; conflicts: number };
  formIdError?: string;
  rootIds: number[];
  nodes: ViewNode[];
  definitions: ViewDefinition[];
  filterSlots: Record<string, string[]>;
}

export interface ViewNode {
  id: number;
  parentId: number | null;
  kind: NodeKind;
  label: string;
  detail?: string;
  filter?: string;
  childIds: number[];
  definitionIndex?: number;
  conflict: boolean;
  formCount: number;
}

export interface ViewDefinition {
  index: number;
  formId: string;
  mnemonic: string;
  operandSummary: string;
  commentText?: string;
  visibleOperandCount: number;
  path: string;
  raw: unknown;
}

export type DiffStatus = 'unchanged' | 'added' | 'removed' | 'changed' | 'moved';

export interface DiffCounts {
  added: number;
  removed: number;
  changed: number;
  moved: number;
}

export interface DiffModel {
  stats: DiffCounts & { unchanged: number };
  formIdError?: string;
  rootIds: number[];
  nodes: DiffNode[];
  pairs: DiffPair[];
}

export interface DiffNode extends ViewNode {
  status: DiffStatus;
  counts: DiffCounts;
  baseDefinitionIndex?: number;
  headDefinitionIndex?: number;
}

export interface DiffPair {
  baseIndex?: number;
  headIndex?: number;
  status: DiffStatus;
  basePath?: string;
  headPath?: string;
  changedPaths: string[];
}
