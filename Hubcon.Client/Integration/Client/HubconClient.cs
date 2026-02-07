using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Core.Configurations;
using Hubcon.Client.Core.Helpers;
using Hubcon.Client.Core.HubconInvocationContext;
using Hubcon.Client.Core.Websockets;
using Hubcon.Shared.Abstractions.Attributes;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Abstractions.Standard.Extensions;
using Hubcon.Shared.Core.Context;
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
using System.Reactive.Linq;
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
        public async ValueTask SendAsync<T>(
            IOperationRequest request,
            IClientOperationContext context,
            CancellationToken cancellationToken)
        {
            await context.AcquireRateLimiter();

            try
            {
                await context.OperationOptions.CallValidationHook(context.ScopeServiceProvider, request, cancellationToken);
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
            await context.AcquireRateLimiter();
            await context.CallValidationHooks();

            try
            {
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
            await context.AcquireRateLimiter();
            await context.CallValidationHooks();

            IAsyncEnumerable<JsonElement>? enumerable = null;

            await context.CallHooksAndInterceptors(HookType.OnSend, cancellationToken);

            try
            {
                enumerable = await context.Transport.GetStream(request, context, cancellationToken);
            }
            catch (Exception ex)
            {
                await context.CallHooksAndInterceptors(HookType.OnError, cancellationToken);

                if (HubconContext.Current?.IsWrapped == true)
                {
                    HubconContext.Current.SetException(ex);
                    await context.SetResponse(HubconResponse.InternalError<IAsyncEnumerable<JsonElement>>(ex));
                }

                throw;
            }

            await context.CallHooksAndInterceptors(HookType.OnSubscribed, cancellationToken);
            return enumerable;
        }



        public async ValueTask Ingest<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken)
        {
            await context.AcquireRateLimiter();
            await context.CallValidationHooks();

            try
            {
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

        //public async ValueTask<IObservable<JsonElement>> GetSubscription(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default)
        //{
        //    await context.AcquireRateLimiter();
        //    await context.CallValidationHooks();

        //    IObservable<JsonElement>? observable = null;

        //    await context.CallHooksAndInterceptors(HookType.OnSend, cancellationToken);

        //    try
        //    {
        //        observable = await context.Transport.GetSubscription(request, context, cancellationToken);

        //        if (HubconContext.Current?.IsWrapped == true)
        //            await context.SetResponse(HubconResponse.OkT<IAsyncEnumerable<JsonElement>>());
        //    }
        //    catch (Exception ex)
        //    {
        //        await context.CallHooksAndInterceptors(HookType.OnError, cancellationToken);

        //        if (HubconContext.Current?.IsWrapped == true)
        //        {
        //            HubconContext.Current.SetException(ex);
        //            await context.SetResponse(HubconResponse.InternalError<IAsyncEnumerable<JsonElement>>(ex));
        //        }

        //        throw;
        //    }

        //    var options = new BoundedChannelOptions(999999999);
        //    var observer = AsyncObserver.Create<JsonElement>(context.Converter, options) as ChannelAsyncObserver<JsonElement>;

        //    await context.CallHooksAndInterceptors(HookType.OnSubscribed, cancellationToken);

        //    var observerContext = context.CallContext as CallContext;

        //    if (observer != null)
        //    {
        //        async void nextMethod(JsonElement x)
        //        {
        //            await context.AcquireRateLimiter();
        //            await context.CallHooksAndInterceptors(HookType.OnEventReceived);
        //        }

        //        async void errorMethod() => await context.CallHooksAndInterceptors(HookType.OnError);

        //        async void completedMethod() => await context.CallHooksAndInterceptors(HookType.OnUnsubscribed);

        //        observer.Next += nextMethod;
        //        observer.Error += errorMethod;
        //        observer.Completed += completedMethod;

        //        _ = Task.Factory.StartNew(async () =>
        //        {
        //            try
        //            {
        //                HubconContext.UseContext(observerContext!);
        //                using (observable.Subscribe(observer))
        //                {
        //                    var enumerator = observer!
        //                        .GetAsyncEnumerable(cancellationToken)
        //                        .GetAsyncEnumerator();

        //                    while (true)
        //                    {
        //                        try
        //                        {
        //                            if (!await enumerator.MoveNextAsync() || cancellationToken.IsCancellationRequested)
        //                                break;
        //                        }
        //                        catch (Exception ex)
        //                        {
        //                            if (HubconContext.Current?.IsWrapped == true)
        //                            {
        //                                HubconContext.Current.SetException(ex);
        //                                await context.SetResponse(HubconResponse.InternalError<IAsyncEnumerable<JsonElement>>(ex));
        //                            }

        //                            await enumerator.DisposeAsync();
        //                            break;
        //                        }
        //                    }
        //                }
        //            }
        //            finally
        //            {
        //                observer!.OnCompleted();
        //                observer.Next -= nextMethod;
        //                observer.Error -= errorMethod;
        //                observer.Completed -= completedMethod;
        //            }
        //        });
        //    }

        //    await context.CallHooksAndInterceptors(HookType.OnResponse);
        //    return observable;
        //}
    }
}