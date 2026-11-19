using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace Bonsai.Design
{
    /// <summary>
    /// Represents the error that occurs when an edit requested through the
    /// <see cref="IWorkflowEditingService"/> cannot be applied to the workflow.
    /// </summary>
    [Serializable]
    public class WorkflowEditException : Exception
    {
        const string CandidatesName = "Candidates";
        static readonly string[] EmptyCandidates = new string[0];
        readonly string[] candidates;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowEditException"/> class.
        /// </summary>
        public WorkflowEditException()
        {
            candidates = EmptyCandidates;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowEditException"/> class with
        /// a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public WorkflowEditException(string message)
            : base(message)
        {
            candidates = EmptyCandidates;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowEditException"/> class with
        /// a specified error message and a reference to the inner exception that is the
        /// cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">
        /// The exception that is the cause of the current exception, or a null reference
        /// (Nothing in Visual Basic) if no inner exception is specified.
        /// </param>
        public WorkflowEditException(string message, Exception innerException)
            : base(message, innerException)
        {
            candidates = EmptyCandidates;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowEditException"/> class with
        /// a specified error message and the alternatives the caller may have meant.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="candidates">
        /// The names of the operators which the caller may have meant, or a null reference
        /// (Nothing in Visual Basic) if no alternatives were found.
        /// </param>
        public WorkflowEditException(string message, IReadOnlyList<string> candidates)
            : base(message)
        {
            this.candidates = candidates == null ? EmptyCandidates : candidates.ToArray();
        }

#if NETFRAMEWORK
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowEditException"/> class with
        /// serialized data.
        /// </summary>
        /// <param name="info">
        /// The <see cref="SerializationInfo"/> that holds the serialized object data about
        /// the exception being thrown.
        /// </param>
        /// <param name="context">
        /// The <see cref="StreamingContext"/> that contains contextual information about
        /// the source or destination.
        /// </param>
        protected WorkflowEditException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            candidates = (string[])info.GetValue(CandidatesName, typeof(string[])) ?? EmptyCandidates;
        }

        /// <summary>
        /// Sets the <see cref="SerializationInfo"/> with information about the exception,
        /// including the names of the operators which the caller may have meant.
        /// </summary>
        /// <param name="info">
        /// The <see cref="SerializationInfo"/> that holds the serialized object data about
        /// the exception being thrown.
        /// </param>
        /// <param name="context">
        /// The <see cref="StreamingContext"/> that contains contextual information about
        /// the source or destination.
        /// </param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(CandidatesName, candidates);
        }
#endif

        /// <summary>
        /// Gets the names of the operators which the caller may have meant, when the name
        /// of an operator could not be resolved to exactly one match. The list is empty
        /// when no alternatives were found, or when the error has another cause.
        /// </summary>
        public IReadOnlyList<string> Candidates
        {
            get { return Array.AsReadOnly(candidates); }
        }
    }
}
