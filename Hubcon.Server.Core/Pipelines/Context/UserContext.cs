using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.IdentityModel.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Server.Core.Pipelines.Context
{
    public class UserContext(HttpContext context) : IUserContext
    {
        private readonly HttpContext _context = context;

        public IFeatureCollection Features => _context.Features;
        public HttpRequest Request => _context.Request;
        public HttpResponse Response => _context.Response;
        public ConnectionInfo Connection => _context.Connection;
        public WebSocketManager WebSockets => _context.WebSockets;
        public ClaimsPrincipal User { get => _context.User; set => _context.User = value; }
        public IDictionary<object, object?> Items { get => _context.Items; set => _context.Items = value; }
        public IServiceProvider RequestServices { get => _context.RequestServices; set => _context.RequestServices = value; }
        public CancellationToken RequestAborted { get => _context.RequestAborted; set => _context.RequestAborted = value; }
        public string TraceIdentifier { get => _context.TraceIdentifier; set => _context.TraceIdentifier = value; }
        public ISession Session { get => _context.Session; set => _context.Session = value; }
        public void Abort() => _context.Abort();
    }
}
