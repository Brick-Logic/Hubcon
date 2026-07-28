namespace Hubcon;

public interface IEndpointInvoker
{
    public object? Invoke(object controller, object? wrappedParameters, CancellationToken cancellationToken);
}