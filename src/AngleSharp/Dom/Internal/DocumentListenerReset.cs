namespace AngleSharp.Dom
{
    using System.Collections.Generic;

    // document.open erases listeners on the shadow-including connected tree,
    // including text nodes, while leaving detached nodes alone.
    internal static class DocumentListenerReset
    {
        internal static void Clear(Document document)
        {
            var pending = new Stack<INode>();
            pending.Push(document);

            while (pending.Count > 0)
            {
                var node = pending.Pop();
                if (node is EventTarget target)
                {
                    target.RemoveEventListeners();
                }

                foreach (var child in node.ChildNodes)
                {
                    pending.Push(child);
                }

                if (node is Element element && element.ShadowRoot is { } shadow)
                {
                    pending.Push(shadow);
                }
            }
        }
    }
}
