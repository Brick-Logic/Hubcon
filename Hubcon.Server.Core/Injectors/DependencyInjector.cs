using Hubcon.Shared.Abstractions.Interfaces;
using System.Linq.Expressions;
using System.Reflection;

namespace Hubcon.Server.Core.Injectors
{
    internal sealed class DependencyInjector<T, TProp> : IDependencyInjector<T, TProp>
    {
        IServiceProvider _serviceProvider;
        private readonly object _instance;

        public DependencyInjector(IServiceProvider serviceProvider, object instance)
        {
            _serviceProvider = serviceProvider;
            _instance = instance;
        }

        public IPropertyInjector Inject(Expression<Func<T, TProp>> propertyExpression)
        {
            var memberExpression = (MemberExpression)propertyExpression.Body;
            PropertyInfo propertyInfo = (PropertyInfo)memberExpression.Member;

            return new PropertyInjector(_serviceProvider, _instance, propertyInfo);
        }

        public IPropertyInjector Inject(PropertyInfo propertyInfo)
        {
            return new PropertyInjector(_serviceProvider, _instance, propertyInfo);
        }

    }
}
