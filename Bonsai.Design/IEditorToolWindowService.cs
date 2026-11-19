using System.Windows.Forms;

namespace Bonsai.Design
{
    /// <summary>
    /// Provides a docked tool window panel inside the workflow editor where plugins
    /// can host their own user interface.
    /// </summary>
    public interface IEditorToolWindowService
    {
        /// <summary>
        /// Docks the specified control in the editor tool window panel and shows it.
        /// The panel hosts one control at a time; showing another control replaces the
        /// current one without disposing it.
        /// </summary>
        /// <param name="title">The caption of the tool window.</param>
        /// <param name="control">The control to dock in the tool window panel.</param>
        void ShowToolWindow(string title, Control control);

        /// <summary>
        /// Hides the tool window panel if it is currently showing the specified control.
        /// The control is removed from the panel but not disposed.
        /// </summary>
        /// <param name="control">The control to hide.</param>
        void HideToolWindow(Control control);
    }
}
