import { describe, expect, it } from 'vitest';

import type { DiffModel, DiffNode, DiffStatus, NodeKind, ViewDefinition, ViewModel } from '../src/model';
import { createMatcher, diffMatchingNodeIds, scopeValue, type MatchMode } from '../src/search/matcher';

function matcher(mode: MatchMode, text: string): { test: (value: string) => boolean } {
  const created = createMatcher(mode, text);
  if ('error' in created) {
    throw new Error(created.error);
  }
  return created;
}

function definition(raw: unknown): ViewDefinition {
  return { index: 0, formId: 'x', mnemonic: 'x', operandSummary: '', visibleOperandCount: 0, path: '', raw };
}

function diffNode(id: number, parentId: number | null, kind: NodeKind, status: DiffStatus, childIds: number[],
  indices: { base?: number; head?: number } = {}): DiffNode {
  return {
    id, parentId, kind, label: String(id), childIds, conflict: false, formCount: 0, status,
    counts: { added: 0, removed: 0, changed: 0, moved: 0 },
    definitionIndex: indices.head ?? indices.base, baseDefinitionIndex: indices.base, headDefinitionIndex: indices.head,
  };
}

function viewModel(definitions: ViewDefinition[]): ViewModel {
  return {
    schema: 'array', stats: { definitions: 0, nodes: 0, conflicts: 0 }, rootIds: [], nodes: [], definitions,
    filterSlots: {},
  };
}

describe('createMatcher', () => {
  it('exact is case-insensitive and whole', () => {
    const exact = matcher('exact', 'CFCMOVB');
    expect(exact.test('cfcmovb')).toBe(true);
    expect(exact.test('cfcmovbe')).toBe(false);
  });

  it('startsWith', () => {
    const startsWith = matcher('startsWith', 'MOV');
    expect(startsWith.test('movsb')).toBe(true);
    expect(startsWith.test('cmovb')).toBe(false);
  });

  it('contains', () => {
    const contains = matcher('contains', 'MOVB');
    expect(contains.test('cfcmovbe')).toBe(true);
    expect(contains.test('movsb')).toBe(false);
  });

  it('regex compiles with the i flag', () => {
    const regex = matcher('regex', '^cfcmovb$');
    // The second call catches a global flag, whose lastIndex would make the same value fail.
    expect(regex.test('CFCMOVB')).toBe(true);
    expect(regex.test('CFCMOVB')).toBe(true);
    expect(regex.test('cfcmovbe')).toBe(false);
  });

  it('invalid regular expression returns an error', () => {
    expect(createMatcher('regex', '[')).toEqual({ error: expect.stringMatching(/^Invalid regular expression/) });
  });
});

describe('scopeValue', () => {
  it('opcode is two upper-case hex digits', () => {
    expect(scopeValue(definition({ opcode: '0f' }), 'opcode')).toBe('0F');
    // The definition reader also accepts a short hex string and a decimal number.
    expect(scopeValue(definition({ opcode: 'f' }), 'opcode')).toBe('0F');
    expect(scopeValue(definition({ opcode: 55 }), 'opcode')).toBe('37');
  });

  it('isaSet reads raw.meta_info.isa_set', () => {
    const form = definition({ meta_info: { category: 'AVX512', extension: 'AVX512EVEX', isa_set: 'AVX512F_512' } });
    expect(scopeValue(form, 'isaSet')).toBe('AVX512F_512');
  });
});

describe('diffMatchingNodeIds', () => {
  it('keeps the old row of a moved form when only its head form matches', () => {
    // 0 PRIMARY
    //   1 opcode 20
    //     2 moved
    //   3 opcode 30
    //     4 unchanged
    //   5 opcode 10, only in the base
    //     6 moved, the old row
    const diff: DiffModel = {
      stats: { added: 0, removed: 0, changed: 0, moved: 1, unchanged: 1 },
      rootIds: [0],
      nodes: [
        diffNode(0, null, 'table', 'unchanged', [1, 3, 5]),
        diffNode(1, 0, 'opcode', 'added', [2]),
        diffNode(2, 1, 'definition', 'moved', [], { base: 0, head: 0 }),
        diffNode(3, 0, 'opcode', 'unchanged', [4]),
        diffNode(4, 3, 'definition', 'unchanged', [], { base: 1, head: 1 }),
        diffNode(5, 0, 'opcode', 'removed', [6]),
        diffNode(6, 5, 'definition', 'moved', [], { base: 0 }),
      ],
      pairs: [
        { baseIndex: 0, headIndex: 0, status: 'moved', basePath: '10', headPath: '20', changedPaths: ['opcode'] },
        { baseIndex: 1, headIndex: 1, status: 'unchanged', basePath: '30', headPath: '30', changedPaths: [] },
      ],
    };
    const base = viewModel([definition({ opcode: '10' }), definition({ opcode: '30' })]);
    const head = viewModel([definition({ opcode: '20' }), definition({ opcode: '30' })]);

    expect(diffMatchingNodeIds(diff, base, head, 'opcode', matcher('exact', '20'))).toEqual(new Set([0, 1, 2, 5, 6]));
  });
});
