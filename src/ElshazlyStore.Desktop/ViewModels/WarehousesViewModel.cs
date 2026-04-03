using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ElshazlyStore.Desktop.Models;
using ElshazlyStore.Desktop.Models.Dtos;
using ElshazlyStore.Desktop.Services;
using ElshazlyStore.Desktop.Services.Api;

namespace ElshazlyStore.Desktop.ViewModels;

public sealed partial class WarehousesViewModel : PagedListViewModelBase<WarehouseDto>
{
    private readonly IPermissionService _permissionService;
    private readonly IMessageService _messageService;

    public WarehousesViewModel(ApiClient apiClient, IPermissionService permissionService, IMessageService messageService)
        : base(apiClient)
    {
        _permissionService = permissionService;
        _messageService = messageService;
        Title = Localization.Strings.Nav_Warehouses;
        CanWrite = _permissionService.HasPermission(PermissionCodes.WarehousesWrite);
    }

    [ObservableProperty]
    private bool _canWrite;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _formName = string.Empty;

    [ObservableProperty]
    private string _formCode = string.Empty;

    [ObservableProperty]
    private string _formAddress = string.Empty;

    [ObservableProperty]
    private bool _formIsDefault;

    [ObservableProperty]
    private bool _formIsActive = true;

    [ObservableProperty]
    private string _formError = string.Empty;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private bool _isEditMode;

    private Guid? _editingId;

    protected override Task<ApiResult<PagedResponse<WarehouseDto>>> FetchPageAsync(
        int page, int pageSize, string? search, string? sort)
    {
        // Warehouses use Shape B (same JSON), no custom sort param
        var url = BuildQueryString("/api/v1/warehouses", page, pageSize, search, sort);
        return ApiClient.GetAsync<PagedResponse<WarehouseDto>>(url);
    }

    [RelayCommand]
    private void OpenCreate()
    {
        _editingId = null;
        IsEditMode = false;
        FormName = string.Empty;
        FormCode = string.Empty;
        FormAddress = string.Empty;
        FormIsDefault = false;
        FormIsActive = true;
        FormError = string.Empty;
        IsEditing = true;
    }

    [RelayCommand]
    private void OpenEdit(WarehouseDto? warehouse)
    {
        if (warehouse is null) return;
        _editingId = warehouse.Id;
        IsEditMode = true;
        FormName = warehouse.Name;
        FormCode = warehouse.Code;
        FormAddress = warehouse.Address ?? string.Empty;
        FormIsDefault = warehouse.IsDefault;
        FormIsActive = warehouse.IsActive;
        FormError = string.Empty;
        IsEditing = true;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(FormName))
        {
            FormError = Localization.Strings.Validation_NameRequired;
            return;
        }

        IsSaving = true;
        FormError = string.Empty;

        try
        {
            if (_editingId is null)
            {
                if (string.IsNullOrWhiteSpace(FormCode))
                {
                    FormError = Localization.Strings.Validation_CodeRequired;
                    return;
                }
                var body = new CreateWarehouseRequest
                {
                    Code = FormCode.Trim(),
                    Name = FormName.Trim(),
                    Address = string.IsNullOrWhiteSpace(FormAddress) ? null : FormAddress.Trim(),
                    IsDefault = FormIsDefault
                };
                var result = await ApiClient.PostAsync<WarehouseDto>("/api/v1/warehouses", body);
                if (!result.IsSuccess)
                {
                    FormError = result.ErrorMessage ?? Localization.Strings.State_UnexpectedError;
                    return;
                }
            }
            else
            {
                var body = new UpdateWarehouseRequest
                {
                    Name = FormName.Trim(),
                    Address = string.IsNullOrWhiteSpace(FormAddress) ? null : FormAddress.Trim(),
                    IsActive = FormIsActive
                };
                var result = await ApiClient.PutAsync<WarehouseDto>($"/api/v1/warehouses/{_editingId}", body);
                if (!result.IsSuccess)
                {
                    FormError = result.ErrorMessage ?? Localization.Strings.State_UnexpectedError;
                    return;
                }
            }

            IsEditing = false;
            await LoadPageAsync();
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
    }

    [RelayCommand]
    private async Task DeactivateAsync(WarehouseDto? warehouse)
    {
        if (warehouse is null) return;
        if (!_messageService.ShowConfirm(Localization.Strings.Dialog_ConfirmDeactivate))
            return;

        var result = await ApiClient.DeleteAsync<object>($"/api/v1/warehouses/{warehouse.Id}");
        if (!result.IsSuccess)
        {
            _messageService.ShowError(result.ErrorMessage ?? Localization.Strings.State_UnexpectedError);
            return;
        }

        await LoadPageAsync();
    }
}
