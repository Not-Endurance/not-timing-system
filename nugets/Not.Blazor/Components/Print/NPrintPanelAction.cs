using Not.Files;
using Not.Print;

namespace Not.Blazor.Components.Print;

public sealed class NPrintPanelAction
{
    public static NPrintPanelAction PrintPdf(
        string content,
        Func<NPrintPanelContext, Task<NPrintDocumentRequest>> request,
        string? icon = null,
        Func<Task>? afterSuccess = null
    )
    {
        return new(content, icon, NPrintPanelActionKind.PrintPdf, request, null, null, afterSuccess);
    }

    public static NPrintPanelAction DownloadPdf(
        string content,
        Func<NPrintPanelContext, Task<NPrintDocumentRequest>> request,
        string? icon = null,
        Func<Task>? afterSuccess = null
    )
    {
        return new(content, icon, NPrintPanelActionKind.DownloadPdf, request, null, null, afterSuccess);
    }

    public static NPrintPanelAction DownloadZip(
        string content,
        Func<NPrintPanelContext, Task<NPrintBatchRequest>> request,
        string? icon = null,
        Func<Task>? afterSuccess = null
    )
    {
        return new(content, icon, NPrintPanelActionKind.DownloadZip, null, request, null, afterSuccess);
    }

    public static NPrintPanelAction DownloadFile(
        string content,
        Func<NPrintPanelContext, Task<NFile>> file,
        string? icon = null,
        Func<Task>? afterSuccess = null
    )
    {
        return new(content, icon, NPrintPanelActionKind.DownloadFile, null, null, file, afterSuccess);
    }

    readonly Func<NPrintPanelContext, Task<NPrintDocumentRequest>>? _documentRequest;
    readonly Func<NPrintPanelContext, Task<NPrintBatchRequest>>? _batchRequest;
    readonly Func<NPrintPanelContext, Task<NFile>>? _file;
    readonly Func<Task>? _afterSuccess;

    NPrintPanelAction(
        string content,
        string? icon,
        NPrintPanelActionKind kind,
        Func<NPrintPanelContext, Task<NPrintDocumentRequest>>? documentRequest,
        Func<NPrintPanelContext, Task<NPrintBatchRequest>>? batchRequest,
        Func<NPrintPanelContext, Task<NFile>>? file,
        Func<Task>? afterSuccess
    )
    {
        Content = content;
        Icon = icon;
        Kind = kind;
        _documentRequest = documentRequest;
        _batchRequest = batchRequest;
        _file = file;
        _afterSuccess = afterSuccess;
    }

    public string Content { get; }
    public string? Icon { get; }
    public NPrintPanelActionKind Kind { get; }

    internal Task<NPrintDocumentRequest> GetDocumentRequest(NPrintPanelContext context)
    {
        return _documentRequest?.Invoke(context)
            ?? throw new InvalidOperationException("Print document request factory is missing.");
    }

    internal Task<NPrintBatchRequest> GetBatchRequest(NPrintPanelContext context)
    {
        return _batchRequest?.Invoke(context)
            ?? throw new InvalidOperationException("Print batch request factory is missing.");
    }

    internal Task<NFile> GetFile(NPrintPanelContext context)
    {
        return _file?.Invoke(context) ?? throw new InvalidOperationException("File factory is missing.");
    }

    internal Task AfterSuccess()
    {
        return _afterSuccess?.Invoke() ?? Task.CompletedTask;
    }
}
