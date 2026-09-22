namespace AngleSharp.Html.Construction;

/// <summary>Ends a script-created parser input stream.</summary>
internal interface IHtmlInputStream
{
    System.Boolean IsScriptCreated { get; }

    void CloseInput();
}
