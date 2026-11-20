using System.Windows.Forms;

namespace Bonsai.Design
{
    /// <summary>
    /// Provides dockable tool windows inside the workflow editor where plugins can host
    /// their own user interface, alongside the editor tool windows such as the find results
    /// and watch windows.
    /// </summary>
    public interface IEditorToolWindowService
    {
        /// <summary>
        /// Shows the specified control in an editor tool window and activates it. The first
        /// time a control is shown, a new tool window is created for it and docked along the
        /// bottom of the workflow editor, from where it can be moved, floated, auto hidden or
        /// grouped with other tool windows; showing the control again restores the tool
        /// window to its last position and updates its caption.
        /// </summary>
        /// <param name="title">The caption of the tool window.</param>
        /// <param name="control">The control to host in the tool window.</param>
        void ShowToolWindow(string title, Control control);

        /// <summary>
        /// Hides the tool window hosting the specified control, if any. The control stays in
        /// the tool window and is not disposed. Closing a tool window from its caption also
        /// hides it. Disposing the control removes its tool window from the editor.
        /// </summary>
        /// <param name="control">The control to hide.</param>
        void HideToolWindow(Control control);
    }
}
