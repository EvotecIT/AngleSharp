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

        [Test]
        public void ThrowingObserverDoesNotPreventOtherNotifications()
        {
            var loop = new ManualLoop();
            var document = "<p></p>".ToHtmlDocument(Configuration.Default.With<IEventLoop>(_ => loop));
            loop.Spin();
            var element = document.QuerySelector("p");
            var error = new InvalidOperationException("observer failure");
            var first = new MutationObserver((_, __) => throw error);
            var delivered = new List<IMutationRecord>();
            var second = new MutationObserver((records, _) => delivered.AddRange(records));
            first.Connect(element, attributes: true);
            second.Connect(element, attributes: true);
            element.SetAttribute("data-value", "1");

            var reported = Assert.Throws<AggregateException>(() => loop.Spin());
            Assert.AreSame(error, reported.InnerExceptions.Single());
            Assert.AreEqual(1, delivered.Count);
            Assert.AreSame(element, delivered[0].Target);
            Assert.IsEmpty(second.Flush());
            first.Disconnect();
            element.SetAttribute("data-value", "2");
            loop.Spin();
            Assert.AreEqual(2, delivered.Count);
            second.Disconnect();
        }

        private sealed class MutationListener : IDomMutationListener
        {
            internal readonly List<IMutationRecord> Records = new List<IMutationRecord>();
            public void OnMutation(IDocument document, IMutationRecord record) => Records.Add(record);
        }

        private sealed class ManualLoop : IEventLoop
        {
            private readonly Queue<Action<CancellationToken>> _actions = new Queue<Action<CancellationToken>>();
            public ICancellable Enqueue(Action<CancellationToken> action, TaskPriority priority)
            {
                _actions.Enqueue(action);
                return new Pending();
            }
            public void Spin()
            {
                while (_actions.Count > 0) _actions.Dequeue()(CancellationToken.None);
            }
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
