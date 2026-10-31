using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;

using Zydis.Viewer.Core;
using Zydis.Viewer.Core.Diff;

namespace Zydis.Viewer.Wasm;

/// <summary>
/// The functions the browser front end calls. Each loaded database stays in memory under a handle until the front end
/// releases it.
/// </summary>
[SupportedOSPlatform("browser")]
internal static partial class ViewerApi
{
    private static readonly Dictionary<int, ViewerDatabase> Databases = [];

    // Handles only grow, so a stale handle the front end still holds fails instead of reaching a newer database.
    private static int _nextHandle = 1;

    // OutputType Exe needs an entry point, but the web app starts the runtime itself and never runs Main.
    private static void Main()
    {
    }

    /// <summary>
    /// Loads the database in <paramref name="json"/> and returns its handle.
    /// </summary>
    /// <param name="json">The content of an <c>instructions.json</c> file.</param>
    /// <returns>The handle of the loaded database.</returns>
    /// <exception cref="JsonException">Thrown if <paramref name="json"/> is not a JSON array of forms.</exception>
    /// <exception cref="InvalidDataException">
    /// Thrown if a form uses a filter or a filter value the generator does not know.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown in the cases <see cref="ViewerDatabase.Load"/> names.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown if a form belongs to no opcode table of the generator.
    /// </exception>
    [JSExport]
    public static int LoadDatabase(string json)
    {
        var database = ViewerDatabase.Load(json);

        var handle = _nextHandle++;
        Databases.Add(handle, database);

        return handle;
    }

    /// <summary>
    /// Returns the view model of the database with the given handle as JSON.
    /// </summary>
    /// <param name="handle">The handle <see cref="LoadDatabase"/> returned.</param>
    /// <returns>The view model of the database as JSON.</returns>
    /// <exception cref="ArgumentException">Thrown if no loaded database has <paramref name="handle"/>.</exception>
    [JSExport]
    public static string GetViewModel(int handle)
    {
        return Get(handle).ToViewModelJson();
    }

    /// <summary>
    /// Compares the database with the handle <paramref name="baseHandle"/> to the one with the handle
    /// <paramref name="headHandle"/> and returns the difference as JSON.
    /// </summary>
    /// <param name="baseHandle">The handle of the database to compare from.</param>
    /// <param name="headHandle">The handle of the database to compare to.</param>
    /// <returns>The difference of the two databases as JSON.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown if no loaded database has <paramref name="baseHandle"/> or <paramref name="headHandle"/>.
    /// </exception>
    [JSExport]
    public static string Diff(int baseHandle, int headHandle)
    {
        return DatabaseDiff.Compare(Get(baseHandle), Get(headHandle)).ToJson();
    }

    /// <summary>
    /// Releases the database with the given handle.
    /// </summary>
    /// <param name="handle">The handle <see cref="LoadDatabase"/> returned.</param>
    /// <exception cref="ArgumentException">Thrown if no loaded database has <paramref name="handle"/>.</exception>
    [JSExport]
    public static void Release(int handle)
    {
        var database = Get(handle);

        Databases.Remove(handle);
        database.Dispose();
    }

    // The caller's argument expression names the parameter, so the exception of Diff tells its two handles apart.
    private static ViewerDatabase Get(int handle, [CallerArgumentExpression(nameof(handle))] string parameterName = "")
    {
        if (!Databases.TryGetValue(handle, out var database))
        {
            throw new ArgumentException($"No loaded database has the handle {handle}.", parameterName);
        }

        return database;
    }
}
