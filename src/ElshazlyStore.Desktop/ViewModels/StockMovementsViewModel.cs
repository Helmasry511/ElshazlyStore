using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ElshazlyStore.Desktop.Models;
using ElshazlyStore.Desktop.Models.Dtos;
using ElshazlyStore.Desktop.Services;
using ElshazlyStore.Desktop.Services.Api;

namespace ElshazlyStore.Desktop.ViewModels;

public sealed partial class StockMovementsViewModel : ViewModelBase
{
    private readonly ApiClient _apiClient;
    private readonly IMessageService _messageService;
    private readonly IStockChangeNotifier _stockNotifier;
    private CancellationTokenSource? _variantSearchCts;

    public StockMovementsViewModel(ApiClient apiClient, IMessageService messageService, IStockChangeNotifier stockNotifier)
    {
        _apiClient = apiClient;
        _messageService = messageService;
        _stockNotifier = stockNotifier;
        Title = Localization.Strings.Stock_MovementsTitle;
    }

    // ── Movement Types ──
    public List<MovementTypeItem> MovementTypes { get; } =
    [
        new(0, Localization.Strings.Stock_MovementType_OpeningBalance),
        new(4, Localization.Strings.Stock_MovementType_Adjustment),
        new(3, Localization.Strings.Stock_MovementType_Transfer),
    ];

    [ObservableProperty]
    private MovementTypeItem? _selectedMovementType;

    [ObservableProperty]
    private string _reference = string.Empty;

    // ── Computed ──
    /// <summary>True when the selected movement type is Transfer (3). Drives UI visibility of From/To pickers.</summary>
    public bool IsTransferMode => SelectedMovementType?.Value == 3;

    partial void OnSelectedMovementTypeChanged(MovementTypeItem? value)
    {
        OnPropertyChanged(nameof(IsTransferMode));
    }

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private string _formError = string.Empty;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private bool _isInitialized;

    // ── Lines ──
    public ObservableCollection<MovementLineVm> Lines { get; } = [];

    // ── Warehouses ──
    public ObservableCollection<WarehouseDto> Warehouses { get; } = [];

    [ObservableProperty]
    private bool _isRefreshingWarehouses;

    // ── Variant search (shared for line editing) ──
    [ObservableProperty]
    private string _variantSearchText = string.Empty;

    [ObservableProperty]
    private bool _hasVariantSearchResults;

    public ObservableCollection<VariantListDto> VariantSearchResults { get; } = [];

    /// <summary>Debounced typeahead: triggers search 250ms after the user stops typing (min 2 chars).</summary>
    partial void OnVariantSearchTextChanged(string value)
    {
        _variantSearchCts?.Cancel();

        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length < 2)
        {
            VariantSearchResults.Clear();
            HasVariantSearchResults = false;
            return;
        }

        _variantSearchCts = new CancellationTokenSource();
        var token = _variantSearchCts.Token;

