namespace AngleSharp.Core.Tests.Library
{
    using AngleSharp.Core.Tests.Mocks;
    using AngleSharp.Dom;
    using AngleSharp.Html.Dom;
    using AngleSharp.Html.Parser;
    using AngleSharp.Io;
    using AngleSharp.Browser;
    using AngleSharp.Common;
    using System.Collections.Generic;
    using AngleSharp.Scripting;
    using System.Threading;
    using NUnit.Framework;
    using System;
    using System.Threading.Tasks;

    [TestFixture]
    public class DocumentInputStreamTests
    {
        private static Task<IDocument> OpenDocument(IConfiguration configuration = null) =>
            BrowsingContext.New(configuration ?? Configuration.Default).OpenAsync(request => request
                .Address("https://write.example/page").Content("<!doctype html><p>Original</p>"));

        [Test]
        public async Task WritesUpdateTheSameTreeBeforeClose()
        {
            var document = await OpenDocument();
            document.Open();
            document.Write("<p id='first'>A");
            var first = document.GetElementById("first");
            Assert.IsNotNull(first);
            Assert.AreEqual("A", first.TextContent);
            Assert.AreEqual(DocumentReadyState.Loading, document.ReadyState);

            document.Write("B</p><p id='second'>C</p>");

            Assert.AreSame(first, document.GetElementById("first"));
            Assert.AreEqual("AB", first.TextContent);
            Assert.AreEqual("C", document.GetElementById("second").TextContent);
        }

        [Test]
        public async Task SplitTagsAndReferencesWaitForInputWithoutLosingCompletedText()
        {
            var document = await OpenDocument();
            document.Open();
            document.Write("<p id='first");
            Assert.IsNull(document.QuerySelector("p"));
            document.Write("'>A&am");
            var first = document.GetElementById("first");
            Assert.IsNotNull(first);
            Assert.AreEqual("A", first.TextContent);
            document.Write("p;B</p>");
            Assert.AreSame(first, document.GetElementById("first"));
            Assert.AreEqual("A&B", first.TextContent);
        }

        [Test]
        public async Task SplitDoctypeAndCommentDoNotCommitPrematureEofRecovery()
        {
            var document = await OpenDocument();
            document.Open();
            document.Write("<!DOC");
            Assert.IsNull(document.Doctype);
            document.Write("TYPE html><!--comment");
            Assert.IsNotNull(document.Doctype);
            Assert.AreEqual(1, document.ChildNodes.Length);
            document.Write("--><p>After</p>");
            Assert.AreEqual(NodeType.Comment, document.ChildNodes[1].NodeType);
            Assert.AreEqual("comment", document.ChildNodes[1].TextContent);
            Assert.AreEqual("After", document.QuerySelector("p").TextContent);
        }

        [Test]
        public async Task SplitScriptRunsOnceAndNestedWriteUsesItsInsertionPoint()
        {
            var calls = 0;
            var configuration = Configuration.Default.WithScripts(new CallbackScriptEngine(options =>
            {
                calls++;
                options.Document.Write("<b id='nested'>Nested</b>");
            }));
            var document = await OpenDocument(configuration);
            document.Open();
            document.Write("<script type='c-sharp'>body");
            Assert.AreEqual("body", document.QuerySelector("script").TextContent);
            Assert.AreEqual(0, calls);
            document.Write("</scr");
            Assert.AreEqual(0, calls);
            document.Write("ipt><p id='after'>After</p>");
            Assert.AreEqual(1, calls);
            Assert.AreEqual("Nested", document.GetElementById("nested").TextContent);
            Assert.AreEqual("after", document.GetElementById("nested").NextElementSibling.Id);
        }

        [Test]
        public async Task ReopeningAbandonsThePreviousInputWithoutReplacingTheDocument()
        {
            var document = await OpenDocument();
            document.Open();
            document.Write("<p id='old'>Old</p><div title='");
            var old = document.GetElementById("old");
            Assert.IsNotNull(old);
            Assert.AreSame(document, document.Open());
            document.Write("<h1>New</h1>");
            Assert.IsFalse(document.Contains(old));
            Assert.IsNull(document.GetElementById("old"));
            Assert.AreEqual("New", document.QuerySelector("h1").TextContent);
        }
        [Test]
        public async Task ConsecutiveNestedWritesDoNotConsumeTheOuterInputEarly()
        {
            var configuration = Configuration.Default.WithScripts(new CallbackScriptEngine(options =>
            {
                options.Document.Write("<b id='nested'>");
                Assert.IsNull(options.Document.GetElementById("after"));
                options.Document.Write("Nested</b>");
                Assert.AreEqual("Nested", options.Document.GetElementById("nested").TextContent);
            }));
            var document = await OpenDocument(configuration);
            document.Open();
            document.Write("<script type='c-sharp'>body</script><p id='after'>After</p>");
            document.Close();
            Assert.AreEqual("after", document.GetElementById("nested").NextElementSibling.Id);
        }

