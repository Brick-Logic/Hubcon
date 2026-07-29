#pragma warning disable CS1591
using Hubcon.Client.Abstractions.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hubcon.Shared.Abstractions.Interfaces;


namespace Hubcon.Client.Integration.Client
{
    public sealed class HubconClient : IHubconClient
    {
        private static readonly ConcurrentDictionary<IOperationOptions, bool> _shouldTrace = new();
        
        public async ValueTask SendAsync<T>(
            IOperationRequest request,
            IClientOperationContext context,
            CancellationToken cancellationToken)
        {
            try
            {
                await context.AcquireRateLimiter();

                var tracingEnabled = _shouldTrace.GetOrAdd(context.OperationOptions, _ => context.OperationOptions.TracingEnabled ?? context.ContractOptions.TracingEnabled ?? context.ClientOptions.TracingEnabled ?? false);
                if(tracingEnabled) 
                    HubconContext.Current.AddTracing();
                
                var authManager = WrappedContext.CurrentWrapped.ShouldCheckAuth ? context.AuthenticationManagerFactory?.Invoke() : null;
                if (authManager != null && context.RequiresAuthentication && !authManager.IsSessionActive && authManager.ShouldRefreshSession)
                {
                    var result = await authManager.TryRefreshSessionAsync();

                    if (result.IsFailure)
                    {
                        await context.SetResponse(HubconResponse.InternalError<IAsyncEnumerable<JsonElement>>(
                            null!,
                            "Received an error when trying to refresh token from '" + authManager.GetType().Name + "' authentication manager. Message: " + result.ErrorMessage));
                        return;
                    }
                }

                await context.OperationOptions.CallValidationHook(context.ScopedServiceProvider, request, cancellationToken);
                await context.CallHooks(HookType.OnSend, cancellationToken);

                await context.Transport.SendAsync<T>(request, context, cancellationToken);

                await context.CallHooks(HookType.OnAfterSend, cancellationToken);
                await context.CallHooks(HookType.OnResponse, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                await context.CallHooksAndInterceptors(HookType.OnError, cancellationToken);

                if (HubconContext.Current?.IsWrapped == true)
                {
                    await context.SetResponse(HubconResponse.Cancelled());
                    return;
                }

                throw;
            }
            catch (Exception ex)
            {
                await context.CallHooksAndInterceptors(HookType.OnError, cancellationToken);

                if (HubconContext.Current?.IsWrapped == true)
                {
                    HubconContext.Current.SetException(ex);
                    await context.SetResponse(HubconResponse.InternalError(ex));
                    return;
                }

                throw;
            }
        }

        public async ValueTask CallAsync(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken)
        {
            try
            {
                await context.AcquireRateLimiter();
                
                var tracingEnabled = _shouldTrace.GetOrAdd(context.OperationOptions, _ => context.OperationOptions.TracingEnabled ?? context.ContractOptions.TracingEnabled ?? context.ClientOptions.TracingEnabled ?? false);
                if(tracingEnabled) 
                    HubconContext.Current.AddTracing();
                
                await context.CallValidationHooks();

                var authManager = WrappedContext.CurrentWrapped.ShouldCheckAuth ? context.AuthenticationManagerFactory?.Invoke() : null;
                if (authManager != null && context.RequiresAuthentication && !authManager.IsSessionActive && authManager.ShouldRefreshSession)
                {
                    var result = await authManager.TryRefreshSessionAsync();

                    if (result.IsFailure)
                    {
                        await context.SetResponse(HubconResponse.InternalError<IAsyncEnumerable<JsonElement>>(
                            null!,
                            "Received an error when trying to refresh token from '" + authManager.GetType().Name + "' authentication manager. Message: " + result.ErrorMessage));
                        return;
                    }
                }

                await context.Transport.CallAsync(request, context, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                await context.CallHooksAndInterceptors(HookType.OnError, cancellationToken);

                if (HubconContext.Current?.IsWrapped == true)
                {
                    await context.SetResponse(HubconResponse.Cancelled());
                    return;
                }

                throw;
            }
            catch (Exception ex)
            {
                await context.CallHooksAndInterceptors(HookType.OnError, cancellationToken);

                if (HubconContext.Current?.IsWrapped == true)
                {
                    HubconContext.Current.SetException(ex);
                    await context.SetResponse(HubconResponse.InternalError(ex));
                    return;
                }

                throw;
            }
        }

        public async ValueTask<IAsyncEnumerable<JsonElement>> GetStream(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default)
        {
            IAsyncEnumerable<JsonElement>? enumerable = null;

            try
            {
                await context.AcquireRateLimiter();
                
                var tracingEnabled = _shouldTrace.GetOrAdd(context.OperationOptions, _ => context.OperationOptions.TracingEnabled ?? context.ContractOptions.TracingEnabled ?? context.ClientOptions.TracingEnabled ?? false);
                if(tracingEnabled) 
                    HubconContext.Current.AddTracing();
                
                await context.CallValidationHooks();

                var authManager = WrappedContext.CurrentWrapped.ShouldCheckAuth ? context.AuthenticationManagerFactory?.Invoke() : null;
                if (authManager != null && context.RequiresAuthentication && !authManager.IsSessionActive && authManager.ShouldRefreshSession)
                {
                    var result = await authManager.TryRefreshSessionAsync();

                    if (result.IsFailure)
                    {
                        await context.SetResponse(HubconResponse.InternalError<IAsyncEnumerable<JsonElement>>(
                            null!,
                            "Received an error when trying to refresh token from '" + authManager.GetType().Name + "' authentication manager. Message: " + result.ErrorMessage));
                        return default!;
                    }
                }

                await context.CallHooksAndInterceptors(HookType.OnSend, cancellationToken);

                enumerable = await context.Transport.GetStream(request, context, cancellationToken);
            }
            catch (Exception ex)
            {
                await context.CallHooksAndInterceptors(HookType.OnError, cancellationToken);

                if (HubconContext.Current?.IsWrapped == true)
                {
                    HubconContext.Current.SetException(ex);
                    await context.SetResponse(HubconResponse.InternalError<IAsyncEnumerable<JsonElement>>(ex));
                    return default!;
                }

                throw;
            }

            await context.CallHooksAndInterceptors(HookType.OnSubscribed, cancellationToken);
            return enumerable;
        }



        public async ValueTask Ingest<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken)
        {
            try
            {
                await context.AcquireRateLimiter();
                
                var tracingEnabled = _shouldTrace.GetOrAdd(context.OperationOptions, _ => context.OperationOptions.TracingEnabled ?? context.ContractOptions.TracingEnabled ?? context.ClientOptions.TracingEnabled ?? false);
                if(tracingEnabled) 
                    HubconContext.Current.AddTracing();
                
                await context.CallValidationHooks();

                var authManager = WrappedContext.CurrentWrapped.ShouldCheckAuth ? context.AuthenticationManagerFactory?.Invoke() : null;
                if (authManager != null && context.RequiresAuthentication && !authManager.IsSessionActive && authManager.ShouldRefreshSession)
                {
                    var result = await authManager.TryRefreshSessionAsync();

                    if (result.IsFailure)
                    {
                        await context.SetResponse(HubconResponse.InternalError<IAsyncEnumerable<JsonElement>>(
                            null!,
                            "Received an error when trying to refresh token from '" + authManager.GetType().Name + "' authentication manager. Message: " + result.ErrorMessage));
                        return;
                    }
                }

                await context.CallHooksAndInterceptors(HookType.OnSend, cancellationToken);

                await context.Transport.Ingest<T>(request, context, cancellationToken);

                await context.CallHooksAndInterceptors(HookType.OnAfterSend, cancellationToken);
                await context.CallHooksAndInterceptors(HookType.OnResponse, cancellationToken);
            }
            catch (Exception ex)
            {
                await context.CallHooksAndInterceptors(HookType.OnError, cancellationToken);

                if (HubconContext.Current?.IsWrapped == true)
                {
                    HubconContext.Current.SetException(ex);
                    await context.SetResponse(HubconResponse.InternalError<T>(ex));
                    return;
                }

                throw;
            }
        }
    }
}