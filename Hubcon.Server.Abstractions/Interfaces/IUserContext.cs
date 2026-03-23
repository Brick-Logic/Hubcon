using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
#pragma warning disable CS1591
namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface IUserContext
    {
        IFeatureCollection Features { get; }
        HttpRequest Request { get; }
        HttpResponse Response { get; }
        ConnectionInfo Connection { get; }
        WebSocketManager WebSockets { get; }
        ClaimsPrincipal User { get; set; }
        IDictionary<object, object?> Items { get; set; }
        IServiceProvider RequestServices { get; set; }
        CancellationToken RequestAborted { get; set; }
        string TraceIdentifier { get; set; }
        ISession Session { get; set; }

        void Abort();
    }
}
