using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Bonsai.Design;
using Bonsai.Editor.GraphModel;
using Bonsai.Expressions;

namespace Bonsai.Editor.Tests
{
    class MockGraphView : IGraphView
    {
        ExpressionBuilderGraph workflow;
        readonly HashSet<GraphNode> selectedNodes = new HashSet<GraphNode>();

        public MockGraphView(ExpressionBuilderGraph workflow = null)
        {
            Workflow = workflow ?? new ExpressionBuilderGraph();
            CommandExecutor = new CommandExecutor();
            EditorService = new MockWorkflowEditorService(CommandExecutor);
            var serviceContainer = new ServiceContainer();
            serviceContainer.AddService(typeof(WorkflowBuilder), new WorkflowBuilder(Workflow));
            serviceContainer.AddService(typeof(CommandExecutor), CommandExecutor);
            serviceContainer.AddService(typeof(IWorkflowEditorService), EditorService);
            ServiceProvider = serviceContainer;
        }

        public CommandExecutor CommandExecutor { get;  }

        public MockWorkflowEditorService EditorService { get; }

        public IServiceProvider ServiceProvider { get; }

        public ExpressionBuilderGraph Workflow
        {
            get { return workflow; }
            set
            {
                workflow = value;
                UpdateGraphLayout();
            }
        }

        public IReadOnlyList<GraphNodeGrouping> Nodes { get; set; }

        public IEnumerable<GraphNode> SelectedNodes
        {
            get { return selectedNodes; }
            set
            {
                if (selectedNodes != value)
                {
                    selectedNodes.Clear();
                    if (value != null)
                    {
                        foreach (var node in value)
                        {
                            selectedNodes.Add(node);
                            CursorNode = node;
                        }
                    }
                }
            }
        }

        public GraphNode CursorNode { get; set; }

        public void UpdateSelection(IEnumerable<ExpressionBuilder> selection)
        {
            SelectedNodes = Nodes
                .LayeredNodes()
                .Where(node =>
                {
                    var nodeBuilder = WorkflowEditor.GetGraphNodeBuilder(node);
                    return selection.Any(builder => ExpressionBuilder.Unwrap(builder) == nodeBuilder);
                });
        }

        public void UpdateGraphLayout()
        {
            Nodes = Workflow.ConnectedComponentLayering();
        }

        public void UpdateGraphLayout(bool _)
        {
            UpdateGraphLayout();
        }
    }

    class MockWorkflowEditorService : IWorkflowEditorService
    {
        readonly CommandExecutor commandExecutor;

        public MockWorkflowEditorService(CommandExecutor executor)
        {
            commandExecutor = executor;
        }

        public List<bool> UndoRequests { get; } = new List<bool>();

        public void Undo(bool allowRedo)
        {
            // the editor compensates its dirty state version counter here before undoing,
            // which the tests assert by inspecting the recorded requests
            UndoRequests.Add(allowRedo);
            commandExecutor.Undo(allowRedo);
        }

        public string GetProjectDisplayName() => string.Empty;

        public void OnKeyDown(KeyEventArgs e)
        {
        }

        public void OnKeyPress(KeyPressEventArgs e)
        {
        }

        public void OnContextMenuOpening(EventArgs e)
        {
        }

        public void OnContextMenuClosed(EventArgs e)
        {
        }

        public DirectoryInfo EnsureExtensionsDirectory() => null;

        public WorkflowBuilder LoadWorkflow(string fileName) => null;

        public void NavigateTo(WorkflowEditorPath workflowPath, NavigationPreference navigationPreference = default)
        {
        }

        public void SelectBuilderNode(ExpressionBuilder builder, NavigationPreference navigationPreference = default)
        {
        }

        public void SelectBuilderNode(WorkflowEditorPath builderPath, NavigationPreference navigationPreference = default)
        {
        }

        public void SelectNextControl(bool forward)
        {
        }

        public bool ValidateWorkflow() => true;

        public void RefreshEditor()
        {
        }
    }
}