        [Test]
        public async Task CloseFinalizesTheStreamAndASecondCloseDoesNotRepeatLoad()
        {
            var document = await OpenDocument();
            document.Open();
            var loads = 0;
            var loaded = new TaskCompletionSource<Boolean>();
            document.DefaultView.AddEventListener("load", (_, _) => { loads++; loaded.TrySetResult(true); });
            document.Write("<p>Final &amp");
            document.Close();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(5));
            document.Close();
            Assert.AreEqual("Final &", document.QuerySelector("p").TextContent);
            Assert.AreEqual(DocumentReadyState.Complete, document.ReadyState);
            Assert.AreEqual(1, loads);
        }

        [Test]
        public async Task TrailingCarriageReturnIsVisibleAndTheFollowingLineFeedIsSkipped()
        {
            var document = await OpenDocument();
            document.Open();
            document.Write("<p>A\r");
            Assert.AreEqual("A\n", document.QuerySelector("p").TextContent);
            document.Write("\nB</p>");
            Assert.AreEqual("A\nB", document.QuerySelector("p").TextContent);
        }

        [TestCase("&amp;", "&")]
        [TestCase("&#65;", "A")]
        [TestCase("&#x41;", "A")]
        public async Task CompletedReferencesAreVisibleBeforeTheNextWrite(String source, String expected)
        {
            var document = await OpenDocument();
            document.Open();
            document.Write("<p>" + source);
            Assert.AreEqual(expected, document.QuerySelector("p").TextContent);
        }

        [TestCase("<p>", "</p>")]
        [TestCase("<textarea>", "</textarea>")]
        [TestCase("<style>", "</style>")]
        [TestCase("<plaintext>", "")]
        public async Task SkipTextOptionsDoNotDependOnWriteBoundaries(String start, String end)
        {
            var parser = new HtmlParser(new HtmlParserOptions {
                SkipDataText = true, SkipRCDataText = true, SkipRawText = true, SkipPlaintext = true
            });
            var document = await OpenDocument(Configuration.Default.WithOnly<IHtmlParser>(parser));
            document.Open();
            document.Write(start + "Text");
            document.Write(end);
            document.Close();
            Assert.IsFalse(document.DocumentElement.TextContent.Contains("Text"));
        }

        [Test]
        public async Task ReopeningPreventsADelayedOldScriptFromWritingIntoTheNewStream()
        {
            var requester = new DelayedInputRequester();
            var calls = 0;
            var document = await OpenDocument(Configuration.Default.WithMockRequester(requester)
                .WithScripts(new CallbackScriptEngine(options => { calls++; options.Document.Write("<b>Old</b>"); })));
            document.Open();
            document.Write("<script type='c-sharp' src='/slow'></script>");
            document.Open();
            document.Write("<p>New</p>");
            requester.Complete();
            Assert.AreEqual(0, calls);
            Assert.AreEqual("New", document.Body.TextContent);
        }

        [Test]
        public async Task AnAbandonedCloseCannotCompleteTheReplacementStream()
        {
            var requester = new DelayedInputRequester();
            var calls = 0;
            var document = await OpenDocument(Configuration.Default.WithMockRequester(requester)
                .WithScripts(new CallbackScriptEngine(_ => calls++)));
            document.Open();
            document.Write("<script type='c-sharp' defer src='/slow'></script>");
            document.Close();
            document.Open();
            document.Write("<p>New</p>");
            requester.Complete();
            Assert.AreEqual(0, calls);
            Assert.AreEqual(DocumentReadyState.Loading, document.ReadyState);
            document.Close();
            Assert.AreEqual(DocumentReadyState.Complete, document.ReadyState);
        }

        [Test]
        public void InlineCancellationCannotQueueCompletionForTheReplacementInput()
        {
            var loop = new QueuedInputLoop();
            var requester = new DelayedInputRequester(cancelInline: true);
            var context = BrowsingContext.New(Configuration.Default.WithMockRequester(requester)
                .WithScripts(new CallbackScriptEngine(_ => Assert.Fail("The abandoned script ran")))
                .With<IEventLoop>(_ => loop));
            var document = context.GetService<IHtmlParser>().ParseDocument("<p>Original</p>");
            loop.Drain();
            document.Open();
            document.Write("<script type='c-sharp' src='https://write.example/slow'></script>");
            document.Close();
            document.Open();
            document.Write("<p>Replacement</p>");
            loop.Drain();
            Assert.AreEqual(DocumentReadyState.Loading, document.ReadyState);
            Assert.AreEqual("Replacement", document.Body.TextContent);
            document.Close();
            loop.Drain();
            Assert.AreEqual(DocumentReadyState.Complete, document.ReadyState);
        }

