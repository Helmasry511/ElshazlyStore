using ElshazlyStore.Desktop.Models.Dtos;
using ElshazlyStore.Desktop.Services.Printing;

namespace ElshazlyStore.Desktop.Helpers;

/// <summary>
/// Document-specific print methods built on <see cref="ReceiptPrintService"/>.
/// Each method constructs a professional receipt/document from server DTO data.
/// </summary>
public static class DocumentPrintHelper
{
    /// <summary>
    /// Prints a professional dual-copy supplier payment receipt (أصل + صورة).
    /// Each copy includes company header, payment details, and signature lines.
    /// </summary>
    public static void PrintPaymentReceipt(PaymentDto payment)
    {
        var dateTimeDisplay = (payment.PaymentDateUtc ?? payment.CreatedAtUtc)
            .ToString("yyyy-MM-dd  HH:mm");

        ReceiptPrintService.PrintDualCopy(doc =>
        {
            ReceiptPrintService.AddCompanyHeader(doc, "إيصال دفع مورد", compact: true);

            ReceiptPrintService.AddFieldPair(doc,
                "رقم الإيصال:", payment.PaymentNumber ?? "—",
                "التاريخ:", dateTimeDisplay);

            ReceiptPrintService.AddField(doc, "المورد:", payment.PartyName ?? "—");

            ReceiptPrintService.AddFieldPair(doc,
                "المبلغ:", InvoiceNumberFormat.Format(payment.Amount),
                "طريقة الدفع:", payment.MethodDisplay);

            if (!string.IsNullOrWhiteSpace(payment.Reference))
                ReceiptPrintService.AddField(doc, "المرجع:", payment.Reference);

            if (!string.IsNullOrWhiteSpace(payment.CreatedByUsername))
                ReceiptPrintService.AddField(doc, "بواسطة:", payment.CreatedByUsername);

            ReceiptPrintService.AddNotes(doc, payment.Notes);
            ReceiptPrintService.AddSignatureBlock(doc, compact: true);

        }, $"إيصال دفع — {payment.PaymentNumber}");
    }

    /// <summary>
    /// Prints a professional dual-copy customer payment receipt (أصل + صورة).
    /// Each copy includes company header, customer name, payment details,
    /// amount in words, and a treasury-signature area with the cashier's name.
    /// </summary>
    public static void PrintCustomerPaymentReceipt(PaymentDto payment)
    {
        var dateTimeDisplay = (payment.PaymentDateUtc ?? payment.CreatedAtUtc)
            .ToString("yyyy-MM-dd  HH:mm");

        ReceiptPrintService.PrintDualCopy(doc =>
        {
            ReceiptPrintService.AddCompanyHeader(doc, "إيصال دفع عميل", compact: true);

            ReceiptPrintService.AddFieldPair(doc,
                "رقم الإيصال:", payment.PaymentNumber ?? "—",
                "التاريخ:", dateTimeDisplay);

            ReceiptPrintService.AddField(doc, "العميل:", payment.PartyName ?? "—");

            ReceiptPrintService.AddFieldPair(doc,
                "المبلغ:", InvoiceNumberFormat.Format(payment.Amount),
                "طريقة الدفع:", payment.MethodDisplay);

            // Amount in words — formal traceability
            ReceiptPrintService.AddAmountInWords(doc, payment.Amount);

            if (!string.IsNullOrWhiteSpace(payment.Reference))
                ReceiptPrintService.AddField(doc, "المرجع:", payment.Reference);

            ReceiptPrintService.AddNotes(doc, payment.Notes);

            // Treasury-signature area with cashier name printed for traceability
            ReceiptPrintService.AddSignatureBlock(doc, compact: true,
                treasuryPerson: payment.CreatedByUsername);

        }, $"إيصال دفع عميل — {payment.PaymentNumber}");
    }

