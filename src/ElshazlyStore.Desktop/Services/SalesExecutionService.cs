using ElshazlyStore.Desktop.Models;
using ElshazlyStore.Desktop.Models.Dtos;
using ElshazlyStore.Desktop.Services.Api;

namespace ElshazlyStore.Desktop.Services;

/// <summary>
/// Shared sales execution pipeline used by Sales and POS flows.
/// Keeps create/post/payment/fetch semantics in one contract-grounded place.
/// </summary>
public interface ISalesExecutionService
{
    Task<ApiResult<object>> PostDraftSaleAsync(Guid saleId, CancellationToken ct = default);
    Task<ApiResult<SaleDto>> FetchSaleAsync(Guid saleId, CancellationToken ct = default);
    Task<SalesCheckoutExecutionResult> ExecuteImmediateCheckoutAsync(
        SalesCheckoutExecutionRequest request,
        CancellationToken ct = default);
}

public sealed class SalesExecutionService : ISalesExecutionService
{
    private readonly ApiClient _apiClient;

    public SalesExecutionService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<ApiResult<object>> PostDraftSaleAsync(Guid saleId, CancellationToken ct = default)
        => _apiClient.PostAsync<object>($"/api/v1/sales/{saleId}/post", ct: ct);

    public Task<ApiResult<SaleDto>> FetchSaleAsync(Guid saleId, CancellationToken ct = default)
        => _apiClient.GetAsync<SaleDto>($"/api/v1/sales/{saleId}", ct);

    public async Task<SalesCheckoutExecutionResult> ExecuteImmediateCheckoutAsync(
        SalesCheckoutExecutionRequest request,
        CancellationToken ct = default)
    {
        var result = new SalesCheckoutExecutionResult
        {
            IsAnonymousSale = !request.CustomerId.HasValue
        };

        var createRequest = new CreateSaleRequest
        {
            WarehouseId = request.WarehouseId,
            CustomerId = request.CustomerId,
            InvoiceDateUtc = NormalizeInvoiceDateForRequest(request.InvoiceDateUtc),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            Lines = request.Lines.ToList()
        };

        var createResponse = await _apiClient.PostAsync<SaleDto>("/api/v1/sales", createRequest, ct);
        if (!createResponse.IsSuccess || createResponse.Data is null)
        {
            result.SaleCreateErrorMessage = createResponse.ErrorMessage ?? Localization.Strings.State_UnexpectedError;
            return result;
        }

        result.IsSaleCreated = true;
        result.SaleId = createResponse.Data.Id;
        result.SaleDraft = createResponse.Data;
        result.InvoiceTotalAmount = createResponse.Data.TotalAmount;

        var postResponse = await PostDraftSaleAsync(createResponse.Data.Id, ct);
        if (!postResponse.IsSuccess)
        {
            result.SalePostErrorMessage = postResponse.ErrorMessage ?? Localization.Strings.State_UnexpectedError;
            return result;
        }

        result.IsSalePosted = true;

        if (request.CustomerId.HasValue && request.CreatePaymentForNamedCustomer)
        {
            result.IsPaymentAttempted = true;
            var paymentAmount = ResolvePaymentAmount(request.PaymentAmount, createResponse.Data.TotalAmount);

            var paymentRequest = new CreatePaymentRequest
            {
                PartyType = "Customer",
                PartyId = request.CustomerId.Value,
                Amount = paymentAmount,
                Method = string.IsNullOrWhiteSpace(request.PaymentMethod)
                    ? "Cash"
                    : request.PaymentMethod.Trim(),
                WalletName = string.IsNullOrWhiteSpace(request.WalletName)
                    ? null
                    : request.WalletName.Trim(),
                Reference = string.IsNullOrWhiteSpace(request.Reference)
                    ? null
                    : request.Reference.Trim(),
                RelatedInvoiceId = createResponse.Data.Id,
                PaymentDateUtc = request.PaymentDateUtc ?? DateTime.UtcNow
            };

            var paymentResponse = await _apiClient.PostAsync<PaymentDto>("/api/v1/payments", paymentRequest, ct);
            result.IsPaymentPersisted = paymentResponse.IsSuccess;
            result.RecordedPaymentAmount = paymentAmount;
            if (!paymentResponse.IsSuccess)
            {
                result.PaymentErrorMessage = paymentResponse.ErrorMessage ?? Localization.Strings.State_UnexpectedError;
            }
        }

        var fetchResponse = await FetchSaleAsync(createResponse.Data.Id, ct);
        if (fetchResponse.IsSuccess && fetchResponse.Data is not null)
        {
            result.SaleFresh = fetchResponse.Data;
        }
        else
        {
            result.SaleFetchErrorMessage = fetchResponse.ErrorMessage ?? Localization.Strings.State_UnexpectedError;
        }

        return result;
    }

    private static decimal ResolvePaymentAmount(decimal? requestedAmount, decimal invoiceTotal)
    {
        if (!requestedAmount.HasValue || requestedAmount.Value <= 0m)
            return invoiceTotal;

        return Math.Min(requestedAmount.Value, invoiceTotal);
    }

    private static DateTime? NormalizeInvoiceDateForRequest(DateTime? invoiceDate)
    {
        if (!invoiceDate.HasValue)
            return null;

        var value = invoiceDate.Value;
        if (value.Kind == DateTimeKind.Utc)
            return value;

        if (value.TimeOfDay == TimeSpan.Zero)
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);

        return value.Kind == DateTimeKind.Local
            ? value.ToUniversalTime()
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}

public sealed class SalesCheckoutExecutionRequest
{
    public Guid WarehouseId { get; init; }
    public Guid? CustomerId { get; init; }
    public DateTime? InvoiceDateUtc { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<SaleLineRequest> Lines { get; init; } = [];

    public bool CreatePaymentForNamedCustomer { get; init; }
    public string? PaymentMethod { get; init; }
    public decimal? PaymentAmount { get; init; }
    public string? WalletName { get; init; }
    public string? Reference { get; init; }
    public DateTime? PaymentDateUtc { get; init; }
}

public sealed class SalesCheckoutExecutionResult
{
    public bool IsSaleCreated { get; set; }
    public bool IsSalePosted { get; set; }

    public bool IsAnonymousSale { get; set; }
    public bool IsPaymentAttempted { get; set; }
    public bool IsPaymentPersisted { get; set; }

    public Guid? SaleId { get; set; }
    public SaleDto? SaleDraft { get; set; }
    public SaleDto? SaleFresh { get; set; }
    public decimal InvoiceTotalAmount { get; set; }
    public decimal RecordedPaymentAmount { get; set; }

    public string? SaleCreateErrorMessage { get; set; }
    public string? SalePostErrorMessage { get; set; }
    public string? PaymentErrorMessage { get; set; }
    public string? SaleFetchErrorMessage { get; set; }
}
