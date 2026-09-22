namespace AngleSharp.Common;

using System;
using AngleSharp.Text;

public abstract partial class BaseTokenizer
{
    // Only script-created input streams suspend at their current end. Ordinary
    // document parsing retains its existing EOF and buffer behavior.
    internal Boolean IsInputOpen { get; set; }
    private Boolean _pendingInputLineFeed;
    internal Int32 InputLimit { get; set; } = Int32.MaxValue;
    private Int32 AvailableInputLength => Math.Min(_source.Length, InputLimit);
    private protected Boolean AtInputBoundary => IsInputOpen && _source.Index >= AvailableInputLength;

    // A start tag with a valid name cannot produce a token until its closing
    // bracket arrives (or the input stream closes). While inside a quoted
    // attribute, even a bracket cannot complete the token; only the matching
    // quote is worth replaying. Scan only newly appended input.
    private protected Boolean CanWaitForStartTagTerminator(Int32 start, Int32 scannedLength, Char terminator,
        Boolean scanNewInput, out Int32 currentLength)
    {
        currentLength = AvailableInputLength;
        if (!IsInputOpen || InputLimit != Int32.MaxValue || _source.Index != start ||
            start < 0 || start + 1 >= currentLength || scannedLength > currentLength ||
            _source[start] != '<')
        {
            return false;
        }

        var nameStart = _source[start + 1];
        if (!((nameStart >= 'a' && nameStart <= 'z') || (nameStart >= 'A' && nameStart <= 'Z')))
        {
            return false;
        }

        // The tokenizer already proved the whole prefix incomplete on the first
        // pass. Only later appends need a terminator check.
        for (var i = scanNewInput ? scannedLength : currentLength; i < currentLength; i++)
        {
            if (_source[i] == terminator) return false;
        }

        return true;
    }

    internal void UseExpandableInput()
    {
        _charBuffer.Dispose();
        _apb = null;
        _charBuffer = _sbb = new StringBuilderBuffer();
        IsInputOpen = true;
    }

    private void RequireInput(Int32 count)
    {
        if (IsInputOpen && AvailableInputLength - _source.Index + 1 < count)
        {
            throw new IncompleteInputException();
        }
    }

    private protected InputBookmark MarkInput() => new(
        _source.Index, _column, _row, _current, _normalized, _columns.Count, _pendingInputLineFeed);

    private protected void RestoreInput(InputBookmark mark)
    {
        _source.Index = mark.Index;
        _column = mark.Column;
        _row = mark.Row;
        _current = mark.Current;
        _normalized = mark.Normalized;
        _pendingInputLineFeed = mark.PendingLineFeed;
        while (_columns.Count > mark.Columns) _columns.Pop();
        _charBuffer.Discard();
        _stringBuilder.Clear();
    }

    private protected readonly struct InputBookmark
    {
        internal InputBookmark(Int32 index, UInt16 column, UInt16 row, Char current, Boolean normalized, Int32 columns, Boolean pendingLineFeed)
        {
            Index = index;
            Column = column;
            Row = row;
            Current = current;
            Normalized = normalized;
            Columns = columns;
            PendingLineFeed = pendingLineFeed;
        }
        internal Int32 Index { get; }
        internal UInt16 Column { get; }
        internal UInt16 Row { get; }
        internal Char Current { get; }
        internal Boolean Normalized { get; }
        internal Int32 Columns { get; }
        internal Boolean PendingLineFeed { get; }
    }
}

/// <summary>Unwinds only an unfinished lexical token, never an emitted DOM node.</summary>
internal sealed class IncompleteInputException : Exception { }
