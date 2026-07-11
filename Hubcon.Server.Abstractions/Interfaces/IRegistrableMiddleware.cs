using Microsoft.Extensions.DependencyInjection;

namespace Hubcon.Server.Abstractions.Interfaces;

public interface IRegisterer
{
    public IServiceCollection Register(IServiceCollection serviceCollection);
    public TService Get<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService>(IServiceProvider services) where TService: class;
}

public interface IRegisterer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] out TService> : IRegisterer where TService : class
{
    public TService Get(IServiceProvider services);
}