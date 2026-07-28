using System.Reflection;

namespace Hubcon.Server.Abstractions.Interfaces;

public interface IParameterWrapper
{
    public IWrapper GetWrapped(IReadOnlyDictionary<string, object> parameters);
}