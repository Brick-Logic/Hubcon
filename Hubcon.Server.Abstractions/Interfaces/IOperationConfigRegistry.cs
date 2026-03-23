namespace Hubcon.Server.Abstractions.Interfaces
{
    /// <summary>
    /// Defines a registry for mapping unique operation identifiers to their 
    /// corresponding execution blueprints.
    /// </summary>
    public interface IOperationConfigRegistry
    {
        /// <summary>
        /// Creates a link between an observable ID and an operation blueprint.
        /// Typically used during the initialization of a stream or an ingest flow.
        /// </summary>
        /// <param name="observableId">The unique identifier for the specific operation instance.</param>
        /// <param name="blueprint">The execution metadata and handler for the operation.</param>
        /// <returns><see langword="true"/> if the link was successfully created; otherwise, <see langword="false"/>.</returns>
        bool Link(Guid observableId, IOperationBlueprint blueprint);

        /// <summary>
        /// Attempts to retrieve the blueprint associated with a given observable ID.
        /// </summary>
        /// <param name="observableId">The unique identifier to look up.</param>
        /// <param name="blueprint">When this method returns, contains the blueprint if found.</param>
        /// <returns><see langword="true"/> if the blueprint exists; otherwise, <see langword="false"/>.</returns>
        bool TryGet(Guid observableId, out IOperationBlueprint blueprint);

        /// <summary>
        /// Removes the link for a specific observable ID, effectively closing 
        /// the operation's configuration mapping.
        /// </summary>
        /// <param name="observableId">The identifier to unlink.</param>
        /// <returns><see langword="true"/> if the link was found and removed; otherwise, <see langword="false"/>.</returns>
        bool Unlink(Guid observableId);
    }
}
