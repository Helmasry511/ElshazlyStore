using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace ElshazlyStore.Desktop.Services.Printing;

/// <summary>
/// Shared receipt/document printing service with reusable layout primitives.
/// Provides professional A5-ready receipt construction with Arabic-first RTL support.
/// All displayed data must originate from server DTOs — no locally computed business facts.
/// </summary>
/// <remarks>
/// LOGO INTEGRATION (when final logo asset is available):
///   1. Add logo file:  Resources/company-logo.png
///   2. Add to .csproj:  &lt;Resource Include="Resources\company-logo.png" /&gt;
///   3. In AddCompanyHeader(), replace the placeholder Run("◻") with:
///      <code>
///      var logo = new System.Windows.Controls.Image
///      {
///          Source = new System.Windows.Media.Imaging.BitmapImage(
///              new Uri("pack://application:,,,/Resources/company-logo.png")),
///          Width = 40, Height = 40
///      };
///      namePara.Inlines.Add(new InlineUIContainer(logo));
///      </code>
/// </remarks>
public static class ReceiptPrintService
{
    // ── Company branding (placeholder until final assets) ──
    public const string CompanyName = "الشاذلي";
    public const string CompanySubtitle = "للتجارة والتوزيع";

    // ── Typography ──
    private static readonly FontFamily _font = new("Segoe UI");
    private const double TitleSize = 15;
    private const double SubtitleSize = 10;
    private const double HeadingSize = 13;
    private const double BodySize = 10;
    private const double SmallSize = 8.5;
    private const double FieldLabelSize = 10;

    // ── Colors ──
    private static readonly SolidColorBrush _dark = Freeze(new SolidColorBrush(Color.FromRgb(30, 30, 30)));
    private static readonly SolidColorBrush _gray = Freeze(new SolidColorBrush(Color.FromRgb(140, 140, 140)));
    private static readonly SolidColorBrush _lightGray = Freeze(new SolidColorBrush(Color.FromRgb(225, 225, 225)));
    private static readonly SolidColorBrush _border = Freeze(new SolidColorBrush(Color.FromRgb(180, 180, 180)));

    // ── Public API ──

    /// <summary>Creates a new RTL FlowDocument configured for receipt printing.</summary>
    public static FlowDocument CreateDocument()
    {
        return new FlowDocument
        {
            FontFamily = _font,
            FontSize = BodySize,
            FlowDirection = FlowDirection.RightToLeft,
            PagePadding = new Thickness(30, 20, 30, 16),
            Foreground = _dark
        };
    }

    /// <summary>
    /// Adds a professional company header: logo placeholder, company name, subtitle, rule, document title.
    /// </summary>
    /// <param name="doc">Target document.</param>
    /// <param name="documentTitle">Document type label (e.g. "إيصال دفع مورد").</param>
    /// <param name="compact">Use compact spacing for dual-copy mode.</param>
    public static void AddCompanyHeader(FlowDocument doc, string documentTitle, bool compact = false)
    {
        // Company name + logo placeholder
        var namePara = new Paragraph
        {
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 0)
        };
        // Logo placeholder — replace with actual Image per the remarks above
        namePara.Inlines.Add(new Run("◻ ") { FontSize = compact ? 18 : 24, Foreground = _gray });
        namePara.Inlines.Add(new Run(CompanyName)
        {
            FontSize = compact ? TitleSize - 1 : TitleSize,
            FontWeight = FontWeights.Bold
        });
        doc.Blocks.Add(namePara);

