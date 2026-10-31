import type { ViewModel } from './model';
import type { ViewerRuntime } from './runtime';

export interface AppState {
  runtime: ViewerRuntime | null;
  database: LoadedDatabase | null;
  error: string | null;
  loading: string | null;
  // Replaced on every change, never mutated, so a subscriber can tell a change by identity.
  expanded: Set<number>;
  selectedNodeId: number | null;
  // The widths of the item, mnemonic, operands and comment columns in px.
  columnWidths: number[];
}

export interface LoadedDatabase {
  name: string;
  handle: number;
  model: ViewModel;
}

type Listener = (s: AppState) => void;

const listeners = new Set<Listener>();

export const store: {
  state: AppState;
  subscribe(fn: Listener): () => void;
  set(partial: Partial<AppState>): void;
} = {
  state: {
    runtime: null,
    database: null,
    error: null,
    loading: null,
    expanded: new Set(),
    selectedNodeId: null,
    columnWidths: [420, 120, 260, 400],
  },

  subscribe(fn) {
    listeners.add(fn);
    return () => {
      listeners.delete(fn);
    };
  },

  set(partial) {
    store.state = { ...store.state, ...partial };
    for (const fn of listeners) {
      fn(store.state);
    }
  },
};
