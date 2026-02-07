using Hubcon.Shared.Abstractions.Attributes;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace Hubcon.Shared.Abstractions.Attributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public abstract class HttpMethodDataAttribute : Attribute
    {
        public string Template { get; }
        public abstract HttpMethod HttpMethod { get; }
        public string ContentType { get; protected set; } = "application/json";

        protected HttpMethodDataAttribute(string template = "/")
        {
            Template = template ?? "/";
        }
    }
}

namespace Hubcon
{
    public class HttpGetAttribute : HttpMethodDataAttribute
    {
        public override HttpMethod HttpMethod => HttpMethod.Get;
        public HttpGetAttribute(string template = "/") : base(template) {  }
    }

    public class HttpPostAttribute : HttpMethodDataAttribute
    {
        public override HttpMethod HttpMethod => HttpMethod.Post;
        public HttpPostAttribute(string template = "/") : base(template) { }
    }

    public class HttpPutAttribute : HttpMethodDataAttribute
    {
        public override HttpMethod HttpMethod => HttpMethod.Put;
        public HttpPutAttribute(string template = "/") : base(template) { }
    }

    public class HttpDeleteAttribute : HttpMethodDataAttribute
    {
        public override HttpMethod HttpMethod => HttpMethod.Delete;
        public HttpDeleteAttribute(string template = "/") : base(template) { }
    }

    public class HttpPatchAttribute : HttpMethodDataAttribute
    {
        public override HttpMethod HttpMethod => HttpMethod.Patch;
        public HttpPatchAttribute(string template = "/") : base(template) 
        {
            base.ContentType = "application/merge-patch+json";
        }
    }

    public class HttpHeadAttribute : HttpMethodDataAttribute
    {
        public override HttpMethod HttpMethod => HttpMethod.Head;
        public HttpHeadAttribute(string template = "/") : base(template) { }
    }

    public class HttpOptionsAttribute : HttpMethodDataAttribute
    {
        public override HttpMethod HttpMethod => HttpMethod.Options;
        public HttpOptionsAttribute(string template = "/") : base(template) { }
    }
}
