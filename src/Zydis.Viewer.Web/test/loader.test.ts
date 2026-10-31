import { describe, expect, it } from 'vitest';

import { describeLoadError } from '../src/loader';

describe('describeLoadError', () => {
  it('returns the .NET message without the stack', () => {
    const e = new Error('System.Text.Json.JsonException: The JSON value could not be converted\n   at ...');
    expect(describeLoadError(e)).toBe('The JSON value could not be converted');
  });
  it('keeps a bare message with a colon unchanged', () => {
    const message = 'Form "x" belongs to no opcode table: PRIMARY';
    expect(describeLoadError(new Error(message))).toBe(message);
  });
  it('stringifies anything else', () => { expect(describeLoadError(42)).toBe('42'); });
});
