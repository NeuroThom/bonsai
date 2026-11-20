using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Bonsai.Design;

namespace Bonsai.Editor
{
    /// <summary>
    /// Adds plugin buttons to the editor toolbar, after the buttons of the editor itself,
    /// isolating every failure in a plugin callback so one broken plugin can never take the
    /// editor down.
    /// </summary>
    sealed class EditorToolbarService : IEditorToolbarService
    {
        readonly ToolStrip toolStrip;

        public EditorToolbarService(ToolStrip toolbar)
        {
            toolStrip = toolbar ?? throw new ArgumentNullException(nameof(toolbar));
        }

        public IEditorToolbarButton AddButton(string text, Image image, Action onClick)
        {
            var button = new ToolStripButton();
            button.DisplayStyle = ToolStripItemDisplayStyle.Image;
            button.ImageTransparentColor = Color.Magenta;
            button.Image = image;
            button.Text = text;
            if (onClick != null)
            {
                button.Click += (sender, e) => InvokeCallback(text, onClick);
            }

            toolStrip.Items.Add(button);
            return new EditorToolbarButton(toolStrip, button);
        }

        static void InvokeCallback(string text, Action onClick)
        {
            try { onClick(); }
            catch (Exception ex)
            {
                Trace.TraceError("Failed to run the editor toolbar button \"{0}\": {1}", text, ex);
            }
        }

        sealed class EditorToolbarButton : IEditorToolbarButton
        {
            readonly ToolStrip toolStrip;
            readonly ToolStripButton button;

            public EditorToolbarButton(ToolStrip toolbar, ToolStripButton toolbarButton)
            {
                toolStrip = toolbar;
                button = toolbarButton;
            }

            public bool Checked
            {
                get { return button.Checked; }
                set { button.Checked = value; }
            }

            public bool Enabled
            {
                get { return button.Enabled; }
                set { button.Enabled = value; }
            }

            public void Remove()
            {
                toolStrip.Items.Remove(button);
                button.Dispose();
            }
        }
    }
}
