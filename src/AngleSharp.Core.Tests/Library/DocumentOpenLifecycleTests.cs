namespace AngleSharp.Core.Tests.Library
{
    using AngleSharp.Dom;
    using AngleSharp.Dom.Events;
    using AngleSharp.Core.Tests.Mocks;
    using AngleSharp.Html.Dom;
    using AngleSharp.Io;
    using NUnit.Framework;
    using System.Threading.Tasks;

    [TestFixture]
    public class DocumentOpenLifecycleTests
    {
        [Test]
        public async Task NestedUnloadScopesKeepOpenAndWriteSuppressedUntilTheOuterScopeEnds()
        {
            var document = (Document)await BrowsingContext.New(Configuration.Default).OpenAsync(request => request.Content("<h1>Keep</h1>"));
            using (document.BeginUnloadScope())
            {
                using (document.BeginUnloadScope())
                {
                    Assert.AreSame(document, document.Open());
                }
                document.Write("<h1>Wrong</h1>");
                Assert.AreSame(document, document.Open());
                Assert.AreEqual("Keep", document.QuerySelector("h1").TextContent);
            }

            document.Open();
            Assert.AreEqual(0, document.ChildNodes.Length);
        }

        [Test]
        public async Task TopLevelDocumentOpenPreservesIdentityAndUrlWithoutUnloading()
        {
            var context = BrowsingContext.New(Configuration.Default);
            var document = await context.OpenAsync(request => request.Content("<h1>Original</h1>").Address("https://open.example/page#keep"));
            var unloads = 0;
            document.DefaultView.Unloading += (_, _) => unloads++;

            var result = document.Open();

            Assert.AreSame(document, result);
            Assert.AreEqual("https://open.example/page#keep", document.Url);
            Assert.AreEqual(0, unloads);
            Assert.AreEqual(0, document.ChildNodes.Length);
        }

        [Test]
        public async Task ExplicitEntryDocumentRewritesAnActiveUrlAndDropsItsFragment()
        {
            var target = await BrowsingContext.New(Configuration.Default).OpenAsync(request =>
                request.Content("<base href='/old-base/'><h1>Old</h1>").Address("https://open.example/old#old"));
            var entry = await BrowsingContext.New(Configuration.Default).OpenAsync(request =>
                request.Content("<base href='/unrelated-base/'>").Address("https://open.example/entry/page?query#fragment"));

            var result = ((Document)target).OpenFrom(entry);

            Assert.AreSame(target, result);
            Assert.AreEqual("https://open.example/entry/page?query", target.Url);
            Assert.AreEqual(target.Url, target.BaseUri);
        }

        [Test]
        public async Task ExplicitEntryDocumentDoesNotRewriteAnInactiveDocumentUrl()
        {
            var context = BrowsingContext.New(Configuration.Default);
            var target = await context.OpenAsync(request => request.Content("<h1>Old</h1>").Address("https://open.example/old#keep"));
            var entry = await context.OpenAsync(request => request.Content("<h1>Current</h1>").Address("https://open.example/current"));

            ((Document)target).OpenFrom(entry);

            Assert.AreEqual("https://open.example/old#keep", target.Url);
            Assert.AreEqual(0, target.ChildNodes.Length);
            Assert.AreEqual("Current", entry.QuerySelector("h1").TextContent);
        }

        [Test]
        public async Task OpeningFromABlankChildPreservesTheTargetsOrigin()
        {
            var config = Configuration.Default.WithDefaultLoader(new LoaderOptions { IsResourceLoadingEnabled = true });
            var parent = await BrowsingContext.New(config).OpenAsync(request => request
                .Content("<base href='/assets/'><iframe></iframe>").Address("https://open.example/parent"));
            var child = parent.QuerySelector<IHtmlInlineFrameElement>("iframe").ContentDocument;

            ((Document)parent).OpenFrom(child);

            Assert.AreEqual("about:blank", parent.Url);
            Assert.AreEqual("https://open.example", parent.Origin);
            Assert.AreEqual(parent.Origin, ((IDocument)parent.Clone()).Origin);
        }

        [Test]
        public async Task DetachedFrameIsNotFullyActiveWhenOpenedByItsFormerParent()
        {
            var config = Configuration.Default.WithDefaultLoader(new LoaderOptions { IsResourceLoadingEnabled = true });
            var parent = await BrowsingContext.New(config).OpenAsync(request => request
                .Content("<base href='/assets/'><iframe srcdoc='<h1>Child</h1>'></iframe>")
                .Address("https://open.example/parent#fragment"));
            var frame = parent.QuerySelector<IHtmlInlineFrameElement>("iframe");
            var child = frame.ContentDocument;
            Assert.IsTrue(((Document)child).IsFullyActive);
            frame.Remove();
            Assert.IsFalse(((Document)child).IsFullyActive);

            ((Document)child).OpenFrom(parent);

            Assert.AreEqual("about:srcdoc", child.Url);
            Assert.AreEqual("https://open.example/assets/", child.BaseUri);
            Assert.AreEqual(0, child.ChildNodes.Length);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task ActiveLocalFrameUsesTheEntryUrlInsteadOfItsInheritedBase(bool srcdoc)
        {
            var config = Configuration.Default.WithDefaultLoader(new LoaderOptions { IsResourceLoadingEnabled = true });
            var parent = await BrowsingContext.New(config).OpenAsync(request => request
                .Content("<base href='/assets/'><iframe " + (srcdoc ? "srcdoc='<h1>Child</h1>'" : "") + "></iframe>")
                .Address("https://open.example/parent?query#fragment"));
            var child = parent.QuerySelector<IHtmlInlineFrameElement>("iframe").ContentDocument;

            ((Document)child).OpenFrom(parent);

            Assert.AreEqual("https://open.example/parent?query", child.Url);
            Assert.AreEqual(child.Url, child.BaseUri);
            Assert.AreEqual("https://open.example", child.Origin);
        }

        [Test]
        public async Task ForeignEntryDocumentIsRejectedBeforeChangingTheTarget()
        {
            var target = await BrowsingContext.New(Configuration.Default).OpenAsync(request => request.Content("<h1>Keep</h1>").Address("https://open.example/old"));
            var entry = await BrowsingContext.New(Configuration.Default).OpenAsync(request => request.Content("").Address("https://foreign.example/"));
            var calls = 0;
            target.AddEventListener("probe", (_, _) => calls++);

            Assert.Throws<DomException>(() => ((Document)target).OpenFrom(entry));
            target.Dispatch(new Event("probe"));

            Assert.AreEqual("https://open.example/old", target.Url);
            Assert.AreEqual("Keep", target.QuerySelector("h1").TextContent);
            Assert.AreEqual(1, calls);
        }

        [Test]
        public async Task OpenErasesShadowIncludingListenersButKeepsDetachedListeners()
        {
            var document = await BrowsingContext.New(Configuration.Default).OpenAsync(request => request.Content("<body><div>Text</div>"));
            var element = document.QuerySelector("div");
            var text = element.FirstChild;
            var shadow = element.AttachShadow(ShadowRootMode.Closed);
            var shadowChild = document.CreateElement("span");
            shadow.AppendChild(shadowChild);
            var detached = document.CreateElement("div");
            var calls = 0;
            DomEventHandler listener = (_, _) => calls++;
            IEventTarget[] connected = { document, document.DefaultView, element, text, shadow, shadowChild };
            foreach (var target in connected) target.AddEventListener("probe", listener);
            detached.AddEventListener("probe", listener);

            document.Open("text/plain", "replace");

            foreach (var target in connected) target.Dispatch(new Event("probe"));
            Assert.AreEqual(0, calls);
            detached.Dispatch(new Event("probe"));
            Assert.AreEqual(1, calls);
            document.AddEventListener("probe", listener);
            document.Dispatch(new Event("probe"));
            Assert.AreEqual(2, calls);
            Assert.AreEqual("text/html", document.ContentType);
        }

        [Test]
        public async Task OpenFromAParserExecutingScriptIsIgnored()
        {
            IDocument opened = null;
            var scripting = new CallbackScriptEngine(options => opened = options.Document.Open());
            var document = await BrowsingContext.New(Configuration.Default.WithScripts(scripting))
                .OpenAsync(request => request.Content("<!doctype html><h1>Before</h1><script type='c-sharp'>open</script><p>After</p>"));

            Assert.AreSame(document, opened);
            Assert.AreEqual("Before", document.QuerySelector("h1").TextContent);
            Assert.AreEqual("After", document.QuerySelector("p").TextContent);
        }
    }
}
