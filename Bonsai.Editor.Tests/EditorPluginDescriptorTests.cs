using System.Linq;
using Bonsai.Design;
using Bonsai.Editor.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: EditorPlugin(typeof(EditorPluginHostTests.RecordingPlugin))]
[assembly: EditorPlugin(EditorPluginDescriptorTests.PluginTypeName)]

namespace Bonsai.Editor.Tests
{
    [TestClass]
    public class EditorPluginDescriptorTests
    {
        internal const string PluginTypeName = "Missing.Plugin, Missing.Assembly";

        [TestMethod]
        public void Constructor_AttributeWithPluginType_UsesAssemblyQualifiedName()
        {
            var descriptor = new EditorPluginDescriptor(new EditorPluginAttribute(typeof(EditorPluginHostTests.RecordingPlugin)));
            Assert.AreEqual(typeof(EditorPluginHostTests.RecordingPlugin).AssemblyQualifiedName, descriptor.PluginTypeName);
        }

        [TestMethod]
        public void Constructor_AttributeWithPluginTypeName_UsesTypeName()
        {
            var descriptor = new EditorPluginDescriptor(new EditorPluginAttribute(PluginTypeName));
            Assert.AreEqual(PluginTypeName, descriptor.PluginTypeName);
        }

        [TestMethod]
        public void Constructor_AttributeData_ReadsPluginTypeAndPluginTypeName()
        {
            var pluginTypeNames = typeof(EditorPluginDescriptorTests).Assembly
                .GetCustomAttributesData()
                .Where(attribute => attribute.AttributeType == typeof(EditorPluginAttribute))
                .Select(attribute => new EditorPluginDescriptor(attribute).PluginTypeName)
                .OrderBy(typeName => typeName)
                .ToArray();
            var expectedTypeNames = new[]
            {
                typeof(EditorPluginHostTests.RecordingPlugin).AssemblyQualifiedName,
                PluginTypeName
            }.OrderBy(typeName => typeName).ToArray();
            CollectionAssert.AreEqual(expectedTypeNames, pluginTypeNames);
        }
    }
}
