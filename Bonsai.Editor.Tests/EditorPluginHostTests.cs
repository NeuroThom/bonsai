using System;
using System.Collections.Generic;
using System.Diagnostics;
using Bonsai.Design;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Bonsai.Editor.Tests
{
    [TestClass]
    public class EditorPluginHostTests
    {
        static readonly List<string> Events = new();
        readonly TraceRecorder traceRecorder = new();

        [TestInitialize]
        public void Initialize()
        {
            Events.Clear();
            RecordingPlugin.Provider = null;
            Trace.Listeners.Add(traceRecorder);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Trace.Listeners.Remove(traceRecorder);
        }

        static EditorPluginDescriptor CreateDescriptor(Type pluginType)
        {
            return new EditorPluginDescriptor(new EditorPluginAttribute(pluginType));
        }

        static EditorPluginHost LoadPlugins(params Type[] pluginTypes)
        {
            var host = new EditorPluginHost();
            foreach (var pluginType in pluginTypes)
            {
                host.Load(CreateDescriptor(pluginType), new EmptyServiceProvider());
            }
            return host;
        }

        [TestMethod]
        public void Load_ValidPlugin_InitializesWithProvider()
        {
            var provider = new EmptyServiceProvider();
            using var host = new EditorPluginHost();
            host.Load(CreateDescriptor(typeof(RecordingPlugin)), provider);
            Assert.AreSame(provider, RecordingPlugin.Provider);
            CollectionAssert.AreEqual(new[] { "RecordingPlugin.Initialize" }, Events);
        }

        [TestMethod]
        public void Load_UnresolvableTypeName_TracesUnresolvedTypeAndDiscardsPlugin()
        {
            const string PluginTypeName = "Missing.Plugin, Missing.Assembly";
            var host = new EditorPluginHost();
            var descriptor = new EditorPluginDescriptor(new EditorPluginAttribute(PluginTypeName));
            host.Load(descriptor, new EmptyServiceProvider());
            host.Dispose();
            var trace = traceRecorder.Single();
            StringAssert.Contains(trace, "resolve");
            StringAssert.Contains(trace, PluginTypeName);
            Assert.AreEqual(0, Events.Count);
        }

        [TestMethod]
        public void Load_TypeNotImplementingEditorPlugin_DiscardsWithoutCreatingInstance()
        {
            using var host = LoadPlugins(typeof(NotAPlugin));
            Assert.AreEqual(0, Events.Count);
        }

        [TestMethod]
        public void Load_MissingParameterlessConstructor_TracesFailureAndDiscardsPlugin()
        {
            var host = LoadPlugins(typeof(NoDefaultConstructorPlugin), typeof(RecordingPlugin));
            Events.Clear();
            host.Dispose();
            var trace = traceRecorder.Single();
            StringAssert.Contains(trace, typeof(NoDefaultConstructorPlugin).AssemblyQualifiedName);
            StringAssert.Contains(trace, nameof(MissingMethodException));
            CollectionAssert.AreEqual(new[] { "RecordingPlugin.Dispose" }, Events);
        }

        [TestMethod]
        public void Load_ThrowingConstructor_DiscardsPlugin()
        {
            using var host = LoadPlugins(typeof(ThrowingConstructorPlugin));
            CollectionAssert.AreEqual(new[] { "ThrowingConstructorPlugin.Constructor" }, Events);
        }

        [TestMethod]
        public void Load_ThrowingInitialize_DisposesPluginOnceAndDiscardsIt()
        {
            var host = LoadPlugins(typeof(ThrowingInitializePlugin));
            host.Dispose();
            CollectionAssert.AreEqual(new[]
            {
                "ThrowingInitializePlugin.Initialize",
                "ThrowingInitializePlugin.Dispose"
            }, Events);
        }

        [TestMethod]
        public void Dispose_LoadedPlugins_DisposesInReverseOrder()
        {
            var host = LoadPlugins(typeof(RecordingPlugin), typeof(SecondPlugin));
            Events.Clear();
            host.Dispose();
            CollectionAssert.AreEqual(new[] { "SecondPlugin.Dispose", "RecordingPlugin.Dispose" }, Events);
        }

        [TestMethod]
        public void Dispose_ThrowingDispose_DisposesRemainingPlugins()
        {
            var host = LoadPlugins(typeof(RecordingPlugin), typeof(ThrowingDisposePlugin), typeof(SecondPlugin));
            Events.Clear();
            host.Dispose();
            CollectionAssert.AreEqual(new[]
            {
                "SecondPlugin.Dispose",
                "ThrowingDisposePlugin.Dispose",
                "RecordingPlugin.Dispose"
            }, Events);
        }

        [TestMethod]
        public void Dispose_CalledTwice_DisposesPluginsOnce()
        {
            var host = LoadPlugins(typeof(RecordingPlugin));
            Events.Clear();
            host.Dispose();
            host.Dispose();
            CollectionAssert.AreEqual(new[] { "RecordingPlugin.Dispose" }, Events);
        }

        class TraceRecorder : TraceListener
        {
            readonly List<string> messages = new();

            public override void Write(string message)
            {
            }

            public override void WriteLine(string message)
            {
                messages.Add(message);
            }

            public string Single()
            {
                Assert.AreEqual(1, messages.Count, string.Join(Environment.NewLine, messages));
                return messages[0];
            }
        }

        class EmptyServiceProvider : IServiceProvider
        {
            public object GetService(Type serviceType)
            {
                return null;
            }
        }

        public class RecordingPlugin : IEditorPlugin
        {
            public static IServiceProvider Provider;

            public void Initialize(IServiceProvider provider)
            {
                Provider = provider;
                Events.Add($"{GetType().Name}.{nameof(Initialize)}");
            }

            public void Dispose()
            {
                Events.Add($"{GetType().Name}.{nameof(Dispose)}");
            }
        }

        public class SecondPlugin : RecordingPlugin
        {
        }

        public class NotAPlugin
        {
            public NotAPlugin()
            {
                Events.Add($"{nameof(NotAPlugin)}.Constructor");
            }
        }

        public class NoDefaultConstructorPlugin : RecordingPlugin
        {
            public NoDefaultConstructorPlugin(int value)
            {
                Events.Add($"{nameof(NoDefaultConstructorPlugin)}.Constructor");
            }
        }

        public class ThrowingConstructorPlugin : RecordingPlugin
        {
            public ThrowingConstructorPlugin()
            {
                Events.Add($"{nameof(ThrowingConstructorPlugin)}.Constructor");
                throw new InvalidOperationException();
            }
        }

        public class ThrowingInitializePlugin : IEditorPlugin
        {
            public void Initialize(IServiceProvider provider)
            {
                Events.Add($"{nameof(ThrowingInitializePlugin)}.{nameof(Initialize)}");
                throw new InvalidOperationException();
            }

            public void Dispose()
            {
                Events.Add($"{nameof(ThrowingInitializePlugin)}.{nameof(Dispose)}");
            }
        }

        public class ThrowingDisposePlugin : IEditorPlugin
        {
            public void Initialize(IServiceProvider provider)
            {
            }

            public void Dispose()
            {
                Events.Add($"{nameof(ThrowingDisposePlugin)}.{nameof(Dispose)}");
                throw new InvalidOperationException();
            }
        }
    }
}
