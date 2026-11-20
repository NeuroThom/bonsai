using System;

namespace Bonsai.Design
{
    /// <summary>
    /// Specifies the color theme of the workflow editor.
    /// </summary>
    public enum EditorColorTheme
    {
        /// <summary>
        /// The light color theme.
        /// </summary>
        Light,

        /// <summary>
        /// The dark color theme.
        /// </summary>
        Dark
    }

    /// <summary>
    /// Provides access to the color theme of the workflow editor, for plugins and
    /// other design time components that follow the editor appearance.
    /// </summary>
    public interface IEditorThemeService
    {
        /// <summary>
        /// Gets the active color theme of the editor.
        /// </summary>
        EditorColorTheme ActiveTheme { get; }

        /// <summary>
        /// Occurs when the active color theme of the editor changes.
        /// </summary>
        event EventHandler ThemeChanged;
    }
}
