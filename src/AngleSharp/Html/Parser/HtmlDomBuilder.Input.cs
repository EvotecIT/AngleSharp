namespace AngleSharp.Html.Parser;

using AngleSharp.Dom;
using AngleSharp.Html.Construction;
using AngleSharp.Html.Parser.Tokens.Struct;
using System;
using System.Threading.Tasks;

partial class HtmlDomBuilder<TDocument, TElement> : IHtmlInputStream
    where TElement : class, IConstructableElement
    where TDocument : class, IConstructableDocument
{
    Boolean IHtmlInputStream.IsScriptCreated => _scriptCreatedInput;

    private Boolean _scriptCreatedInput;
    private Boolean _inputClosed;
    private Boolean _skipInputNewLine;
    private Boolean _inputContinuation;
    private Int32 _inputPumpDepth;

    internal void StartInput(HtmlParserOptions options)
    {
        SetOptions(options);
        _scriptCreatedInput = true;
        _tokenizer.UseExpandableInput();
    }

    private Boolean WriteInput(String content)
    {
        if (_ended) return false;
        var source = _document.Source;
        var resume = source.Index;
        var insertion = _insertionPoints.Count > 0
            ? _insertionPoints[_insertionPoints.Count - 1].Position : source.Length;
        source.Index = insertion;
        source.InsertText(content);
        foreach (var point in _insertionPoints)
        {
            if (point.Position >= insertion) point.Position += content.Length;
        }
        source.Index = resume > insertion ? resume + content.Length : resume;
        PumpInput();
        return true;
    }

    void IHtmlInputStream.CloseInput()
    {
        if (!_scriptCreatedInput || _ended) return;
        _inputClosed = true;
        if (_inputPumpDepth == 0) PumpInput();
    }

    private void PumpInput()
    {
        if (_ended || _inputContinuation && _insertionPoints.Count == 0 || _reentryPaused) return;
        _inputPumpDepth++;
        _reentryDepth++;
        try
        {
            while (!_ended)
            {
                _tokenizer.IsInputOpen = !_inputClosed || _insertionPoints.Count > 0;
                _tokenizer.InputLimit = _insertionPoints.Count > 0
                    ? _insertionPoints[_insertionPoints.Count - 1].Position : Int32.MaxValue;
                if (!_tokenizer.TryReadInputToken(out var token)) break;
                Consume(ref token);
                if (token.Type == HtmlTokenType.EndOfFile)
                {
                    _ended = true;
                    break;
                }
                if (_pendingReentrantScript is not null || _waiting is not null)
                {
                    if (!_inputContinuation)
                    {
                        _inputContinuation = true;
                        _ = ContinueInputAsync();
                    }
                    break;
                }
            }
        }
        finally
        {
            _reentryDepth--;
            _inputPumpDepth--;
        }
    }

    private async Task ContinueInputAsync()
    {
        try
        {
            var waiting = _waiting;
            _waiting = null;
            if (waiting is not null) await waiting.ConfigureAwait(false);
            while (_pendingReentrantScript is not null && !_ended)
            {
                var pending = _pendingReentrantScript;
                _pendingReentrantScript = null;
                _reentryPaused = false;
                await RunScript(pending).ConfigureAwait(false);
            }
        }
        catch (Exception error)
        {
            _document.TrackError(error);
        }
        finally
        {
            var syncRoot = (_document as IDocument)?.Context.GetService<IDomSynchronization>()?.SyncRoot;
            if (syncRoot is null) Resume();
            else lock (syncRoot) Resume();
        }

        void Resume()
        {
            _inputContinuation = false;
            _reentryPaused = false;
            PumpInput();
        }
    }
}
