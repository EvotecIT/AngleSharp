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
