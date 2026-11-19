using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms.Design;
using Bonsai.Design;
using Bonsai.Expressions;

namespace Bonsai.Editor.GraphModel
{
    /// <summary>
    /// Applies path addressed, undoable edits to the workflow the user has open in the
    /// editor, through the same command path used by the editor itself.
    /// </summary>
    class WorkflowEditingService : IWorkflowEditingService
    {
        const int MaxCandidates = 10;
        const string NameSeparator = ".";
        static readonly Action EmptyAction = () => { };

        readonly IServiceProvider serviceProvider;
        readonly IWorkflowViewProvider viewProvider;
        readonly CommandExecutor commandExecutor;
        readonly IWorkflowEditorService editorService;
        readonly IWorkflowEditorState editorState;
        readonly IWorkflowToolboxService toolboxService;
        readonly ISynchronizeInvoke synchronizingObject;
        readonly IUIService uiService;
        readonly Action recoverTransaction;
        WeakReference<WorkflowEditTransaction> activeTransaction;
        Exception pendingError;
        bool executedCommand;
        bool recoveryPending;
        int transactionDepth;

        public WorkflowEditingService(IServiceProvider provider, IWorkflowViewProvider views)
        {
            serviceProvider = provider ?? throw new ArgumentNullException(nameof(provider));
            viewProvider = views ?? throw new ArgumentNullException(nameof(views));
            commandExecutor = (CommandExecutor)provider.GetService(typeof(CommandExecutor));
            if (commandExecutor == null)
            {
                throw new ArgumentException("The service provider must provide a command executor.", nameof(provider));
            }

            editorService = (IWorkflowEditorService)provider.GetService(typeof(IWorkflowEditorService));
            if (editorService == null)
            {
                throw new ArgumentException("The service provider must provide a workflow editor service.", nameof(provider));
            }

            editorState = (IWorkflowEditorState)provider.GetService(typeof(IWorkflowEditorState));
            toolboxService = (IWorkflowToolboxService)provider.GetService(typeof(IWorkflowToolboxService));
            synchronizingObject = (ISynchronizeInvoke)provider.GetService(typeof(ISynchronizeInvoke));
            uiService = (IUIService)provider.GetService(typeof(IUIService));
            recoverTransaction = OnRecoveryRequest;
        }

        public bool CanEdit
        {
            get
            {
                RecoverAbandonedTransaction();
                return editorState == null || !editorState.WorkflowRunning;
            }
        }

        // Collects errors raised by the workflow editor while an edit transaction is in
        // progress, so that they are reported to the caller instead of being shown in a
        // message box on the user interface thread.
        internal bool TryHandleError(Exception ex)
        {
            if (!HasActiveTransaction)
            {
                // the error belongs to the user, not to a plugin, so let it reach the dialog
                // and schedule the recovery of any transaction the caller dropped
                RequestRecovery();
                return false;
            }

            pendingError ??= ex;
            return true;
        }

        #region Transactions

        // A transaction which the caller dropped without committing or disposing it would
        // otherwise keep the composite command open and keep swallowing editor errors for
        // the rest of the session, so the transaction is held weakly and is considered gone
        // as soon as the weak reference is cleared.
        private bool HasActiveTransaction
        {
            get
            {
                var transaction = activeTransaction;
                return transaction != null && transaction.TryGetTarget(out _);
            }
        }

        // Closes the composite command of a transaction which was abandoned by the caller.
        // The edits are left applied and recorded as a single entry the user can undo
        // deliberately, because the user may have made edits of their own which nested into
        // the same composite, and undoing here would destroy them without any redo.
        private void RecoverAbandonedTransaction()
        {
            if (activeTransaction == null || HasActiveTransaction)
            {
                return;
            }

            if (commandExecutor.CompositeDepth != transactionDepth)
            {
                // the editor has opened a composite of its own on top of the abandoned one,
                // and ending ours now would close theirs instead, so leave it for the next
                // operation to recover
                return;
            }

            EndEdit(rollback: false);
        }

