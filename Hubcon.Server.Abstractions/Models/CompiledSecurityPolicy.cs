using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using Hubcon.Server.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Hubcon
{
    /// <summary>
    /// Represents the resolved and immutable security configuration for a Hubcon operation.
    /// This record consolidates roles, policies, and custom handlers into a single 
    /// object to avoid redundant reflection lookups during the request lifecycle.
    /// </summary>
    /// <param name="Handlers">A collection of custom authentication and authorization handlers associated with the operation.</param>
    /// <param name="Roles">The specific security roles required to access this operation.</param>
    /// <param name="Policies">The specific named authorization policies that must be satisfied.</param>
    /// <param name="AllowAnonymous">Indicates whether the operation can be accessed without an authenticated session.</param>
    [StructLayout(LayoutKind.Sequential)]
    public sealed class CompiledSecurityPolicy(
        IReadOnlyList<IUseAuthAttribute> Handlers,
        string[] Roles,
        string[] Policies,
        bool AllowAnonymous
    )
    {
        public IReadOnlyList<IUseAuthAttribute> Handlers { get; } = Handlers;
        private FrozenDictionary<Type, (IUseAuthAttribute, IAuthHandler)>? AuthHandlers { get; set; }
        public string[] Roles { get; } = Roles;
        public string[] Policies { get; } = Policies;
        public bool AllowAnonymous { get; } = AllowAnonymous;


        public async ValueTask<ClaimsPrincipal?> Execute(IOperationContext context, IServiceProvider serviceProvider)
        {
            ClaimsPrincipal? user = null;

            if (AuthHandlers == null)
            {
                var handlers = new Dictionary<Type, (IUseAuthAttribute, IAuthHandler)>();
                for(int i = 0; i < Handlers.Count; i++)
                {
                    var useHandler = Handlers[i];
                    var handler = (IRegisterer)Handlers[i];
                    var newAuthHandler = handler!.Get<IAuthHandler>(serviceProvider);
                    handlers.TryAdd(useHandler.HandlerType, (useHandler, newAuthHandler));
                }

                AuthHandlers = handlers.ToFrozenDictionary();
            }

            if (AuthHandlers.Count == 1)
            {
                var handler = Handlers[0];
                var handlerPair = AuthHandlers[handler.HandlerType];
                return await handlerPair.Item2.AuthenticateAsync(context, handlerPair.Item1);
            }
            else
            {
                for (int i = 0; i < Handlers.Count; i++)
                {
                    var handler = Handlers[i];
                    AuthHandlers.TryGetValue(handler.HandlerType, out var handlerPair);
                    user = await handlerPair.Item2!.AuthenticateAsync(context, handlerPair.Item1);

                    if (user != null)
                        return user;
                }
            }

            return user;
        }
    }
}
