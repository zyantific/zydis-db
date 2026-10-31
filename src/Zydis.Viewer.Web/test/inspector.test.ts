// @vitest-environment jsdom
import { describe, expect, it } from 'vitest';

import { definitionSections, nodeSections, type Section } from '../src/inspector/inspector';
import type { NodeKind, ViewDefinition, ViewModel, ViewNode } from '../src/model';

function definition(raw: Record<string, unknown>): ViewDefinition {
  return { index: 0, formId: 'FORM', mnemonic: 'nop', operandSummary: '', visibleOperandCount: 0, path: '', raw };
}

function section(sections: Section[], title: string): Section {
  const found = sections.find((candidate) => candidate.title === title);
  if (found === undefined) {
    throw new Error(`No section ${title} in ${sections.map((candidate) => candidate.title).join(', ')}`);
  }
  return found;
}

function node(id: number, parentId: number | null, kind: NodeKind, label: string, childIds: number[]): ViewNode {
  return { id, parentId, kind, label, childIds, conflict: false, formCount: 1 };
}

describe('inspector sections', () => {
  it('puts unknown top-level keys into Other', () => {
    const sections = definitionSections(definition({
      mnemonic: 'erets',
      opcode: '01',
      force_modrm_reg: true,
      future_field: { level: 2 },
    }));

    expect(sections[sections.length - 1]).toEqual({
      title: 'Other',
      rows: [{ key: 'future_field', value: '{"level":2}' }],
    });
  });

  it('lists filters in recorded order with numbers', () => {
    const recorded = definitionSections(definition({
      filters: [
        { filter: 'rex_w', value: '1' },
        { filter: 'mode', value: '!64' },
        { filter: 'modrm_mod', value: '3' },
      ],
    }));
    expect(section(recorded, 'Filters').rows).toEqual([
      { key: '1', value: 'rex_w = 1' },
      { key: '2', value: 'mode = !64' },
      { key: '3', value: 'modrm_mod = 3' },
    ]);

    // The legacy schema records the filters as an object, so its order is the order of the keys in the file.
    const legacy = definitionSections(definition({ filters: { rex_w: '1', mode: '!64' } }));
    expect(section(legacy, 'Filters').rows).toEqual([
      { key: '1', value: 'rex_w = 1' },
      { key: '2', value: 'mode = !64' },
    ]);
  });

  it('renders hidden operands with the hidden class', () => {
    const sections = definitionSections(definition({
      operands: [
        { operand_type: 'gpr16_32_64', encoding: 'modrm_reg' },
        { operand_type: 'implicit_reg', register: 'al', visible: false },
      ],
    }));

    const table = section(sections, 'Operands').rows[0].value as HTMLElement;
    const rows = Array.from(table.querySelectorAll('tbody tr'));
    expect(rows.map((row) => row.classList.contains('hidden-operand'))).toEqual([false, true]);
  });

  it('nodeSections of a decision node lists every slot from filterSlots and marks the occupied ones', () => {
    const model: ViewModel = {
      schema: 'array',
      stats: { definitions: 0, nodes: 4, conflicts: 0 },
      rootIds: [0],
      nodes: [
        { ...node(0, null, 'decision', 'operand_size', [1, 2, 3]), filter: 'operand_size' },
        node(1, 0, 'slot', '02 (64)', []),
        node(2, 0, 'slot', '!00 (not 16)', []),
        node(3, 0, 'slot', 'else', []),
      ],
      definitions: [],
      filterSlots: { operand_size: ['16', '32', '64'] },
    };

    expect(section(nodeSections(model, 0), 'Slots').rows).toEqual([
      { key: '00 (16)', value: 'empty' },
      { key: '!00 (not 16)', value: 'occupied' },
      { key: '01 (32)', value: 'empty' },
      { key: '02 (64)', value: 'occupied' },
      { key: 'else', value: 'occupied' },
    ]);
  });
});
