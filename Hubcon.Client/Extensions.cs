using Hubcon.Client.Core.Proxies;
using System;
using System.Threading.Tasks;
#pragma warning disable CS1591

namespace Hubcon
{
    public static class ContractExtensions
    {
        public static bool TryGetAuthenticationManager(this IControllerContract contract, out IAuthenticationManager authenticationManager)
        {
            try
            {
                if (contract is not IContractDataAccessor dataAccessor)
                {
                    authenticationManager = null!;
                    return false;
                }

                authenticationManager = dataAccessor.AuthenticationManager;
                return true;
            }
            catch
            {
                authenticationManager = null!;
                return false;
            }

        }
    }


    public static class Extensions
    {
        /// <summary>
        /// Creates a hubcon response by creating a result wrapper, while providing additional details and shielding against exceptions that disrupt normal code execution.
        /// </summary>
        public static async ValueTask<IHubconResponse<TOut?>> Execute<T, TOut>(this T contract, Func<T, Task<TOut>> call, bool shouldTryRefreshAuth = false) where T : IControllerContract
        {
            WrappedContext.SetWrapped(true);
            WrappedContext.CurrentWrapped.SetShouldCheckAuth(shouldTryRefreshAuth);
            Exception? exception = null;
            IHubconResponse<TOut> response = default!;

            try
            {
                var data = await call.Invoke(contract);
                response = WrappedContext.CurrentWrapped.GetResponse<TOut>() ?? HubconResponse.OkT(data)!;
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                if (exception != null)
                {
                    response = exception switch
                    {
                        OperationCanceledException => HubconResponse.Cancelled<TOut>(exception),
                        HubconRemoteException => HubconResponse.InternalError<TOut>(exception),
                        HubconGenericException => HubconResponse.InternalError<TOut>(exception),
                        _ => HubconResponse.InternalError<TOut>(exception)
                    };
                }
            }

            return response!;
        }

        /// <summary>
        /// Creates a hubcon response by creating a result wrapper, while providing additional details and shielding against exceptions that disrupt normal code execution.
        /// </summary>
        public static async ValueTask<IHubconResponse<TOut?>> Execute<T, TOut>(this T contract, Func<T, Task<HubconResponse<TOut>>> call, bool shouldTryRefreshAuth = false) where T : IControllerContract
        {
            WrappedContext.SetWrapped(true);
            WrappedContext.CurrentWrapped.SetShouldCheckAuth(shouldTryRefreshAuth);
            Exception? exception = null;
            HubconResponse<TOut?> response = default!;           

            try
            {
                var data = await call.Invoke(contract);
                response = (WrappedContext.CurrentWrapped.GetRawResponse() as HubconResponse<TOut?>)! ?? HubconResponse.OkT<TOut>()!;
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                if (exception != null)
                {
                    response = exception switch
                    {
                        OperationCanceledException => HubconResponse.Cancelled<TOut?>(exception),
                        HubconRemoteException => HubconResponse.InternalError<TOut?>(exception),
                        HubconGenericException => HubconResponse.InternalError<TOut?>(exception),
                        _ => HubconResponse.InternalError<TOut?>(exception)
                    };
                }
            }

            return response;
        }

        /// <summary>
        /// Creates a hubcon response by creating a result wrapper, while providing additional details and shielding against exceptions that disrupt normal code execution.
        /// </summary>
        public static async ValueTask<IHubconResponse<TOut?>> Execute<T, TOut>(this T contract, Func<T, TOut> call, bool shouldTryRefreshAuth = false) where T : IControllerContract
        {
            WrappedContext.SetWrapped(true);
            WrappedContext.CurrentWrapped.SetShouldCheckAuth(shouldTryRefreshAuth);
            Exception? exception = null;
            IHubconResponse<TOut?> response = default!;

            try
            {
                var data = call.Invoke(contract);
                response = WrappedContext.CurrentWrapped.GetResponse<TOut>() as IHubconResponse<TOut?> ?? HubconResponse.OkT(data)!;
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                if (exception != null)
                {
                    response = exception switch
                    {
                        OperationCanceledException => HubconResponse.Cancelled<TOut?>(exception),
                        HubconRemoteException => HubconResponse.InternalError<TOut?>(exception),
                        HubconGenericException => HubconResponse.InternalError<TOut?>(exception),
                        _ => HubconResponse.InternalError<TOut?>(exception)
                    };
                }
            }

            return response;
        }

        /// <summary>
        /// Creates a hubcon response by creating a result wrapper, while providing additional details and shielding against exceptions that disrupt normal code execution.
        /// </summary>
        public static async ValueTask<IResponse> Execute<T>(this T contract, Func<T, Task> call, bool shouldTryRefreshAuth = false) where T : IControllerContract
        {
            WrappedContext.SetWrapped(true);
            WrappedContext.CurrentWrapped.SetShouldCheckAuth(shouldTryRefreshAuth);
            Exception? exception = null;
            IResponse response = default!;
            try
            {
                await call.Invoke(contract);
                response = WrappedContext.CurrentWrapped.GetResponse() ?? HubconResponse.Ok()!;
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                if (exception != null)
                {                  
                    response = exception switch
                    {
                        OperationCanceledException => HubconResponse.Cancelled<IResponse>(exception),
                        HubconRemoteException => HubconResponse.InternalError<IResponse>(exception),
                        HubconGenericException => HubconResponse.InternalError<IResponse>(exception),
                        _ => HubconResponse.InternalError<IResponse>(exception)
                    };
                }
            }

            return response;
        }
    }
}
