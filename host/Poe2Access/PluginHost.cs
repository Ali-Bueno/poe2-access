namespace Poe2Access
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Loader;
    using GameReader;
    using GameReader.Plugin;

    /// <summary>The accessibility plugin, loaded from its folder, and the calls the host makes on it.</summary>
    internal sealed class PluginHost
    {
        private readonly IPCore plugin;
        private readonly string name;
        private bool faulted;

        private PluginHost(IPCore plugin, string name)
        {
            this.plugin = plugin;
            this.name = name;
        }

        /// <summary>Loads <c>&lt;directory&gt;/&lt;name&gt;.dll</c>; null when it is missing or has no plugin type.</summary>
        public static PluginHost? Load(string directory, string name)
        {
            if (TryLoad(directory, name, out var host, out var fault))
            {
                return host;
            }

            HostLog.Line(fault!);
            return null;
        }

        /// <summary>The load itself, saying why it failed: also what "check" runs, so the check cannot differ from a start.</summary>
        public static bool TryLoad(string directory, string name, out PluginHost? host, out string? fault)
        {
            host = null;
            var path = Path.Join(directory, $"{name}.dll");
            if (!File.Exists(path))
            {
                fault = $"Plugin not found: {path}";
                return false;
            }

            try
            {
                var assembly = new PluginLoadContext(directory).LoadFromAssemblyPath(path);
                var type = assembly.GetTypes().FirstOrDefault(t => typeof(IPCore).IsAssignableFrom(t) && !t.IsAbstract);
                if (type == null || Activator.CreateInstance(type) is not IPCore plugin)
                {
                    fault = $"No plugin type in {path}";
                    return false;
                }

                plugin.SetPluginDllLocation(directory);
                host = new PluginHost(plugin, name);
                fault = null;
                return true;
            }
            catch (Exception error)
            {
                fault = $"Plugin load failed: {error}";
                return false;
            }
        }

        public void Enable() => this.Call("OnEnable", () => this.plugin.OnEnable(HostFrame.GameOpened));

        /// <summary>One frame of the plugin; a fault is logged once in a row, not every frame.</summary>
        public void Tick()
        {
            try
            {
                this.plugin.DrawUI();
                this.faulted = false;
            }
            catch (Exception error)
            {
                if (!this.faulted)
                {
                    HostLog.Line($"{this.name} DrawUI threw: {error}");
                }

                this.faulted = true;
            }
        }

        public void Disable()
        {
            this.Call("SaveSettings", this.plugin.SaveSettings);
            this.Call("OnDisable", this.plugin.OnDisable);
        }

        private void Call(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception error)
            {
                HostLog.Line($"{this.name} {what} threw: {error}");
            }
        }

        /// <summary>Plugin-private assemblies from its folder; everything the host has (its reader, JSON) is shared.</summary>
        private sealed class PluginLoadContext(string directory) : AssemblyLoadContext(isCollectible: false)
        {
            protected override Assembly? Load(AssemblyName assemblyName)
            {
                if (Default.Assemblies.Any(a => AssemblyName.ReferenceMatchesDefinition(assemblyName, a.GetName())))
                {
                    return null;
                }

                var path = Path.Join(directory, $"{assemblyName.Name}.dll");
                return File.Exists(path) ? this.LoadFromAssemblyPath(path) : null;
            }

            protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
            {
                var file = unmanagedDllName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? unmanagedDllName : $"{unmanagedDllName}.dll";
                var path = Path.Join(directory, file);
                return File.Exists(path) ? this.LoadUnmanagedDllFromPath(path) : IntPtr.Zero;
            }
        }
    }
}
