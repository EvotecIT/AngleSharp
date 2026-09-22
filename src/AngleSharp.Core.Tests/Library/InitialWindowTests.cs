namespace AngleSharp.Core.Tests.Library
{
    using AngleSharp.Dom;
    using AngleSharp.Browser;
    using AngleSharp.Html.Dom;
    using NUnit.Framework;
    using System.Threading.Tasks;

    [TestFixture]
    public class InitialWindowTests
    {
        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public async Task DocumentFormSubmissionHonorsTheContextSandbox(bool initial, bool blocked)
        {
            var parent = BrowsingContext.New(Configuration.Default);
            await parent.OpenAsync(request => request.Address("https://popup.example/").Content(""));
            var context = parent.CreateChild(null, blocked ? Sandboxes.Forms : Sandboxes.None);
            var document = initial ? context.OpenInitialDocument() : await context.OpenAsync(request =>
                request.Address("https://popup.example/child").Content("<body>"));
            document.Body.InnerHtml = "<form action='https://popup.example/submit'><input name='value' value='42'></form>";
            var form = document.QuerySelector<IHtmlFormElement>("form");

            Assert.AreEqual(blocked, form.GetSubmission() is null);
        }

        [Test]
        public async Task ClosingAnAuxiliaryWindowMakesItsDocumentInactive()
        {
            var opener = await BrowsingContext.New(Configuration.Default).OpenAsync(request => request
                .Address("https://popup.example/opener").Content("<!doctype html>"));
            var window = opener.DefaultView.Open();
            var document = (Document)window.Document;
            Assert.IsTrue(document.IsFullyActive);
            window.Close();
            Assert.IsFalse(document.IsFullyActive);
            Assert.IsTrue(((Document)opener).IsFullyActive);
        }

        [Test]
        public async Task AuxiliaryDocumentRemainsFullyActiveWithoutAnActiveOpener()
        {
            var opener = await BrowsingContext.New(Configuration.Default).OpenAsync(request => request
                .Address("https://popup.example/opener#fragment").Content("<!doctype html><p>Opener</p>"));
            var document = opener.DefaultView.Open("about:blank?query#blank", "report").Document;
            Assert.AreEqual("about:blank?query#blank", document.Url);
            Assert.IsFalse(((BrowsingContext)document.Context).IsFrame);
            opener.Context.Active = null;

            ((Document)document).OpenFrom(opener);

            Assert.AreEqual("https://popup.example/opener", document.Url);
            Assert.AreEqual("https://popup.example", document.Origin);
            Assert.AreSame(document, document.Context.Active);
        }

        [Test]
        public async Task InitialWindowHasOneActiveDocumentAndInheritsTheOpenersBaseAndOrigin()
        {
            var opener = await BrowsingContext.New(Configuration.Default).OpenAsync(request => request
                .Address("https://popup.example/reports/start").Content("<!doctype html><base href='/assets/'>"));

            var window = opener.DefaultView.Open();
            var document = window.Document;

            Assert.AreSame(window, document.DefaultView);
            Assert.AreSame(document, document.Context.Active);
            Assert.AreEqual("about:blank", document.Url);
            Assert.AreEqual("https://popup.example/assets/", document.BaseUri);
            Assert.AreEqual("https://popup.example", document.Origin);
            Assert.IsNotNull(document.DocumentElement);
            Assert.AreEqual("BackCompat", document.CompatMode);
            Assert.AreEqual(opener.Url, document.Referrer);
            Assert.IsNotNull(document.Head);
            Assert.IsNotNull(document.Body);
            Assert.AreEqual(DocumentReadyState.Complete, document.ReadyState);
            opener.QuerySelector("base").SetAttribute("href", "/later/");
            Assert.AreEqual("https://popup.example/assets/", document.BaseUri);
        }
    }
}
