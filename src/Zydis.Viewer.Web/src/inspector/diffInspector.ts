import { COUNT_SIGNS, counterpartOf, definitionFor, selectDiffRow, type DiffSession } from '../diff/diffState';
import type { DiffNode, DiffPair, ViewDefinition } from '../model';
import type { LoadedDatabase } from '../store';
import {
  copyButton, definitionSections, nodeSections, renderSections, type DiffPanelRenderer, type Section,
} from './inspector';

type Row = Section['rows'][number];

// The rows definitionSections writes for the top-level keys the switch in markPath does not handle.
const KEY_ROWS: Record<string, readonly [title: string, key: string]> = {
  adsize_map: ['Definition', 'address size map'],
  cpl: ['Definition', 'CPL'],
  encoding: ['Definition', 'encoding'],
  exception_class: ['Definition', 'exception class'],
  flags: ['Flags', 'flags'],
  force_modrm_reg: ['Definition', 'force modrm reg'],
  force_modrm_rm: ['Definition', 'force modrm rm'],
  mnemonic: ['Definition', 'mnemonic'],
  opcode: ['Definition', 'opcode'],
  opcode_map: ['Definition', 'opcode map'],
  opsize_map: ['Definition', 'operand size map'],
  prefix_flags: ['Flags', 'prefix flags'],
};

const META_KEYS: Record<string, string> = { category: 'category', extension: 'extension', isa_set: 'ISA set' };

// What the changed paths of a pair tint. A path that names no single row tints the title of its section.
interface Marks {
  // `${title}\n${key}`
  keys: Set<string>;
  // `${title}\n${position}`, for the filters and the annotations, whose rows follow the array.
  positions: Set<string>;
  // The sections a change tints as a whole: the comment and the raw block.
  sections: Set<string>;
  titles: Set<string>;
  operands: Set<number>;
  flags: Set<string>;
}

export function createDiffPanel(): DiffPanelRenderer {
  // Kept from one selection to the next, so a reader who wants every field does not ask again on each row.
  let showUnchanged = false;

  return (state, header, body) => {
    const { database, diff, selectedNodeId } = state;
    if (database === null || diff === null || selectedNodeId === null) {
      return;
    }

    const node = diff.model.nodes[selectedNodeId];
    const { base, head, pair } = definitionFor(diff, database, selectedNodeId);
    const shown = head ?? base;
    if (pair === null || shown === null) {
      // The decision rows read the slot names of their filter, which are the same on both sides. A filter only the base
      // tree tests has its names in the base model alone.
      const filterSlots = { ...diff.base.model.filterSlots, ...database.model.filterSlots };
      const tree = { filterSlots, nodes: diff.model.nodes, rootIds: diff.model.rootIds };
      header.replaceChildren(element('span', 'inspector-title', node.label));
      body.replaceChildren(...renderSections([...nodeSections(tree, selectedNodeId), changeSection(node)]));
      return;
    }

    const { formId } = shown;
    const title = [element('span', 'inspector-title', formId), copyButton(formId),
      element('span', `diff-status status-${pair.status}`, pair.status)];
    if (base === null || head === null) {
      const side = base === null ? `only in B: ${database.name}` : `only in A: ${diff.base.name}`;
      header.replaceChildren(...title, element('span', 'diff-side', side));
      body.replaceChildren(...renderSections(definitionSections(shown)));
      return;
    }

    const grid = comparison(base, head, marksOf(pair), diff, database);
    grid.classList.toggle('show-unchanged', showUnchanged);

    const toggle = document.createElement('input');
    toggle.type = 'checkbox';
    toggle.checked = showUnchanged;
    toggle.addEventListener('change', () => {
      showUnchanged = toggle.checked;
      grid.classList.toggle('show-unchanged', showUnchanged);
    });
    const toggleLabel = element('label', 'show-unchanged-toggle');
    toggleLabel.append(toggle, ' Show unchanged');

    header.replaceChildren(...title, toggleLabel);
    body.replaceChildren(...(pair.status === 'moved' ? renderSections([moveSection(diff, selectedNodeId, pair)]) : []),
      grid);
  };
}

function changeSection(node: DiffNode): Section {
  const counts = COUNT_SIGNS.map(([status, sign]) => `${sign}${node.counts[status]} ${status}`).join(', ');
  return { title: 'Changes', rows: [{ key: 'status', value: node.status }, { key: 'rows below', value: counts }] };
}

