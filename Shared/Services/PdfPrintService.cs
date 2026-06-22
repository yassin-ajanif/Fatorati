namespace GestionCommerciale.Shared.Services;

public sealed class PdfPrintService : IPdfPrintService
{
    public async Task PrintPdfAsync(byte[] pdfBytes, string documentTitle, CancellationToken cancellationToken = default)
    {
        if (pdfBytes.Length == 0)
            throw new ArgumentException("Le contenu PDF est vide.", nameof(pdfBytes));

        var path = await WriteTempPdfAsync(pdfBytes, documentTitle, cancellationToken);
        await WindowsPdfPrintHost.PrintAsync(path, documentTitle, cancellationToken);
    }

    private static async Task<string> WriteTempPdfAsync(byte[] pdfBytes, string documentTitle, CancellationToken cancellationToken)
    {
        var dir = Path.Combine(Path.GetTempPath(), "GestionCommerciale", "print");
        Directory.CreateDirectory(dir);

        var safeName = string.Join("_", documentTitle.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(safeName))
            safeName = "document";

        var path = Path.Combine(dir, $"{safeName}-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(path, pdfBytes, cancellationToken);
        return path;
    }
}