    /// <summary>Prints a professional purchase order document with line items.</summary>
    public static void PrintPurchase(PurchaseDto purchase)
    {
        var doc = ReceiptPrintService.CreateDocument();

        ReceiptPrintService.AddCompanyHeader(doc, "إذن شراء");

        ReceiptPrintService.AddFieldPair(doc,
            "رقم المستند:", purchase.DocumentNumber ?? "—",
            "التاريخ:", purchase.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm"));

        ReceiptPrintService.AddFieldPair(doc,
            "المورد:", purchase.SupplierName,
            "المخزن:", purchase.WarehouseName);

        ReceiptPrintService.AddFieldPair(doc,
            "الحالة:", purchase.StatusDisplay,
            "الإجمالي:", InvoiceNumberFormat.Format(purchase.Total));

        ReceiptPrintService.AddNotes(doc, purchase.Notes);

        if (purchase.Lines is { Count: > 0 })
        {
            ReceiptPrintService.AddSectionTitle(doc, "بنود الشراء");
            ReceiptPrintService.AddTable(doc,
                ["SKU", "المنتج", "الكمية", "تكلفة الوحدة", "الإجمالي"],
                purchase.Lines.Select(l => new[]
                {
                    l.VariantSku, l.ProductName ?? "—",
                    InvoiceNumberFormat.Format(l.Quantity), InvoiceNumberFormat.Format(l.UnitCost), InvoiceNumberFormat.Format(l.LineTotal)
                }));
        }

        ReceiptPrintService.AddSignatureBlock(doc);
        ReceiptPrintService.PrintDocument(doc, $"إذن شراء — {purchase.DocumentNumber}");
    }

    /// <summary>Prints a professional purchase return document with line items.</summary>
    public static void PrintPurchaseReturn(PurchaseReturnDto ret)
    {
        var doc = ReceiptPrintService.CreateDocument();

        ReceiptPrintService.AddCompanyHeader(doc, "مرتجع شراء");

        ReceiptPrintService.AddFieldPair(doc,
            "رقم المرتجع:", ret.DocumentNumber ?? "—",
            "التاريخ:", ret.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm"));

        ReceiptPrintService.AddFieldPair(doc,
            "المورد:", ret.SupplierName,
            "المخزن:", ret.WarehouseName);

        ReceiptPrintService.AddFieldPair(doc,
            "الحالة:", ret.StatusDisplay,
            "الإجمالي:", InvoiceNumberFormat.Format(ret.Total));

        ReceiptPrintService.AddNotes(doc, ret.Notes);

        if (ret.Lines is { Count: > 0 })
        {
            ReceiptPrintService.AddSectionTitle(doc, "بنود المرتجع");
            ReceiptPrintService.AddTable(doc,
                ["SKU", "المنتج", "الكمية", "تكلفة الوحدة", "السبب"],
                ret.Lines.Select(l => new[]
                {
                    l.VariantSku, l.ProductName ?? "—",
                    InvoiceNumberFormat.Format(l.Quantity), InvoiceNumberFormat.Format(l.UnitCost), l.ReasonCodeName ?? "—"
                }));
        }

        ReceiptPrintService.AddSignatureBlock(doc);
        ReceiptPrintService.PrintDocument(doc, $"مرتجع شراء — {ret.DocumentNumber}");
    }

    /// <summary>Prints a professional sales return document with line items.</summary>
    public static void PrintSalesReturn(SalesReturnDto ret)
    {
        var doc = ReceiptPrintService.CreateDocument();

        ReceiptPrintService.AddCompanyHeader(doc, "مرتجع مبيعات");

        ReceiptPrintService.AddFieldPair(doc,
            "رقم المرتجع:", ret.DocumentNumber ?? "—",
            "التاريخ:", ret.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm"));

        ReceiptPrintService.AddFieldPair(doc,
            "العميل:", ret.CustomerNameDisplay,
            "المخزن:", ret.WarehouseName);

        ReceiptPrintService.AddFieldPair(doc,
            "الفاتورة الأصلية:", ret.OriginalInvoiceNumber ?? "—",
            "الحالة:", ret.StatusDisplay);

        ReceiptPrintService.AddField(doc,
            "الإجمالي:", InvoiceNumberFormat.Format(ret.Total));

        ReceiptPrintService.AddNotes(doc, ret.Notes);

        if (ret.Lines is { Count: > 0 })
        {
            ReceiptPrintService.AddSectionTitle(doc, "بنود المرتجع");
            ReceiptPrintService.AddTable(doc,
                ["SKU", "المنتج", "الكمية", "سعر الوحدة", "السبب", "الحالة"],
                ret.Lines.Select(l => new[]
                {
                    l.VariantSku,
                    l.ProductName ?? "—",
                    InvoiceNumberFormat.Format(l.Quantity),
                    InvoiceNumberFormat.Format(l.UnitPrice),
                    l.ReasonCodeName ?? "—",
                    l.DispositionDisplay
                }));
        }

        ReceiptPrintService.AddSignatureBlock(doc);
        ReceiptPrintService.PrintDocument(doc, $"مرتجع مبيعات — {ret.DocumentNumber}");
    }

    /// <summary>Prints a professional sales invoice with line items.</summary>
    public static void PrintSale(SaleDto sale, SalePaymentTraceDto? paymentTraceOverride = null)
    {
        var doc = ReceiptPrintService.CreateDocument();
        var paymentTrace = paymentTraceOverride ?? sale.PaymentTrace;

        ReceiptPrintService.AddCompanyHeader(doc, "فاتورة بيع");

        ReceiptPrintService.AddFieldPair(doc,
            "رقم الفاتورة:", sale.InvoiceNumber,
            "التاريخ:", sale.InvoiceDateUtc.ToString("yyyy-MM-dd HH:mm"));

        ReceiptPrintService.AddFieldPair(doc,
            "العميل:", ResolveCustomerDisplay(sale),
            "المخزن:", sale.WarehouseName);

        ReceiptPrintService.AddFieldPair(doc,
            "الحالة:", sale.StatusDisplay,
            "الكاشير:", sale.CashierUsername);

        ReceiptPrintService.AddFieldPair(doc,
            "وقت الترحيل:", sale.PostedAtUtc?.ToString("yyyy-MM-dd HH:mm") ?? "—",
            "إجمالي الفاتورة:", InvoiceNumberFormat.Format(sale.TotalAmount));

        ReceiptPrintService.AddSectionTitle(doc, "بيان السداد");
        ReceiptPrintService.AddFieldPair(doc,
            "حالة السداد:", ResolvePaymentStateLabel(sale, paymentTrace),
            "طريقة الدفع:", ResolvePaymentMethodDisplay(sale, paymentTrace));

        ReceiptPrintService.AddFieldPair(doc,
            "مرجع الدفع:", ResolvePaymentReferenceDisplay(sale, paymentTrace),
            "المدفوع:", ResolveAmountDisplay(sale, paymentTrace, paymentTrace?.PaidAmount));

        ReceiptPrintService.AddField(doc,
            "المتبقي:",
            ResolveAmountDisplay(sale, paymentTrace, paymentTrace?.RemainingAmount));

        if (!string.IsNullOrWhiteSpace(paymentTrace?.Note))
            ReceiptPrintService.AddField(doc, "ملاحظة السداد:", paymentTrace.Note!);

        ReceiptPrintService.AddNotes(doc, sale.Notes);

        // Amount in words
        ReceiptPrintService.AddAmountInWords(doc, sale.TotalAmount);

        if (sale.Lines is { Count: > 0 })
        {
            ReceiptPrintService.AddSectionTitle(doc, "بنود الفاتورة");
            ReceiptPrintService.AddTable(doc,
                ["SKU", "المنتج", "الكمية", "سعر الوحدة", "الخصم", "الإجمالي"],
                sale.Lines.Select(line => new[]
                {
                    line.Sku,
                    line.ProductName ?? "—",
                    InvoiceNumberFormat.Format(line.Quantity),
                    InvoiceNumberFormat.Format(line.UnitPrice),
                    InvoiceNumberFormat.Format(line.DiscountAmount),
                    InvoiceNumberFormat.Format(line.LineTotal)
                }));
        }

        ReceiptPrintService.AddSignatureBlock(doc);
        ReceiptPrintService.PrintDocument(doc, $"فاتورة بيع — {sale.InvoiceNumber}");
    }

    private static string ResolveCustomerDisplay(SaleDto sale)
    {
        if (sale.CustomerId is not null)
            return sale.CustomerNameDisplay;

        return "عميل نقدي";
    }

    private static string ResolvePaymentStateLabel(SaleDto sale, SalePaymentTraceDto? paymentTrace)
    {
        if (paymentTrace?.IsOperationalOnly == true)
        {
            // Anonymous sale — present payment state professionally
            if (paymentTrace.PaidAmount.HasValue && paymentTrace.RemainingAmount is null or 0m)
                return "مسدد";
            if (paymentTrace.PaidAmount.HasValue && paymentTrace.RemainingAmount > 0m)
                return "مسدد جزئيًا";
            return "نقدي";
        }

        if (sale.CustomerId is null)
            return "نقدي";

        if (paymentTrace is null)
            return sale.StatusDisplay;

        var paidAmount = paymentTrace.PaidAmount ?? 0m;
        var remainingAmount = paymentTrace.RemainingAmount ?? 0m;

        if (remainingAmount > 0m && paidAmount > 0m)
            return "دفع جزئي / متبقي على العميل";

        if (remainingAmount > 0m)
            return "مستحق على العميل";

        if (paidAmount > 0m)
            return "مسدد بالكامل";

        return "بدون دفعة مرتبطة";
    }

    private static string ResolvePaymentMethodDisplay(SaleDto sale, SalePaymentTraceDto? paymentTrace)
    {
        if (paymentTrace?.PaymentCount > 1 && string.IsNullOrWhiteSpace(paymentTrace.PaymentMethod))
            return "دفعات متعددة";

        var method = paymentTrace?.PaymentMethod;
        if (string.IsNullOrWhiteSpace(method))
            return sale.CustomerId is null ? "نقدي" : "—";

        return method switch
        {
            "Cash" => "نقدي",
            "Visa" => "فيزا",
            "InstaPay" => "إنستاباي",
            "EWallet" => string.IsNullOrWhiteSpace(paymentTrace?.WalletName)
                ? "محفظة إلكترونية"
                : $"محفظة إلكترونية ({paymentTrace.WalletName})",
            _ => method,
        };
    }

    private static string ResolvePaymentReferenceDisplay(SaleDto sale, SalePaymentTraceDto? paymentTrace)
    {
        if (!string.IsNullOrWhiteSpace(paymentTrace?.PaymentReference))
            return paymentTrace.PaymentReference!;

        if (paymentTrace?.PaymentCount > 1)
            return "دفعات متعددة";

        return "—";
    }

    private static string ResolveAmountDisplay(SaleDto sale, SalePaymentTraceDto? paymentTrace, decimal? amount)
    {
        if (amount.HasValue)
            return InvoiceNumberFormat.Format(amount.Value);

        return "—";
    }
}
