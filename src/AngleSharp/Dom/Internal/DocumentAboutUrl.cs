namespace AngleSharp.Dom
{
    using AngleSharp.Browser;
    using System;

    // Local frame documents retain their creator's base and origin independently
    // of their own about: URL and later changes to the creator document.
    internal sealed class DocumentAboutUrl
    {
        internal Url BaseUrl { get; }
        internal String Origin { get; }
        internal Boolean IsSrcdoc { get; }

        internal DocumentAboutUrl(IDocument creator, Sandboxes security, Boolean isSrcdoc)
        {
            BaseUrl = new Url(creator.BaseUri);
            Origin = (security & Sandboxes.Origin) != 0 ? "null" : creator.Origin ?? "null";
            IsSrcdoc = isSrcdoc;
        }

        internal static Boolean IsBlank(Url url) => IsLocal(url, "blank");
        internal static Boolean IsSrcdocUrl(Url url) => IsLocal(url, "srcdoc") && url.Query is null;

        private static Boolean IsLocal(Url url, String path) =>
            !url.IsInvalid && url.Scheme == "about" && url.Data == path && String.IsNullOrEmpty(url.HostName) &&
            String.IsNullOrEmpty(url.UserName) && String.IsNullOrEmpty(url.Password);
    }
}
