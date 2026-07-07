using System.Reflection;

namespace Hubcon.Server.Abstractions.Interfaces;

public interface IParameterWrapper
{
    public object GetWrapped(IReadOnlyDictionary<string, object> parameters);
}