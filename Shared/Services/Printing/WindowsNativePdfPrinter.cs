using System.Drawing.Printing;
using PdfiumViewer;
using WinFormsDialogResult = System.Windows.Forms.DialogResult;
using WinFormsPrintDialog = System.Windows.Forms.PrintDialog;

namespace GestionCommerciale.Shared.Services.Printing;

/// <summary>
/// Prints a PDF via PdfiumViewer and the native Windows print dialog (PrintDlgEx).
/// </summary>
public static class WindowsNativePdfPrinter
{
    public sealed record PrintResult(bool Success, bool CancelledByUser, string? ErrorMessage);

    public static Task<PrintResult> PrintAsync(
        string pdfPath,
        string documentTitle,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pdfPath);

        var tcs = new TaskCompletionSource<PrintResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));

        var thread = new Thread(() =>
        {
            try
            {
                tcs.TrySetResult(PrintCore(pdfPath, documentTitle));
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = "PdfPrintDialog"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return tcs.Task;
    }

    private static PrintResult PrintCore(string pdfPath, string documentTitle)
    {
        var fullPath = Path.GetFullPath(pdfPath);
        if (!File.Exists(fullPath))
            return Fail($"Le fichier PDF est introuvable : {fullPath}");

        if (!fullPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return Fail("Seuls les fichiers PDF peuvent être imprimés.");

        using var document = PdfDocument.Load(fullPath);
        using var printDocument = document.CreatePrintDocument(PdfPrintMode.ShrinkToMargin);
        printDocument.DocumentName = string.IsNullOrWhiteSpace(documentTitle) ? "Document" : documentTitle;

        using var dialog = new WinFormsPrintDialog
        {
            AllowSomePages = true,
            AllowSelection = false,
            UseEXDialog = true,
            Document = printDocument
        };

        if (dialog.ShowDialog() != WinFormsDialogResult.OK)
            return new PrintResult(Success: false, CancelledByUser: true, ErrorMessage: null);

        printDocument.Print();
        return new PrintResult(Success: true, CancelledByUser: false, ErrorMessage: null);
    }

    private static PrintResult Fail(string message) =>
        new(Success: false, CancelledByUser: false, ErrorMessage: message);
}
