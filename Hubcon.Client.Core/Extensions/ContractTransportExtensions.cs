using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Core.Proxies;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
#pragma warning disable CS1591

namespace Hubcon
{
    /// <summary>
    /// Provides extension methods for <see cref="IControllerContract"/> to manage 
    /// the connection state of real-time transports.
    /// </summary>
    public static class ContractTransportExtensions
    {
        /// <summary>
        /// Initiates a connection for the specified transport type on the given contract.
        /// </summary>
        /// <typeparam name="T">The transport attribute type (e.g., <see cref="WebSocketTransport"/>).</typeparam>
        /// <param name="contract">The service contract instance.</param>
        /// <param name="url">An optional override URL for the connection.</param>
        /// <returns>A <see cref="HubconResponse"/> indicating success or failure of the connection attempt.</returns>
        public static async Task<HubconResponse> Connect<T>(this IControllerContract contract, string? url = null) where T : HubconTransportAttribute
        {
            if (contract is IContractDataAccessor accessor && accessor.GetTransportClient<T>() is not null and IRealTimeTransport client)
                return await client.Connect(url);

            return HubconResponse.Fail("Specified transport is not being used by this contract or is not a real-time transport.");
        }

        /// <summary>
        /// Attempts to reconnect an existing real-time transport to a specific URL.
        /// </summary>
        public static async Task<HubconResponse> Reconnect<T>(this IControllerContract contract, string? url = null) where T : HubconTransportAttribute
        {
            if (contract is IContractDataAccessor accessor && accessor.GetTransportClient<T>() is not null and IRealTimeTransport client)
                return await client.Reconnect(url);

            return HubconResponse.Fail("Specified transport is not being used by this contract or is not a real-time transport.");
        }

        /// <summary>
        /// Gracefully closes the connection for the specified real-time transport.
        /// </summary>
        public static async Task<HubconResponse> Disconnect<T>(this IControllerContract contract) where T : HubconTransportAttribute
        {
            if (contract is IContractDataAccessor accessor && accessor.GetTransportClient<T>() is not null and IRealTimeTransport client)
                return await client.Disconnect();

            return HubconResponse.Fail("Specified transport is not being used by this contract or is not a real-time transport.");
        }

        /// <summary>
        /// Checks the current connection status of the specified real-time transport.
        /// </summary>
        public static HubconResponse<bool> IsConnected<T>(this IControllerContract contract) where T : HubconTransportAttribute
        {
            if (contract is IContractDataAccessor accessor && accessor.GetTransportClient<T>() is not null and IRealTimeTransport client)
                return client.IsConnected();

            return HubconResponse.Fail<bool>("Specified transport is not being used by this contract or is not a real-time transport.");
        }

        /// <summary>
        /// Gets the transport for this contract as a <see cref="IRealTimeTransport"/> object if present.
        /// </summary>
        public static IRealTimeTransport AsRealTimeTransport<T>(this IControllerContract contract) where T : HubconTransportAttribute
        {
            if (contract is IContractDataAccessor accessor && accessor.GetTransportClient<T>() is not null and IRealTimeTransport client)
                return client;

            return null!;
        }
    }
}
