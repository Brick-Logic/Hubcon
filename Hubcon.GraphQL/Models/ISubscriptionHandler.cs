using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hubcon.GraphQL.Models
{
    public delegate void HubconEventHandler<T>(T? eventValue);

    public interface ISubscriptionHandler<T>
    {
        public event HubconEventHandler<T?>? OnEventReceived;

        public void Subscribe();
        public void Unsubscribe();
        public void Build(PropertyInfo property);
        public void AddHandler(HubconEventHandler<T?> handler);
        public void RemoveHandler(HubconEventHandler<T?> handler);
    }
}
