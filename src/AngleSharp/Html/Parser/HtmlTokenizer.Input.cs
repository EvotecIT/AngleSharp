namespace AngleSharp.Html.Parser;

using AngleSharp.Common;
using AngleSharp.Text;
using AngleSharp.Html.Parser.Tokens.Struct;
using System;
using System.Collections.Generic;

public sealed partial class HtmlTokenizer
{
    private ScriptState _inputScriptState;

    private ref StructHtmlToken NewInputScriptChunk(ScriptState state)
    {
        _inputScriptState = state;
        return ref SkipScriptText ? ref NewSkippedContent() : ref NewCharacter();
    }

    private List<(HtmlParseError Code, TextPosition Position)>? _inputErrors;
    private Int32 _pendingStartTagAt = -1;
    private Int32 _pendingStartTagScannedLength;
    private Char _pendingStartTagTerminator = '>';
    private Char _inputAttributeQuote;

    /// <summary>
    /// Reads a complete token or leaves an unfinished lexical construct for the
    /// next write. Previously emitted tokens and the tree builder are retained.
    /// </summary>
    internal Boolean TryReadInputToken(out StructHtmlToken token)
    {
        if (!IsInputOpen)
        {
            token = GetStructToken();
            return true;
        }
        var bookmark = MarkInput();
        if (State == HtmlParseMode.PCData && bookmark.Index == _pendingStartTagAt &&
            CanWaitForStartTagTerminator(bookmark.Index, _pendingStartTagScannedLength,
                _pendingStartTagTerminator, scanNewInput: true, out var length))
        {
            _pendingStartTagScannedLength = length;
            token = default;
            return false;
        }
        _pendingStartTagAt = -1;
        _inputAttributeQuote = default;
        var state = State;
        var lastTag = _lastStartTag;
        var position = _position;
        var scriptState = _inputScriptState;
        var errors = new List<(HtmlParseError Code, TextPosition Position)>();
        _inputErrors = errors;
        try
        {
            token = GetNextStructToken();
        }
        catch (IncompleteInputException)
        {
            RestoreInput(bookmark);
            State = state;
            _lastStartTag = lastTag;
            _position = position;
            _inputScriptState = scriptState;
            _token = default;
            if (state == HtmlParseMode.PCData &&
                CanWaitForStartTagTerminator(bookmark.Index, bookmark.Index,
                    _inputAttributeQuote == default ? '>' : _inputAttributeQuote,
                    scanNewInput: false, out var currentLength))
            {
                _pendingStartTagAt = bookmark.Index;
                _pendingStartTagScannedLength = currentLength;
                _pendingStartTagTerminator = _inputAttributeQuote == default ? '>' : _inputAttributeQuote;
            }
            token = default;
            return false;
        }
        finally
        {
            _inputErrors = null;
        }
        foreach (var error in errors) RaiseErrorOccurred(error.Code, error.Position);
        OnToken?.Invoke(token.ToHtmlToken(), new TextRange(_position, GetCurrentPosition().After(Current)));
        return true;
    }
}
