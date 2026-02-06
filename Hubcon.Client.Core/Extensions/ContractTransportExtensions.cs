using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Core.Proxies;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Client.Core.Extensions
{
    public static class ContractTransportExtensions
    {
        public static async Task<HubconResponse> Connect<T>(this IControllerContract contract, string? url = null) where T : HubconTransportAttribute
        {
            if (contract is IContractDataAccessor accessor && accessor.GetTransportClient<T>() is IRealTimeTransport client)
                return await client.Connect(url);
            else
                return HubconResponse.Fail("Specified transport is not being used by this transport or is not a real time transport.");
        }

        public static async Task<HubconResponse> Reconnect<T>(this IControllerContract contract, string url) where T : HubconTransportAttribute
        {
            if (contract is IContractDataAccessor accessor && accessor.GetTransportClient<T>() is IRealTimeTransport client) 
                return await client.Reconnect(url);
            else
                return HubconResponse.Fail("Specified transport is not being used by this transport or is not a real time transport.");
        }

        public static async Task<HubconResponse> Disconnect<T>(this IControllerContract contract) where T : HubconTransportAttribute
        {
            if (contract is IContractDataAccessor accessor && accessor.GetTransportClient<T>() is IRealTimeTransport client) 
                return await client.Disconnect();
            else
                return HubconResponse.Fail("Specified transport is not being used by this transport or is not a real time transport.");
        }

        public static async Task<HubconResponse<bool>> IsConnected<T>(this IControllerContract contract) where T : HubconTransportAttribute
        {
            if (contract is IContractDataAccessor accessor && accessor.GetTransportClient<T>() is IRealTimeTransport client)
                return await client.IsConnected();
            else
                return HubconResponse.Fail<bool>("Specified transport is not being used by this transport or is not a real time transport.");
        }
    }
}
