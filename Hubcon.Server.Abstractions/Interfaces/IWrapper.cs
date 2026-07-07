namespace Hubcon;

public interface IWrapper
{
    public void Populate(IReadOnlyDictionary<string, object> parameters);
}