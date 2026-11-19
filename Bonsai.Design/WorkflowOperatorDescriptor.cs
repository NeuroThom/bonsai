namespace Bonsai.Design
{
    /// <summary>
    /// Provides information about an operator which is available in the workflow editor
    /// toolbox, and which can be used to create new nodes in the workflow.
    /// </summary>
    public class WorkflowOperatorDescriptor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowOperatorDescriptor"/> class
        /// using the specified operator metadata.
        /// </summary>
        /// <param name="name">The display name of the operator.</param>
        /// <param name="namespace">The namespace the operator belongs to.</param>
        /// <param name="fullyQualifiedName">
        /// The fully qualified name uniquely identifying the operator.
        /// </param>
        /// <param name="description">The description of the operator.</param>
        /// <param name="elementCategory">
        /// The category of workflow element which the operator represents.
        /// </param>
        public WorkflowOperatorDescriptor(
            string name,
            string @namespace,
            string fullyQualifiedName,
            string description,
            ElementCategory elementCategory)
        {
            Name = name;
            Namespace = @namespace;
            FullyQualifiedName = fullyQualifiedName;
            Description = description;
            ElementCategory = elementCategory;
        }

        /// <summary>
        /// Gets the display name of the operator.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the namespace the operator belongs to.
        /// </summary>
        public string Namespace { get; }

        /// <summary>
        /// Gets the fully qualified name uniquely identifying the operator. This is the
        /// name which identifies the operator when creating a new node.
        /// </summary>
        public string FullyQualifiedName { get; }

        /// <summary>
        /// Gets the description of the operator.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets the category of workflow element which the operator represents.
        /// </summary>
        public ElementCategory ElementCategory { get; }

        /// <summary>
        /// Returns a string representation of the operator, combining the namespace
        /// and the display name.
        /// </summary>
        /// <returns>
        /// A string containing the namespace and the display name of the operator.
        /// </returns>
        public override string ToString()
        {
            return string.IsNullOrEmpty(Namespace) ? Name : Namespace + "." + Name;
        }
    }
}
