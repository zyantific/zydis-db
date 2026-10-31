import { describe, expect, it } from 'vitest';

import type { ViewDefinition } from '../src/model';
import { createMatcher, scopeValue, type MatchMode } from '../src/search/matcher';

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
