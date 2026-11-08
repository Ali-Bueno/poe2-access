using System.Reflection;
using System.Runtime.InteropServices;

/// <summary>
///     Finds oo2core.dll for LibBundle3, which imports it by name: beside the executable, or in the "app" folder of the
///     packaged layout, where the single-file executable sits at the package root.
/// </summary>
internal static class OodleLibrary
{
    private const string LibraryName = "oo2core";
    private const string FileName = "oo2core.dll";
    private const string AppFolder = "app";
    private static bool registered;

    public static void Register()
    {
        lock (typeof(OodleLibrary))
        {
            if (registered)
            {
                return;
            }

            registered = true;
            try
            {
                NativeLibrary.SetDllImportResolver(typeof(LibBundle3.Index).Assembly, Resolve);
            }
            catch (InvalidOperationException)
            {
                // A resolver is already set for this assembly: the default probing stays.
            }
        }
    }

    private static IntPtr Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(Path.GetFileNameWithoutExtension(name), LibraryName, StringComparison.OrdinalIgnoreCase))
        {
            return IntPtr.Zero;
        }

        foreach (var folder in new[] { Path.Join(AppContext.BaseDirectory, AppFolder), AppContext.BaseDirectory })
        {
            var path = Path.Join(folder, FileName);
            if (File.Exists(path) && NativeLibrary.TryLoad(path, out var handle))
            {
                return handle;
            }
        }

        return IntPtr.Zero;
    }
}
