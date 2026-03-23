using Hubcon.Shared.Abstractions.Attributes;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace Hubcon.Shared.Abstractions.Attributes
{
    /// <summary>
    /// Base method data attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public abstract class HttpMethodDataAttribute : Attribute
    {
        /// <summary>
        /// The default url template.
        /// </summary>
        public string Template { get; }

        /// <summary>
        /// The default HttpMethod.
        /// </summary>
        public abstract HttpMethod HttpMethod { get; }

        /// <summary>
        /// The default content type.
        /// </summary>
        public string ContentType { get; protected set; } = "application/json";


        /// <summary>
        /// Configures method headers.
        /// </summary>
        /// <param name="content"></param>
        public virtual void ConfigureHeaders(StringContent content)
        {
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(ContentType);
        }

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="template"></param>
        protected HttpMethodDataAttribute(string template = "/")
        {
            Template = template ?? "/";
        }
    }
}

namespace Hubcon
{
    /// <summary>
    /// Represents an HTTP GET method attribute for defining HTTP GET requests. Allows path parameter replacement if the name matches with any of the arguments. Path parameters don't support classes.
    /// Example: '/api/{myParameter}'.
    /// </summary>
    public class HttpGetAttribute : HttpMethodDataAttribute
    {
        /// <summary>
        /// Gets the HTTP method type (GET).
        /// </summary>
        public override HttpMethod HttpMethod => HttpMethod.Get;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpGetAttribute"/> class with an optional template.
        /// </summary>
        /// <param name="template">The route template. Defaults to "/".</param>
        public HttpGetAttribute(string template = "/") : base(template) {  }
    }

    /// <summary>
    /// Represents an HTTP POST method attribute for defining HTTP POST requests. Allows path parameter replacement if the name matches with any of the arguments. Path parameters don't support classes.
    /// Example: '/api/{myParameter}'.
    /// </summary>
    public class HttpPostAttribute : HttpMethodDataAttribute
    {
        /// <summary>
        /// Gets the HTTP method type (POST).
        /// </summary>
        public override HttpMethod HttpMethod => HttpMethod.Post;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpPostAttribute"/> class with an optional template.
        /// </summary>
        /// <param name="template">The route template. Defaults to "/".</param>
        public HttpPostAttribute(string template = "/") : base(template) { }
    }

    /// <summary>
    /// Represents an HTTP PUT method attribute for defining HTTP PUT requests. Allows path parameter replacement if the name matches with any of the arguments. Path parameters don't support classes.
    /// Example: '/api/{myParameter}'.
    /// </summary>
    public class HttpPutAttribute : HttpMethodDataAttribute
    {
        /// <summary>
        /// Gets the HTTP method type (PUT).
        /// </summary>
        public override HttpMethod HttpMethod => HttpMethod.Put;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpPutAttribute"/> class with an optional template.
        /// </summary>
        /// <param name="template">The route template. Defaults to "/".</param>
        public HttpPutAttribute(string template = "/") : base(template) { }
    }

    /// <summary>
    /// Represents an HTTP DELETE method attribute for defining HTTP DELETE requests. Allows path parameter replacement if the name matches with any of the arguments. Path parameters don't support classes.
    /// Example: '/api/{myParameter}'.
    /// </summary>
    public class HttpDeleteAttribute : HttpMethodDataAttribute
    {
        /// <summary>
        /// Gets the HTTP method type (DELETE).
        /// </summary>
        public override HttpMethod HttpMethod => HttpMethod.Delete;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpDeleteAttribute"/> class with an optional template.
        /// </summary>
        /// <param name="template">The route template. Defaults to "/".</param>
        public HttpDeleteAttribute(string template = "/") : base(template) { }
    }

    /// <summary>
    /// Represents an HTTP PATCH method attribute for defining HTTP PATCH requests. Allows path parameter replacement if the name matches with any of the arguments. Path parameters don't support classes.
    /// Example: '/api/{myParameter}'.
    /// </summary>
    public class HttpPatchAttribute : HttpMethodDataAttribute
    {
        /// <summary>
        /// Gets the HTTP method type (PATCH).
        /// </summary>
        public override HttpMethod HttpMethod => HttpMethod.Patch;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpPatchAttribute"/> class with an optional template.
        /// Sets the content type to "application/merge-patch+json".
        /// </summary>
        /// <param name="template">The route template. Defaults to "/".</param>
        public HttpPatchAttribute(string template = "/") : base(template) 
        {
            base.ContentType = "application/merge-patch+json";
        }

        /// <summary>
        /// Configures the headers for the HTTP content to match the content type.
        /// </summary>
        /// <param name="content">The HTTP content.</param>
        public override void ConfigureHeaders(StringContent content)
        {
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(ContentType);
        }
    }

    /// <summary>
    /// Represents an HTTP HEAD method attribute for defining HTTP HEAD requests. Allows path parameter replacement if the name matches with any of the arguments. Path parameters don't support classes.
    /// Example: '/api/{myParameter}'.
    /// </summary>
    public class HttpHeadAttribute : HttpMethodDataAttribute
    {
        /// <summary>
        /// Gets the HTTP method type (HEAD).
        /// </summary>
        public override HttpMethod HttpMethod => HttpMethod.Head;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpHeadAttribute"/> class with an optional template.
        /// </summary>
        /// <param name="template">The route template. Defaults to "/".</param>
        public HttpHeadAttribute(string template = "/") : base(template) { }
    }

    /// <summary>
    /// Represents an HTTP OPTIONS method attribute for defining HTTP OPTIONS requests. Allows path parameter replacement if the name matches with any of the arguments. Path parameters don't support classes. 
    /// Example: '/api/{myParameter}'.
    /// </summary>
    public class HttpOptionsAttribute : HttpMethodDataAttribute
    {
        /// <summary>
        /// Gets the HTTP method type (OPTIONS).
        /// </summary>
        public override HttpMethod HttpMethod => HttpMethod.Options;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpOptionsAttribute"/> class with an optional template.
        /// </summary>
        /// <param name="template">The route template. Defaults to "/".</param>
        public HttpOptionsAttribute(string template = "/") : base(template) { }
    }
}
