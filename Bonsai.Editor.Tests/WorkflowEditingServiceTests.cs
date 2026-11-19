using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Bonsai.Design;
using Bonsai.Editor.GraphModel;
using Bonsai.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Bonsai.Editor.Tests
{
    [TestClass]
    public class WorkflowEditingServiceTests
    {
        const string TimerOperator = "Bonsai.Reactive.Timer";
        const string GroupOperator = "Bonsai.Expressions.GroupWorkflow";

        class MockToolboxService : IWorkflowToolboxService
        {
            public string GetPackageDisplayName(string packageKey)
            {
                return packageKey;
            }

            public IEnumerable<WorkflowElementDescriptor> GetToolboxElements()
            {
                yield return new WorkflowElementDescriptor
                {
                    Name = nameof(Reactive.Timer),
                    Namespace = typeof(Reactive.Timer).Namespace,
                    FullyQualifiedName = typeof(Reactive.Timer).AssemblyQualifiedName,
                    Description = "Generates a sequence of values at regular intervals.",
                    ElementTypes = new[] { ElementCategory.Source }
                };
                yield return new WorkflowElementDescriptor
                {
                    Name = nameof(Reactive.Timeout),
                    Namespace = typeof(Reactive.Timeout).Namespace,
                    FullyQualifiedName = typeof(Reactive.Timeout).AssemblyQualifiedName,
                    Description = "Raises an error if the next element is not received in time.",
                    ElementTypes = new[] { ElementCategory.Combinator }
                };
                yield return new WorkflowElementDescriptor
                {
                    Name = "GroupWorkflow",
                    Namespace = typeof(GroupWorkflowBuilder).Namespace,
                    FullyQualifiedName = typeof(GroupWorkflowBuilder).AssemblyQualifiedName,
                    Description = "Groups a set of operators into a nested workflow.",
                    ElementTypes = new[] { ElementCategory.Nested, ElementCategory.Workflow }
                };
            }
        }

        class MockWorkflowEditorState : IWorkflowEditorState
        {
            public bool WorkflowRunning { get; set; }

            public event EventHandler WorkflowStarted
            {
                add { }
                remove { }
            }

            public event EventHandler WorkflowStopped
            {
                add { }
                remove { }
            }
        }

        class MockViewProvider : IWorkflowViewProvider
        {
            readonly WorkflowEditor editor;

            public MockViewProvider(WorkflowEditor workflowEditor)
            {
                editor = workflowEditor;
            }

            public WorkflowEditor GetWorkflowEditor(WorkflowEditorPath workflowPath)
            {
                editor.NavigateTo(workflowPath);
                return editor;
            }

            public void InvalidateLayout()
            {
            }
        }

        (WorkflowEditingService service, WorkflowEditor editor, MockGraphView graphView) CreateMockEditingService(
            ExpressionBuilderGraph workflow = null,
            bool workflowRunning = false)
        {
            var graphView = new MockGraphView(workflow);
            var editor = new WorkflowEditor(graphView.ServiceProvider, graphView);
            editor.UpdateLayout.Subscribe(graphView.UpdateGraphLayout);
            editor.UpdateSelection.Subscribe(graphView.UpdateSelection);

            var serviceContainer = (ServiceContainer)graphView.ServiceProvider;
            serviceContainer.AddService(typeof(IWorkflowToolboxService), new MockToolboxService());
            serviceContainer.AddService(
                typeof(IWorkflowEditorState),
                new MockWorkflowEditorState { WorkflowRunning = workflowRunning });

            var service = new WorkflowEditingService(graphView.ServiceProvider, new MockViewProvider(editor));
            editor.Error.Subscribe(ex => service.TryHandleError(ex));
            return (service, editor, graphView);
        }

        // Counts the entries recorded in the undo history, rather than only checking that
        // there is at least one, so that a batch which splits into several entries fails.
        static Action ExpectSingleUndoEntry(MockGraphView graphView, WorkflowEditor editor)
        {
            var commandExecutor = graphView.CommandExecutor;
            var nodeSequence = editor.Workflow.ToArray();
            var historyEntries = 0;
            EventHandler onStatusChanged = (sender, e) => historyEntries++;
            commandExecutor.StatusChanged += onStatusChanged;
            return () =>
            {
                commandExecutor.StatusChanged -= onStatusChanged;
                Assert.AreEqual(expected: 1, historyEntries, "The batch of edits did not record exactly one undo entry.");
                Assert.IsTrue(commandExecutor.CanUndo, "The batch of edits did not produce an undo entry.");
                commandExecutor.Undo();
                CollectionAssert.AreEqual(nodeSequence, editor.Workflow.ToArray(), "Undo did not restore the original node sequence.");
            };
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void AbandonTransaction(WorkflowEditingService service)
        {
            // the transaction is deliberately neither committed nor disposed, and goes out
            // of scope when this method returns
            service.BeginEdit("abandoned");
            service.CreateNode(string.Empty, TimerOperator, null, branch: false);
        }

        static ExpressionBuilderGraph CreateIncludeGraph(string includePath)
        {
            var graph = new ExpressionBuilderGraph();
            graph.Add(new IncludeWorkflowBuilder { Path = includePath });
            return graph.ToInspectableGraph();
        }

        [TestMethod]
        public void CreateNode_EmptyWorkflow_SingleUndoEntry()
        {
            var (service, editor, graphView) = CreateMockEditingService();
            var assertSingleUndoEntry = ExpectSingleUndoEntry(graphView, editor);
            using (var transaction = service.BeginEdit("create a timer"))
            {
                var nodePath = service.CreateNode(string.Empty, TimerOperator, null, branch: false);
                Assert.AreEqual(expected: "0", nodePath);
                transaction.Commit();
            }

            Assert.AreEqual(expected: 1, editor.Workflow.Count);
            assertSingleUndoEntry();
        }

        [TestMethod]
        public void CreateSubject_SelectedTargetNode_ConnectsToTarget()
        {
            var workflow = EditorHelper.CreateEditorGraph("A");
            var (service, editor, graphView) = CreateMockEditingService(workflow);
            var assertSingleUndoEntry = ExpectSingleUndoEntry(graphView, editor);
            using (var transaction = service.BeginEdit("broadcast the source"))
            {
                var nodePath = service.CreateSubject(string.Empty, "Values", multicast: true, "0", branch: false);
                Assert.AreEqual(expected: "1", nodePath);
                transaction.Commit();
            }

            Assert.AreEqual(expected: 2, editor.Workflow.Count);
            Assert.AreEqual(expected: 1, editor.Workflow[0].Successors.Count);
            var subject = (MulticastSubject)ExpressionBuilder.Unwrap(editor.Workflow[1].Value);
            Assert.AreEqual(expected: "Values", subject.Name);
            assertSingleUndoEntry();
        }

        [TestMethod]
        public void CreateInclude_EmptyWorkflow_CreatesIncludeNode()
        {
            var (service, editor, graphView) = CreateMockEditingService();
            var assertSingleUndoEntry = ExpectSingleUndoEntry(graphView, editor);
            using (var transaction = service.BeginEdit("include a workflow"))
            {
                service.CreateInclude(string.Empty, "Extensions:Nested.bonsai", null, branch: false);
                transaction.Commit();
            }

            var includeBuilder = (IncludeWorkflowBuilder)ExpressionBuilder.Unwrap(editor.Workflow[0].Value);
            Assert.AreEqual(expected: "Extensions:Nested.bonsai", includeBuilder.Path);
            assertSingleUndoEntry();
        }

        [TestMethod]
        public void Connect_TwoSiblingNodes_SingleUndoEntry()
        {
            var workflow = EditorHelper.CreateEditorGraph("A", "B");
            var (service, editor, graphView) = CreateMockEditingService(workflow);
            var assertSingleUndoEntry = ExpectSingleUndoEntry(graphView, editor);
            using (var transaction = service.BeginEdit("connect the nodes"))
            {
                service.Connect("0", "1");
                transaction.Commit();
            }

            Assert.AreEqual(expected: 1, editor.Workflow[0].Successors.Count);
            assertSingleUndoEntry();
        }

        [TestMethod]
        public void Disconnect_ConnectedNodes_SingleUndoEntry()
        {
            var workflow = EditorHelper.CreateEditorGraph("A", "B");
            var (service, editor, graphView) = CreateMockEditingService(workflow);
            editor.ConnectNodes("A", "B");
            var assertSingleUndoEntry = ExpectSingleUndoEntry(graphView, editor);
            using (var transaction = service.BeginEdit("disconnect the nodes"))
            {
                service.Disconnect("0", "1");
                transaction.Commit();
            }

            Assert.AreEqual(expected: 0, editor.Workflow.Sum(node => node.Successors.Count));
            assertSingleUndoEntry();

            // the node sequence is unchanged by a disconnection, so the connection itself is
            // what has to be checked after the undo
            var sourceNode = editor.Workflow.Single(node => ExpressionBuilder.GetElementDisplayName(node.Value) == "A");
            Assert.AreEqual(expected: 1, sourceNode.Successors.Count);
        }

        [TestMethod]
        public void Delete_SingleNode_SingleUndoEntry()
        {
            var workflow = EditorHelper.CreateEditorGraph("A", "B");
            var (service, editor, graphView) = CreateMockEditingService(workflow);
            var assertSingleUndoEntry = ExpectSingleUndoEntry(graphView, editor);
            using (var transaction = service.BeginEdit("delete a node"))
            {
                service.Delete(new[] { "0" });
                transaction.Commit();
            }

            Assert.AreEqual(expected: 1, editor.Workflow.Count);
            assertSingleUndoEntry();
        }

        [TestMethod]
        public void SetProperty_TimerDueTime_SingleUndoEntry()
        {
            var (service, editor, graphView) = CreateMockEditingService();
            string nodePath;
            using (var transaction = service.BeginEdit("create a timer"))
            {
                nodePath = service.CreateNode(string.Empty, TimerOperator, null, branch: false);
                transaction.Commit();
            }

            var timer = (Reactive.Timer)ExpressionBuilder.GetWorkflowElement(editor.Workflow[0].Value);
            Assert.AreEqual(expected: TimeSpan.Zero, timer.DueTime);

            var assertSingleUndoEntry = ExpectSingleUndoEntry(graphView, editor);
            using (var transaction = service.BeginEdit("set the due time"))
            {
                service.SetProperty(nodePath, nameof(Reactive.Timer.DueTime), "00:00:01");
                transaction.Commit();
            }

            Assert.AreEqual(expected: TimeSpan.FromSeconds(1), timer.DueTime);
            assertSingleUndoEntry();
            Assert.AreEqual(expected: TimeSpan.Zero, timer.DueTime);
        }

        [TestMethod]
        public void SetProperty_UnknownPropertyName_ThrowsEditException()
        {
            var (service, _, _) = CreateMockEditingService();
            using var transaction = service.BeginEdit("set an unknown property");
            var nodePath = service.CreateNode(string.Empty, TimerOperator, null, branch: false);
            Assert.ThrowsException<WorkflowEditException>(
                () => service.SetProperty(nodePath, "NotAProperty", "1"));
        }

        [TestMethod]
        public void Group_TwoConnectedNodes_SingleUndoEntry()
        {
            var workflow = EditorHelper.CreateEditorGraph("A", "B");
            var (service, editor, graphView) = CreateMockEditingService(workflow);
            editor.ConnectNodes("A", "B");
            var assertSingleUndoEntry = ExpectSingleUndoEntry(graphView, editor);
            using (var transaction = service.BeginEdit("group the nodes"))
            {
                var nodePath = service.Group(new[] { "0", "1" });
                Assert.AreEqual(expected: "0", nodePath);
                transaction.Commit();
            }

            Assert.AreEqual(expected: 1, editor.Workflow.Count);
            assertSingleUndoEntry();
        }

        [TestMethod]
        public void Ungroup_SingleGroupNode_SingleUndoEntry()
        {
            var workflow = EditorHelper.CreateEditorGraph("A", "B");
            var (service, editor, graphView) = CreateMockEditingService(workflow);
            editor.ConnectNodes("A", "B");
            var assertSingleUndoEntry = ExpectSingleUndoEntry(graphView, editor);
            using (var transaction = service.BeginEdit("ungroup the node"))
            {
                service.Ungroup(new[] { "1" });
                transaction.Commit();
            }

            assertSingleUndoEntry();
        }

        [TestMethod]
        public void Disable_SingleNode_SingleUndoEntry()
        {
            var workflow = EditorHelper.CreateEditorGraph("A", "B");
            var (service, editor, graphView) = CreateMockEditingService(workflow);
            var assertSingleUndoEntry = ExpectSingleUndoEntry(graphView, editor);
            using (var transaction = service.BeginEdit("disable a node"))
            {
                service.Disable(new[] { "0" });
                transaction.Commit();
            }

            Assert.IsInstanceOfType(ExpressionBuilder.Unwrap(editor.Workflow[0].Value), typeof(DisableBuilder));
            assertSingleUndoEntry();
        }

        [TestMethod]
        public void Enable_DisabledNode_SingleUndoEntry()
        {
            var workflow = EditorHelper.CreateEditorGraph("A", "B");
            var (service, editor, graphView) = CreateMockEditingService(workflow);
            using (var transaction = service.BeginEdit("disable a node"))
            {
                service.Disable(new[] { "0" });
                transaction.Commit();
            }

            var assertSingleUndoEntry = ExpectSingleUndoEntry(graphView, editor);
            using (var transaction = service.BeginEdit("enable a node"))
            {
                service.Enable(new[] { "0" });
                transaction.Commit();
            }

            Assert.IsNotInstanceOfType(ExpressionBuilder.Unwrap(editor.Workflow[0].Value), typeof(DisableBuilder));
            assertSingleUndoEntry();
        }

        [TestMethod]
        public void BeginEdit_BatchOfFiveEdits_SingleUndoEntry()
        {
            var workflow = EditorHelper.CreateEditorGraph("A", "B", "C");
            var (service, editor, graphView) = CreateMockEditingService(workflow);
            var assertSingleUndoEntry = ExpectSingleUndoEntry(graphView, editor);
            using (var transaction = service.BeginEdit("build a chain"))
            {
                service.Connect("0", "1");
                service.Connect("1", "2");
                var nodePath = service.CreateNode(string.Empty, TimerOperator, null, branch: false);
                service.SetProperty(nodePath, nameof(Reactive.Timer.DueTime), "00:00:01");
                service.Disable(new[] { nodePath });
                transaction.Commit();
            }

            Assert.AreEqual(expected: 4, editor.Workflow.Count);
            assertSingleUndoEntry();
        }

        [TestMethod]
        public void BeginEdit_FailedEditInBatch_RollsBackEveryEdit()
        {
            var workflow = EditorHelper.CreateEditorGraph("A", "B");
            var (service, editor, graphView) = CreateMockEditingService(workflow);
            var nodeSequence = editor.Workflow.ToArray();
            Assert.ThrowsException<WorkflowEditException>(() =>
            {
                using var transaction = service.BeginEdit("build a chain");
                service.CreateNode(string.Empty, TimerOperator, null, branch: false);
                service.Connect("0", "1");
                service.Connect("0", "1");
                transaction.Commit();
            });

            CollectionAssert.AreEqual(nodeSequence, editor.Workflow.ToArray());
            Assert.AreEqual(expected: 0, editor.Workflow[0].Successors.Count);
            Assert.IsFalse(graphView.CommandExecutor.CanUndo);
            Assert.IsFalse(graphView.CommandExecutor.CanRedo);

            // the rollback goes through the editor service, which compensates the dirty
            // state version counter, and not straight through the command executor
            CollectionAssert.AreEqual(new[] { false }, graphView.EditorService.UndoRequests);
        }

        [TestMethod]
        public void BeginEdit_TransactionAbandonedWithoutDisposing_ClosesTheBatch()
        {
            var workflow = EditorHelper.CreateEditorGraph("A", "B");
            var (service, editor, graphView) = CreateMockEditingService(workflow);
            var commandExecutor = graphView.CommandExecutor;
            var nodeSequence = editor.Workflow.ToArray();
            AbandonTransaction(service);
            Assert.AreEqual(expected: 3, editor.Workflow.Count);
            Assert.AreEqual(expected: 1, commandExecutor.CompositeDepth);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // the error sink stops claiming errors as soon as the transaction is gone, before
            // anything has closed the composite command
            Assert.IsFalse(service.TryHandleError(new InvalidOperationException()));

            // the next call into the service closes the abandoned batch, leaving its edits
            // applied rather than destroying edits the user may have made in the meantime
            Assert.IsTrue(service.CanEdit);
            Assert.AreEqual(expected: 0, commandExecutor.CompositeDepth);
            Assert.AreEqual(expected: 0, graphView.EditorService.UndoRequests.Count);
            Assert.AreEqual(expected: 3, editor.Workflow.Count);

            // the batch is a single entry which the user can choose to undo
            Assert.IsTrue(commandExecutor.CanUndo);
            commandExecutor.Undo();
            Assert.IsFalse(commandExecutor.CanUndo);
            CollectionAssert.AreEqual(nodeSequence, editor.Workflow.ToArray());

            // and a later transaction still behaves normally
            var assertSingleUndoEntry = ExpectSingleUndoEntry(graphView, editor);
            using (var transaction = service.BeginEdit("a later edit"))
            {
                service.Connect("0", "1");
                transaction.Commit();
            }

            assertSingleUndoEntry();
        }

        [TestMethod]
        public void BeginEdit_TransactionAlreadyInProgress_ThrowsInvalidOperationException()
        {
            var (service, _, _) = CreateMockEditingService();
            using var transaction = service.BeginEdit("the first batch");
            Assert.ThrowsException<InvalidOperationException>(() => service.BeginEdit("the second batch"));
        }

        [TestMethod]
        public void CanEdit_WorkflowRunning_RefusesToStartATransaction()
        {
            var (service, _, _) = CreateMockEditingService(workflowRunning: true);
            Assert.IsFalse(service.CanEdit);
            Assert.ThrowsException<WorkflowEditException>(() => service.BeginEdit("edit while running"));
        }

        [TestMethod]
        public void Delete_IncludeNodeItself_IsAllowed()
        {
            var workflow = CreateIncludeGraph("Extensions:Nested.bonsai");
            var (service, editor, graphView) = CreateMockEditingService(workflow);
            var assertSingleUndoEntry = ExpectSingleUndoEntry(graphView, editor);
            using (var transaction = service.BeginEdit("delete the include node"))
            {
                service.Delete(new[] { "0" });
                transaction.Commit();
            }

            Assert.AreEqual(expected: 0, editor.Workflow.Count);
            assertSingleUndoEntry();
        }

        [TestMethod]
        public void Disable_IncludeNodeItself_IsAllowed()
        {
            var workflow = CreateIncludeGraph("Extensions:Nested.bonsai");
            var (service, editor, graphView) = CreateMockEditingService(workflow);
            var assertSingleUndoEntry = ExpectSingleUndoEntry(graphView, editor);
            using (var transaction = service.BeginEdit("disable the include node"))
            {
                service.Disable(new[] { "0" });
                transaction.Commit();
            }

            Assert.IsInstanceOfType(ExpressionBuilder.Unwrap(editor.Workflow[0].Value), typeof(DisableBuilder));
            assertSingleUndoEntry();
        }

        [TestMethod]
        public void CreateNode_AmbiguousOperatorName_ThrowsWithCandidates()
        {
            var (service, editor, _) = CreateMockEditingService();
            var exception = Assert.ThrowsException<WorkflowEditException>(() =>
            {
                using var transaction = service.BeginEdit("create an unknown node");
                service.CreateNode(string.Empty, "Time", null, branch: false);
            });

            Assert.AreEqual(expected: 2, exception.Candidates.Count);
            CollectionAssert.Contains(exception.Candidates.ToArray(), TimerOperator);
            Assert.IsTrue(exception.Message.Contains(TimerOperator));
            Assert.AreEqual(expected: 0, editor.Workflow.Count);

            // the candidates are not the internal array, which a caller could cast and modify
            Assert.IsNotInstanceOfType(exception.Candidates, typeof(string[]));
        }

        [TestMethod]
        public void CreateNode_UnknownOperatorName_ThrowsEditException()
        {
            var (service, _, _) = CreateMockEditingService();
            using var transaction = service.BeginEdit("create an unknown node");
            Assert.ThrowsException<WorkflowEditException>(
                () => service.CreateNode(string.Empty, "NotAnOperator", null, branch: false));
        }

        [TestMethod]
        public void CreateNode_PathInsideIncludedWorkflow_ThrowsEditException()
        {
            var includePath = "Extensions:Nested.bonsai";
            var workflow = CreateIncludeGraph(includePath);
            var (service, _, _) = CreateMockEditingService(workflow);
            var exception = Assert.ThrowsException<WorkflowEditException>(() =>
            {
                using var transaction = service.BeginEdit("edit an included workflow");
                service.CreateNode("0", TimerOperator, null, branch: false);
            });

            Assert.IsTrue(exception.Message.Contains(includePath));
        }

        [TestMethod]
        public void CreateNode_WithoutTransaction_ThrowsInvalidOperationException()
        {
            var (service, _, _) = CreateMockEditingService();
            Assert.ThrowsException<InvalidOperationException>(
                () => service.CreateNode(string.Empty, TimerOperator, null, branch: false));
        }

        [TestMethod]
        public void FindOperators_PartialName_ReturnsMatchingOperators()
        {
            var (service, _, _) = CreateMockEditingService();
            var operators = service.FindOperators("Time", maxResults: 10);
            Assert.AreEqual(expected: 2, operators.Count);

            var timer = operators.First(descriptor => descriptor.FullyQualifiedName == typeof(Reactive.Timer).AssemblyQualifiedName);
            Assert.AreEqual(expected: ElementCategory.Source, timer.ElementCategory);
            Assert.AreEqual(expected: TimerOperator, timer.ToString());
        }

        [TestMethod]
        public void FindOperators_NestedElementCategory_ReturnsFirstDeclaredCategory()
        {
            var (service, _, _) = CreateMockEditingService();
            var operators = service.FindOperators(GroupOperator, maxResults: 10);
            Assert.AreEqual(expected: 1, operators.Count);
            Assert.AreEqual(expected: ElementCategory.Workflow, operators[0].ElementCategory);
        }

        [TestMethod]
        public void GetWorkflowXml_TopLevelWorkflow_MatchesSavedWorkflow()
        {
            var workflow = EditorHelper.CreateEditorGraph("A", "B");
            var (service, editor, graphView) = CreateMockEditingService(workflow);
            editor.ConnectNodes("A", "B");

            var workflowBuilder = (WorkflowBuilder)graphView.ServiceProvider.GetService(typeof(WorkflowBuilder));
            var serializerWorkflowBuilder = new WorkflowBuilder(workflowBuilder.Workflow.FromInspectableGraph());
            var fileName = Path.GetTempFileName();
            try
            {
                ElementStore.SaveElement(WorkflowBuilder.Serializer, fileName, serializerWorkflowBuilder);
                var expected = File.ReadAllText(fileName);
                Assert.AreEqual(expected, service.GetWorkflowXml(string.Empty));
            }
            finally
            {
                File.Delete(fileName);
            }
        }
    }
}
