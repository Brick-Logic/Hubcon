using Hubcon.Client.Core.Exceptions;
using Hubcon.Client.Core.HubconInvocationContext;
using Hubcon.Client.Core.Proxies;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using System.Text.Json;
using System;
using System.Threading.Tasks;

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
        static bool HandleException(Exception ex, out Exception outEx)
        {
            outEx = ex;
            return false;
        }

        /// <summary>
        /// Creates a hubcon response by creating a result wrapper, while providing additional details and shielding against exceptions that disrupt normal code execution.
        /// This extension method catches local and remote exceptions in a performant way.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="task"></param>
        /// <returns></returns>
        public static async ValueTask<IHubconResponse<TOut?>> Execute<T, TOut>(this T contract, Func<T, Task<TOut>> call) where T : IControllerContract
        {
            WrappedContext.SetWrapped(true);
            Exception? exception = null;
            IHubconResponse<TOut> response = default!;

            try
            {
                var data = await call.Invoke(contract);
                response = HubconContext.Current.GetResponse<TOut>() ?? HubconResponse.OkT(data)!;
            }
            catch (Exception ex) when (HandleException(ex, out exception))
            {
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
                        UnauthorizedAccessException => HubconResponse.Unauthorized<TOut>(exception),
                        _ => HubconResponse.InternalError<TOut>(exception)
                    };
                }
            }

            return response!;
        }

        /// <summary>
        /// Creates a hubcon response by creating a result wrapper, while providing additional details and shielding against exceptions that disrupt normal code execution.
        /// This extension method catches local and remote exceptions in a performant way.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="task"></param>
        /// <returns></returns>
        public static async ValueTask<IHubconResponse<TOut?>> ExecuteResponse<T, TOut>(this T contract, Func<T, Task<HubconResponse<TOut?>>> call) where T : IControllerContract
        {
            WrappedContext.SetWrapped(true);
            Exception? exception = null;
            IHubconResponse<TOut?> response = default!;

            try
            {
                var data = await call.Invoke(contract);
                response = data!;
            }
            catch (Exception ex) when (HandleException(ex, out exception))
            {
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
                        UnauthorizedAccessException => HubconResponse.Unauthorized<TOut?>(exception),
                        _ => HubconResponse.InternalError<TOut?>(exception)
                    };
                }
            }

            return response;
        }

        /// <summary>
        /// Creates a hubcon response by creating a result wrapper, while providing additional details and shielding against exceptions that disrupt normal code execution.
        /// This extension method catches local and remote exceptions in a performant way.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="task"></param>
        /// <returns></returns>
        public static async ValueTask<IHubconResponse<TOut?>> Execute<T, TOut>(this T contract, Func<T, TOut> call) where T : IControllerContract
        {
            WrappedContext.SetWrapped(true);
            Exception? exception = null;
            IHubconResponse<TOut?> response = default!;

            try
            {
                var data = call.Invoke(contract);
                response = HubconContext.Current?.GetResponse<TOut>() as IHubconResponse<TOut?> ?? HubconResponse.OkT(data)!;
            }
            catch (Exception ex) when (HandleException(ex, out exception))
            {
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
                        UnauthorizedAccessException => HubconResponse.Unauthorized<TOut>(exception),
                        _ => HubconResponse.InternalError<TOut>(exception)
                    };
                }
            }

            return response;
        }

        /// <summary>
        /// Creates a hubcon response by creating a result wrapper, while providing additional details and shielding against exceptions that disrupt normal code execution.
        /// This extension method catches local and remote exceptions in a performant way.
        /// </summary>
        /// <param name="task"></param>
        /// <returns></returns>
        public static async ValueTask<IHubconResponse<JsonElement>> Execute<T>(this T contract, Func<T, Task> call) where T : IControllerContract
        {
            WrappedContext.SetWrapped(true);
            Exception? exception = null;
            IHubconResponse<JsonElement> response = default!;
            try
            {
                await call.Invoke(contract);
                response = HubconContext.Current.GetResponse<JsonElement>() ?? HubconResponse.OkT<JsonElement>()!;
            }
            catch (Exception ex) when (HandleException(ex, out exception))
            {
            }
            finally
            {
                if (exception != null)
                {                  
                    response = exception switch
                    {
                        OperationCanceledException => HubconResponse.Cancelled<JsonElement>(exception),
                        HubconRemoteException => HubconResponse.InternalError<JsonElement>(exception),
                        HubconGenericException => HubconResponse.InternalError<JsonElement>(exception),
                        UnauthorizedAccessException => HubconResponse.Unauthorized<JsonElement>(exception),
                        _ => HubconResponse.InternalError<JsonElement>(exception)
                    };
                }
            }

            return response;
        }
    }
}
