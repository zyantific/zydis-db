export type LoadSource = { kind: 'url'; url: string; name: string } | { kind: 'file'; file: File };

export interface SourceText {
  name: string;
  json: string;
}

// A missing file is the normal case when the app is served without a checkout, so it leads to the empty state.
export async function readSource(source: LoadSource): Promise<SourceText | null> {
  const read = await readSourceOrNotFound(source);
  return 'notFound' in read ? null : read;
}

// Returns the body of a 404 answer as notFound, since the local host writes the cause of a failed git read there.
export async function readSourceOrNotFound(source: LoadSource): Promise<SourceText | { notFound: string }> {
  if (source.kind === 'file') {
    return { name: source.file.name, json: await source.file.text() };
  }

  const response = await fetch(source.url);
  if (response.status === 404) {
    return { notFound: await response.text() };
  }
  if (!response.ok) {
    throw new Error(`${source.url}: ${response.status} ${response.statusText}`);
  }

  return { name: source.name, json: await response.text() };
}

// The .NET 9 runtime hands JavaScript the bare exception message. Text in the form Exception.ToString() writes, with
// the type name in front and any inner exception and the stack on the lines below, is cut down to the same message.
const dotnetTypePrefix = /^(?:[\w`+]+\.)*\w*Exception: /;
const dotnetTrailer = /\r?\n\s*(?:at |---> )/;

export function describeLoadError(error: unknown): string {
  if (!(error instanceof Error)) {
    return String(error);
  }

  const trailer = error.message.search(dotnetTrailer);
  const message = trailer < 0 ? error.message : error.message.slice(0, trailer);

  return message.replace(dotnetTypePrefix, '').trim();
}
