using System;

namespace Bonsai.Design
{
    /// <summary>
    /// Represents a batch of workflow edits which are applied to the workflow editor as a
    /// single undoable operation.
    /// </summary>
    /// <remarks>
    /// Edits made while the transaction is open are applied immediately, but they are
    /// recorded as a single entry in the editor undo history. Disposing the transaction
    /// without calling <see cref="Commit"/> reverses every edit made since the transaction
    /// was started, which means a batch of edits is all or nothing. A transaction which is
    /// neither committed nor disposed is closed when the editor next notices it, leaving
    /// the edits made so far applied and recorded in the undo history.
    /// </remarks>
    public interface IWorkflowEditTransaction : IDisposable
    {
        /// <summary>
        /// Gets the description of the batch of edits which was specified when the
        /// transaction was started.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Commits every edit made since the transaction was started, and records them as a
        /// single entry in the editor undo history.
        /// </summary>
        void Commit();
    }
}
