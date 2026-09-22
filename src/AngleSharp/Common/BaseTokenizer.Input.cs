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
