using System;
using System.Collections.Generic;
using System.Text;

namespace Hubcon
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public abstract class HttpMethodDataAttribute : Attribute
    {
        public string Template { get; }
        public abstract HttpMethod HttpMethod { get; }

        protected HttpMethodDataAttribute(string template = "/")
        {
            Template = template ?? "/";
        }
    }

    public class HttpGetAttribute : HttpMethodDataAttribute
    {
        public override HttpMethod HttpMethod => HttpMethod.Get;
        public HttpGetAttribute(string template = "/") : base(template) { }
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
}
