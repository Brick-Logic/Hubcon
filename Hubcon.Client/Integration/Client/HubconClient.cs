using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Core.Configurations;
using Hubcon.Client.Core.Helpers;
using Hubcon.Client.Core.HubconInvocationContext;
using Hubcon.Client.Core.Websockets;
using Hubcon.Shared.Abstractions.Attributes;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Abstractions.Standard.Extensions;
using Hubcon.Shared.Core.Extensions;
using Hubcon.Shared.Core.Websockets.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;


namespace Hubcon.Client.Integration.Client
{
    public sealed class HubconClient : IHubconClient
    {
        public async Task<T> SendAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(
            IOperationRequest request,
            IClientOperationContext context,
            CancellationToken cancellationToken)
        {
            await context.AcquireRateLimiter();

            try
            {
                await context.OperationOptions.CallValidationHook(context.ServiceProvider, request, cancellationToken);
                await context.CallHooks(HookType.OnSend);

                var result = await context.Transport.SendAsync<T>(request, context, cancellationToken);
                result ??= HubconResponse.Fail<T>("Received an empty response");
                HubconContext.Current.SetResponse(result);

                await context.CallHooks(HookType.OnAfterSend);
                await context.CallHooks(HookType.OnResponse);

                return result.Data;
            }
            catch (OperationCanceledException)
            {
                await context.CallHooks(HookType.OnError);
                await context.CallInterceptor(InterceptorType.OnError);

                if (HubconContext.Current?.IsWrapped == true)
                {
                    HubconContext.Current.SetResponse(HubconResponse.Cancelled());
                    return default!;
                }

                throw;
            }
            catch (Exception ex)
            {
                await context.CallHooks(HookType.OnError);
                await context.CallInterceptor(InterceptorType.OnError);

                if (HubconContext.Current?.IsWrapped == true)
                {
                    HubconContext.Current.SetException(ex);
                    HubconContext.Current.SetResponse(HubconResponse.InternalError<T>(ex));
                    return default!;
                }

                throw;
            }
        }

        public async Task CallAsync(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken)
        {
            await context.AcquireRateLimiter();
            await context.CallValidationHooks();

            try
            {
                await context.Transport.CallAsync(request, context, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                await context.CallHooks(HookType.OnError);
                await context.CallInterceptor(InterceptorType.OnError);

                if (HubconContext.Current?.IsWrapped == true)
                {
                    HubconContext.Current.SetResponse(HubconResponse.Cancelled());
                    return;
                }

                throw;
            }
            catch (Exception ex)
            {
                await context.CallHooks(HookType.OnError);
                await context.CallInterceptor(InterceptorType.OnError);

                if (HubconContext.Current?.IsWrapped == true)
                {
                    HubconContext.Current.SetException(ex);
                    HubconContext.Current.SetResponse(HubconResponse.InternalError(ex));
                    return;
                }

                throw;
            }
        }

        public async IAsyncEnumerable<JsonElement> GetStream(IOperationRequest request, IClientOperationContext context, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await context.AcquireRateLimiter();
            await context.CallValidationHooks();

            IAsyncEnumerable<JsonElement>? enumerable = null;

            await context.CallHooks(HookType.OnSend);
            await context.CallInterceptor(InterceptorType.OnSend);

            try
            {
                enumerable = context.Transport.GetStream(request, context, cancellationToken);

                if (HubconContext.Current?.IsWrapped == true)
                    HubconContext.Current.SetResponse(HubconResponse.OkT<IAsyncEnumerable<JsonElement>>());
            }
            catch (Exception ex)
            {
                await context.CallHooks(HookType.OnError);
                await context.CallInterceptor(InterceptorType.OnError);

                if (HubconContext.Current?.IsWrapped == true)
                {
                    HubconContext.Current.SetException(ex);
                    HubconContext.Current.SetResponse(HubconResponse.InternalError<IAsyncEnumerable<JsonElement>>(ex));
                }

                throw;
            }

            var enumerator = enumerable.GetAsyncEnumerator(cancellationToken);

            await context.CallHooks(HookType.OnSubscribed);
            await context.CallInterceptor(InterceptorType.OnSubscribed);

            while (true)
            {
                JsonElement result = default;
                try
                {
                    if (!await enumerator.MoveNextAsync() || cancellationToken.IsCancellationRequested)
                        break;

                    await context.AcquireRateLimiter();

                    result = enumerator.Current;
                }
                catch (Exception ex)
                {
                    await context.CallHooks(HookType.OnError);
                    await context.CallInterceptor(InterceptorType.OnError);

                    if (HubconContext.Current?.IsWrapped == true)
                    {
                        HubconContext.Current.SetException(ex);
                        HubconContext.Current.SetResponse(HubconResponse.InternalError<IAsyncEnumerable<JsonElement>>(ex));
                    }

                    throw;
                }

                yield return result;
            }

            await context.CallHooks(HookType.OnUnsubscribed);
            await context.CallInterceptor(InterceptorType.OnUnsubscribed);
        }

        

        public async Task<T> Ingest<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken)
        {
            await context.AcquireRateLimiter();
            await context.CallValidationHooks();

            try
            {
                await context.CallHooks(HookType.OnSend);
                await context.CallInterceptor(InterceptorType.OnSend);

                var response = await context.Transport.Ingest<T>(request, context, cancellationToken);

                if (HubconContext.Current?.IsWrapped == true)
                    HubconContext.Current.SetResponse(response);

                await context.CallHooks(HookType.OnAfterSend);
                await context.CallInterceptor(InterceptorType.OnAfterSend);

                await context.CallHooks(HookType.OnResponse);
                await context.CallInterceptor(InterceptorType.OnResponse);

                return response.Data;
            }
            catch (Exception ex)
            {
                await context.CallHooks(HookType.OnError);
                await context.CallInterceptor(InterceptorType.OnError);

                if (HubconContext.Current?.IsWrapped == true)
                {
                    HubconContext.Current.SetException(ex);
                    HubconContext.Current.SetResponse(HubconResponse.InternalError<T>(ex));
                    return default!;
                }

                throw;
            }
        }