        private sealed class QueuedInputLoop : IEventLoop
        {
            private readonly Queue<InputTask> _queue = new();
            public ICancellable Enqueue(Action<CancellationToken> action, TaskPriority priority)
            {
                var task = new InputTask(action);
                _queue.Enqueue(task);
                return task;
            }
            internal void Drain()
            {
                while (_queue.Count != 0) _queue.Dequeue().Run();
            }
            public void Spin() => Drain();
            public void CancelAll()
            {
                while (_queue.Count != 0) _queue.Dequeue().Cancel();
            }
            private sealed class InputTask(Action<CancellationToken> action) : ICancellable
            {
                public Boolean IsCompleted { get; private set; }
                public Boolean IsRunning { get; private set; }
                public void Cancel() => IsCompleted = true;
                internal void Run()
                {
                    if (IsCompleted) return;
                    IsRunning = true;
                    action(CancellationToken.None);
                    IsRunning = false;
                    IsCompleted = true;
                }
            }
        }

        [Test]
        public async Task ABlockingStylesheetDelaysSynchronousWrittenScripts()
        {
            var requester = new DelayedInputRequester();
            var scripts = new SynchronousInputScripts();
            var document = await OpenDocument(Configuration.Default.WithMockRequester(requester)
                .With(new Css.MockStylingService()).WithScripts(scripts));
            document.Open();
            document.Write("<link rel='stylesheet' href='/slow'><script type='c-sharp'>body</script>");
            Assert.AreEqual(0, scripts.Calls);
            requester.Complete();
            await scripts.Executed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreEqual(1, scripts.Calls);
        }

        private sealed class DelayedInputRequester(Boolean cancelInline = false) : BaseRequester
        {
            private readonly TaskCompletionSource<IResponse> _response = new();
            private Url _address;
            public override Boolean SupportsProtocol(String protocol) => true;
            protected override Task<IResponse> PerformRequestAsync(Request request, CancellationToken cancellationToken)
            {
                _address = request.Address;
                if (cancelInline) cancellationToken.Register(() => _response.TrySetCanceled());
                return _response.Task;
            }
            internal void Complete() => _response.SetResult(VirtualResponse.Create(r => r.Address(_address).Content("body")));
        }

        private sealed class SynchronousInputScripts : IScriptingService, ISynchronousScriptingService
        {
            internal Int32 Calls;
            internal readonly TaskCompletionSource<Boolean> Executed = new();
            public String Type => "c-sharp";
            public Boolean SupportsType(String type) => type == Type;
            public Object EvaluateScript(IDocument document, String source, String type, String sourceUrl)
            {
                Calls++;
                Executed.TrySetResult(true);
                return null;
            }
            public Task EvaluateScriptAsync(IResponse response, ScriptOptions options, CancellationToken cancellationToken)
            {
                EvaluateScript(options.Document, "", Type, "");
                return Task.CompletedTask;
            }
        }

        [TestCase("<!doctype html><p>A&amp;B&#x20;C</p>")]
        [TestCase("<!--one--><div title='a&copy;b' data-x=one>Text</div><!--tail-->")]
        [TestCase("<title>A&amp;B</title><textarea>\nA&lt;B</textarea>")]
        [TestCase("<pre>\nA\r\nB</pre><listing>\nC</listing>")]
        [TestCase("<style>a<b{color:red}</style><p>After</p>")]
        [TestCase("<script><!--<script>one</script>two--></script><p>After</p>")]
        [TestCase("<table>Text<tr><td>A<td>B</table><p><b>one<i>two</b>three</i>")]
        [TestCase("<svg><![CDATA[a<b]]><text>Hello</text></svg>")]
        [TestCase("<!DOCTYPE html PUBLIC \"x\" \"y\"><!--unfinished")]
        [TestCase("<p>Text &amp")]
        public async Task EverySplitMatchesACompleteParse(String markup)
        {
            var expected = await BrowsingContext.New(Configuration.Default).OpenAsync(r => r.Content(markup));
            using (var document = await OpenDocument())
            {
                document.Open();
                foreach (var character in markup) document.Write(character.ToString());
                document.Close();
                Assert.AreEqual(expected.ToHtml(), document.ToHtml(), "Character-by-character input");
            }
            for (var split = 0; split <= markup.Length; split++)
            {
                using var document = await OpenDocument();
                document.Open();
                document.Write(markup.Substring(0, split));
                document.Write(markup.Substring(split));
                document.Close();
                Assert.AreEqual(expected.ToHtml(), document.ToHtml(), "Split at " + split);
            }
        }

    }
}
