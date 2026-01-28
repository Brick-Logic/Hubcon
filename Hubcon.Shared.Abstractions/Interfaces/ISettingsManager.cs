using System;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface ISettingsManager
    {
        T GetSettings<T>(Guid linkId, Func<T> onNull);
        T GetSettings<T>(IOperationEndpoint operationRequest, TransportAttribute transportAttribute, Func<T> onNull);
    }
}