        public async IAsyncEnumerable<JsonElement> GetSubscription(IOperationRequest request, IClientOperationContext context, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await context.AcquireRateLimiter();
            await context.CallValidationHooks();

            IAsyncEnumerable<JsonElement>? enumerable = null;

            await context.CallHooks(HookType.OnSend);
            await context.CallInterceptor(InterceptorType.OnSend);

            try
            {
                enumerable = context.Transport.GetSubscription(request, context, cancellationToken);

                if (HubconContext.Current?.IsWrapped == true)
                    HubconContext.Current.SetResponse(HubconResponse.OkT<IAsyncEnumerable<JsonElement>>());
            }
            catch (Exception ex)
            {
                await context.CallHooks(HookType.OnError);
                await context.CallInterceptor(InterceptorType.OnError);

                if (HubconContext.Current?.IsWrapped == true)
                {
                    HubconContext.Current.SetException(ex);
                    HubconContext.Current.SetResponse(HubconResponse.InternalError<IAsyncEnumerable<JsonElement>>(ex));
                }

                throw;
            }

            var enumerator = enumerable.GetAsyncEnumerator(cancellationToken);

            await context.CallHooks(HookType.OnSubscribed);
            await context.CallInterceptor(InterceptorType.OnSubscribed);

            while (true)
            {
                JsonElement result = default;
                try
                {
                    if (!await enumerator.MoveNextAsync() || cancellationToken.IsCancellationRequested)
                        break;

                    await context.AcquireRateLimiter();

                    result = enumerator.Current;
                }
                catch (Exception ex)
                {
                    await context.CallHooks(HookType.OnError);
                    await context.CallInterceptor(InterceptorType.OnError);

                    if (HubconContext.Current?.IsWrapped == true)
                    {
                        HubconContext.Current.SetException(ex);
                        HubconContext.Current.SetResponse(HubconResponse.InternalError<IAsyncEnumerable<JsonElement>>(ex));
                    }

                    throw;
                }

                yield return result;
            }

            await context.CallHooks(HookType.OnUnsubscribed);
            await context.CallInterceptor(InterceptorType.OnUnsubscribed);
        }
    }
}