        _ = DebounceSearchVariantsAsync(value.Trim(), token);
    }

    private async Task DebounceSearchVariantsAsync(string query, CancellationToken ct)
    {
        try
        {
            await Task.Delay(250, ct);
            if (ct.IsCancellationRequested) return;
            await SearchVariantsCoreAsync(query, ct);
        }
        catch (TaskCanceledException) { /* expected on new keystroke */ }
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (IsInitialized) return;
        await LoadWarehousesAsync();
        IsInitialized = true;
    }

    [RelayCommand]
    private async Task RefreshWarehousesAsync()
    {
        await LoadWarehousesAsync();
    }

    private async Task LoadWarehousesAsync()
    {
        IsRefreshingWarehouses = true;
        try
        {
            var result = await _apiClient.GetAsync<PagedResponse<WarehouseDto>>(
                "/api/v1/warehouses?page=1&pageSize=500");

            // Remember currently selected warehouse IDs per line (to restore after refresh)
            var lineSelections = Lines
                .Select(l => (Line: l,
                    WarehouseId: l.SelectedWarehouse?.Id,
                    FromId: l.SelectedFromWarehouse?.Id,
                    ToId: l.SelectedToWarehouse?.Id))
                .ToList();

            Warehouses.Clear();
            if (result.IsSuccess && result.Data is not null)
            {
                foreach (var w in result.Data.Items.Where(w => w.IsActive))
                    Warehouses.Add(w);
            }

            // Restore line selections if the warehouse is still active
            foreach (var (line, warehouseId, fromId, toId) in lineSelections)
            {
                if (warehouseId.HasValue)
                    line.SelectedWarehouse = Warehouses.FirstOrDefault(w => w.Id == warehouseId.Value);
                if (fromId.HasValue)
                    line.SelectedFromWarehouse = Warehouses.FirstOrDefault(w => w.Id == fromId.Value);
                if (toId.HasValue)
                    line.SelectedToWarehouse = Warehouses.FirstOrDefault(w => w.Id == toId.Value);
            }
        }
        finally
        {
            IsRefreshingWarehouses = false;
        }
    }

    [RelayCommand]
    private async Task SearchVariantsAsync()
    {
        if (string.IsNullOrWhiteSpace(VariantSearchText) || VariantSearchText.Trim().Length < 2)
        {
            VariantSearchResults.Clear();
            HasVariantSearchResults = false;
            return;
        }

        _variantSearchCts?.Cancel();
        _variantSearchCts = new CancellationTokenSource();
        await SearchVariantsCoreAsync(VariantSearchText.Trim(), _variantSearchCts.Token);
    }

    private async Task SearchVariantsCoreAsync(string query, CancellationToken ct)
    {
        var url = $"/api/v1/variants?page=1&pageSize=8&q={Uri.EscapeDataString(query)}";
        var result = await _apiClient.GetAsync<PagedResponse<VariantListDto>>(url);

        if (ct.IsCancellationRequested) return;

        VariantSearchResults.Clear();
        if (result.IsSuccess && result.Data is not null)
        {
            foreach (var v in result.Data.Items)
                VariantSearchResults.Add(v);
        }
        HasVariantSearchResults = VariantSearchResults.Count > 0;
    }

    [RelayCommand]
    private void AddLine()
    {
        Lines.Add(new MovementLineVm());
    }

    [RelayCommand]
    private void RemoveLine(MovementLineVm? line)
    {
        if (line is not null)
            Lines.Remove(line);
    }

    [RelayCommand]
    private void SelectVariantForLine(VariantListDto? variant)
    {
        if (variant is null || _editingLine is null) return;
        _editingLine.VariantId = variant.Id;

        // Build display: ProductName (Color/Size) — SKU
        var meta = BuildColorSizeMeta(variant.Color, variant.Size);
        _editingLine.VariantDisplay = string.IsNullOrEmpty(meta)
            ? $"{variant.ProductName} — {variant.Sku}"
            : $"{variant.ProductName} ({meta}) — {variant.Sku}";

        // Show default warehouse info (display-only)
        _editingLine.DefaultWarehouseDisplay = !string.IsNullOrWhiteSpace(variant.DefaultWarehouseName)
            ? string.Format(Localization.Strings.Stock_VariantDefaultWarehouse, variant.DefaultWarehouseName)
            : string.Empty;

        // Pre-select default warehouse if user hasn't chosen one yet
        if (_editingLine.SelectedWarehouse is null && variant.DefaultWarehouseId.HasValue)
        {
            var match = Warehouses.FirstOrDefault(w => w.Id == variant.DefaultWarehouseId.Value);
            if (match is not null)
                _editingLine.SelectedWarehouse = match;
        }

        // For Transfer mode: pre-select default warehouse as From (source)
        if (IsTransferMode && _editingLine.SelectedFromWarehouse is null && variant.DefaultWarehouseId.HasValue)
        {
            var match = Warehouses.FirstOrDefault(w => w.Id == variant.DefaultWarehouseId.Value);
            if (match is not null)
                _editingLine.SelectedFromWarehouse = match;
        }

        VariantSearchResults.Clear();
        HasVariantSearchResults = false;
        VariantSearchText = string.Empty;
        _editingLine = null;
    }

    private MovementLineVm? _editingLine;

    [RelayCommand]
    private void StartLineVariantSearch(MovementLineVm? line)
    {
        _editingLine = line;
    }

    private static string BuildColorSizeMeta(string? color, string? size)
    {
        var parts = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(color)) parts.Add(color!);
        if (!string.IsNullOrWhiteSpace(size)) parts.Add(size!);
        return string.Join(" / ", parts);
    }

    [RelayCommand]
    private async Task PostAsync()
    {
        FormError = string.Empty;

        if (SelectedMovementType is null)
        {
            FormError = Localization.Strings.Field_MovementType;
            return;
        }

        if (Lines.Count == 0)
        {
            FormError = Localization.Strings.Validation_LinesRequired;
            return;
        }

        var isTransfer = SelectedMovementType.Value == 3;

        foreach (var line in Lines)
        {
            if (line.VariantId == Guid.Empty)
            {
                FormError = Localization.Strings.Validation_VariantRequired;
                return;
            }
            if (line.QuantityDelta == 0)
            {
                FormError = Localization.Strings.Validation_QuantityRequired;
                return;
            }

            if (isTransfer)
            {
                // Transfer: require From + To, must differ
                if (line.SelectedFromWarehouse is null || line.SelectedFromWarehouse.Id == Guid.Empty
                    || line.SelectedToWarehouse is null || line.SelectedToWarehouse.Id == Guid.Empty)
                {
                    FormError = Localization.Strings.Validation_TransferFromToRequired;
                    return;
                }
                if (line.SelectedFromWarehouse.Id == line.SelectedToWarehouse.Id)
                {
                    FormError = Localization.Strings.Validation_TransferSameWarehouse;
                    return;
                }
            }
            else
            {
                // Non-transfer: require single warehouse
                if (line.SelectedWarehouse is null || line.SelectedWarehouse.Id == Guid.Empty)
                {
                    FormError = Localization.Strings.Validation_WarehouseRequired;
                    return;
                }
            }
        }

        IsSaving = true;
        try
        {
            List<PostStockMovementLineRequest> requestLines;

            if (isTransfer)
            {
                // Transfer: generate 2 backend lines per UI line (negative from source, positive to dest)
                requestLines = [];
                foreach (var l in Lines)
                {
                    var absQty = Math.Abs(l.QuantityDelta);
                    // Outbound from source warehouse
                    requestLines.Add(new PostStockMovementLineRequest
                    {
                        VariantId = l.VariantId,
                        WarehouseId = l.SelectedFromWarehouse!.Id,
                        QuantityDelta = -absQty,
                        UnitCost = l.UnitCost,
                        Reason = string.IsNullOrWhiteSpace(l.Reason) ? null : l.Reason.Trim()
                    });
                    // Inbound to destination warehouse
                    requestLines.Add(new PostStockMovementLineRequest
                    {
                        VariantId = l.VariantId,
                        WarehouseId = l.SelectedToWarehouse!.Id,
                        QuantityDelta = absQty,
                        UnitCost = l.UnitCost,
                        Reason = string.IsNullOrWhiteSpace(l.Reason) ? null : l.Reason.Trim()
                    });
                }
            }
            else
            {
                requestLines = Lines.Select(l => new PostStockMovementLineRequest
                {
                    VariantId = l.VariantId,
                    WarehouseId = l.SelectedWarehouse!.Id,
                    QuantityDelta = l.QuantityDelta,
                    UnitCost = l.UnitCost,
                    Reason = string.IsNullOrWhiteSpace(l.Reason) ? null : l.Reason.Trim()
                }).ToList();
            }

            var request = new PostStockMovementRequest
            {
                Type = SelectedMovementType.Value,
                Reference = string.IsNullOrWhiteSpace(Reference) ? null : Reference.Trim(),
                Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
                Lines = requestLines
            };

            // Debug: log payload for transfer closeout verification
            if (isTransfer)
            {
                Debug.WriteLine("[Transfer Payload] " + System.Text.Json.JsonSerializer.Serialize(request,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            }

            var result = await _apiClient.PostAsync<PostStockMovementResponse>(
                "/api/v1/stock-movements/post", request);

            if (!result.IsSuccess)
            {
                // Enhance warehouse-related errors with code/name context
                var errorMsg = result.ErrorMessage ?? Localization.Strings.State_UnexpectedError;
                FormError = EnhanceWarehouseError(errorMsg);
                return;
            }

            _messageService.ShowInfo(isTransfer
                ? BuildTransferSuccessMessage()
                : Localization.Strings.Stock_PostSuccess);

            _stockNotifier.NotifyStockChanged();

            // Reset form
            SelectedMovementType = null;
            Reference = string.Empty;
            Notes = string.Empty;
            Lines.Clear();
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// <summary>
    /// If the error is a generic warehouse-not-found message from the mapper,
    /// enhance it with the actual warehouse code/name from the lines if possible.
    /// </summary>
    private string EnhanceWarehouseError(string errorMsg)
    {
        var warehouseNotFoundMsg = Models.ErrorCodeMapper.ToArabicMessage("WAREHOUSE_NOT_FOUND");
        var warehouseInactiveMsg = Models.ErrorCodeMapper.ToArabicMessage("WAREHOUSE_INACTIVE");

        if (errorMsg == warehouseNotFoundMsg || errorMsg == warehouseInactiveMsg)
        {
            var warehouseInfos = Lines
                .SelectMany(l => new[] { l.SelectedWarehouse, l.SelectedFromWarehouse, l.SelectedToWarehouse })
                .Where(w => w is not null)
                .DistinctBy(w => w!.Id)
                .ToList();

            if (warehouseInfos.Count > 0)
            {
                var whInfo = warehouseInfos.First()!;
                return string.Format(Localization.Strings.Stock_WarehouseInactiveOrMissing,
                    whInfo.Code, whInfo.Name);
            }
        }

        return errorMsg;
    }

    private string BuildTransferSuccessMessage()
    {
        var firstLine = Lines.FirstOrDefault();
        if (firstLine?.SelectedFromWarehouse is not null && firstLine.SelectedToWarehouse is not null)
        {
            return string.Format(
                Localization.Strings.Stock_TransferPostSuccess,
                firstLine.SelectedFromWarehouse.Name,
                firstLine.SelectedToWarehouse.Name);
        }
        return Localization.Strings.Stock_PostSuccess;
    }
}

public sealed record MovementTypeItem(int Value, string Display)
{
    public override string ToString() => Display;
}

public sealed partial class MovementLineVm : ObservableObject
{
    [ObservableProperty]
    private Guid _variantId;

    [ObservableProperty]
    private string _variantDisplay = string.Empty;

    [ObservableProperty]
    private string _defaultWarehouseDisplay = string.Empty;

    // ── Single warehouse (non-transfer) ──
    [ObservableProperty]
    private WarehouseDto? _selectedWarehouse;

    // ── Transfer From/To warehouses ──
    [ObservableProperty]
    private WarehouseDto? _selectedFromWarehouse;

    [ObservableProperty]
    private WarehouseDto? _selectedToWarehouse;

    partial void OnSelectedFromWarehouseChanged(WarehouseDto? value) => UpdateTransferRouteDisplay();
    partial void OnSelectedToWarehouseChanged(WarehouseDto? value) => UpdateTransferRouteDisplay();

    private void UpdateTransferRouteDisplay()
    {
        if (SelectedFromWarehouse is not null && SelectedFromWarehouse.Id != Guid.Empty
            && SelectedToWarehouse is not null && SelectedToWarehouse.Id != Guid.Empty)
        {
            TransferRouteDisplay = string.Format(
                Localization.Strings.Stock_TransferRoute,
                SelectedFromWarehouse.Name,
                SelectedToWarehouse.Name);
        }
        else
        {
            TransferRouteDisplay = string.Empty;
        }
    }

    [ObservableProperty]
    private string _transferRouteDisplay = string.Empty;

    [ObservableProperty]
    private decimal _quantityDelta;

    [ObservableProperty]
    private decimal? _unitCost;

    [ObservableProperty]
    private string _reason = string.Empty;
}
