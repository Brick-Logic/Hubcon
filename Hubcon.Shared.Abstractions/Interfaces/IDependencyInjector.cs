using System.Linq.Expressions;
using System.Reflection;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IDependencyInjector<T, TProp>
    {
        IPropertyInjector Inject(Expression<Func<T, TProp>> propertyExpression);
        IPropertyInjector Inject(PropertyInfo propertyInfo);
    }
}