        // Ends a transaction the caller completed itself. Returns false when the transaction
        // was already recovered, so that completing it cannot close a composite command
        // which now belongs to somebody else.
        private bool CompleteTransaction(WorkflowEditTransaction transaction, bool rollback)
        {
            var active = activeTransaction;
            if (active == null || !active.TryGetTarget(out var current) || current != transaction)
            {
                return false;
            }

            EndEdit(rollback);
            return true;
        }

        // An abandoned transaction is noticed while an editor command is on the stack, where
        // closing the composite is not safe, so the recovery is posted to the editor thread
        // instead, to run once that command has unwound.
        private void RequestRecovery()
        {
            if (activeTransaction == null || synchronizingObject == null || recoveryPending)
            {
                return;
            }

            recoveryPending = true;
            try { synchronizingObject.BeginInvoke(recoverTransaction, null); }
            catch (InvalidOperationException)
            {
                // the editor is shutting down and there is nothing left to recover
                recoveryPending = false;
            }
        }

        private void OnRecoveryRequest()
        {
            recoveryPending = false;
            RecoverAbandonedTransaction();
        }

        public IWorkflowEditTransaction BeginEdit(string description)
        {
            RecoverAbandonedTransaction();
            if (HasActiveTransaction)
            {
                throw new InvalidOperationException("A workflow edit transaction is already in progress.");
            }

            if (!CanEdit)
            {
                throw new WorkflowEditException("The workflow cannot be edited while it is running.");
            }

            pendingError = null;
            executedCommand = false;
            commandExecutor.StatusChanged += CommandExecutor_StatusChanged;
            commandExecutor.BeginCompositeCommand();
            transactionDepth = commandExecutor.CompositeDepth;
            var transaction = new WorkflowEditTransaction(this, description);
            activeTransaction = new WeakReference<WorkflowEditTransaction>(transaction);
            return transaction;
        }

        private void CommandExecutor_StatusChanged(object sender, EventArgs e)
        {
            executedCommand = true;
        }

        private void EndEdit(bool rollback)
        {
            try
            {
                commandExecutor.EndCompositeCommand();
                if (rollback && executedCommand && commandExecutor.CanUndo)
                {
                    // the editor service compensates the dirty state version counter, so that
                    // rolling back a batch of edits leaves the workflow exactly as it was
                    editorService.Undo(allowRedo: false);
                }
            }
            finally
            {
                commandExecutor.StatusChanged -= CommandExecutor_StatusChanged;
                activeTransaction = null;
                pendingError = null;
                executedCommand = false;
                recoveryPending = false;
                transactionDepth = 0;
            }
        }

        // Validates that an edit transaction is in progress, and discards any error left
        // over from a previous operation in the same batch.
        private void BeginOperation()
        {
            RecoverAbandonedTransaction();
            if (!HasActiveTransaction)
            {
                throw new InvalidOperationException($"{nameof(BeginEdit)} must be called before editing the workflow.");
            }

            pendingError = null;
        }

        private void ThrowPendingError()
        {
            var error = pendingError;
            if (error != null)
            {
                pendingError = null;
                throw new WorkflowEditException(error.Message, error);
            }
        }

        #endregion

        #region Path Resolution

        private WorkflowBuilder GetWorkflowBuilder()
        {
            var workflowBuilder = (WorkflowBuilder)serviceProvider.GetService(typeof(WorkflowBuilder));
            if (workflowBuilder == null)
            {
                throw new WorkflowEditException("There is no workflow open in the editor.");
            }

            return workflowBuilder;
        }

        private static WorkflowEditorPath ParsePath(string path)
        {
            try { return WorkflowEditorPath.Parse(path); }
            catch (Exception ex) when (ex is FormatException || ex is OverflowException)
            {
                throw new WorkflowEditException($"The path '{path}' is not a valid workflow editor path.", ex);
            }
        }

        private static WorkflowEditorPath ParseNodePath(string nodePath)
        {
            var path = ParsePath(nodePath);
            if (path == null)
            {
                throw new WorkflowEditException("The path of a node cannot be empty.");
            }

            return path;
        }

        private ExpressionBuilder ResolveNode(WorkflowEditorPath nodePath)
        {
            try { return nodePath.Resolve(GetWorkflowBuilder()); }
            catch (ArgumentException ex)
            {
                throw new WorkflowEditException($"There is no node at the path '{nodePath}'.", ex);
            }
        }

