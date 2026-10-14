namespace AngleSharp.Core.Tests.Library
{
    using AngleSharp.Dom;
    using AngleSharp.Html.Dom;
    using AngleSharp.Io;
    using NUnit.Framework;
    using System;
    using System.Threading.Tasks;

    [TestFixture]
    public class DocumentAdoptionTests
    {
        [TestCase("template")]
        [TestCase("section")]
        public void AdoptingTemplatesUpdatesTheirContentDocuments(String selector)
        {
            var source = "<section><template id=outer><input><template id=inner><span>Nested</span></template></template></section>".ToHtmlDocument();
            var target = "<p>Target</p>".ToHtmlDocument();
            var outer = source.QuerySelector<IHtmlTemplateElement>("#outer");
            var inner = outer.Content.QuerySelector<IHtmlTemplateElement>("#inner");
            var adopted = source.QuerySelector(selector);

            target.AdoptNode(adopted);

            Assert.AreSame(target, adopted.Owner);
            Assert.AreSame(target, outer.Owner);
            Assert.AreSame(target, outer.Content.Owner);
            Assert.AreSame(target, outer.Content.QuerySelector("input").Owner);
            Assert.AreSame(target, inner.Owner);
            Assert.AreSame(target, inner.Content.Owner);
            Assert.AreSame(target, inner.Content.QuerySelector("span").Owner);
            Assert.AreEqual("Nested", inner.Content.TextContent);
        }

        [Test]
        public async Task ReplacingAdoptedTemplateContentKeepsItsBaseInactiveUntilConnected()
        {
            var source = "<base href='https://source.test/'><section><template></template></section>".ToHtmlDocument();
            var target = await BrowsingContext.New().OpenAsync(response => response
                .Address("https://target.test/page").Content("<p>Target</p>")).ConfigureAwait(false);
            var section = source.QuerySelector("section");
            var template = section.QuerySelector<IHtmlTemplateElement>("template");

            target.AdoptNode(section);
            target.Body.AppendChild(section);
            template.InnerHtml = "<base href='https://target.test/replacement/'><a href='item'>Item</a>";

            Assert.AreSame(target, template.Content.Owner);
            Assert.AreSame(target, template.Content.QuerySelector("a").Owner);
            Assert.AreEqual("https://source.test/", source.BaseUri);
            Assert.AreEqual("https://target.test/page", target.BaseUri);

            target.Head.InsertBefore(template.Content.QuerySelector("base"), target.Head.FirstChild);

            Assert.AreEqual("https://target.test/replacement/", target.BaseUri);
            Assert.AreEqual("https://source.test/", source.BaseUri);
        }
    }
}
