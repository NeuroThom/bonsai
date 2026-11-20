using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Bonsai.Design;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Bonsai.Editor.Tests
{
    [TestClass]
    public class EditorToolbarServiceTests
    {
        const string ButtonText = "Toggle Plugin Panel";
        readonly TraceRecorder traceRecorder = new();
        ToolStrip toolStrip;
        ToolStripButton editorButton;
        EditorToolbarService service;

        [TestInitialize]
        public void Initialize()
        {
            toolStrip = new ToolStrip();
            editorButton = new ToolStripButton();
            toolStrip.Items.Add(editorButton);
            service = new EditorToolbarService(toolStrip);
            Trace.Listeners.Add(traceRecorder);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Trace.Listeners.Remove(traceRecorder);
            toolStrip.Dispose();
        }

        static Bitmap CreateImage()
        {
            return new Bitmap(16, 16);
        }

        ToolStripButton AddButton(Action onClick = null)
        {
            service.AddButton(ButtonText, CreateImage(), onClick);
            return (ToolStripButton)toolStrip.Items[toolStrip.Items.Count - 1];
        }

        [TestMethod]
        public void AddButton_ExistingEditorButtons_AppendsButtonAfterEditorButtons()
        {
            using var image = CreateImage();
            service.AddButton(ButtonText, image, null);
            Assert.AreEqual(2, toolStrip.Items.Count);
            Assert.AreSame(editorButton, toolStrip.Items[0]);
            var button = toolStrip.Items[1];
            Assert.AreSame(image, button.Image);
            Assert.AreEqual(ButtonText, button.Text);
            Assert.AreEqual(ToolStripItemDisplayStyle.Image, button.DisplayStyle);
        }

        [TestMethod]
        public void AddButton_TwoPluginButtons_KeepsTheOrderTheyWereAddedIn()
        {
            var first = AddButton();
            var second = AddButton();
            Assert.AreEqual(1, toolStrip.Items.IndexOf(first));
            Assert.AreEqual(2, toolStrip.Items.IndexOf(second));
        }

        [TestMethod]
        public void AddButton_DisplayText_IsUsedAsTheButtonTooltip()
        {
            var button = AddButton();
            Assert.IsTrue(button.AutoToolTip);
            Assert.AreEqual(ButtonText, button.ToolTipText);
        }

        [TestMethod]
        public void AddButton_Button_IsOwnedByTheEditorToolbar()
        {
            var button = AddButton();
            Assert.AreSame(toolStrip, button.Owner);
            Assert.AreSame(editorButton.Owner, button.Owner);
        }

        [TestMethod]
        public void Checked_SetThroughHandle_UpdatesTheButton()
        {
            using var image = CreateImage();
            var handle = service.AddButton(ButtonText, image, null);
            var button = (ToolStripButton)toolStrip.Items[1];
            Assert.IsFalse(handle.Checked);
            handle.Checked = true;
            Assert.IsTrue(button.Checked);
            Assert.IsTrue(handle.Checked);
            handle.Checked = false;
            Assert.IsFalse(button.Checked);
        }

        [TestMethod]
        public void Enabled_SetThroughHandle_UpdatesTheButton()
        {
            using var image = CreateImage();
            var handle = service.AddButton(ButtonText, image, null);
            var button = (ToolStripButton)toolStrip.Items[1];
            Assert.IsTrue(handle.Enabled);
            handle.Enabled = false;
            Assert.IsFalse(button.Enabled);
            Assert.IsFalse(handle.Enabled);
            handle.Enabled = true;
            Assert.IsTrue(button.Enabled);
        }

        [TestMethod]
        public void Remove_AddedButton_RestoresTheOriginalToolbarItems()
        {
            var items = new List<ToolStripItem>();
            foreach (ToolStripItem item in toolStrip.Items)
            {
                items.Add(item);
            }

            using var image = CreateImage();
            var handle = service.AddButton(ButtonText, image, null);
            handle.Remove();
            CollectionAssert.AreEqual(items, toolStrip.Items);
        }

        [TestMethod]
        public void Remove_CalledTwice_LeavesTheToolbarUnchanged()
        {
            using var image = CreateImage();
            var handle = service.AddButton(ButtonText, image, null);
            handle.Remove();
            handle.Remove();
            Assert.AreEqual(1, toolStrip.Items.Count);
            Assert.AreSame(editorButton, toolStrip.Items[0]);
        }

        [TestMethod]
        public void Click_Callback_CallsTheCallbackOnce()
        {
            var clicks = 0;
            var button = AddButton(() => clicks++);
            button.PerformClick();
            Assert.AreEqual(1, clicks);
        }

        [TestMethod]
        public void Click_ThrowingCallback_TracesTheFailureWithoutPropagating()
        {
            var button = AddButton(() => throw new InvalidOperationException());
            button.PerformClick();
            var trace = traceRecorder.Single();
            StringAssert.Contains(trace, ButtonText);
            StringAssert.Contains(trace, nameof(InvalidOperationException));
        }

        [TestMethod]
        public void Click_ThrowingCallback_KeepsTheButtonOnTheToolbar()
        {
            var button = AddButton(() => throw new InvalidOperationException());
            button.PerformClick();
            button.PerformClick();
            Assert.AreEqual(2, traceRecorder.Count);
            Assert.AreEqual(2, toolStrip.Items.Count);
            Assert.AreSame(button, toolStrip.Items[1]);
        }

        [TestMethod]
        public void Click_MissingCallback_DoesNothing()
        {
            var button = AddButton(onClick: null);
            button.PerformClick();
            Assert.AreEqual(0, traceRecorder.Count);
        }

        [TestMethod]
        public void AddButton_ToolbarRestyled_KeepsTheButtonAndItsRenderer()
        {
            var button = AddButton();
            toolStrip.Renderer = new ToolStripProfessionalRenderer();
            Assert.AreSame(toolStrip, button.Owner);
            Assert.AreSame(toolStrip.Renderer, button.Owner.Renderer);
            Assert.AreSame(editorButton.Owner.Renderer, button.Owner.Renderer);
        }

        [TestMethod]
        public void AddButton_NullToolbar_ThrowsArgumentNullException()
        {
            Assert.ThrowsException<ArgumentNullException>(() => new EditorToolbarService(null));
        }

        class TraceRecorder : TraceListener
        {
            readonly List<string> messages = new();

            public int Count
            {
                get { return messages.Count; }
            }

            public override void Write(string message)
            {
            }

            public override void WriteLine(string message)
            {
                messages.Add(message);
            }

            public string Single()
            {
                Assert.AreEqual(1, messages.Count, string.Join(Environment.NewLine, messages));
                return messages[0];
            }
        }
    }
}
