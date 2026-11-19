using System;
using System.Drawing;
using System.Windows.Forms;
using Bonsai.Editor.Themes;

namespace Bonsai.Editor
{
    /// <summary>
    /// Hosts a single plugin control in a tool window panel docked to the right of
    /// the editor content, restructuring the form layout the first time it is shown.
    /// </summary>
    sealed class ToolWindowHost
    {
        const int DefaultToolWindowWidth = 480;
        const int MinimumToolWindowWidth = 320;
        const float DefaultDpi = 96f;
        readonly Control content;
        readonly ThemeRenderer themeRenderer;
        SplitContainer splitContainer;
        Panel captionPanel;
        Label captionLabel;
        Button closeButton;
        Panel hostPanel;
        Control activeControl;
        bool sizeInitialized;

        public ToolWindowHost(Control content, ThemeRenderer themeRenderer)
        {
            this.content = content ?? throw new ArgumentNullException(nameof(content));
            this.themeRenderer = themeRenderer ?? throw new ArgumentNullException(nameof(themeRenderer));
        }

        public void ShowToolWindow(string title, Control control)
        {
            if (control == null)
            {
                throw new ArgumentNullException(nameof(control));
            }

            EnsureSplitContainer();
            if (activeControl != control)
            {
                RemoveActiveControl();
                control.Dock = DockStyle.Fill;
                hostPanel.Controls.Add(control);
                activeControl = control;
            }

            captionLabel.Text = title;
            splitContainer.Panel2Collapsed = false;
            if (!sizeInitialized)
            {
                InitializeSize();
            }

            control.Focus();
        }

        public void HideToolWindow(Control control)
        {
            if (control == null)
            {
                throw new ArgumentNullException(nameof(control));
            }

            if (control == activeControl)
            {
                splitContainer.Panel2Collapsed = true;
                RemoveActiveControl();
            }
        }

        void RemoveActiveControl()
        {
            if (activeControl != null)
            {
                hostPanel.Controls.Remove(activeControl);
                activeControl = null;
            }
        }

        void EnsureSplitContainer()
        {
            if (splitContainer != null)
            {
                return;
            }

            var labelHeight = themeRenderer.LabelHeight;
            captionLabel = new Label();
            captionLabel.AutoEllipsis = true;
            captionLabel.Dock = DockStyle.Fill;
            captionLabel.TextAlign = ContentAlignment.MiddleLeft;

            closeButton = new Button();
            closeButton.Dock = DockStyle.Right;
            closeButton.FlatStyle = FlatStyle.Flat;
            closeButton.FlatAppearance.BorderSize = 0;
            closeButton.TabStop = false;
            closeButton.Text = "×";
            closeButton.Width = labelHeight;
            closeButton.Click += closeButton_Click;

            captionPanel = new Panel();
            captionPanel.Dock = DockStyle.Top;
            captionPanel.Height = labelHeight;
            captionPanel.Controls.Add(captionLabel);
            captionPanel.Controls.Add(closeButton);

            hostPanel = new Panel();
            hostPanel.Dock = DockStyle.Fill;

            var parent = content.Parent;
            var dock = content.Dock;
            var childIndex = parent.Controls.GetChildIndex(content);
            splitContainer = new SplitContainer();
            splitContainer.Orientation = Orientation.Vertical;
            splitContainer.FixedPanel = FixedPanel.Panel2;
            splitContainer.Size = content.Size;
            splitContainer.Panel2.Controls.Add(hostPanel);
            splitContainer.Panel2.Controls.Add(captionPanel);
            splitContainer.Panel2Collapsed = true;

            parent.SuspendLayout();
            parent.Controls.Remove(content);
            content.Dock = DockStyle.Fill;
            splitContainer.Panel1.Controls.Add(content);
            splitContainer.Dock = dock;
            parent.Controls.Add(splitContainer);
            parent.Controls.SetChildIndex(splitContainer, childIndex);
            parent.ResumeLayout();

            InitializeTheme();
            themeRenderer.ThemeChanged += themeRenderer_ThemeChanged;
        }

        void InitializeSize()
        {
            // Sized when the panel is first shown, against the current editor layout, with
            // pixel sizes scaled to the screen resolution.
            float scale;
            using (var graphics = splitContainer.CreateGraphics())
            {
                scale = graphics.DpiX / DefaultDpi;
            }

            var toolWindowWidth = Math.Min((int)(DefaultToolWindowWidth * scale), splitContainer.Width / 2);
            splitContainer.Panel2MinSize = Math.Min((int)(MinimumToolWindowWidth * scale), toolWindowWidth);
            splitContainer.SplitterDistance = Math.Max(
                splitContainer.Panel1MinSize,
                splitContainer.Width - toolWindowWidth - splitContainer.SplitterWidth);
            sizeInitialized = true;
        }

        void InitializeTheme()
        {
            // Same colors as the toolbox, explorer and properties captions.
            var colorTable = themeRenderer.ToolStripRenderer.ColorTable;
            captionPanel.BackColor = colorTable.SeparatorDark;
            captionPanel.ForeColor = colorTable.ControlForeColor;
            closeButton.FlatAppearance.MouseOverBackColor = colorTable.ButtonSelectedHighlight;
            hostPanel.BackColor = colorTable.ContentPanelBackColor;
        }

        private void closeButton_Click(object sender, EventArgs e)
        {
            if (activeControl != null)
            {
                HideToolWindow(activeControl);
            }
        }

        private void themeRenderer_ThemeChanged(object sender, EventArgs e)
        {
            InitializeTheme();
        }
    }
}
