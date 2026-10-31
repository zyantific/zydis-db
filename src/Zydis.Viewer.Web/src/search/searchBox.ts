import { store, type AppState } from '../store';
import type { MatchMode, SearchScope } from './matcher';
import { setSearch } from './searchState';

const MODES: ReadonlyArray<readonly [MatchMode, string]> = [
  ['exact', 'Exact'],
  ['startsWith', 'Starts with'],
  ['contains', 'Contains'],
  ['regex', 'Regular expression'],
];

const SCOPES: ReadonlyArray<readonly [SearchScope, string]> = [
  ['mnemonic', 'Mnemonic'],
  ['category', 'Category'],
  ['extension', 'Extension'],
  ['formId', 'Form id'],
  ['isaSet', 'ISA set'],
  ['opcode', 'Opcode'],
];

// Returns the search controls and the error line for the toolbar, in that order. The error is a toolbar item of its
// own, so the stylesheet can give it a line below the controls. Escape in the box clears the search and then calls
// onEscape.
export function createSearchBox(onEscape: () => void): HTMLElement[] {
  const input = document.createElement('input');
  input.type = 'search';
  input.placeholder = 'Search (Ctrl+F)';
  input.autocomplete = 'off';
  input.spellcheck = false;
  input.setAttribute('aria-label', 'Search');

  const { mode, scope } = store.state.search;
  const modeSelect = select('Match mode', MODES, mode);
  const scopeSelect = select('Search scope', SCOPES, scope);

  const error = document.createElement('span');
  error.className = 'search-error';

  const box = document.createElement('div');
  box.className = 'search';
  box.append(input, modeSelect, scopeSelect);

  const apply = (): void => setSearch({
    text: input.value,
    mode: modeSelect.value as MatchMode,
    scope: scopeSelect.value as SearchScope,
  });
  input.addEventListener('input', apply);
  modeSelect.addEventListener('change', apply);
  scopeSelect.addEventListener('change', apply);

  input.addEventListener('keydown', (event) => {
    if (event.key === 'Escape') {
      input.value = '';
      apply();
      onEscape();
    }
  });

  // On the document so the shortcut works wherever the focus is. It replaces the find bar of the browser, which sees
  // only the rows the virtualized grid has in the DOM.
  document.addEventListener('keydown', (event) => {
    if (event.ctrlKey && !event.altKey && !event.metaKey && !event.shiftKey && event.key.toLowerCase() === 'f') {
      event.preventDefault();
      input.focus();
      input.select();
    }
  });

  const render = (state: AppState): void => {
    // A comparison clears the search text in the store, since the rows of the merged tree have other node ids.
    if (input.value !== state.search.text) {
      input.value = state.search.text;
    }
    const disabled = state.database === null;
    input.disabled = disabled;
    modeSelect.disabled = disabled;
    scopeSelect.disabled = disabled;
    error.textContent = state.searchError ?? '';
    error.title = state.searchError ?? '';
    error.hidden = state.searchError === null;
  };
  store.subscribe(render);
  render(store.state);

  return [box, error];
}

function select<T extends string>(
  label: string,
  options: ReadonlyArray<readonly [T, string]>,
  value: T,
): HTMLSelectElement {
  const created = document.createElement('select');
  created.setAttribute('aria-label', label);
  for (const [optionValue, text] of options) {
    created.append(new Option(text, optionValue));
  }
  created.value = value;
  return created;
}
