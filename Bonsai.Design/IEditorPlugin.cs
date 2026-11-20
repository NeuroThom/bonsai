using System;

namespace Bonsai.Design
{
    /// <summary>
    /// Represents a plugin which the workflow editor loads at startup. The editor calls
    /// <see cref="Initialize"/> once on the UI thread after the toolbox, the type visualizers
    /// and the initial workflow have been loaded, and before the workflow is started by a
    /// command line option, and disposes the plugin when the editor window closes. A plugin
    /// which throws is discarded without taking the editor down.
    /// </summary>
    public interface IEditorPlugin : IDisposable
    {
        /// <summary>
        /// Initializes the plugin using the specified service provider, which exposes the
        /// editor services available to design time components: <see cref="System.ComponentModel.ISynchronizeInvoke"/>
        /// for the editor window, the <see cref="WorkflowBuilder"/> open in the editor, the
        /// <see cref="Bonsai.Expressions.ExpressionBuilderGraph"/> of the selected view, the
        /// <see cref="CommandExecutor"/> command history, <see cref="IWorkflowEditorState"/>,
        /// <see cref="System.Windows.Forms.Design.IUIService"/>, <see cref="IEditorThemeService"/>,
        /// <see cref="IEditorToolWindowService"/>, <see cref="IEditorToolbarService"/> and
        /// <see cref="IWorkflowSelectionService"/>.
        /// </summary>
        /// <param name="provider">The editor service provider.</param>
        void Initialize(IServiceProvider provider);
    }
}
