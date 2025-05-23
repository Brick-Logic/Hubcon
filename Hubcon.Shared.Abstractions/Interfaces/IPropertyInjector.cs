namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IPropertyInjector
    {
        void WithType(Type type);
        void WithType<TType>();
        void WithValue(object? value);
    }
}