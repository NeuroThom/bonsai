using System;
using System.Drawing;

namespace Bonsai.Design
{
    /// <summary>
    /// Provides buttons on the workflow editor toolbar where plugins can expose their own
    /// commands, alongside the editor toolbar buttons such as the toggle watch button.
    /// </summary>
    public interface IEditorToolbarService
    {
        /// <summary>
        /// Adds a button to the editor toolbar, after the buttons of the editor itself and
        /// after any button added earlier by a plugin.
        /// </summary>
        /// <param name="text">
        /// The display text of the button, which is also shown as its tooltip.
        /// </param>
        /// <param name="image">The image to display on the button.</param>
        /// <param name="onClick">
        /// The action to call when the button is clicked. Exceptions thrown by the action are
        /// reported in the trace output and do not reach the editor.
        /// </param>
        /// <returns>
        /// A handle used to update the state of the button and to remove it from the toolbar.
        /// </returns>
        IEditorToolbarButton AddButton(string text, Image image, Action onClick);
    }
}
