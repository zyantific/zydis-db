import type { ViewDefinition, ViewModel } from '../model';

export type MatchMode = 'exact' | 'startsWith' | 'contains' | 'regex';
export type SearchScope = 'mnemonic' | 'category' | 'extension' | 'formId' | 'isaSet' | 'opcode';

export interface Matcher {
  test: (value: string) => boolean;
}

// The fields the search reads from a form as it stands in the file. The definition reader accepts a meta_info object
// without some of its members and an opcode written as a decimal number or as a hex string of any length.
interface RawSearchFields {
  opcode: string | number;
  meta_info: { category?: string; extension?: string; isa_set?: string };
}

export function scopeValue(definition: ViewDefinition, scope: SearchScope): string {
  const raw = definition.raw as RawSearchFields;
  switch (scope) {
    case 'category':
      return raw.meta_info.category ?? '';
    case 'extension':
      return raw.meta_info.extension ?? '';
    case 'formId':
      return definition.formId;
    case 'isaSet':
      return raw.meta_info.isa_set ?? '';
    case 'mnemonic':
      return definition.mnemonic;
    case 'opcode': {
      const opcode = typeof raw.opcode === 'number' ? raw.opcode : Number.parseInt(raw.opcode, 16);
      return opcode.toString(16).toUpperCase().padStart(2, '0');
    }
  }
}

export function createMatcher(mode: MatchMode, text: string): Matcher | { error: string } {
  const needle = text.toLowerCase();
  switch (mode) {
    case 'exact':
      return { test: (value) => value.toLowerCase() === needle };
    case 'startsWith':
      return { test: (value) => value.toLowerCase().startsWith(needle) };
    case 'contains':
      return { test: (value) => value.toLowerCase().includes(needle) };
    case 'regex':
      try {
        // Without the g flag, so that test keeps no lastIndex from one value to the next.
        const pattern = new RegExp(text, 'i');
        return { test: (value) => pattern.test(value) };
      } catch (error) {
        if (error instanceof SyntaxError) {
          return { error: error.message };
        }
        throw error;
      }
  }
}

// Returns the matched definition node ids plus every ancestor id.
export function matchingNodeIds(model: ViewModel, scope: SearchScope, matcher: Matcher): Set<number> {
  const nodeIds = new Set<number>();
  for (const node of model.nodes) {
    if (node.definitionIndex === undefined) {
      continue;
    }
    if (!matcher.test(scopeValue(model.definitions[node.definitionIndex], scope))) {
      continue;
    }

    // Stops at the first ancestor already kept, since the match that kept it also kept the rest of its chain.
    let nodeId: number | null = node.id;
    while (nodeId !== null && !nodeIds.has(nodeId)) {
      nodeIds.add(nodeId);
      nodeId = model.nodes[nodeId].parentId;
    }
  }

  return nodeIds;
}
