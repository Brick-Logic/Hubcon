using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hubcon.Server.Core.Routing.Models
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class JsonReadResult
    {
        public JsonElement? JsonElement { get; }
        public bool IsSuccess { get; }
        public string? ErrorMessage { get; }

        private JsonReadResult(JsonElement? jsonElement, bool isSuccess, string? errorMessage)
        {
            JsonElement = jsonElement;
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
        }

        public static JsonReadResult Success(JsonElement jsonElement)
            => new(jsonElement, true, null);

        public static JsonReadResult Failure(string errorMessage)
            => new(null, false, errorMessage);
    }
}
