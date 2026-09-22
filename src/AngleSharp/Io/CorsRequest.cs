namespace AngleSharp.Io
{
    using AngleSharp.Dom;

    /// <summary>
    /// Represents the arguments to perform a fetch with CORS.
    /// </summary>
    public class CorsRequest
    {
        /// <summary>
        /// Creates a new CORS enabled request.
        /// </summary>
        /// <param name="request">The original request.</param>
        public CorsRequest(ResourceRequest request)
        {
            Request = request;
            IntegrityMetadata = request.IntegritySnapshot is { IsResolved: true } snapshot
                ? snapshot.Value
                : request.IntegrityMetadata ?? request.Source.GetAttribute(AttributeNames.Integrity);
        }

        /// <summary>
        /// Gets the original request to perform.
        /// </summary>
        public ResourceRequest Request
        {
            get;
        }

        /// <summary>
        /// Gets or sets the CORS settings to use.
        /// </summary>
        public CorsSetting Setting
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the behavior in case of no CORS.
        /// </summary>
        public OriginBehavior Behavior
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the integrity provider, if any.
        /// </summary>
        public IIntegrityProvider? Integrity
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the integrity metadata captured when the request was prepared.
        /// Defaults to the resource request's prepared metadata or the source attribute.
        /// An explicitly empty value disables integrity requirements for this request.
        /// </summary>
        public string? IntegrityMetadata
        {
            get;
            set;
        }
    }
}
