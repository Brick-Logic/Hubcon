using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Server.Core.Configuration
{
    public class CoreServerOptions : ICoreServerOptions, IInternalServerOptions
    {
        private int? maxWsSize;
        private int? maxHttpSize;
        private TimeSpan? wsTimeout;
        private TimeSpan? httpTimeout;
        private bool? pongEnabled;
        private LogLevel? logLevel;
        private string? wsPrefix;
        private string? httpPrefix;

        private bool? allowWsIngest;
        private bool? allowWsSubs;
        private bool? allowWsEvents;
        private bool? allowWsMethods;
        private bool? websocketRequiresPing;
        private bool? messageRetryIsEnabled;

        // Defaults
        public int MaxWebSocketMessageSize => maxWsSize ?? (64 * 1024); // 64 KB
        public int MaxHttpMessageSize => maxHttpSize ?? (128 * 1024);   // 128 KB


        
        public TimeSpan WebSocketTimeout => wsTimeout ?? TimeSpan.FromSeconds(30);
      
        public TimeSpan HttpTimeout => httpTimeout ?? TimeSpan.FromSeconds(15);
       
        public bool WebSocketPongEnabled => pongEnabled ?? true;
        
        public LogLevel GlobalLogLevel => logLevel ?? LogLevel.Information;

        public string WebSocketPathPrefix => wsPrefix ?? "/ws";
        public string HttpPathPrefix => httpPrefix ?? "/";

        public bool WebSocketIngestIsAllowed => allowWsIngest ?? true;
        public bool WebSocketSubscriptionIsAllowed => allowWsSubs ?? true;
        public bool WebSocketEventsIsAllowed => allowWsEvents ?? true;
        public bool WebSocketMethodsIsAllowed => allowWsMethods ?? false;
        public bool WebsocketRequiresPing => websocketRequiresPing ?? true;
        public bool MessageRetryIsEnabled => messageRetryIsEnabled ?? false;

        public ICoreServerOptions SetMaxWebSocketMessageSize(int bytes)
        {
            maxWsSize ??= bytes;
            return this;
        }

        public ICoreServerOptions SetMaxHttpMessageSize(int bytes)
        {
            maxHttpSize ??= bytes;
            return this;
        }

        public ICoreServerOptions SetWebSocketTimeout(TimeSpan timeout)
        {
            wsTimeout ??= timeout;
            return this;
        }

        public ICoreServerOptions SetHttpTimeout(TimeSpan timeout)
        {
            httpTimeout ??= timeout;
            return this;
        }

        public ICoreServerOptions EnableWebSocketPong(bool enabled = true)
        {
            pongEnabled ??= enabled;
            return this;
        }

        public ICoreServerOptions SetGlobalLogLevel(LogLevel level)
        {
            logLevel ??= level;
            return this;
        }

        public ICoreServerOptions AddLogging()
        {
            logLevel ??= LogLevel.Information; // o dejarlo como está
            return this;
        }

        public ICoreServerOptions SetWebSocketPathPrefix(string prefix)
        {
            wsPrefix ??= prefix;
            return this;
        }

        public ICoreServerOptions SetHttpPathPrefix(string prefix)
        {
            httpPrefix ??= prefix;
            return this;
        }

        public ICoreServerOptions AllowWebSocketIngest(bool enabled = true)
        {
            allowWsIngest ??= enabled;
            return this;
        }

        public ICoreServerOptions AllowWebSocketSubscriptions(bool enabled = true)
        {
            allowWsSubs ??= enabled;
            return this;
        }

        public ICoreServerOptions AllowWebSocketEvents(bool enabled = true)
        {
            allowWsEvents ??= enabled;
            return this;
        }

        public ICoreServerOptions AllowWebSocketNormalMethods(bool enabled = true)
        {
            allowWsMethods ??= enabled;
            return this;
        }

        public ICoreServerOptions RequirePing(bool enabled = true)
        {
            websocketRequiresPing ??= enabled;
            return this;
        }

        public ICoreServerOptions AllowRetryableMessages(bool enabled = true)
        {
            messageRetryIsEnabled ??= enabled;
            return this;
        }
    }
}
