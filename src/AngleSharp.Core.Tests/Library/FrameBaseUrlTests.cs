namespace AngleSharp.Core.Tests.Library
{
    using AngleSharp.Dom;
    using AngleSharp.Html.Dom;
    using AngleSharp.Io;
    using NUnit.Framework;
    using System.Threading.Tasks;

    [TestFixture]
    public class FrameBaseUrlTests
    {
        [TestCase("srcdoc=\"<a href='item'>item</a>\"", "about:srcdoc")]
        [TestCase("src='about:blank'", "about:blank")]
        [TestCase("src='about:blank?query#fragment'", "about:blank?query#fragment")]
        [TestCase("", "about:blank")]
        [TestCase("src=''", "about:blank")]
        public async Task LocalFramesKeepTheirUrlAndFreezeTheCreatorsBase(string attributes, string expectedUrl)
        {
            var context = BrowsingContext.New(Configuration.Default.WithDefaultLoader(new LoaderOptions { IsResourceLoadingEnabled = true }));
            var parent = await context.OpenAsync(response => response.Address("https://example.test/reports/start.html")
                .Content("<base href='/assets/'><iframe " + attributes + "></iframe>"));
            var child = parent.QuerySelector<IHtmlInlineFrameElement>("iframe").ContentDocument;
            Assert.IsNotNull(child);
            Assert.AreEqual(expectedUrl, child.Url);
            Assert.AreEqual("https://example.test/assets/", child.BaseUri);
            Assert.AreEqual("https://example.test", child.Origin);
            parent.QuerySelector("base").SetAttribute("href", "/later/");
            ((Document)parent).DocumentUrl.Href = "https://example.test/history/changed";
            Assert.AreEqual("https://example.test/assets/", child.BaseUri);
            child.Head.InnerHtml = "<base href='local/'>";
            Assert.AreEqual("https://example.test/assets/local/", child.BaseUri);
            child.QuerySelector("base").Remove();
            Assert.AreEqual("https://example.test/assets/", child.BaseUri);
        }
        [TestCase("", "https://example.test")]
        [TestCase("sandbox='allow-scripts'", "null")]
        [TestCase("sandbox='allow-scripts allow-same-origin'", "https://example.test")]
        public async Task InheritedOriginDoesNotFollowACrossOriginBase(string sandbox, string expectedOrigin)
        {
            var context = BrowsingContext.New(Configuration.Default.WithDefaultLoader(new LoaderOptions { IsResourceLoadingEnabled = true }));
            var parent = await context.OpenAsync(response => response.Address("https://example.test/start")
                .Content("<base href='https://cdn.example/assets/'><iframe " + sandbox + " srcdoc=\"<p>Child</p>\"></iframe>"));
            var child = parent.QuerySelector<IHtmlInlineFrameElement>("iframe").ContentDocument;
            Assert.AreEqual("about:srcdoc", child.Url);
            Assert.AreEqual("https://cdn.example/assets/", child.BaseUri);
            Assert.AreEqual(expectedOrigin, child.Origin);
            var clone = (IDocument)child.Clone(true);
            Assert.AreEqual(child.BaseUri, clone.BaseUri);
            Assert.AreEqual(child.Origin, clone.Origin);
        }

        [Test]
        public async Task BlankFrameInsertedAfterParentBaseChangeUsesInsertionBase()
        {
            var context = BrowsingContext.New(Configuration.Default.WithDefaultLoader(new LoaderOptions { IsResourceLoadingEnabled = true }));
            var parent = await context.OpenAsync(response => response.Address("https://example.test/start").Content("<base href='/first/'><body></body>"));
            var frame = (IHtmlInlineFrameElement)parent.CreateElement("iframe");
            Assert.IsNull(frame.ContentDocument);
            parent.QuerySelector("base").SetAttribute("href", "/second/");
            parent.Body.AppendChild(frame);
            Assert.IsNotNull(frame.ContentDocument);
            Assert.AreEqual("https://example.test/second/", frame.ContentDocument.BaseUri);
        }
    }
}
