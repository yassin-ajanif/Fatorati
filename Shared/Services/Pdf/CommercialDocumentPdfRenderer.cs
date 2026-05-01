using GestionCommerciale.Shared.Models.Pdf;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GestionCommerciale.Shared.Services.Pdf;

public static class CommercialDocumentPdfRenderer
{
    private const string PanelBg = "#F3F4F6";
    private const string PanelBorder = "#D1D5DB";
    private const string TableHeaderBg = "#E5E7EB";
    private const string TableBorder = "#D1D5DB";
    private const string TableRowAlt = "#F9FAFB";
    private const string TextPrimary = "#111827";
    private const string TextSecondary = "#4B5563";
    private const string TextMuted = "#6B7280";
    private const string AmountBoxBg = "#FFFBEB";
    private const string AmountBoxBorder = "#FDE047";
    private const string TtcBlue = "#3730A3";
    private const string SummaryRowBg = "#E5E7EB";
    private const string TableRowEven = "#FFFFFF";
    /// <summary>Rounded corners for panels, table frame, and bottom boxes (QuestPDF points).</summary>
    private const float ComponentCornerRadius = 8f;

    public static byte[] Render(CommercialDocumentPdfModel model, byte[]? logoBytes)
    {
        if (model.Columns.Count == 0)
            throw new ArgumentException("PDF model must define at least one column.", nameof(model));

        foreach (var row in model.Rows)
        {
            if (row.Count != model.Columns.Count)
                throw new ArgumentException($"Each row must have {model.Columns.Count} cells.");
        }

        if (model.SummaryRow != null)
        {
            var sr = model.SummaryRow;
            if (sr.LeadingSpan < 1 || sr.LeadingSpan > model.Columns.Count)
                throw new ArgumentException("Invalid summary LeadingSpan.");
            if (sr.Values.Count != model.Columns.Count - sr.LeadingSpan)
                throw new ArgumentException("Summary Values count must match columns after LeadingSpan.");
        }

        var doc = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.MarginHorizontal(40);
                page.MarginTop(28);
                page.MarginBottom(32);
                page.DefaultTextStyle(x => x.FontSize(9.5f).FontColor(TextPrimary));

                page.Header().Element(h => DrawHeader(h, model, logoBytes));

                page.Content().Column(main =>
                {
                    main.Spacing(16);
                    main.Item().Row(row =>
                    {
                        row.RelativeItem().Element(c => DrawKeyValuePanel(c, model.DocumentInfoLines));
                        row.Spacing(14);
                        row.RelativeItem().Element(c => DrawKeyValuePanel(c, model.PartyInfoLines));
                    });

                    main.Item().Element(c => DrawTable(c, model));

                    if (!string.IsNullOrWhiteSpace(model.Note))
                        main.Item().PaddingTop(6).Text(model.Note!).FontSize(9).FontColor(TextSecondary).Italic();

                    main.Item().PaddingTop(4).Row(bottom =>
                    {
                        bottom.RelativeItem(3).Element(c => DrawAmountWordsBox(c, model));
                        bottom.Spacing(14);
                        bottom.RelativeItem(2).Element(c => DrawTotalsBox(c, model));
                    });
                });

                page.Footer().AlignCenter().Column(fc =>
                {
                    fc.Item().LineHorizontal(0.5f).LineColor("#E5E7EB");
                    fc.Item().PaddingTop(8);
                    foreach (var line in model.FooterLines)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                            fc.Item().Text(line).FontSize(8).FontColor(TextMuted).AlignCenter();
                    }

                    fc.Item().PaddingTop(6).AlignCenter().Text(t =>
                    {
                        t.DefaultTextStyle(x => x.FontSize(8).FontColor(TextMuted));
                        t.Span("Page ");
                        t.CurrentPageNumber();
                        t.Span(" / ");
                        t.TotalPages();
                    });
                });
            });
        });

        return doc.GeneratePdf();
    }

    private static void DrawHeader(IContainer header, CommercialDocumentPdfModel model, byte[]? logoBytes)
    {
        header.PaddingBottom(8).Column(c =>
        {
            c.Item().Row(row =>
            {
                if (logoBytes is { Length: > 0 })
                {
                    row.ConstantItem(96).Height(58).PaddingRight(16)
                        .AlignMiddle()
                        .CornerRadius(6)
                        .Image(logoBytes).FitArea();
                }

                row.RelativeItem().AlignMiddle().Column(block =>
                {
                    block.Item().Text(model.CompanyName.ToUpperInvariant())
                        .Bold()
                        .FontSize(15)
                        .FontColor(TextPrimary)
                        .LetterSpacing(0.3f);

                    if (!string.IsNullOrWhiteSpace(model.DocumentKindLabel))
                    {
                        block.Item().PaddingTop(4).Text(model.DocumentKindLabel)
                            .FontSize(11)
                            .FontColor(TextSecondary);
                    }
                });
            });
        });
    }

    private static void DrawKeyValuePanel(IContainer container, IReadOnlyList<PdfKeyValueLine> lines)
    {
        var visible = lines.Where(l => !string.IsNullOrWhiteSpace(l.Value)).ToList();
        container.Element(InfoPanel).Padding(14).Column(col =>
        {
            col.Spacing(8);
            foreach (var line in visible)
            {
                col.Item().Row(r =>
                {
                    r.AutoItem().Text(line.Key + " :").SemiBold().FontSize(9).FontColor(TextMuted);
                    r.Spacing(8);
                    r.RelativeItem().Text(line.Value).FontSize(9).FontColor(TextPrimary);
                });
            }
        });
    }

    private static void DrawAmountWordsBox(IContainer container, CommercialDocumentPdfModel model)
    {
        var text = string.IsNullOrWhiteSpace(model.AmountInWords) ? "" : model.AmountInWords;

        container.Element(AmountPanel).Padding(14).Column(col =>
        {
            col.Item().Text("Arrêté le présent document à la somme de :")
                .SemiBold()
                .FontSize(9)
                .FontColor(TextSecondary);
            col.Item().PaddingTop(6).Text(text).FontSize(10).FontColor(TextPrimary).LineHeight(1.35f);
        });
    }

    private static void DrawTotalsBox(IContainer container, CommercialDocumentPdfModel model)
    {
        container.Element(InfoPanel).Padding(14).Column(col =>
        {
            col.Spacing(6);
            col.Item().Row(r =>
            {
                r.RelativeItem().Text("Total HT :").FontColor(TextSecondary);
                r.AutoItem().Text($"{model.TotalHt:N2} {model.Devise}").SemiBold();
            });
            if (model.ShowTaxAndTtcInTotalsBox)
            {
                col.Item().Row(r =>
                {
                    r.RelativeItem().Text("Taxe :").FontColor(TextSecondary);
                    r.AutoItem().Text($"{model.TotalTva:N2} {model.Devise}").SemiBold();
                });
                col.Item().PaddingTop(4).LineHorizontal(0.5f).LineColor(TableBorder);
                col.Item().PaddingTop(6).Row(r =>
                {
                    r.RelativeItem().Text("Total TTC :").SemiBold().FontSize(10);
                    r.AutoItem().Text($"{model.TotalTtc:N2} {model.Devise}")
                        .Bold()
                        .FontSize(13)
                        .FontColor(TtcBlue);
                });
            }
        });
    }

    private static void DrawTable(IContainer container, CommercialDocumentPdfModel model)
    {
        container
            .Border(1)
            .BorderColor(TableBorder)
            .CornerRadius(ComponentCornerRadius)
            .Table(t =>
        {
            t.ColumnsDefinition(cols =>
            {
                foreach (var c in model.Columns)
                    cols.RelativeColumn(c.RelativeWidth);
            });

            t.Header(h =>
            {
                for (var i = 0; i < model.Columns.Count; i++)
                {
                    var col = model.Columns[i];
                    var cell = h.Cell().Element(c => TableHeaderCell(c));
                    ApplyAlign(cell, col.Align).Text(col.Header).SemiBold().FontSize(9).FontColor(TextPrimary);
                }
            });

            var rowIndex = 0;
            foreach (var row in model.Rows)
            {
                var bg = rowIndex % 2 == 0 ? TableRowEven : TableRowAlt;
                for (var i = 0; i < model.Columns.Count; i++)
                {
                    var col = model.Columns[i];
                    var cell = t.Cell().Element(c => TableBodyCell(c, bg));
                    ApplyAlign(cell, col.Align).Text(row[i]).FontSize(9).FontColor(TextPrimary);
                }

                rowIndex++;
            }

            if (model.SummaryRow != null)
            {
                var sr = model.SummaryRow;
                t.Cell().ColumnSpan((uint)sr.LeadingSpan).Element(TableSummaryCell).AlignLeft().Text(sr.Label)
                    .SemiBold().FontSize(9).FontColor(TextPrimary);
                foreach (var v in sr.Values)
                    t.Cell().Element(TableSummaryCell).AlignRight().Text(v).SemiBold().FontSize(9).FontColor(TextPrimary);
            }
        });
    }

    private static IContainer ApplyAlign(IContainer cell, PdfTextAlignment align) =>
        align switch
        {
            PdfTextAlignment.Center => cell.AlignCenter(),
            PdfTextAlignment.End => cell.AlignRight(),
            _ => cell.AlignLeft()
        };

    private static IContainer InfoPanel(IContainer c) =>
        c.Background(PanelBg).Border(1).BorderColor(PanelBorder).CornerRadius(ComponentCornerRadius);

    private static IContainer AmountPanel(IContainer c) =>
        c.Background(AmountBoxBg).Border(1).BorderColor(AmountBoxBorder).CornerRadius(ComponentCornerRadius);

    private static IContainer TableHeaderCell(IContainer c) =>
        c.Background(TableHeaderBg)
            .Border(0.5f)
            .BorderColor(TableBorder)
            .PaddingVertical(8)
            .PaddingHorizontal(6);

    private static IContainer TableBodyCell(IContainer c, string backgroundHex) =>
        c.Background(backgroundHex)
            .Border(0.5f)
            .BorderColor(TableBorder)
            .PaddingVertical(6)
            .PaddingHorizontal(6);

    private static IContainer TableSummaryCell(IContainer c) =>
        c.Background(SummaryRowBg)
            .Border(0.5f)
            .BorderColor(TableBorder)
            .PaddingVertical(8)
            .PaddingHorizontal(6);
}
