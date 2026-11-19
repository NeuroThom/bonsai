using System;
using System.Collections.Generic;
using Bonsai.Design;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Bonsai.Editor.Tests
{
    [TestClass]
    public class CommandExecutorTests
    {
        static (CommandExecutor executor, List<string> log, Func<int> statusChangedCount) CreateMockExecutor()
        {
            var log = new List<string>();
            var statusChangedCount = 0;
            var executor = new CommandExecutor();
            executor.StatusChanged += (sender, e) => statusChangedCount++;
            return (executor, log, () => statusChangedCount);
        }

        static void ExecuteCommand(CommandExecutor executor, List<string> log, string name)
        {
            executor.Execute(
                () => log.Add(name),
                () =>
                {
                    Assert.AreEqual(expected: name, log[log.Count - 1], "Commands should be undone in reverse order.");
                    log.RemoveAt(log.Count - 1);
                });
        }

        [TestMethod]
        public void BeginCompositeCommand_NestedComposite_DoesNotThrow()
        {
            var (executor, log, _) = CreateMockExecutor();
            executor.BeginCompositeCommand();
            executor.BeginCompositeCommand();
            ExecuteCommand(executor, log, "A");
            executor.EndCompositeCommand();
            executor.EndCompositeCommand();
            CollectionAssert.AreEqual(new[] { "A" }, log);
        }

        [TestMethod]
        public void EndCompositeCommand_NestedComposite_StatusChangedRaisedOnlyOnOutermostEnd()
        {
            var (executor, log, statusChangedCount) = CreateMockExecutor();
            executor.BeginCompositeCommand();
            ExecuteCommand(executor, log, "A");
            executor.BeginCompositeCommand();
            ExecuteCommand(executor, log, "B");
            executor.EndCompositeCommand();
            Assert.AreEqual(expected: 0, statusChangedCount());
            ExecuteCommand(executor, log, "C");
            executor.EndCompositeCommand();
            Assert.AreEqual(expected: 1, statusChangedCount());
        }

        [TestMethod]
        public void Undo_NestedComposite_RevertsAllCommandsInSingleStep()
        {
            var (executor, log, _) = CreateMockExecutor();
            executor.BeginCompositeCommand();
            ExecuteCommand(executor, log, "A");
            executor.BeginCompositeCommand();
            ExecuteCommand(executor, log, "B");
            ExecuteCommand(executor, log, "C");
            executor.EndCompositeCommand();
            executor.EndCompositeCommand();

            Assert.IsTrue(executor.CanUndo);
            executor.Undo();
            Assert.AreEqual(expected: 0, log.Count);
            Assert.IsFalse(executor.CanUndo);
        }

        [TestMethod]
        public void Redo_NestedComposite_RestoresExecutedOrderInSingleStep()
        {
            var (executor, log, _) = CreateMockExecutor();
            executor.BeginCompositeCommand();
            ExecuteCommand(executor, log, "A");
            executor.BeginCompositeCommand();
            ExecuteCommand(executor, log, "B");
            ExecuteCommand(executor, log, "C");
            executor.EndCompositeCommand();
            executor.EndCompositeCommand();
            executor.Undo();

            Assert.IsTrue(executor.CanRedo);
            executor.Redo();
            CollectionAssert.AreEqual(new[] { "A", "B", "C" }, log);
            Assert.IsFalse(executor.CanRedo);
        }

        [TestMethod]
        public void Undo_CommandBeforeNestedComposite_RevertsOneStepAtATime()
        {
            var (executor, log, _) = CreateMockExecutor();
            ExecuteCommand(executor, log, "A");
            executor.BeginCompositeCommand();
            executor.BeginCompositeCommand();
            ExecuteCommand(executor, log, "B");
            ExecuteCommand(executor, log, "C");
            executor.EndCompositeCommand();
            executor.EndCompositeCommand();

            executor.Undo();
            CollectionAssert.AreEqual(new[] { "A" }, log);
            executor.Undo();
            Assert.AreEqual(expected: 0, log.Count);
            Assert.IsFalse(executor.CanUndo);
        }

        [TestMethod]
        public void EndCompositeCommand_EmptyNestedComposite_NoStatusChanged()
        {
            var (executor, _, statusChangedCount) = CreateMockExecutor();
            executor.BeginCompositeCommand();
            executor.BeginCompositeCommand();
            executor.EndCompositeCommand();
            executor.EndCompositeCommand();
            Assert.AreEqual(expected: 0, statusChangedCount());
            Assert.IsFalse(executor.CanUndo);
        }

        [TestMethod]
        public void EndCompositeCommand_NestedCommandWithoutUndoAction_ClearsCommandHistory()
        {
            var (executor, log, _) = CreateMockExecutor();
            ExecuteCommand(executor, log, "A");
            executor.BeginCompositeCommand();
            executor.BeginCompositeCommand();
            executor.Execute(() => log.Add("B"), undo: null);
            executor.EndCompositeCommand();
            executor.EndCompositeCommand();

            CollectionAssert.AreEqual(new[] { "A", "B" }, log);
            Assert.IsFalse(executor.CanUndo);
            Assert.IsFalse(executor.CanRedo);
        }

        [TestMethod]
        public void EndCompositeCommand_NoMatchingBegin_ThrowsInvalidOperationException()
        {
            var (executor, log, _) = CreateMockExecutor();
            ExecuteCommand(executor, log, "A");
            Assert.ThrowsException<InvalidOperationException>(() => executor.EndCompositeCommand());
        }

        [TestMethod]
        public void EndCompositeCommand_MoreEndsThanBegins_ThrowsInvalidOperationException()
        {
            var (executor, log, _) = CreateMockExecutor();
            executor.BeginCompositeCommand();
            executor.BeginCompositeCommand();
            ExecuteCommand(executor, log, "A");
            executor.EndCompositeCommand();
            executor.EndCompositeCommand();
            Assert.ThrowsException<InvalidOperationException>(() => executor.EndCompositeCommand());
        }

        [TestMethod]
        public void Clear_OpenCompositeCommand_ThrowsInvalidOperationException()
        {
            var (executor, log, _) = CreateMockExecutor();
            executor.BeginCompositeCommand();
            ExecuteCommand(executor, log, "A");
            Assert.ThrowsException<InvalidOperationException>(() => executor.Clear());
        }

        [TestMethod]
        public void Undo_OpenCompositeCommand_ThrowsInvalidOperationException()
        {
            var (executor, log, _) = CreateMockExecutor();
            executor.BeginCompositeCommand();
            ExecuteCommand(executor, log, "A");
            Assert.ThrowsException<InvalidOperationException>(() => executor.Undo());
        }

        [TestMethod]
        public void Redo_OpenCompositeCommand_ThrowsInvalidOperationException()
        {
            var (executor, log, _) = CreateMockExecutor();
            executor.BeginCompositeCommand();
            ExecuteCommand(executor, log, "A");
            Assert.ThrowsException<InvalidOperationException>(() => executor.Redo());
        }

        [TestMethod]
        public void Undo_OpenOutermostCompositeCommand_ThrowsInvalidOperationException()
        {
            var (executor, log, _) = CreateMockExecutor();
            executor.BeginCompositeCommand();
            executor.BeginCompositeCommand();
            ExecuteCommand(executor, log, "A");
            executor.EndCompositeCommand();
            Assert.ThrowsException<InvalidOperationException>(() => executor.Undo());
        }

        [TestMethod]
        public void Execute_NoCompositeCommand_StatusChangedRaisedForEveryCommand()
        {
            var (executor, log, statusChangedCount) = CreateMockExecutor();
            ExecuteCommand(executor, log, "A");
            ExecuteCommand(executor, log, "B");
            Assert.AreEqual(expected: 2, statusChangedCount());
            CollectionAssert.AreEqual(new[] { "A", "B" }, log);

            executor.Undo();
            CollectionAssert.AreEqual(new[] { "A" }, log);
            executor.Redo();
            CollectionAssert.AreEqual(new[] { "A", "B" }, log);
        }

        [TestMethod]
        public void EndCompositeCommand_SingleComposite_StatusChangedRaisedOnce()
        {
            var (executor, log, statusChangedCount) = CreateMockExecutor();
            executor.BeginCompositeCommand();
            ExecuteCommand(executor, log, "A");
            ExecuteCommand(executor, log, "B");
            executor.EndCompositeCommand();

            Assert.AreEqual(expected: 1, statusChangedCount());
            executor.Undo();
            Assert.AreEqual(expected: 0, log.Count);
            Assert.IsFalse(executor.CanUndo);
        }

        [TestMethod]
        public void EndCompositeCommand_EmptyComposite_NoStatusChanged()
        {
            var (executor, _, statusChangedCount) = CreateMockExecutor();
            executor.BeginCompositeCommand();
            executor.EndCompositeCommand();
            Assert.AreEqual(expected: 0, statusChangedCount());
            Assert.IsFalse(executor.CanUndo);
        }

        [TestMethod]
        public void BeginCompositeCommand_AfterPreviousComposite_StartsNewHistoryEntry()
        {
            var (executor, log, statusChangedCount) = CreateMockExecutor();
            executor.BeginCompositeCommand();
            ExecuteCommand(executor, log, "A");
            executor.EndCompositeCommand();
            executor.BeginCompositeCommand();
            ExecuteCommand(executor, log, "B");
            executor.EndCompositeCommand();

            Assert.AreEqual(expected: 2, statusChangedCount());
            executor.Undo();
            CollectionAssert.AreEqual(new[] { "A" }, log);
            executor.Undo();
            Assert.AreEqual(expected: 0, log.Count);
        }
    }
}
