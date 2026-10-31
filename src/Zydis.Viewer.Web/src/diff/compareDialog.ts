import { describeLoadError, type LoadSource } from '../loader';
import { store } from '../store';
import { compareWith } from './diffState';

export interface CompareDialog {
  element: HTMLDialogElement;
  open(): void;
  // Takes a file dropped on the window as the base while the dialog is open. Returns false while it is closed.
  takeFile(file: File): boolean;
}

// The page picks the base, A, from a file. Served by the local host, it also picks it from a git reference of the
// checkout. The loaded database is the head, B, in both cases.
export function createCompareDialog(): CompareDialog {
  const headLine = element('p', 'compare-head');

  const refChoice = radio('ref');
  const refSelect = document.createElement('select');
  refSelect.setAttribute('aria-label', 'Base reference');
  const refLine = element('label', 'compare-choice');
  refLine.append(refChoice, ' Git reference ', refSelect);

  const fileChoice = radio('file');
  const fileInput = document.createElement('input');
  fileInput.type = 'file';
  fileInput.accept = '.json,application/json';
  fileInput.setAttribute('aria-label', 'Base file');
  const fileLine = element('label', 'compare-choice');
  fileLine.append(fileChoice, ' File ', fileInput);

  const error = element('p', 'compare-error');
  const cancelButton = element('button', '', 'Cancel');
  cancelButton.type = 'button';
  const compareButton = element('button', '', 'Compare');
  compareButton.type = 'submit';
  const buttons = element('div', 'compare-buttons');
  buttons.append(cancelButton, compareButton);

  const fieldset = document.createElement('fieldset');
  fieldset.append(element('legend', '', 'A (base)'), refLine, fileLine);
  const form = document.createElement('form');
  form.append(element('h2', '', 'Compare'), fieldset, headLine, error, buttons);
  const dialog = element('dialog', 'compare-dialog');
  dialog.append(form);

  let hostMode = false;
  let busy = false;
  // Counts the opens, so a refs answer that arrives after the dialog was closed and opened again is dropped.
  let opens = 0;

  const chosenSource = (): LoadSource | null => {
    if (hostMode && refChoice.checked && refSelect.value !== '') {
      const ref = refSelect.value;
      return { kind: 'url', url: `api/git/show?rev=${encodeURIComponent(ref)}`, name: ref };
    }
    const file = fileInput.files?.[0];
    return file === undefined ? null : { kind: 'file', file };
  };

  const render = (): void => {
    const name = store.state.database?.name ?? '';
    headLine.textContent = `B (head): ${hostMode ? `working tree (${name})` : name}`;
    refLine.hidden = !hostMode;
    // Without the host the file is the only choice, so its radio button would offer nothing.
    fileChoice.hidden = !hostMode;
    compareButton.disabled = busy || chosenSource() === null;
    compareButton.textContent = busy ? 'Comparing…' : 'Compare';
    cancelButton.disabled = busy;
  };

  const showRefs = (refs: string[]): void => {
    hostMode = refs.length > 0;
    refSelect.replaceChildren(...refs.map((ref) => new Option(ref, ref)));
    refSelect.value = refs.includes('master') ? 'master' : refs[0] ?? '';
    // A file picked before the refs arrived stays the choice.
    refChoice.checked = hostMode && !fileChoice.checked;
    render();
  };

  refSelect.addEventListener('change', () => {
    refChoice.checked = true;
    render();
  });
  fileInput.addEventListener('change', () => {
    fileChoice.checked = (fileInput.files?.length ?? 0) > 0;
    render();
  });
  refChoice.addEventListener('change', () => render());
  fileChoice.addEventListener('change', () => render());
  cancelButton.addEventListener('click', () => dialog.close());
  dialog.addEventListener('cancel', (event) => {
    // Escape would close the dialog that shows the outcome of the comparison still running.
    if (busy) {
      event.preventDefault();
    }
  });

  form.addEventListener('submit', (event) => {
    // The dialog stays open until the comparison ends, so an error shows next to the choice that caused it.
    event.preventDefault();
    const source = chosenSource();
    if (source === null) {
      return;
    }

    error.textContent = '';
    busy = true;
    render();
    // Lets the busy text paint before the parse and the comparison block the main thread.
    void new Promise((resolve) => requestAnimationFrame(() => setTimeout(resolve)))
      .then(() => compareWith(source))
      .then(() => dialog.close(), (failure: unknown) => {
        error.textContent = describeLoadError(failure);
      })
      .finally(() => {
        busy = false;
        render();
      });
  });

  return {
    element: dialog,

    open() {
      const ticket = ++opens;
      hostMode = false;
      fileInput.value = '';
      fileChoice.checked = false;
      error.textContent = '';
      render();
      dialog.showModal();
      void fetchRefs().then((refs) => {
        if (ticket === opens && dialog.open && refs !== null) {
          showRefs(refs);
        }
      });
    },

    takeFile(file) {
      if (!dialog.open) {
        return false;
      }
      const transfer = new DataTransfer();
      transfer.items.add(file);
      fileInput.files = transfer.files;
      fileChoice.checked = true;
      render();
      return true;
    },
  };
}

// The local host lists the git references of its checkout. A static server answers with an error page, which leaves
// the file as the only source of the base.
async function fetchRefs(): Promise<string[] | null> {
  try {
    const response = await fetch('api/git/refs');
    if (response.status !== 200) {
      return null;
    }
    const { refs } = (await response.json()) as { refs?: unknown };
    return Array.isArray(refs) && refs.every((ref) => typeof ref === 'string') ? refs : null;
  } catch {
    // A server that answers with HTML instead of JSON, or no answer at all, is a server without the host endpoints.
    return null;
  }
}

function radio(value: string): HTMLInputElement {
  const created = document.createElement('input');
  created.type = 'radio';
  created.name = 'compare-base';
  created.value = value;
  return created;
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
