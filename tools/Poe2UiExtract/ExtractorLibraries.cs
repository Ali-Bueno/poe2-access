using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;

/// <summary>
///     Where the extractor's own native imports (the game's FMOD libraries) are loaded from. One resolver for the
///     assembly, since it can be set only once and several commands run in the same process.
/// </summary>
internal static class ExtractorLibraries
{
    private static readonly ConcurrentDictionary<string, string> Paths = new(StringComparer.Ordinal);
    private static readonly object Gate = new();
    private static bool registered;

    /// <summary>Loads the import <paramref name="name"/> from <paramref name="path"/> from now on.</summary>
    public static void Use(string name, string path)
    {
        Paths[name] = path;
        lock (Gate)
        {
            if (!registered)
            {
                registered = true;
                NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), Resolve);
            }
        }
    }

    private static IntPtr Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath) =>
        Paths.TryGetValue(name, out var path) ? NativeLibrary.Load(path) : IntPtr.Zero;
}
