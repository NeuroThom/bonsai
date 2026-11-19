using System;

namespace Bonsai.Design
{
    /// <summary>
    /// Declares a type implementing <see cref="IEditorPlugin"/> which the workflow editor
    /// will load and initialize at startup. Apply to the plugin assembly, once per plugin.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class EditorPluginAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EditorPluginAttribute"/> class
        /// using the specified plugin type.
        /// </summary>
        /// <param name="pluginType">The type of the editor plugin.</param>
        public EditorPluginAttribute(Type pluginType)
            : this(pluginType?.AssemblyQualifiedName)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EditorPluginAttribute"/> class
        /// using the assembly qualified name of the plugin type.
        /// </summary>
        /// <param name="pluginTypeName">The assembly qualified name of the editor plugin type.</param>
        public EditorPluginAttribute(string pluginTypeName)
        {
            PluginTypeName = pluginTypeName;
        }

        /// <summary>
        /// Gets the assembly qualified name of the editor plugin type.
        /// </summary>
        public string PluginTypeName { get; }
    }
}