        if (!compact)
        {
            doc.Blocks.Add(new Paragraph(new Run(CompanySubtitle)
            {
                FontSize = SubtitleSize,
                Foreground = _gray
            })
            { TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 2) });
        }

        // Horizontal rule
        doc.Blocks.Add(CreateRule());

        // Document title
        doc.Blocks.Add(new Paragraph(new Run(documentTitle)
        {
            FontSize = compact ? HeadingSize - 1 : HeadingSize,
            FontWeight = FontWeights.Bold
        })
        { TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 3, 0, compact ? 4 : 6) });
    }

    /// <summary>Adds a single labeled field row.</summary>
    public static void AddField(FlowDocument doc, string label, string value)
    {
        var para = new Paragraph { Margin = new Thickness(0, 1.5, 0, 1.5), FontSize = FieldLabelSize };
        para.Inlines.Add(new Run(label) { FontWeight = FontWeights.SemiBold });
        para.Inlines.Add(new Run(" " + value));
        doc.Blocks.Add(para);
    }

    /// <summary>Adds two fields side by side using a two-column table.</summary>
    public static void AddFieldPair(FlowDocument doc,
        string label1, string value1,
        string label2, string value2)
    {
        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 1, 0, 1) };
        table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

        var group = new TableRowGroup();
        var row = new TableRow();

        var c1 = new Paragraph { FontSize = FieldLabelSize };
        c1.Inlines.Add(new Run(label1) { FontWeight = FontWeights.SemiBold });
        c1.Inlines.Add(new Run(" " + value1));
        row.Cells.Add(new TableCell(c1));

        var c2 = new Paragraph { FontSize = FieldLabelSize };
        c2.Inlines.Add(new Run(label2) { FontWeight = FontWeights.SemiBold });
        c2.Inlines.Add(new Run(" " + value2));
        row.Cells.Add(new TableCell(c2));

        group.Rows.Add(row);
        table.RowGroups.Add(group);
        doc.Blocks.Add(table);
    }

    /// <summary>Adds a section heading for grouped content (e.g. line items).</summary>
    public static void AddSectionTitle(FlowDocument doc, string title)
    {
        doc.Blocks.Add(new Paragraph(new Run(title)
        {
            FontSize = BodySize + 1,
            FontWeight = FontWeights.Bold
        })
        { Margin = new Thickness(0, 8, 0, 3) });
    }

    /// <summary>Adds a data table with a styled header row and body rows.</summary>
    public static void AddTable(FlowDocument doc, string[] headers, IEnumerable<string[]> rows)
    {
        var table = new Table { CellSpacing = 0 };
        for (int i = 0; i < headers.Length; i++)
            table.Columns.Add(new TableColumn());

        // Header row
        var hGroup = new TableRowGroup();
        var hRow = new TableRow { Background = _lightGray };
        foreach (var h in headers)
        {
            hRow.Cells.Add(new TableCell(new Paragraph(new Run(h)
            {
                FontWeight = FontWeights.Bold,
                FontSize = SmallSize
            }))
            {
                Padding = new Thickness(4, 2, 4, 2),
                BorderBrush = _border,
                BorderThickness = new Thickness(0, 0, 0, 1)
            });
        }
        hGroup.Rows.Add(hRow);
        table.RowGroups.Add(hGroup);

        // Body rows
        var bGroup = new TableRowGroup();
        foreach (var rowData in rows)
        {
            var r = new TableRow();
            foreach (var cell in rowData)
            {
                r.Cells.Add(new TableCell(new Paragraph(new Run(cell) { FontSize = SmallSize }))
                {
                    Padding = new Thickness(4, 2, 4, 2),
                    BorderBrush = _lightGray,
                    BorderThickness = new Thickness(0, 0, 0, 0.5)
                });
            }
            bGroup.Rows.Add(r);
        }
        table.RowGroups.Add(bGroup);
        doc.Blocks.Add(table);
    }

    /// <summary>Adds a notes line if text is present.</summary>
    public static void AddNotes(FlowDocument doc, string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return;
        var para = new Paragraph { Margin = new Thickness(0, 4, 0, 0), FontSize = SmallSize };
        para.Inlines.Add(new Run("ملاحظات: ") { FontWeight = FontWeights.SemiBold });
        para.Inlines.Add(new Run(notes));
        doc.Blocks.Add(para);
    }

    /// <summary>Adds amount-in-words block in Arabic for formal invoice use.</summary>
    public static void AddAmountInWords(FlowDocument doc, decimal amount)
    {
        var words = ElshazlyStore.Desktop.Helpers.ArabicAmountInWords.Convert(amount);
        var para = new Paragraph { Margin = new Thickness(0, 6, 0, 2), FontSize = BodySize };
        para.Inlines.Add(new Run("المبلغ بالحروف: ") { FontWeight = FontWeights.SemiBold });
        para.Inlines.Add(new Run(words));
        doc.Blocks.Add(para);
    }

    /// <summary>
    /// Adds the standard dual-signature footer (أمين الخزينة + المدير).
    /// When <paramref name="treasuryPerson"/> is provided, the name is printed
    /// beneath the treasury signature line for traceability.
    /// </summary>
    public static void AddSignatureBlock(FlowDocument doc, bool compact = false, string? treasuryPerson = null)
    {
        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, compact ? 12 : 20, 0, 0) };
        table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

        var group = new TableRowGroup();

        // Labels
        var lblRow = new TableRow();
        lblRow.Cells.Add(new TableCell(new Paragraph(new Run("توقيع الخزينة")
        { FontWeight = FontWeights.SemiBold, FontSize = SmallSize })
        { TextAlignment = TextAlignment.Center }));
        lblRow.Cells.Add(new TableCell(new Paragraph(new Run("توقيع المدير")
        { FontWeight = FontWeights.SemiBold, FontSize = SmallSize })
        { TextAlignment = TextAlignment.Center }));
        group.Rows.Add(lblRow);

        // Signature lines
        var lineRow = new TableRow();
        lineRow.Cells.Add(new TableCell(new Paragraph(new Run("___________________")
        { Foreground = _gray, FontSize = SmallSize })
        { TextAlignment = TextAlignment.Center, Padding = new Thickness(0, compact ? 10 : 16, 0, 0) }));
        lineRow.Cells.Add(new TableCell(new Paragraph(new Run("___________________")
        { Foreground = _gray, FontSize = SmallSize })
        { TextAlignment = TextAlignment.Center, Padding = new Thickness(0, compact ? 10 : 16, 0, 0) }));
        group.Rows.Add(lineRow);

        // Treasury person name (if provided)
        if (!string.IsNullOrWhiteSpace(treasuryPerson))
        {
            var nameRow = new TableRow();
            nameRow.Cells.Add(new TableCell(new Paragraph(new Run(treasuryPerson)
            { FontSize = SmallSize, FontWeight = FontWeights.SemiBold })
            { TextAlignment = TextAlignment.Center, Padding = new Thickness(0, 4, 0, 0) }));
            nameRow.Cells.Add(new TableCell(new Paragraph())); // empty manager cell
            group.Rows.Add(nameRow);
        }

        table.RowGroups.Add(group);
        doc.Blocks.Add(table);
    }

    /// <summary>Adds a copy identifier label (e.g. "— أصل —" or "— صورة —").</summary>
    public static void AddCopyLabel(FlowDocument doc, string label)
    {
        doc.Blocks.Add(new Paragraph(new Run(label)
        {
            FontSize = SmallSize,
            FontWeight = FontWeights.Bold,
            Foreground = _gray
        })
        { Margin = new Thickness(0, 0, 0, 2) });
    }

    /// <summary>Adds a tear/cut guide line between dual copies.</summary>
    public static void AddTearGuide(FlowDocument doc)
    {
        doc.Blocks.Add(new Paragraph(new Run("✂ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─")
        {
            FontSize = SmallSize,
            Foreground = _gray
        })
        {
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 6, 0, 6),
            FlowDirection = FlowDirection.LeftToRight
        });
    }

    /// <summary>Prints a FlowDocument via the system print dialog.</summary>
    public static void PrintDocument(FlowDocument doc, string description)
    {
        var dlg = new PrintDialog();
        if (dlg.ShowDialog() == true)
        {
            ApplyPaperLayout(doc, dlg.PrintableAreaWidth, dlg.PrintableAreaHeight);
            var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
            dlg.PrintDocument(paginator, description);
        }
    }

    /// <summary>
    /// Builds and prints a dual-copy receipt (أصل + صورة) on a single sheet.
    /// The <paramref name="buildCopyContent"/> action is called twice to produce
    /// identical content for each copy, separated by a tear guide.
    /// </summary>
    public static void PrintDualCopy(Action<FlowDocument> buildCopyContent, string title)
    {
        var doc = CreateDocument();

        // ── أصل (Original) ──
        AddCopyLabel(doc, "— أصل —");
        buildCopyContent(doc);

        AddTearGuide(doc);

        // ── صورة (Copy) ──
        AddCopyLabel(doc, "— صورة —");
        buildCopyContent(doc);

        PrintDocument(doc, title);
    }

    // ── Private helpers ──

    private static Paragraph CreateRule()
    {
        return new Paragraph
        {
            Margin = new Thickness(0, 2, 0, 2),
            BorderBrush = _border,
            BorderThickness = new Thickness(0, 0, 0, 1.5),
            FontSize = 1
        };
    }

    private static void ApplyPaperLayout(FlowDocument doc, double printableAreaWidth, double printableAreaHeight)
    {
        doc.PagePadding = printableAreaWidth < 300
            ? new Thickness(12, 10, 12, 8)
            : printableAreaWidth < 520
                ? new Thickness(20, 14, 20, 12)
                : new Thickness(30, 20, 30, 16);

        doc.PageWidth = printableAreaWidth;
        doc.PageHeight = printableAreaHeight;
        doc.ColumnWidth = Math.Max(1, printableAreaWidth - doc.PagePadding.Left - doc.PagePadding.Right);
    }

    private static SolidColorBrush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }
}
