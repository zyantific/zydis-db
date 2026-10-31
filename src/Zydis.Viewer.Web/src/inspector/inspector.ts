import type { ViewDefinition, ViewModel, ViewNode } from '../model';
import { store, type AppState } from '../store';
import { ancestorsOf } from '../tree/rows';

export interface Section {
  title: string;
  // A row with an empty key spans the key and the value column, for a table or a block.
  rows: Array<{ key: string; value: string | HTMLElement }>;
}

type Row = Section['rows'][number];
type JsonObject = Record<string, unknown>;
type Fields = ReadonlyArray<readonly [name: string, key: string]>;

const WIDTH_KEY = 'zydis-viewer.inspector';
const DEFAULT_WIDTH = 420;
const MIN_WIDTH = 240;
const MIN_GRID_WIDTH = 200;
const COPY_FEEDBACK_MS = 1000;

// Every other top-level key of a form goes to the Other section, so a field added to the database shows without a
// change to the viewer.
const CONSUMED_KEYS = new Set([
  'adsize_map', 'affected_flags', 'annotations', 'comment', 'cpl', 'encoding', 'evex', 'exception_class', 'filters',
  'flags', 'force_modrm_reg', 'force_modrm_rm', 'meta_info', 'mnemonic', 'mvex', 'opcode', 'opcode_map', 'operands',
  'opsize_map', 'prefix_flags', 'vex',
]);

const OPERAND_COLUMNS = [
  '#', 'action', 'element type', 'encoding', 'register / memory', 'scale factor', 'type', 'visible', 'width16',
  'width32', 'width64', 'is_multisource4', 'ignore_seg_override',
];

export function definitionSections(def: ViewDefinition): Section[] {
  const form: JsonObject = isObject(def.raw) ? def.raw : {};
  const operands = Array.isArray(form.operands) ? form.operands : [];

  const sections: Section[] = [
    { title: 'Definition', rows: definitionRows(form, def.formId) },
    { title: 'Filters', rows: filterRows(form.filters) },
    { title: 'Operands', rows: operands.length > 0 ? [{ key: '', value: operandTable(operands) }] : [] },
    {
      title: 'Meta info',
      rows: isObject(form.meta_info)
        ? objectRows(form.meta_info, [['category', 'category'], ['extension', 'extension'], ['isa_set', 'ISA set']])
        : [],
    },
    { title: 'Affected flags', rows: affectedFlagRows(form.affected_flags) },
    { title: 'Encoding info', rows: encodingInfoRows(form) },
    { title: 'Flags', rows: chipRows(form) },
    { title: 'Annotations', rows: annotationRows(form.annotations) },
    { title: 'Comment', rows: commentRows(def, form) },
    { title: 'Raw', rows: [{ key: '', value: rawBlock(def.raw) }] },
    {
      title: 'Other',
      rows: Object.keys(form)
        .filter((name) => !CONSUMED_KEYS.has(name))
        .map((name) => ({ key: name, value: JSON.stringify(form[name]) })),
    },
  ];

  // A key the form does not carry leaves its row out, and a section without rows is left out as a whole.
  return sections.filter((section) => section.rows.length > 0);
}

export function nodeSections(model: ViewModel, nodeId: number): Section[] {
  const node = model.nodes[nodeId];
  const path = [...ancestorsOf(model, nodeId), nodeId].map((id) => model.nodes[id].label).join('/');
  const rows: Row[] = [
    { key: 'kind', value: node.kind },
    { key: 'label', value: node.label },
    { key: 'path', value: path },
    { key: 'forms', value: String(node.formCount) },
  ];
  if (node.detail !== undefined) {
    rows.push({ key: 'detail', value: node.detail });
  }
  if (node.kind !== 'decision') {
    return [{ title: 'Node', rows }];
  }

  rows.push({
    key: 'note',
    value: 'A form that does not test this filter shows once, under else, which a compact node folds into its second '
      + 'slot. The generator also copies the form into every claimed slot of the node.',
  });
  return [{ title: 'Node', rows }, { title: 'Slots', rows: slotRows(model, node) }];
}