        private ExpressionBuilderGraph ResolveWorkflow(WorkflowEditorPath workflowPath)
        {
            var workflowBuilder = GetWorkflowBuilder();
            if (workflowPath == null)
            {
                return workflowBuilder.Workflow;
            }

            var builder = ResolveNode(workflowPath);
            if (ExpressionBuilder.GetWorkflowElement(builder) is not IWorkflowExpressionBuilder nestedBuilder ||
                nestedBuilder.Workflow == null)
            {
                throw new WorkflowEditException($"The node at the path '{workflowPath}' does not contain a nested workflow.");
            }

            return nestedBuilder.Workflow;
        }

        private void EnsureEditable(WorkflowEditorPath workflowPath)
        {
            if (workflowPath == null)
            {
                return;
            }

            var workflowBuilder = GetWorkflowBuilder();
            WorkflowPathFlags pathFlags;
            try { workflowPath.Resolve(workflowBuilder, out pathFlags); }
            catch (ArgumentException ex)
            {
                throw new WorkflowEditException($"There is no workflow at the path '{workflowPath}'.", ex);
            }

            if ((pathFlags & WorkflowPathFlags.ReadOnly) != 0)
            {
                var includePath = GetIncludeWorkflowPath(workflowBuilder, workflowPath);
                var message = string.IsNullOrEmpty(includePath)
                    ? $"The workflow at the path '{workflowPath}' is included from another file and cannot be edited here."
                    : $"The workflow at the path '{workflowPath}' is included from the file '{includePath}'. Edit that file instead.";
                throw new WorkflowEditException(message);
            }
        }

        private static string GetIncludeWorkflowPath(WorkflowBuilder workflowBuilder, WorkflowEditorPath workflowPath)
        {
            var workflow = workflowBuilder.Workflow;
            foreach (var pathElement in workflowPath.GetPathElements())
            {
                if (workflow == null) break;
                var builder = ExpressionBuilder.Unwrap(workflow[pathElement.Index].Value);
                if (builder is DisableBuilder disableBuilder)
                {
                    builder = disableBuilder.Builder;
                }

                if (builder is IWorkflowExpressionBuilder nestedBuilder)
                {
                    if (nestedBuilder is IncludeWorkflowBuilder includeBuilder)
                    {
                        return includeBuilder.Path;
                    }

                    workflow = nestedBuilder.Workflow;
                }
                else workflow = null;
            }

            return null;
        }

        private string GetNodePath(ExpressionBuilder builder)
        {
            var path = WorkflowEditorPath.GetBuilderPath(GetWorkflowBuilder(), builder);
            if (path == null)
            {
                throw new WorkflowEditException("The node is no longer part of the workflow.");
            }

            return path.ToString();
        }

        #endregion

        #region Workflow Views

        private WorkflowEditor GetWorkflowEditor(WorkflowEditorPath workflowPath)
        {
            var editor = viewProvider.GetWorkflowEditor(workflowPath);
            if (editor == null)
            {
                throw new WorkflowEditException("There is no workflow view available to apply the edit.");
            }

            ThrowPendingError();
            return editor;
        }

        private static GraphNode FindGraphNode(WorkflowEditor editor, ExpressionBuilder builder, WorkflowEditorPath nodePath)
        {
            var graphNode = editor.FindGraphNode(builder);
            if (graphNode == null)
            {
                throw new WorkflowEditException($"The node at the path '{nodePath}' is not visible in the workflow view.");
            }

            return graphNode;
        }

        private static int GetInsertIndex(WorkflowEditor editor, GraphNode targetNode)
        {
            var workflow = editor.Workflow;
            if (targetNode != null)
            {
                for (int i = 0; i < workflow.Count; i++)
                {
                    if (workflow[i].Value == targetNode.Value)
                    {
                        return i + 1;
                    }
                }
            }

            return workflow.Count;
        }

        #endregion

        #region Toolbox

        private IEnumerable<WorkflowElementDescriptor> GetToolboxElements()
        {
            return toolboxService?.GetToolboxElements() ?? Enumerable.Empty<WorkflowElementDescriptor>();
        }

