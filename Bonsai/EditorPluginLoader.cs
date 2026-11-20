using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using Bonsai.Configuration;
using Bonsai.Design;
using Bonsai.Editor;

namespace Bonsai
{
    sealed class EditorPluginLoader : MarshalByRefObject
    {
        static IEnumerable<EditorPluginDescriptor> GetAssemblyPlugins(Assembly assembly)
        {
            return assembly.GetCustomAttributesData()
                .OfType<EditorPluginAttribute>()
                .Select(attribute => new EditorPluginDescriptor(attribute));
        }

        static IEnumerable<EditorPluginDescriptor> GetReflectionEditorPlugins(MetadataLoadContext context, string assemblyName)
        {
            try
            {
                var assembly = context.LoadFromAssemblyName(assemblyName);
                return GetAssemblyPlugins(assembly);
            }
            catch (FileLoadException ex) { Trace.TraceError("{0}", ex); }
            catch (FileNotFoundException ex) { Trace.TraceError("{0}", ex); }
            catch (BadImageFormatException ex) { Trace.TraceError("{0}", ex); }
            return Enumerable.Empty<EditorPluginDescriptor>();
        }

        public static IObservable<EditorPluginDescriptor> GetEditorPlugins(PackageConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var assemblies = configuration.AssemblyReferences.Select(reference => reference.AssemblyName);
            return Observable.Using(
                () => LoaderResource.CreateMetadataLoadContext(configuration),
                context => from assemblyName in assemblies.ToObservable()
                           from plugin in GetReflectionEditorPlugins(context, assemblyName)
                           select plugin);
        }
    }
}
