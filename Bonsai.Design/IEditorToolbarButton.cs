namespace Bonsai.Design
{
    /// <summary>
    /// Represents a button added to the workflow editor toolbar by a plugin.
    /// </summary>
    public interface IEditorToolbarButton
    {
        /// <summary>
        /// Gets or sets a value indicating whether the button is drawn in the pressed state.
        /// </summary>
        bool Checked { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the button responds to user interaction.
        /// </summary>
        bool Enabled { get; set; }

        /// <summary>
        /// Removes the button from the editor toolbar, leaving the toolbar as it was before
        /// the button was added. Removing a button more than once has no effect.
        /// </summary>
        void Remove();
    }
}
