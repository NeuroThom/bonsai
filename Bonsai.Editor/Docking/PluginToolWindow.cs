using System;
using System.Drawing;
using System.Windows.Forms;
using Bonsai.Editor.Themes;
using WeifenLuo.WinFormsUI.Docking;

namespace Bonsai.Editor.Docking
{
    internal class PluginToolWindow : EditorToolWindow
    {
        const char ContentTypeSeparator = ':';
        readonly string contentKey;
        bool floatWindowSizeInitialized;

        public PluginToolWindow(IServiceProvider provider, Control control, string key)
            : base(provider)
        {
            Control = control ?? throw new ArgumentNullException(nameof(control));
            contentKey = key ?? throw new ArgumentNullException(nameof(key));
            control.Dock = DockStyle.Fill;
            Controls.Add(control);
        }

        public Control Control { get; }

        public Size DefaultFloatWindowSize { get; set; }

        public static string GetPersistString(string key)
        {
            return nameof(PluginToolWindow) + ContentTypeSeparator + key;
        }

        protected override string GetPersistString()
        {
            return GetPersistString(contentKey);
        }

        protected override void OnDockStateChanged(EventArgs e)
        {
            // The first float window takes the panel default size, so size it for this window;
            // later floats reuse the float window, keeping any size the user has given it.
            if (!floatWindowSizeInitialized && DockState == DockState.Float && FloatPane?.FloatWindow is not null)
            {
                if (!DefaultFloatWindowSize.IsEmpty)
                    FloatPane.FloatWindow.Size = DefaultFloatWindowSize;
                floatWindowSizeInitialized = true;
            }
            base.OnDockStateChanged(e);
        }

        protected override void InitializeTheme(ThemeRenderer themeRenderer)
        {
            var colorTable = themeRenderer.ToolStripRenderer.ColorTable;
            BackColor = colorTable.ContentPanelBackColor;
        }
    }
}
