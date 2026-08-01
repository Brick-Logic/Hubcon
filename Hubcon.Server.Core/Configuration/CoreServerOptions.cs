using System.Collections.Concurrent;
using Hubcon.Server.Abstractions.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace Hubcon.Server.Core.Configuration
{
    /// <summary>
    /// The implementation of the core server options.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class CoreServerOptions : ICoreServerOptions, IInternalServerOptions
    {
        private bool? detailedErrorsEnabled;
        private Action<IEndpointConventionBuilder>? endpointConventions;
        private Action<RouteHandlerBuilder>? routeHandlerBuilderConfig;
        private bool? throttlingIsDisabled;
        private Func<string, IServiceProvider, (ClaimsPrincipal, DateTime expirationDate)?>? tokenHandler;
        private readonly Dictionary<Type, HubconTransportAttribute> _defaultTransportAttributes = new Dictionary<Type, HubconTransportAttribute>();
        private TokenBucketRateLimiterOptions? _globalRateLimiterOptions;
        private readonly Dictionary<HubconTransportAttribute, Type> _authHandlerTypes = new Dictionary<HubconTransportAttribute, Type>();
        private readonly Dictionary<HubconTransportAttribute, TransportSettings> _transportSettings = new Dictionary<HubconTransportAttribute, TransportSettings>();
        private readonly ConcurrentDictionary<string, object?> _externalSettings = new();
        
        /// <inheritdoc/>
        public bool DetailedErrorsEnabled => detailedErrorsEnabled ?? false;

        /// <inheritdoc/>
        public Action<IEndpointConventionBuilder>? EndpointConventions => endpointConventions;

        /// <inheritdoc/>
        public Action<RouteHandlerBuilder>? RouteHandlerBuilderConfig => routeHandlerBuilderConfig;
        
        /// <inheritdoc/>
        public IReadOnlyDictionary<Type, HubconTransportAttribute> DefaultTransports => _defaultTransportAttributes;

        /// <inheritdoc/>
        public IReadOnlyDictionary<HubconTransportAttribute, Type> AuthHandlerTypes => _authHandlerTypes;

        /// <inheritdoc/>
        public IReadOnlyDictionary<HubconTransportAttribute, TransportSettings> TransportSettings => _transportSettings;

        /// <inheritdoc/>
        public bool ThrottlingIsDisabled => throttlingIsDisabled ?? false;
        
        /// <inheritdoc/>
        public IReadOnlyDictionary<string, object?> ExternalSettings => _externalSettings;

        /// <inheritdoc/>
        public TokenBucketRateLimiterOptions GlobalRateLimiterOptions => _globalRateLimiterOptions ?? new TokenBucketRateLimiterOptions()
        {
            AutoReplenishment = true,
            QueueLimit = 5000,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            TokenLimit = 999999999,
            TokensPerPeriod = 999999999
        };
        
        /// <inheritdoc/>
        public ICoreServerOptions EnableRequestDetailedErrors(bool enabled = true)
        {
            detailedErrorsEnabled ??= enabled;
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions UseGlobalHttpConfigurations(Action<IEndpointConventionBuilder> configure)
        {
            endpointConventions ??= configure;
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions UseGlobalRouteHandlerBuilder(Action<RouteHandlerBuilder> configure)
        {
            routeHandlerBuilderConfig ??= configure;
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions DisableAllRateLimiters()
        {
            throttlingIsDisabled ??= true;
            return this;
        }
        
        /// <inheritdoc/>
        public ICoreServerOptions ConfigureTransport<T>(Action<TransportSettings> configurator) where T : HubconTransportAttribute, new()
        {
            var transport = HubconTransportAttribute.GetDefault<T>();
            if (_transportSettings.TryGetValue(transport, out var settings))
            {
                configurator.Invoke(settings);
            }
            else
            {
                _transportSettings.TryAdd(transport, transport.DefaultTransportSettings);
            }

            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions AddTransport<T>() where T : HubconTransportAttribute, new()
        {
            _defaultTransportAttributes.TryAdd(typeof(T), new T());
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions AddTransport<T>(T transportAttribute) where T : HubconTransportAttribute
        {
            _defaultTransportAttributes.TryAdd(typeof(T), transportAttribute);
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions SetGlobalRateLimiter(TokenBucketRateLimiterOptions options)
        {
            _globalRateLimiterOptions ??= options;
            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions SetGlobalRateLimiter(int requests, int millisecondsToReplenish = 1000, int queueLimit = 0, int rateTokenLimit = 0)
        {
            static int GetOrDefault(int limit, int defaultLimit)
            {
                return limit switch
                {
                    0 => defaultLimit,
                    var l => l
                };
            }

            _globalRateLimiterOptions ??= new TokenBucketRateLimiterOptions()
            {
                TokenLimit = GetOrDefault(rateTokenLimit == 0
                    ? requests :
                    rateTokenLimit, 999999999),

                TokensPerPeriod = GetOrDefault(requests, 999999999),

                ReplenishmentPeriod = millisecondsToReplenish == 0
                    ? TimeSpan.FromSeconds(1)
                    : TimeSpan.FromMilliseconds(millisecondsToReplenish),

                AutoReplenishment = true,
                QueueLimit = GetOrDefault(queueLimit, requests * 2),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            };

            return this;
        }

        /// <inheritdoc/>
        public ICoreServerOptions AddTransportAuth<TTransportAttribute, TAuthHandler>()
            where TTransportAttribute : HubconTransportAttribute, new()
            where TAuthHandler : class, IAuthHandler
        {
            _authHandlerTypes.TryAdd(HubconTransportAttribute.GetDefault<TTransportAttribute>(), typeof(TAuthHandler));
            return this;
        }
        
        /// <inheritdoc/>
        public ICoreServerOptions AddSetting(string key, object? value)
        {
            if(_externalSettings.TryGetValue(key, out var result))
            {
                _externalSettings.TryUpdate(key, value, result);
            }
            else
            {
                _externalSettings.TryAdd(key, value);
            }

            return this;
        }
        
        /// <inheritdoc/>
        public TransportSettings GetTransportSettings<T>() where T: HubconTransportAttribute, new()
        {
            var defaultTransport = HubconTransportAttribute.GetDefault<T>();
            
            if (_transportSettings.TryGetValue(defaultTransport, out var settings)) return settings;
            
            settings = defaultTransport.DefaultTransportSettings;
            _transportSettings.Add(defaultTransport, settings);

            return settings;
        }
        
        /// <inheritdoc/>
        public TransportSettings GetTransportSettings(HubconTransportAttribute transport)
        {
            if (_transportSettings.TryGetValue(transport, out var settings)) return settings;
            
            settings = transport.DefaultTransportSettings;
            _transportSettings.Add(transport, settings);

            return settings;
        }
    }
}