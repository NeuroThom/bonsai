using System.Collections.Generic;

namespace Bonsai.Design
{
    /// <summary>
    /// Provides path addressed, undoable edits to the workflow the user has open in the
    /// editor, for plugins and other design time components which need to modify the
    /// workflow on behalf of the user.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Workflow and node paths use the editor path format: the zero-based index of each node
    /// in its containing workflow, from the top-level workflow down to the target node,
    /// separated by periods. For example, "2.0" is the first node inside the nested workflow
    /// of the third node in the top-level workflow, and the empty string is the top-level
    /// workflow itself. Paths are passed as strings because the type representing an editor
    /// path is internal to the workflow editor and is not visible to the design time
    /// components which use this service.
    /// </para>
    /// <para>
    /// Node indices change as nodes are added, removed and connected, so the paths returned
    /// by one edit are only valid until the next edit is made. Every edit must be made
    /// inside a transaction started by <see cref="BeginEdit"/>, so that a batch of edits
    /// appears as a single entry in the editor undo history. Edits which cannot be applied
    /// raise a <see cref="WorkflowEditException"/> instead of reporting the error to the
    /// user, and leave the workflow unchanged.
    /// </para>
    /// </remarks>
    public interface IWorkflowEditingService
    {
        /// <summary>
        /// Gets a value indicating whether the workflow can currently be edited. The
        /// workflow cannot be edited while it is running.
        /// </summary>
        bool CanEdit { get; }

        /// <summary>
        /// Returns the XML representation of the workflow at the specified path, in the same
        /// format used to store workflow files.
        /// </summary>
        /// <param name="workflowPath">
        /// The path of the workflow to serialize, or an empty string to serialize the
        /// top-level workflow.
        /// </param>
        /// <returns>A string containing the XML representation of the workflow.</returns>
        string GetWorkflowXml(string workflowPath);

        /// <summary>
        /// Returns the operators in the editor toolbox which match the specified query,
        /// ordered from the closest to the loosest match.
        /// </summary>
        /// <param name="query">
        /// The fully qualified name, the qualified name, the display name, or part of the
        /// name of the operators to find.
        /// </param>
        /// <param name="maxResults">The maximum number of operators to return.</param>
        /// <returns>The list of operators matching the query.</returns>
        IReadOnlyList<WorkflowOperatorDescriptor> FindOperators(string query, int maxResults);

        /// <summary>
        /// Starts a batch of edits which are applied to the workflow as a single undoable
        /// operation.
        /// </summary>
        /// <remarks>
        /// The returned transaction must be either committed or disposed, and only one
        /// transaction can be open at a time. Disposing the transaction without committing
        /// it reverses every edit made since it was started. A transaction which is dropped
        /// without being committed or disposed is closed when the editor next notices it,
        /// which leaves the edits made so far applied to the workflow and recorded in the
        /// undo history as a single entry, since the user may have made edits of their own
        /// in the meantime. Committing such a transaction afterwards raises an
        /// <see cref="System.InvalidOperationException"/>.
        /// </remarks>
        /// <param name="description">A short description of the batch of edits.</param>
        /// <returns>
        /// The transaction representing the batch of edits. Disposing the transaction
        /// without committing it reverses every edit made since it was started.
        /// </returns>
        IWorkflowEditTransaction BeginEdit(string description);

        /// <summary>
        /// Creates a new node for the specified operator in the workflow at the specified
        /// path.
        /// </summary>
        /// <param name="workflowPath">
        /// The path of the workflow in which to create the node, or an empty string for the
        /// top-level workflow.
        /// </param>
        /// <param name="operatorName">
        /// The name of the operator, as accepted by <see cref="FindOperators"/>. The name
        /// must match exactly one operator in the editor toolbox.
        /// </param>
        /// <param name="targetNodePath">
        /// The path of the node to connect the new node to, or a null reference
        /// (Nothing in Visual Basic) to create the node without any connections.
        /// </param>
        /// <param name="branch">
        /// <see langword="true"/> to connect the new node as a new branch of the target
        /// node; <see langword="false"/> to insert the new node into the chain of the
        /// target node.
        /// </param>
        /// <returns>The path of the node which was created.</returns>
        string CreateNode(string workflowPath, string operatorName, string targetNodePath, bool branch);

