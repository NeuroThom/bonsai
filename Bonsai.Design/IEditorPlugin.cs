using System;

namespace Bonsai.Design
{
    /// <summary>
    /// Represents a plugin which the workflow editor loads at startup. The editor calls
    /// <see cref="Initialize"/> once on the UI thread after the workflow and toolbox have
    /// been initialized, and disposes the plugin when the editor window closes. A plugin
    /// which throws is discarded without taking the editor down.
    /// </summary>
    public interface IEditorPlugin : IDisposable
    {
        /// <summary>
        /// Initializes the plugin using the specified service provider, which exposes
        /// editor services such as the workflow builder and the selection model.
        /// </summary>
        /// <param name="provider">The editor service provider.</param>
        void Initialize(IServiceProvider provider);
    }
}
