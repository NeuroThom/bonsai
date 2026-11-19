using System;
using System.Collections.Generic;
using System.Diagnostics;
using Bonsai.Design;

namespace Bonsai.Editor
{
    /// <summary>
    /// Instantiates and tracks editor plugins, isolating every failure so one broken
    /// plugin can never take the editor down.
    /// </summary>
    sealed class EditorPluginHost : IDisposable
    {
        readonly List<IEditorPlugin> plugins = new();

        public void Load(EditorPluginDescriptor descriptor, IServiceProvider provider)
        {
            IEditorPlugin plugin;
            try
            {
                var type = Type.GetType(descriptor.PluginTypeName, throwOnError: false);
                if (type == null)
                {
                    Trace.TraceError("Unable to resolve editor plugin type \"{0}\".", descriptor.PluginTypeName);
                    return;
                }

                if (!typeof(IEditorPlugin).IsAssignableFrom(type))
                {
                    Trace.TraceError("The editor plugin type \"{0}\" does not implement {1}.", descriptor.PluginTypeName, nameof(IEditorPlugin));
                    return;
                }

                plugin = (IEditorPlugin)Activator.CreateInstance(type);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Failed to create editor plugin \"{0}\": {1}", descriptor.PluginTypeName, ex);
                return;
            }

            try
            {
                plugin.Initialize(provider);
                plugins.Add(plugin);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Failed to initialize editor plugin \"{0}\": {1}", descriptor.PluginTypeName, ex);
                DisposePlugin(plugin);
            }
        }

        public void Dispose()
        {
            for (int i = plugins.Count - 1; i >= 0; i--)
            {
                DisposePlugin(plugins[i]);
            }

            plugins.Clear();
        }

        static void DisposePlugin(IEditorPlugin plugin)
        {
            try { plugin.Dispose(); }
            catch (Exception ex)
            {
                Trace.TraceError("Failed to dispose editor plugin \"{0}\": {1}", plugin.GetType().AssemblyQualifiedName, ex);
            }
        }
    }
}