// The two rows of a moved form link to each other. The path of the selected row is plain text.
function moveSection(diff: DiffSession, nodeId: number, pair: DiffPair): Section {
  const other = counterpartOf(diff.model, nodeId);
  const atHead = diff.model.nodes[nodeId].headDefinitionIndex !== undefined;
  const path = (text: string | undefined, linked: boolean): string | HTMLElement => {
    if (!linked || other === null) {
      return text ?? '';
    }
    const link = element('button', 'link', text);
    link.type = 'button';
    link.title = 'Select the other row of this form';
    link.addEventListener('click', () => selectDiffRow(other));
    return link;
  };

  return {
    title: 'Move',
    rows: [
      { key: 'old path', value: path(pair.basePath, atHead) },
      { key: 'new path', value: path(pair.headPath, !atHead) },
    ],
  };
}

function marksOf(pair: DiffPair): Marks {
  const marks: Marks = {
    flags: new Set(), keys: new Set(), operands: new Set(), positions: new Set(), sections: new Set(),
    titles: new Set(),
  };
  for (const path of pair.changedPaths) {
    markPath(path, marks);
  }
  // The raw block holds the whole form, so any change shows in it.
  if (pair.changedPaths.length > 0) {
    marks.sections.add('Raw');
  }
  return marks;
}

