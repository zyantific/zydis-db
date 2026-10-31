import type { DiffModel, ViewModel } from './model';

export interface ViewerRuntime {
  loadDatabase(json: string): number;
  getViewModel(handle: number): ViewModel;
  diff(base: number, head: number): DiffModel;
  release(handle: number): void;
}

// The part of the dotnet.js API this module uses. The runtime ships no TypeScript declarations next to dotnet.js.
interface DotnetModule {
  dotnet: {
    create(): Promise<{
      getAssemblyExports(assemblyName: string): Promise<unknown>;
      getConfig(): { mainAssemblyName?: string };
    }>;
  };
}

interface ViewerApiExports {
  LoadDatabase(json: string): number;
  GetViewModel(handle: number): string;
  Diff(baseHandle: number, headHandle: number): string;
  Release(handle: number): void;
}

export async function startRuntime(): Promise<ViewerRuntime> {
  // The runtime files carry content hashes that dotnet.js resolves from its own location, so Vite must neither bundle
  // nor rewrite this import.
  const url = new URL('_framework/dotnet.js', document.baseURI).href;
  const { dotnet } = (await import(/* @vite-ignore */ url)) as DotnetModule;

  const { getAssemblyExports, getConfig } = await dotnet.create();
  const assemblyName = getConfig().mainAssemblyName;
  if (assemblyName === undefined) {
    throw new Error('The .NET runtime configuration names no main assembly.');
  }

  const exports = (await getAssemblyExports(assemblyName)) as {
    Zydis: { Viewer: { Wasm: { ViewerApi: ViewerApiExports } } };
  };
  const api = exports.Zydis.Viewer.Wasm.ViewerApi;

  return {
    loadDatabase: (json) => api.LoadDatabase(json),
    getViewModel: (handle) => JSON.parse(api.GetViewModel(handle)) as ViewModel,
    diff: (base, head) => JSON.parse(api.Diff(base, head)) as DiffModel,
    release: (handle) => api.Release(handle),
  };
}
