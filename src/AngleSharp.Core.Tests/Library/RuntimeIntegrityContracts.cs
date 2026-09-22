namespace AngleSharp.Core.Tests.Library
{
    using AngleSharp.Core.Tests.Mocks;
    using AngleSharp.Dom;
    using AngleSharp.Io;
    using NUnit.Framework;
    using System.Threading.Tasks;

    [TestFixture]
    public class RuntimeIntegrityContracts
    {
        [TestCase(false)]
        [TestCase(true)]
        public async Task PublicCorsRequestCapturesElementIntegrity(bool crossOrigin)
        {
            var config = Configuration.Default.WithMockRequester();
            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(response => response.Address("https://example.com/").Content("<script integrity='original'></script>"));
            var element = document.QuerySelector("script");
            string checkedMetadata = null;
            var cors = new CorsRequest(new ResourceRequest(element, new Url(crossOrigin ? "https://other.example/code.js" : "https://example.com/code.js")))
            {
                Integrity = new MockIntegrityProvider((_, metadata) => { checkedMetadata = metadata; return false; })
            };
            element.SetAttribute("integrity", "changed");
            var download = context.GetService<IResourceLoader>().FetchWithCorsAsync(cors);

            Assert.ThrowsAsync<DomException>(async () => await download.Task);
            Assert.AreEqual(crossOrigin ? null : "original", checkedMetadata);
        }

        [TestCase(false, CorsSetting.None)]
        [TestCase(true, CorsSetting.None)]
        [TestCase(true, CorsSetting.Anonymous)]
        public async Task ExplicitEmptyIntegrityOverridesTheElementAttribute(bool crossOrigin, CorsSetting setting)
        {
            var config = Configuration.Default.WithMockRequester();
            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(response => response.Address("https://example.com/").Content("<script integrity='original'></script>"));
            var cors = new CorsRequest(new ResourceRequest(document.QuerySelector("script"), new Url(crossOrigin ? "https://other.example/code.js" : "https://example.com/code.js")))
            {
                IntegrityMetadata = "",
                Setting = setting,
                Integrity = new MockIntegrityProvider((_, __) => false)
            };

            using var response = await context.GetService<IResourceLoader>().FetchWithCorsAsync(cors).Task;
            Assert.IsNotNull(response);
        }
    }
}