function markPath(path: string, marks: Marks): void {
  const [, name, index, member] = /^([^.[]*)(?:\[(\d+)\])?(?:\.([^.[]+))?/.exec(path) ?? [];
  const key = (title: string, rowKey: string): void => {
    marks.keys.add(`${title}\n${rowKey}`);
  };

  switch (name) {
    case 'affected_flags':
      if (member === 'access') {
        key('Affected flags', 'access');
      } else if (member !== undefined) {
        marks.flags.add(member);
      } else {
        marks.titles.add('Affected flags');
      }
      return;
    case 'annotations':
    case 'filters': {
      const title = name === 'filters' ? 'Filters' : 'Annotations';
      // The legacy filter object has no positions, so a change in it tints the title.
      if (index === undefined) {
        marks.titles.add(title);
      } else {
        marks.positions.add(`${title}\n${index}`);
      }
      return;
    }
    case 'comment':
      marks.sections.add('Comment');
      return;
    case 'evex':
    case 'mvex':
    case 'vex':
      // encodingInfoRows prefixes the key with the object name when a form carries more than one of the three.
      if (member === undefined) {
        marks.titles.add('Encoding info');
      } else {
        key('Encoding info', member);
        key('Encoding info', `${name}.${member}`);
      }
      return;
    case 'meta_info':
      if (member === undefined) {
        marks.titles.add('Meta info');
      } else {
        key('Meta info', META_KEYS[member] ?? member);
      }
      return;
    case 'operands':
      if (index === undefined) {
        marks.titles.add('Operands');
      } else {
        marks.operands.add(Number(index));
      }
      return;
    case undefined:
    case '':
      // An empty path means the forms differ as a whole, which the raw block shows.
      return;
    default: {
      const [title, rowKey] = KEY_ROWS[name] ?? ['Other', name];
      key(title, rowKey);
    }
  }
}

// One grid for both forms, so the key, A and B columns line up across the sections.
function comparison(base: ViewDefinition, head: ViewDefinition, marks: Marks, diff: DiffSession,
  database: LoadedDatabase): HTMLElement {
  const grid = element('div', 'diff-grid');
  grid.append(element('div', 'diff-caption'), element('div', 'diff-caption', `A: ${diff.base.name}`),
    element('div', 'diff-caption', `B: ${database.name}`));

  const baseSections = definitionSections(base);
  const headSections = definitionSections(head);
  let anyChanged = false;
  for (const title of mergeOrder(baseSections.map((s) => s.title), headSections.map((s) => s.title))) {
    const a = keyedRows(baseSections.find((s) => s.title === title), marks);
    const b = keyedRows(headSections.find((s) => s.title === title), marks);
    const titleChanged = marks.titles.has(title);

    const section = element('div', 'diff-section');
    section.append(element('h3', `inspector-section-title diff-span${titleChanged ? ' diff-changed' : ''}`, title));
    let sectionChanged = titleChanged;
    for (const rowKey of mergeOrder([...a.keys()], [...b.keys()])) {
      const changed = rowChanged(a.get(rowKey), b.get(rowKey));
      section.append(renderRow(a.get(rowKey), b.get(rowKey), changed));
      sectionChanged ||= changed;
    }
    section.classList.toggle('unchanged', !sectionChanged);
    anyChanged ||= sectionChanged;
    grid.append(section);
  }

  if (!anyChanged) {
    grid.append(element('div', 'inspector-hint diff-span', 'No field differs between A and B.'));
  }
  return grid;
}

interface SideRow {
  row: Row;
  // Set when a changed path names the row or a row of its table.
  marked: boolean;
  // Set when the tint went to rows of the table inside the value.
  tableMarked: boolean;
}

// Keys the rows by key and occurrence, so the two sides line up even where a key repeats, as the annotation types can.
function keyedRows(section: Section | undefined, marks: Marks): Map<string, SideRow> {
  const keyed = new Map<string, SideRow>();
  if (section === undefined) {
    return keyed;
  }

  const seen = new Map<string, number>();
  section.rows.forEach((row, position) => {
    const occurrence = seen.get(row.key) ?? 0;
    seen.set(row.key, occurrence + 1);

    const tableMarked = markTableRows(section.title, row.value, marks);
    const marked = tableMarked || marks.keys.has(`${section.title}\n${row.key}`)
      || marks.positions.has(`${section.title}\n${position}`) || marks.sections.has(section.title);
    keyed.set(`${row.key}\n${occurrence}`, { row, marked, tableMarked });
  });
  return keyed;
}

// Tints the rows of the operand table and of the affected flags table that a changed path names.
function markTableRows(title: string, value: Row['value'], marks: Marks): boolean {
  if (typeof value === 'string') {
    return false;
  }

  let marked = false;
  value.querySelectorAll('tbody tr').forEach((tableRow, index) => {
    const hit = title === 'Operands' ? marks.operands.has(index)
      : title === 'Affected flags' && marks.flags.has(tableRow.firstElementChild?.textContent ?? '');
    tableRow.classList.toggle('diff-changed', hit);
    marked ||= hit;
  });
  return marked;
}

// A computed row, such as the form id, has no path of its own, so a difference in its text counts as well.
function rowChanged(a: SideRow | undefined, b: SideRow | undefined): boolean {
  if (a === undefined || b === undefined) {
    return true;
  }
  return a.marked || b.marked
    || (typeof a.row.value === 'string' && typeof b.row.value === 'string' && a.row.value !== b.row.value);
}

function renderRow(a: SideRow | undefined, b: SideRow | undefined, changed: boolean): HTMLElement {
  // A table with tinted rows keeps both cells plain, so the tinted rows stand out, also next to a table that lacks
  // the row.
  const tinted = changed && a?.tableMarked !== true && b?.tableMarked !== true;
  const cell = (side: SideRow | undefined, column: string): HTMLElement => {
    const value = side?.row.value ?? '';
    const created = element('div', `inspector-value ${column}${typeof value === 'string' ? ' text' : ''}`);
    created.append(value);
    created.classList.toggle('diff-changed', tinted && side !== undefined);
    return created;
  };

  const row = element('div', changed ? 'diff-row' : 'diff-row unchanged');
  const key = (a ?? b)?.row.key ?? '';
  if (key === '') {
    const wide = element('div', 'diff-wide');
    wide.append(cell(a, 'diff-a'), cell(b, 'diff-b'));
    row.append(wide);
  } else {
    row.append(element('div', 'inspector-key', key), cell(a, 'diff-a'), cell(b, 'diff-b'));
  }
  return row;
}

// Both lists follow the order of definitionSections, so the merge keeps that order and puts a key that one side lacks
// after the key it follows on its own side.
function mergeOrder(a: readonly string[], b: readonly string[]): string[] {
  const merged = [...b];
  let at = 0;
  for (const key of a) {
    const found = merged.indexOf(key);
    if (found >= 0) {
      at = found + 1;
    } else {
      merged.splice(at, 0, key);
      at++;
    }
  }
  return merged;
}

function element<K extends keyof HTMLElementTagNameMap>(tag: K, className: string, text?: string):
  HTMLElementTagNameMap[K] {
  const created = document.createElement(tag);
  if (className !== '') {
    created.className = className;
  }
  if (text !== undefined) {
    created.textContent = text;
  }
  return created;
}
