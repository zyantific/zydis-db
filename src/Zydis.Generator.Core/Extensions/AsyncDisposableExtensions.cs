using System;
using System.Runtime.CompilerServices;

namespace Zydis.Generator.Core.Extensions;

internal static class AsyncDisposableExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IAsyncDisposable AsAsyncDisposable<T>(this T disposable, out T disposableOut)
        where T : IAsyncDisposable
    {
        return disposableOut = disposable;
    }
}
