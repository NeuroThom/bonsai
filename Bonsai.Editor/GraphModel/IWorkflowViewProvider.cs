namespace Bonsai.Editor.GraphModel
{
    /// <summary>
    /// Provides access to the workflow views which are open in the editor, so that edits
    /// requested by a plugin are applied to the workflow the user can see.
    /// </summary>
    interface IWorkflowViewProvider
    {
        /// <summary>
        /// Returns the workflow editor showing the workflow at the specified path. A view
        /// which already shows the workflow is preferred, otherwise the active view is
        /// navigated to the workflow, or a new view is created for it.
        /// </summary>
        /// <param name="workflowPath">
        /// The path of the workflow to show, or a null reference (Nothing in Visual Basic)
        /// for the top-level workflow.
        /// </param>
        /// <returns>The workflow editor showing the specified workflow.</returns>
        WorkflowEditor GetWorkflowEditor(WorkflowEditorPath workflowPath);

        /// <summary>
        /// Invalidates the layout of every open workflow view, so that node captions are
        /// redrawn after the properties of an operator have changed.
        /// </summary>
        void InvalidateLayout();
    }
}