        private static string GetQualifiedName(WorkflowElementDescriptor element)
        {
            return string.IsNullOrEmpty(element.Namespace)
                ? element.Name
                : element.Namespace + NameSeparator + element.Name;
        }

        private static ElementCategory GetElementCategory(WorkflowElementDescriptor element)
        {
            var elementTypes = element.ElementTypes;
            if (elementTypes != null)
            {
                for (int i = 0; i < elementTypes.Length; i++)
                {
                    if (elementTypes[i] == ElementCategory.Nested) continue;
                    return elementTypes[i];
                }
            }

            return ElementCategory.Combinator;
        }

        private List<WorkflowElementDescriptor> FindMatches(string query)
        {
            var fullyQualifiedMatches = new List<WorkflowElementDescriptor>();
            var qualifiedMatches = new List<WorkflowElementDescriptor>();
            var nameMatches = new List<WorkflowElementDescriptor>();
            var partialMatches = new List<WorkflowElementDescriptor>();
            foreach (var element in GetToolboxElements())
            {
                var qualifiedName = GetQualifiedName(element);
                if (string.Equals(element.FullyQualifiedName, query, StringComparison.Ordinal))
                {
                    fullyQualifiedMatches.Add(element);
                }
                else if (string.Equals(qualifiedName, query, StringComparison.OrdinalIgnoreCase))
                {
                    qualifiedMatches.Add(element);
                }
                else if (string.Equals(element.Name, query, StringComparison.OrdinalIgnoreCase))
                {
                    nameMatches.Add(element);
                }
                else if (qualifiedName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    partialMatches.Add(element);
                }
            }

            if (fullyQualifiedMatches.Count > 0) return fullyQualifiedMatches;
            if (qualifiedMatches.Count > 0) return qualifiedMatches;
            if (nameMatches.Count > 0) return nameMatches;
            return partialMatches;
        }

        private List<string> GetSuggestions(string query)
        {
            // when nothing matches, suggest the operators sharing the start of the name the
            // caller asked for, which is the most common way of getting a name slightly wrong
            var separatorIndex = query.LastIndexOf(NameSeparator, StringComparison.Ordinal);
            var name = separatorIndex >= 0 ? query.Substring(separatorIndex + 1) : query;
            var prefix = name.Length > 3 ? name.Substring(0, 3) : name;
            var suggestions = new List<string>();
            if (prefix.Length == 0)
            {
                return suggestions;
            }

            foreach (var element in GetToolboxElements())
            {
                if (suggestions.Count >= MaxCandidates) break;
                if (element.Name != null && element.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    suggestions.Add(GetQualifiedName(element));
                }
            }

            return suggestions;
        }

        private static List<string> GetCandidates(List<WorkflowElementDescriptor> matches)
        {
            var candidates = new List<string>(Math.Min(matches.Count, MaxCandidates));
            for (int i = 0; i < matches.Count && i < MaxCandidates; i++)
            {
                candidates.Add(GetQualifiedName(matches[i]));
            }

            return candidates;
        }

        private WorkflowElementDescriptor ResolveOperator(string operatorName)
        {
            if (string.IsNullOrEmpty(operatorName))
            {
                throw new WorkflowEditException("The name of the operator cannot be empty.");
            }

            var matches = FindMatches(operatorName);
            if (matches.Count == 1)
            {
                return matches[0];
            }

            if (matches.Count == 0)
            {
                var suggestions = GetSuggestions(operatorName);
                throw new WorkflowEditException(
                    FormatCandidates($"No operator named '{operatorName}' was found in the toolbox.", suggestions),
                    suggestions);
            }

            var candidates = GetCandidates(matches);
            throw new WorkflowEditException(
                FormatCandidates($"The operator name '{operatorName}' is ambiguous.", candidates),
                candidates);
        }

        private static string FormatCandidates(string message, List<string> candidates)
        {
            if (candidates.Count == 0)
            {
                return message;
            }

            var builder = new StringBuilder(message);
            builder.Append(" Candidates: ");
            builder.Append(string.Join(", ", candidates));
            builder.Append('.');
            return builder.ToString();
        }