        /// <summary>
        /// Creates a new node including the workflow stored in the specified file, in the
        /// workflow at the specified path.
        /// </summary>
        /// <param name="workflowPath">
        /// The path of the workflow in which to create the node, or an empty string for the
        /// top-level workflow.
        /// </param>
        /// <param name="includePath">
        /// The path of the workflow file to include, relative to the project folder.
        /// </param>
        /// <param name="targetNodePath">
        /// The path of the node to connect the new node to, or a null reference
        /// (Nothing in Visual Basic) to create the node without any connections.
        /// </param>
        /// <param name="branch">
        /// <see langword="true"/> to connect the new node as a new branch of the target
        /// node; <see langword="false"/> to insert the new node into the chain of the
        /// target node.
        /// </param>
        /// <returns>The path of the node which was created.</returns>
        string CreateInclude(string workflowPath, string includePath, string targetNodePath, bool branch);

        /// <summary>
        /// Creates a new node subscribing to, or broadcasting into, the specified subject,
        /// in the workflow at the specified path.
        /// </summary>
        /// <param name="workflowPath">
        /// The path of the workflow in which to create the node, or an empty string for the
        /// top-level workflow.
        /// </param>
        /// <param name="subjectName">The name of the subject.</param>
        /// <param name="multicast">
        /// <see langword="true"/> to broadcast the sequence of the target node into the
        /// subject; <see langword="false"/> to subscribe to the subject.
        /// </param>
        /// <param name="targetNodePath">
        /// The path of the node to connect the new node to, or a null reference
        /// (Nothing in Visual Basic) to create the node without any connections.
        /// </param>
        /// <param name="branch">
        /// <see langword="true"/> to connect the new node as a new branch of the target
        /// node; <see langword="false"/> to insert the new node into the chain of the
        /// target node.
        /// </param>
        /// <returns>The path of the node which was created.</returns>
        string CreateSubject(string workflowPath, string subjectName, bool multicast, string targetNodePath, bool branch);

        /// <summary>
        /// Connects the output of the source node to the input of the target node. Both
        /// nodes must belong to the same workflow.
        /// </summary>
        /// <param name="sourceNodePath">The path of the node providing the sequence.</param>
        /// <param name="targetNodePath">The path of the node receiving the sequence.</param>
        void Connect(string sourceNodePath, string targetNodePath);

        /// <summary>
        /// Removes the connection between the source node and the target node. Both nodes
        /// must belong to the same workflow.
        /// </summary>
        /// <param name="sourceNodePath">The path of the node providing the sequence.</param>
        /// <param name="targetNodePath">The path of the node receiving the sequence.</param>
        void Disconnect(string sourceNodePath, string targetNodePath);

        /// <summary>
        /// Deletes the specified nodes, reconnecting the remaining nodes where possible.
        /// All the nodes must belong to the same workflow.
        /// </summary>
        /// <param name="nodePaths">The paths of the nodes to delete.</param>
        void Delete(IReadOnlyList<string> nodePaths);

        /// <summary>
        /// Assigns a new value to the specified property of the operator in the specified
        /// node.
        /// </summary>
        /// <param name="nodePath">The path of the node to modify.</param>
        /// <param name="propertyName">The name of the property to assign.</param>
        /// <param name="value">
        /// The string representation of the new value, as it would be typed in the editor
        /// property grid.
        /// </param>
        void SetProperty(string nodePath, string propertyName, string value);

        /// <summary>
        /// Groups the specified nodes into a single nested workflow. All the nodes must
        /// belong to the same workflow.
        /// </summary>
        /// <param name="nodePaths">The paths of the nodes to group.</param>
        /// <returns>The path of the node containing the new nested workflow.</returns>
        string Group(IReadOnlyList<string> nodePaths);

        /// <summary>
        /// Replaces the specified nested workflow nodes by their contents. All the nodes
        /// must belong to the same workflow.
        /// </summary>
        /// <param name="nodePaths">The paths of the nodes to ungroup.</param>
        void Ungroup(IReadOnlyList<string> nodePaths);

        /// <summary>
        /// Enables the specified nodes. All the nodes must belong to the same workflow.
        /// </summary>
        /// <param name="nodePaths">The paths of the nodes to enable.</param>
        void Enable(IReadOnlyList<string> nodePaths);

        /// <summary>
        /// Disables the specified nodes, so that they are excluded when the workflow is
        /// built. All the nodes must belong to the same workflow.
        /// </summary>
        /// <param name="nodePaths">The paths of the nodes to disable.</param>
        void Disable(IReadOnlyList<string> nodePaths);
    }
}
