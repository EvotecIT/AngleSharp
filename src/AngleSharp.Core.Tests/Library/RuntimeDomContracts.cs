namespace AngleSharp.Core.Tests.Library
{
    using AngleSharp.Browser;
    using AngleSharp.Common;
    using AngleSharp.Dom;
    using AngleSharp.Html.Dom;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    [TestFixture]
    public class RuntimeDomContracts
    {
        [Test]
        public void ImportAssignsTheDestinationDocumentToTheWholeSubtree()
        {
            var source = "<div><span>text</span></div>".ToHtmlDocument();
            var destination = "".ToHtmlDocument();
            var imported = destination.Import(source.QuerySelector("div"), true);

            Assert.AreSame(destination, imported.Owner);
            Assert.AreSame(destination, imported.FirstChild.Owner);
            Assert.AreSame(destination, imported.FirstChild.FirstChild.Owner);
            Assert.AreSame(source, source.QuerySelector("div").Owner);
        }

        [Test]
        public void DatasetCamelCaseUsesTheCorrespondingAttribute()
        {
            var document = "<p></p>".ToHtmlDocument();
            var element = (IHtmlElement)document.QuerySelector("p");
            element.Dataset["camelCase"] = "value";

            Assert.AreEqual("value", element.GetAttribute("data-camel-case"));
            element.Dataset.Remove("camelCase");
            Assert.IsFalse(element.HasAttribute("data-camel-case"));
        }

        [Test]
        public void DocumentObserverReportsTheDocumentAsItsTarget()
        {
            var loop = new ManualLoop();
            var document = "".ToHtmlDocument(Configuration.Default.With<IEventLoop>(_ => loop));
            var observer = new MutationObserver((_, __) => { });
            observer.Connect(document, childList: true);
            var comment = document.CreateComment("test");
            document.AppendChild(comment);

            var record = observer.Flush().Single();
            Assert.AreSame(document, record.Target);
            Assert.AreSame(comment, record.Added.Single());
            observer.Disconnect();
        }

        [Test]
        public void MutationListenerReceivesRecordsWithoutAWebObserver()
        {
            var listener = new MutationListener();
            var document = "<p></p>".ToHtmlDocument(Configuration.Default.With<IDomMutationListener>(_ => listener));
            var element = document.QuerySelector("p");
            element.SetAttribute("data-value", "1");
            element.AppendChild(document.CreateElement("span"));

            Assert.AreEqual(2, listener.Records.Count);
            Assert.AreEqual("attributes", listener.Records[0].Type);
            Assert.AreEqual("childList", listener.Records[1].Type);
        }

        private sealed class MutationListener : IDomMutationListener
        {
            internal readonly List<IMutationRecord> Records = new List<IMutationRecord>();
            public void OnMutation(IDocument document, IMutationRecord record) => Records.Add(record);
        }

        private sealed class ManualLoop : IEventLoop
        {
            public ICancellable Enqueue(Action<CancellationToken> action, TaskPriority priority) => new Pending();
            public void Spin() { }
            public void CancelAll() { }

            private sealed class Pending : ICancellable
            {
                public Boolean IsCompleted => false;
                public Boolean IsRunning => false;
                public void Cancel() { }
            }
        }
    }
}
