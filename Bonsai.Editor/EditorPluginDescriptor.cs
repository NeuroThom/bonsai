using System;
using System.Reflection;
using Bonsai.Design;

namespace Bonsai.Editor
{
    /// <summary>
    /// Describes an editor plugin declared by an assembly level attribute, discovered
    /// without loading the assembly for execution.
    /// </summary>
    [Serializable]
    public class EditorPluginDescriptor
    {
        /// <summary>
        /// The assembly qualified name of the editor plugin type.
        /// </summary>
        public string PluginTypeName;

        /// <summary>
        /// Initializes a new instance of the <see cref="EditorPluginDescriptor"/> class
        /// from reflection only attribute data.
        /// </summary>
        /// <param name="attribute">The attribute data declaring the editor plugin.</param>
        public EditorPluginDescriptor(CustomAttributeData attribute)
        {
            if (attribute.ConstructorArguments.Count > 0)
            {
                var constructorArgument = attribute.ConstructorArguments[0];
                if (constructorArgument.ArgumentType.AssemblyQualifiedName == typeof(string).AssemblyQualifiedName)
                {
                    PluginTypeName = (string)constructorArgument.Value;
                }
                else PluginTypeName = ((Type)constructorArgument.Value).AssemblyQualifiedName;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EditorPluginDescriptor"/> class
        /// from the specified attribute instance.
        /// </summary>
        /// <param name="editorPlugin">The attribute declaring the editor plugin.</param>
        public EditorPluginDescriptor(EditorPluginAttribute editorPlugin)
        {
            PluginTypeName = editorPlugin.PluginTypeName;
        }
    }
}