export function mountInspector(container: HTMLElement): void {
  const resizer = element('div', 'inspector-resizer');
  const header = element('div', 'inspector-header');
  const body = element('div', 'inspector-body');
  const panel = element('aside', 'inspector');
  panel.append(resizer, header, body);
  container.append(panel);

  panel.style.width = `${readWidth() ?? DEFAULT_WIDTH}px`;
  resizer.addEventListener('pointerdown', (event) => startResize(event, resizer, panel, container));

  let model: ViewModel | null | undefined;
  let selectedNodeId: number | null | undefined;

  const update = (state: AppState): void => {
    const nextModel = state.database?.model ?? null;
    panel.hidden = nextModel === null;
    // Other changes, such as an expansion, leave the panel alone, so an open raw block stays open.
    if (nextModel === model && state.selectedNodeId === selectedNodeId) {
      return;
    }
    model = nextModel;
    selectedNodeId = state.selectedNodeId;

    if (model === null || selectedNodeId === null) {
      header.replaceChildren();
      body.replaceChildren(element('div', 'inspector-hint', 'Select a row'));
      return;
    }

    const node = model.nodes[selectedNodeId];
    if (node.definitionIndex === undefined) {
      header.replaceChildren(element('span', 'inspector-title', node.label));
      body.replaceChildren(...renderSections(nodeSections(model, selectedNodeId)));
    } else {
      const definition = model.definitions[node.definitionIndex];
      header.replaceChildren(element('span', 'inspector-title', definition.formId), copyButton(definition.formId));
      body.replaceChildren(...renderSections(definitionSections(definition)));
    }
    body.scrollTop = 0;
  };

  store.subscribe(update);
  update(store.state);
}

function definitionRows(form: JsonObject, formId: string): Row[] {
  return [
    ...fieldRows(form, [['mnemonic', 'mnemonic'], ['encoding', 'encoding'], ['opcode_map', 'opcode map'],
      ['opcode', 'opcode']]),
    { key: 'form id', value: formId },
    ...fieldRows(form, [['cpl', 'CPL'], ['exception_class', 'exception class'], ['opsize_map', 'operand size map'],
      ['adsize_map', 'address size map'], ['force_modrm_reg', 'force modrm reg'], ['force_modrm_rm', 'force modrm rm']]),
  ];
}

function filterRows(filters: unknown): Row[] {
  // The legacy schema records the filters as an object. JSON.parse keeps its keys in file order, since no filter name
  // is an integer.
  const entries = Array.isArray(filters)
    ? filters.filter(isObject).map((entry) => [entry.filter, entry.value] as const)
    : isObject(filters) ? Object.entries(filters) : [];

  return entries.map(([filter, value], index) => ({
    key: String(index + 1),
    value: `${formatValue(filter)} = ${formatValue(value)}`,
  }));
}

function operandTable(operands: unknown[]): HTMLElement {
  const rows = operands.map((entry, index) => {
    const operand: JsonObject = isObject(entry) ? entry : {};
    const row = tableRow('td', [
      String(index),
      cellText(operand.action),
      cellText(operand.element_type),
      cellText(operand.encoding),
      registerOrMemory(operand),
      cellText(operand.scale_factor),
      cellText(operand.operand_type),
      cellText(operand.visible),
      cellText(operand.width16),
      cellText(operand.width32),
      cellText(operand.width64),
      cellText(operand.is_multisource4),
      cellText(operand.ignore_seg_override),
    ]);
    // An absent visible means visible, so only an explicit false marks a hidden operand.
    row.classList.toggle('hidden-operand', operand.visible === false);
    return row;
  });

  return table(OPERAND_COLUMNS, rows);
}

function registerOrMemory(operand: JsonObject): string {
  if (operand.register !== undefined) {
    return formatValue(operand.register);
  }
  if (operand.mem_base === undefined && operand.mem_segment === undefined) {
    return '';
  }

  const segment = operand.mem_segment === undefined ? '' : `${formatValue(operand.mem_segment)}:`;
  return `${segment}[${cellText(operand.mem_base)}]`;
}