        public IReadOnlyList<WorkflowOperatorDescriptor> FindOperators(string query, int maxResults)
        {
            RecoverAbandonedTransaction();
            if (string.IsNullOrEmpty(query))
            {
                throw new ArgumentException("The query cannot be empty.", nameof(query));
            }

            if (maxResults <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxResults));
            }

            var matches = FindMatches(query);
            var result = new List<WorkflowOperatorDescriptor>(Math.Min(matches.Count, maxResults));
            for (int i = 0; i < matches.Count && i < maxResults; i++)
            {
                var element = matches[i];
                result.Add(new WorkflowOperatorDescriptor(
                    element.Name,
                    element.Namespace,
                    element.FullyQualifiedName,
                    element.Description,
                    GetElementCategory(element)));
            }

            return result;
        }

        #endregion

        #region Workflow Serialization

        public string GetWorkflowXml(string workflowPath)
        {
            RecoverAbandonedTransaction();
            var path = ParsePath(workflowPath);
            var workflow = ResolveWorkflow(path);
            var serializerWorkflowBuilder = new WorkflowBuilder(workflow.FromInspectableGraph());
            using var memoryStream = new MemoryStream();
            // the same writer settings as a saved workflow file, so that the XML returned
            // here is exactly what the editor would write to disk
            using (var writer = XmlnsIndentedWriter.Create(memoryStream, ElementStore.DefaultWriterSettings))
            {
                WorkflowBuilder.Serializer.Serialize(writer, serializerWorkflowBuilder);
            }

            memoryStream.Position = 0;
            using var reader = new StreamReader(memoryStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }

        #endregion

        #region Create Nodes

        public string CreateNode(string workflowPath, string operatorName, string targetNodePath, bool branch)
        {
            BeginOperation();
            var element = ResolveOperator(operatorName);
            var builder = CreateBuilder(
                element.FullyQualifiedName,
                GetElementCategory(element),
                group: false,
                GetQualifiedName(element));
            return CreateNode(workflowPath, builder, targetNodePath, branch);
        }

        public string CreateInclude(string workflowPath, string includePath, string targetNodePath, bool branch)
        {
            BeginOperation();
            if (string.IsNullOrEmpty(includePath))
            {
                throw new WorkflowEditException("The path of the workflow to include cannot be empty.");
            }

            var builder = CreateBuilder(includePath, ~ElementCategory.Workflow, group: false, includePath);
            return CreateNode(workflowPath, builder, targetNodePath, branch);
        }

        public string CreateSubject(string workflowPath, string subjectName, bool multicast, string targetNodePath, bool branch)
        {
            BeginOperation();
            if (string.IsNullOrEmpty(subjectName))
            {
                throw new WorkflowEditException("The name of the subject cannot be empty.");
            }

            var builder = CreateBuilder(subjectName, ~ElementCategory.Source, multicast, subjectName);
            return CreateNode(workflowPath, builder, targetNodePath, branch);
        }

        private static ExpressionBuilder CreateBuilder(string typeName, ElementCategory elementCategory, bool group, string displayName)
        {
            try { return WorkflowEditor.CreateBuilder(typeName, elementCategory, group); }
            catch (Exception ex) when (ex is ArgumentException || ex is MissingMemberException || ex is TargetInvocationException)
            {
                throw new WorkflowEditException($"The operator '{displayName}' could not be created. {ex.Message}", ex);
            }
        }

        private string CreateNode(string workflowPath, ExpressionBuilder builder, string targetNodePath, bool branch)
        {
            var path = ParsePath(workflowPath);
            EnsureEditable(path);

            var targetNode = default(GraphNode);
            var targetBuilder = default(ExpressionBuilder);
            var targetPath = default(WorkflowEditorPath);
            if (!string.IsNullOrEmpty(targetNodePath))
            {
                targetPath = ParseNodePath(targetNodePath);
                if (targetPath.Parent != path)
                {
                    throw new WorkflowEditException(
                        $"The node at the path '{targetPath}' does not belong to the workflow at the path '{workflowPath}'.");
                }

                targetBuilder = ResolveNode(targetPath);
            }

            var editor = GetWorkflowEditor(path);
            if (targetBuilder != null)
            {
                targetNode = FindGraphNode(editor, targetBuilder, targetPath);
            }

            var existingNodes = new HashSet<ExpressionBuilder>(editor.Workflow.Select(node => node.Value));
            var insertIndex = GetInsertIndex(editor, targetNode);
            editor.CreateGraphNode(builder, targetNode, CreateGraphNodeType.Successor, branch, validate: true, insertIndex);
            ThrowPendingError();
            return GetCreatedNodePath(editor, existingNodes);
        }

        private string GetCreatedNodePath(WorkflowEditor editor, HashSet<ExpressionBuilder> existingNodes)
        {
            foreach (var node in editor.Workflow)
            {
                if (!existingNodes.Contains(node.Value))
                {
                    return GetNodePath(node.Value);
                }
            }

            throw new WorkflowEditException("The new node could not be found in the workflow.");
        }

        #endregion

        #region Edit Nodes

        private (WorkflowEditor editor, GraphNode[] nodes) ResolveNodes(IReadOnlyList<string> nodePaths)
        {
            if (nodePaths == null)
            {
                throw new ArgumentNullException(nameof(nodePaths));
            }

            if (nodePaths.Count == 0)
            {
                throw new WorkflowEditException("At least one node path must be specified.");
            }

            var paths = new WorkflowEditorPath[nodePaths.Count];
            var builders = new ExpressionBuilder[nodePaths.Count];
            for (int i = 0; i < paths.Length; i++)
            {
                paths[i] = ParseNodePath(nodePaths[i]);
                if (paths[i].Parent != paths[0].Parent)
                {
                    throw new WorkflowEditException("All the nodes must belong to the same workflow.");
                }
            }

            var workflowPath = paths[0].Parent;
            EnsureEditable(workflowPath);
            for (int i = 0; i < paths.Length; i++)
            {
                builders[i] = ResolveNode(paths[i]);
            }

            var editor = GetWorkflowEditor(workflowPath);
            var nodes = new GraphNode[paths.Length];
            for (int i = 0; i < nodes.Length; i++)
            {
                nodes[i] = FindGraphNode(editor, builders[i], paths[i]);
            }

            return (editor, nodes);
        }

        public void Connect(string sourceNodePath, string targetNodePath)
        {
            BeginOperation();
            var (editor, nodes) = ResolveNodes(new[] { sourceNodePath, targetNodePath });
            var sourceNodes = new[] { nodes[0] };
            var targetNode = nodes[1];
            if (!editor.ValidateConnection(branch: false, shift: false, sourceNodes, targetNode))
            {
                throw new WorkflowEditException(
                    $"The node at the path '{sourceNodePath}' cannot be connected to the node at the path '{targetNodePath}'.");
            }

            editor.ConnectGraphNodes(sourceNodes, targetNode);
            ThrowPendingError();
        }

        public void Disconnect(string sourceNodePath, string targetNodePath)
        {
            BeginOperation();
            var (editor, nodes) = ResolveNodes(new[] { sourceNodePath, targetNodePath });
            var sourceNodes = new[] { nodes[0] };
            var targetNode = nodes[1];
            if (!editor.ValidateConnection(branch: false, shift: true, sourceNodes, targetNode))
            {
                throw new WorkflowEditException(
                    $"The node at the path '{sourceNodePath}' is not connected to the node at the path '{targetNodePath}'.");
            }

            editor.DisconnectGraphNodes(sourceNodes, targetNode);
            ThrowPendingError();
        }

        public void Delete(IReadOnlyList<string> nodePaths)
        {
            BeginOperation();
            var (editor, nodes) = ResolveNodes(nodePaths);
            editor.DeleteGraphNodes(nodes);
            ThrowPendingError();
        }

        public string Group(IReadOnlyList<string> nodePaths)
        {
            BeginOperation();
            var (editor, nodes) = ResolveNodes(nodePaths);
            var existingNodes = new HashSet<ExpressionBuilder>(editor.Workflow.Select(node => node.Value));
            editor.GroupGraphNodes(nodes);
            ThrowPendingError();
            return GetCreatedNodePath(editor, existingNodes);
        }

        public void Ungroup(IReadOnlyList<string> nodePaths)
        {
            BeginOperation();
            var (editor, nodes) = ResolveNodes(nodePaths);
            editor.UngroupGraphNodes(nodes);
            ThrowPendingError();
        }

        public void Enable(IReadOnlyList<string> nodePaths)
        {
            BeginOperation();
            var (editor, nodes) = ResolveNodes(nodePaths);
            editor.EnableGraphNodes(nodes);
            ThrowPendingError();
        }

        public void Disable(IReadOnlyList<string> nodePaths)
        {
            BeginOperation();
            var (editor, nodes) = ResolveNodes(nodePaths);
            editor.DisableGraphNodes(nodes);
            ThrowPendingError();
        }

        public void SetProperty(string nodePath, string propertyName, string value)
        {
            BeginOperation();
            if (string.IsNullOrEmpty(propertyName))
            {
                throw new WorkflowEditException("The name of the property cannot be empty.");
            }

            var path = ParseNodePath(nodePath);
            EnsureEditable(path.Parent);
            var builder = ResolveNode(path);
            var workflowElement = ExpressionBuilder.GetWorkflowElement(builder);
            var propertyDescriptor = TypeDescriptor.GetProperties(workflowElement)[propertyName];
            if (propertyDescriptor == null)
            {
                throw new WorkflowEditException($"The operator at the path '{nodePath}' has no property named '{propertyName}'.");
            }

            if (propertyDescriptor.IsReadOnly)
            {
                throw new WorkflowEditException($"The property '{propertyName}' is read only.");
            }

            if (propertyDescriptor.Converter == null || !propertyDescriptor.Converter.CanConvertFrom(typeof(string)))
            {
                throw new WorkflowEditException($"The property '{propertyName}' cannot be assigned from a text value.");
            }

            object propertyValue;
            try
            {
                var context = new TypeDescriptorContext(workflowElement, propertyDescriptor, serviceProvider);
                propertyValue = propertyDescriptor.Converter.ConvertFromString(context, value);
            }
            catch (Exception ex)
            {
                throw new WorkflowEditException($"The value '{value}' is not valid for the property '{propertyName}'. {ex.Message}", ex);
            }

            var previousValue = propertyDescriptor.GetValue(workflowElement);
            var updateProperties = CreateUpdatePropertiesDelegate();
            commandExecutor.BeginCompositeCommand();
            try
            {
                // a property setter which validates its input can throw from inside the
                // command, and leaving the composite open would wedge the command executor
                commandExecutor.Execute(EmptyAction, updateProperties);
                commandExecutor.Execute(
                    () => propertyDescriptor.SetValue(workflowElement, propertyValue),
                    () => propertyDescriptor.SetValue(workflowElement, previousValue));
                commandExecutor.Execute(updateProperties, EmptyAction);
            }
            finally
            {
                commandExecutor.EndCompositeCommand();
            }

            ThrowPendingError();
        }

        private Action CreateUpdatePropertiesDelegate()
        {
            return () =>
            {
                uiService?.SetUIDirty();
                viewProvider.InvalidateLayout();
            };
        }

        #endregion

        class WorkflowEditTransaction : IWorkflowEditTransaction
        {
            readonly WorkflowEditingService service;
            bool completed;

            public WorkflowEditTransaction(WorkflowEditingService editingService, string description)
            {
                service = editingService;
                Description = description;
            }

            public string Description { get; }

            public void Commit()
            {
                if (completed)
                {
                    throw new InvalidOperationException("The workflow edit transaction has already been completed.");
                }

                completed = true;
                if (!service.CompleteTransaction(this, rollback: false))
                {
                    throw new InvalidOperationException(
                        "The workflow edit transaction was abandoned before it was committed, and its edits have " +
                        "already been closed into the editor undo history.");
                }
            }

            public void Dispose()
            {
                // a transaction which was already recovered has nothing left to roll back,
                // and throwing here would mask whatever error ended the batch
                if (completed) return;
                completed = true;
                service.CompleteTransaction(this, rollback: true);
            }
        }
    }
}
