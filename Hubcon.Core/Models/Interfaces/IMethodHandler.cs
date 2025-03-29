using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Models.Interfaces
{
    public interface IMethodHandler
    {
        public void BuildMethods(object instance, Type type, Action<string, MethodInfo, Delegate>? forEachMethodAction = null);
    }
}