function affectedFlagRows(flags: unknown): Row[] {
  if (!isObject(flags)) {
    return [];
  }

  const rows = Object.keys(flags)
    .filter((name) => name !== 'access')
    .map((name) => tableRow('td', [name, formatValue(flags[name])]));

  return [
    ...fieldRows(flags, [['access', 'access']]),
    ...(rows.length > 0 ? [{ key: '', value: table(['flag', 'operation'], rows) }] : []),
  ];
}

function encodingInfoRows(form: JsonObject): Row[] {
  const objects = (['evex', 'mvex', 'vex'] as const).filter((name) => isObject(form[name]));

  // A form carries one of the three objects, so the field name alone is enough unless it carries more.
  return objects.flatMap((name) => objectRows(form[name] as JsonObject)
    .map((row) => (objects.length > 1 ? { ...row, key: `${name}.${row.key}` } : row)));
}

function chipRows(form: JsonObject): Row[] {
  const lists: Fields = [['flags', 'flags'], ['prefix_flags', 'prefix flags']];

  return lists.flatMap(([name, key]) => {
    const values = form[name];
    if (!Array.isArray(values)) {
      return [];
    }

    const chips = element('div', 'chips');
    chips.append(...values.map((value) => element('span', 'chip', formatValue(value))));
    return [{ key, value: chips }];
  });
}

function annotationRows(annotations: unknown): Row[] {
  if (!Array.isArray(annotations)) {
    return [];
  }

  return annotations.map((annotation) => {
    const { type, ...fields } = isObject(annotation) ? annotation : {};
    return {
      key: type === undefined ? '' : formatValue(type),
      value: Object.entries(fields).map(([name, value]) => `${name}: ${formatValue(value)}`).join('\n'),
    };
  });
}

function commentRows(def: ViewDefinition, form: JsonObject): Row[] {
  if (typeof def.commentText === 'string') {
    return [{ key: '', value: element('pre', 'comment-text', def.commentText) }];
  }

  // The view model has no text for a comment that is not base64, so the stored string shows in its place.
  return form.comment === undefined ? [] : [{ key: 'not base64', value: formatValue(form.comment) }];
}

function rawBlock(raw: unknown): HTMLElement {
  const json = JSON.stringify(raw, null, 2);

  const summary = document.createElement('summary');
  summary.append('JSON ', copyButton(json));
  const details = element('details', 'raw');
  details.append(summary, element('pre', 'raw-json', json));
  return details;
}

// The labels of the slot rows below a decision row name the occupied slots. ViewTreeBuilder.SlotLabel writes the
// index in two hex digits and the slot name, and marks a negated slot with a leading ! and "not".
function slotRows(model: ViewModel, node: ViewNode): Row[] {
  const occupied = new Set(node.childIds.map((id) => model.nodes[id].label));
  const row = (label: string): Row => ({ key: label, value: occupied.has(label) ? 'occupied' : 'empty' });

  const rows: Row[] = [];
  (model.filterSlots[node.filter ?? node.label] ?? []).forEach((name, index) => {
    const hex = index.toString(16).toUpperCase().padStart(2, '0');
    rows.push(row(`${hex} (${name})`));
    // A negated slot is a slot entry of its own, listed only when a form occupies it.
    const negated = `!${hex} (not ${name})`;
    if (occupied.has(negated)) {
      rows.push(row(negated));
    }
  });
  rows.push(row('else'));

  // A child label the loop did not rebuild becomes an occupied row of its own after else, so a change of the label
  // format on the .NET side shows as extra rows instead of a list of empty slots.
  const listed = new Set(rows.map(({ key }) => key));
  for (const label of occupied) {
    if (!listed.has(label)) {
      rows.push({ key: label, value: 'occupied' });
    }
  }

  return rows;
}

function renderSections(sections: Section[]): HTMLElement[] {
  return sections.map((section) => {
    const rows = element('div', 'inspector-rows');
    for (const { key, value } of section.rows) {
      const valueCell = element('div', typeof value === 'string' ? 'inspector-value text' : 'inspector-value');
      valueCell.append(value);
      if (key === '') {
        valueCell.classList.add('wide');
        rows.append(valueCell);
      } else {
        rows.append(element('div', 'inspector-key', key), valueCell);
      }
    }

    const box = element('section', 'inspector-section');
    box.append(element('h3', 'inspector-section-title', section.title), rows);
    return box;
  });
}

