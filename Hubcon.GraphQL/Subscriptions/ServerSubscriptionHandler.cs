using Hubcon.Core.Converters;
using Hubcon.Core.Extensions;
using Hubcon.Core.Injectors.Attributes;
using Hubcon.Core.Models;
using Hubcon.Core.Models.Interfaces;
using Hubcon.GraphQL.Models;
using Hubcon.GraphQL.Models.CustomAttributes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hubcon.GraphQL.Subscriptions
{
    public class ServerSubscriptionHandler : ISubscription
    {
        public PropertyInfo Property { get; } = null!;


        private bool _isSubscribed = false;
        public bool Connected { get => _isSubscribed; }

        public event HubconEventHandler? OnEventReceived;

        public ServerSubscriptionHandler()
        {
        }

        public void AddHandler(HubconEventHandler handler)
        {
            OnEventReceived += handler;
        }

        public void RemoveHandler(HubconEventHandler handler)
        {
            OnEventReceived -= handler;
        }

        public async Task Subscribe()
        {
            await Task.CompletedTask;
        }

        public async Task Unsubscribe()
        {
            await Task.CompletedTask;
        }

        public void Build()
        {
        }

        public void Emit(object? eventValue)
        {
            OnEventReceived?.Invoke(eventValue);
        }
    }
}
