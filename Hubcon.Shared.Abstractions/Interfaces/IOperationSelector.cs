using System;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    /// <summary>
    /// Provides a strongly-typed mechanism for selecting and configuring specific operations 
    /// within a service contract of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The service contract interface type.</typeparam>
    public interface IOperationSelector<T> : Standard.Interfaces.IOperationSelector<T>
    {
        /// <summary>
        /// Selects a method or property that returns a value for configuration.
        /// </summary>
        /// <typeparam name="TResult">The return type of the operation.</typeparam>
        /// <param name="expression">A lambda expression selecting the contract member (e.g., <c>x => x.GetUser(default)</c>).</param>
        /// <returns>An <see cref="IOperationConfigurator"/> to define specific behaviors for the selected operation.</returns>
        IOperationConfigurator Configure<TResult>(Expression<Func<T, TResult>> expression);

        /// <summary>
        /// Selects an operation by its delegate signature for configuration.
        /// Useful for methods that return <see cref="Task"/> or <see cref="ValueTask"/> without a result.
        /// </summary>
        /// <param name="expression">A lambda expression selecting the member (e.g., <c>x => x.NotifyServer</c>).</param>
        /// <returns>An <see cref="IOperationConfigurator"/> to define specific behaviors for the selected operation.</returns>
        IOperationConfigurator Configure(Expression<Func<T, Delegate>> expression);
    }
}