function copyButton(text: string): HTMLButtonElement {
  const button = element('button', 'copy', 'Copy');
  button.type = 'button';
  let reset: number | undefined;

  const show = (label: string): void => {
    button.textContent = label;
    window.clearTimeout(reset);
    reset = window.setTimeout(() => {
      button.textContent = 'Copy';
    }, COPY_FEEDBACK_MS);
  };

  button.addEventListener('click', () => {
    // Outside a secure context navigator.clipboard is missing, and the TypeError of the call ends in the rejection.
    void Promise.resolve()
      .then(() => navigator.clipboard.writeText(text))
      .then(() => show('Copied'), () => show('Copy failed'));
  });
  return button;
}

function table(columns: string[], rows: HTMLTableRowElement[]): HTMLElement {
  const head = document.createElement('thead');
  head.append(tableRow('th', columns));
  const body = document.createElement('tbody');
  body.append(...rows);
  const created = element('table', 'inspector-table');
  created.append(head, body);

  // The operand table is wider than the panel, so it scrolls in a box of its own and the panel keeps its width.
  const scroller = element('div', 'table-scroll');
  scroller.append(created);
  return scroller;
}

function tableRow(tag: 'td' | 'th', cells: string[]): HTMLTableRowElement {
  const row = document.createElement('tr');
  row.append(...cells.map((text) => element(tag, '', text)));
  return row;
}

// Lists the named fields first under their labels, then the other fields under their JSON names, so a field the
// viewer does not know by name still shows.
function objectRows(source: JsonObject, fields: Fields = []): Row[] {
  const named = new Set(fields.map(([name]) => name));
  const others: Fields = Object.keys(source).filter((name) => !named.has(name)).map((name) => [name, name]);
  return [...fieldRows(source, fields), ...fieldRows(source, others)];
}

function fieldRows(source: JsonObject, fields: Fields): Row[] {
  return fields
    .filter(([name]) => source[name] !== undefined)
    .map(([name, key]) => ({ key, value: formatValue(source[name]) }));
}

function cellText(value: unknown): string {
  return value === undefined ? '' : formatValue(value);
}

function formatValue(value: unknown): string {
  return typeof value === 'string' ? value : JSON.stringify(value);
}

function isObject(value: unknown): value is JsonObject {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function startResize(event: PointerEvent, handle: HTMLElement, panel: HTMLElement, container: HTMLElement): void {
  if (event.button !== 0) {
    return;
  }
  // Keeps the drag from selecting text.
  event.preventDefault();
  handle.setPointerCapture(event.pointerId);

  const startX = event.clientX;
  const startWidth = panel.offsetWidth;
  const move = (moveEvent: PointerEvent): void => {
    // The handle sits on the left edge, so a drag to the left widens the panel.
    const maxWidth = Math.max(MIN_WIDTH, container.clientWidth - MIN_GRID_WIDTH);
    const width = Math.min(maxWidth, Math.max(MIN_WIDTH, Math.round(startWidth + startX - moveEvent.clientX)));
    panel.style.width = `${width}px`;
  };

  handle.addEventListener('pointermove', move);
  handle.addEventListener('lostpointercapture', () => {
    handle.removeEventListener('pointermove', move);
    saveWidth(panel.offsetWidth);
  }, { once: true });
}

function readWidth(): number | null {
  try {
    const value: unknown = JSON.parse(localStorage.getItem(WIDTH_KEY) ?? 'null');
    return typeof value === 'number' && Number.isFinite(value) && value >= MIN_WIDTH ? value : null;
  } catch {
    // Blocked storage or stored text that is not JSON falls back to the default width.
    return null;
  }
}

function saveWidth(width: number): void {
  try {
    localStorage.setItem(WIDTH_KEY, JSON.stringify(width));
  } catch {
    // The width is a convenience, so blocked or full storage only costs the next visit its layout.
  }
}

function element<K extends keyof HTMLElementTagNameMap>(
  tag: K,
  className: string,
  text?: string,
): HTMLElementTagNameMap[K] {
  const created = document.createElement(tag);
  if (className !== '') {
    created.className = className;
  }
  if (text !== undefined) {
    created.textContent = text;
  }
  return created;
}
