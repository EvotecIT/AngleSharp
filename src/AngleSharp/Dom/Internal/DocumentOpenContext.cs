namespace AngleSharp.Dom
{
    using AngleSharp.Html.Dom;
    using AngleSharp.Browser;
    using System;

    internal static class DocumentOpenContext
    {
        internal static void CheckOrigin(Document document, IDocument entryDocument)
        {
            if (ReferenceEquals(document, entryDocument)) return;

            // Serialized opaque origins are all "null" and cannot establish
            // cross-document authority by string equality.
            var origin = document.Origin;
            if ((document.Context.Security & Sandboxes.Origin) != 0 ||
                (entryDocument.Context.Security & Sandboxes.Origin) != 0 ||
                String.IsNullOrEmpty(origin) || origin == "null" || origin != entryDocument.Origin)
            {
                throw new DomException(DomError.Security);
            }
        }

        internal static Boolean IsFullyActive(IDocument document)
        {
            var context = document.Context;
            if (!ReferenceEquals(context.Active, document) || document.DefaultView?.IsClosed == true) return false;
            if (context.Parent is null || context is BrowsingContext { IsFrame: false }) return true;

            var parent = context.Parent.Active;
            if (parent is null || !IsFullyActive(parent)) return false;
            foreach (var node in parent.GetDescendants())
            {
                if (node is HtmlFrameElementBase frame && ReferenceEquals(frame.ContentDocument, document))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
