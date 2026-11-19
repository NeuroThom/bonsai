using System;
using System.Collections.Generic;
using Bonsai.Expressions;

namespace Bonsai.Design
{
    /// <summary>
    /// Provides read only access to the node selection in the workflow editor, for plugins
    /// and other design time components that follow what the user is working on.
    /// </summary>
    /// <remarks>
    /// Workflow and node paths use the editor path format: the zero-based index of each node
    /// in its containing workflow, from the top-level workflow down to the target node,
    /// separated by periods. For example, "2.0" is the first node inside the nested workflow
    /// of the third node in the top-level workflow, and the empty string is the top-level
    /// workflow itself.
    /// </remarks>
    public interface IWorkflowSelectionService
    {
        /// <summary>
        /// Gets the path of the workflow view where the current selection was made, or
        /// <see langword="null"/> if no workflow view is selected.
        /// </summary>
        string SelectedWorkflowPath { get; }

        /// <summary>
        /// Gets the paths of the selected nodes, in the same order as the builders returned
        /// by <see cref="GetSelectedBuilders"/>.
        /// </summary>
        IReadOnlyList<string> SelectedNodePaths { get; }

        /// <summary>
        /// Returns the expression builders of the selected nodes, in the same order as
        /// <see cref="SelectedNodePaths"/>.
        /// </summary>
        /// <returns>
        /// The list of selected expression builders, without any inspection decorators.
        /// </returns>
        IReadOnlyList<ExpressionBuilder> GetSelectedBuilders();

        /// <summary>
        /// Occurs when the selected workflow view or the selected nodes change.
        /// </summary>
        event EventHandler SelectionChanged;
    }
}
