namespace AngleSharp.Html.Dom
{
    using AngleSharp.Browser;
    using AngleSharp.Dom;
    using AngleSharp.Io;
    using AngleSharp.Io.Processors;
    using System;

    /// <summary>
    /// Represents the base class for frame elements.
    /// </summary>
    abstract class HtmlFrameElementBase : HtmlFrameOwnerElement
    {
        #region Fields

        private IBrowsingContext? _context;
        private FrameRequestProcessor _request;
        private Boolean _isSetup;

        #endregion

        #region ctor

        public HtmlFrameElementBase(Document owner, String name, String? prefix, NodeFlags flags = NodeFlags.None)
            : base(owner, name, prefix, flags | NodeFlags.Special)
        {
            _request = new FrameRequestProcessor(owner.Context, this);
        }

        #endregion

        #region Properties

        public IDownload? CurrentDownload => _request?.Download;

        public String? Name
        {
            get => this.GetOwnAttribute(AttributeNames.Name);
            set => this.SetOwnAttribute(AttributeNames.Name, value);
        }

        public String? Source
        {
            get => this.GetUrlAttribute(AttributeNames.Src);
            set => this.SetOwnAttribute(AttributeNames.Src, value);
        }

        public String? Scrolling
        {
            get => this.GetOwnAttribute(AttributeNames.Scrolling);
            set => this.SetOwnAttribute(AttributeNames.Scrolling, value);
        }

        public IDocument? ContentDocument => _context?.Active;

        public String? LongDesc
        {
            get => this.GetOwnAttribute(AttributeNames.LongDesc);
            set => this.SetOwnAttribute(AttributeNames.LongDesc, value);
        }

        public String? FrameBorder
        {
            get => this.GetOwnAttribute(AttributeNames.FrameBorder);
            set => this.SetOwnAttribute(AttributeNames.FrameBorder, value);
        }

        public IBrowsingContext NestedContext => _context ??= NewChildContext();

        #endregion

        #region Internal Methods

        internal virtual String GetContentHtml()
        {
            return null!;
        }

        internal override void SetupElement()
        {
            base.SetupElement();
            _isSetup = true;

            _context ??= NewChildContext();
            if (this.GetRoot() is Document || this.GetOwnAttribute(AttributeNames.Src) != null || GetContentHtml() != null)
            {
                UpdateSource();
            }
        }

        internal void UpdateSource()
        {
            var content = GetContentHtml();
            var rawSource = this.GetOwnAttribute(AttributeNames.Src);
            var url = String.IsNullOrWhiteSpace(rawSource) ? new Url("about:blank") : this.HyperReference(Source!);
            if (url is null || url.IsInvalid)
            {
                url = new Url("about:blank");
            }

            if (this.GetRoot() is Document || rawSource != null || content != null)
            {
                var security = GetSecuritySettings();
                if (_context is null || _context.Security != security)
                {
                    _context = NewChildContext(security);
                }
                this.Process(_request, url);
            }
        }

        internal virtual Sandboxes GetSecuritySettings() => Sandboxes.None;

        #endregion

        #region Helpers

        protected override void OnParentChanged()
        {
            base.OnParentChanged();
            if (_isSetup && this.GetRoot() is Document && _context?.Active is null && this.GetOwnAttribute(AttributeNames.Src) is null && GetContentHtml() is null)
            {
                UpdateSource();
            }
        }

        private IBrowsingContext NewChildContext() => NewChildContext(GetSecuritySettings());

        private IBrowsingContext NewChildContext(Sandboxes security)
        {
            var childContext = default(IBrowsingContext);
            if (Context is BrowsingContext context)
            {
                childContext  = context.CreateChild(Name, security, true);
            }
            else
            {
                childContext  = Context.CreateChild(Name, security);
            }
            Owner.AttachReference(childContext);
            return childContext;
        }

        #endregion
    }
}
