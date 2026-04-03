using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ElshazlyStore.Desktop.Models;
using ElshazlyStore.Desktop.Models.Dtos;
using ElshazlyStore.Desktop.Services;
using ElshazlyStore.Desktop.Services.Api;

namespace ElshazlyStore.Desktop.ViewModels;

public sealed partial class SuppliersViewModel : PagedListViewModelBase<SupplierDto>
{
    private readonly IPermissionService _permissionService;
    private readonly IMessageService _messageService;
    private readonly INavigationService _navigationService;

    public SuppliersViewModel(ApiClient apiClient, IPermissionService permissionService, IMessageService messageService, INavigationService navigationService)
        : base(apiClient)
    {
        _permissionService = permissionService;
        _messageService = messageService;
        _navigationService = navigationService;
        Title = Localization.Strings.Nav_Suppliers;
        CanWrite = _permissionService.HasPermission(PermissionCodes.SuppliersWrite);
        CanViewPurchases = _permissionService.HasPermission(PermissionCodes.PurchasesRead);
    }

    [ObservableProperty]
    private bool _canWrite;

    [ObservableProperty]
    private bool _canViewPurchases;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _formName = string.Empty;

    [ObservableProperty]
    private string _formCode = string.Empty;

    [ObservableProperty]
    private string _formPhone = string.Empty;

    [ObservableProperty]
    private string _formPhone2 = string.Empty;

    [ObservableProperty]
    private string _formNotes = string.Empty;

    [ObservableProperty]
    private string _formError = string.Empty;

    [ObservableProperty]
    private bool _isSaving;

    private Guid? _editingId;

    protected override Task<ApiResult<PagedResponse<SupplierDto>>> FetchPageAsync(
        int page, int pageSize, string? search, string? sort)
    {
        var url = BuildQueryString("/api/v1/suppliers", page, pageSize, search, sort);
        return ApiClient.GetAsync<PagedResponse<SupplierDto>>(url);
    }

    [RelayCommand]
    private void OpenCreate()
    {
        _editingId = null;
        FormName = string.Empty;
        FormCode = string.Empty;
        FormPhone = string.Empty;
        FormPhone2 = string.Empty;
        FormNotes = string.Empty;
        FormError = string.Empty;
        IsEditing = true;
    }

    [RelayCommand]
    private void OpenEdit(SupplierDto? supplier)
    {
        if (supplier is null) return;
        _editingId = supplier.Id;
        FormName = supplier.Name;
        FormCode = supplier.Code;
        FormPhone = supplier.Phone ?? string.Empty;
        FormPhone2 = supplier.Phone2 ?? string.Empty;
        FormNotes = supplier.Notes ?? string.Empty;
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
                var body = new CreateSupplierRequest
                {
                    Name = FormName.Trim(),
                    Code = string.IsNullOrWhiteSpace(FormCode) ? null : FormCode.Trim(),
                    Phone = string.IsNullOrWhiteSpace(FormPhone) ? null : FormPhone.Trim(),
                    Phone2 = string.IsNullOrWhiteSpace(FormPhone2) ? null : FormPhone2.Trim(),
                    Notes = string.IsNullOrWhiteSpace(FormNotes) ? null : FormNotes.Trim()
                };
                var result = await ApiClient.PostAsync<SupplierDto>("/api/v1/suppliers", body);
                if (!result.IsSuccess)
                {
                    FormError = result.ErrorMessage ?? Localization.Strings.State_UnexpectedError;
                    return;
                }
            }
            else
            {
                var body = new UpdateSupplierRequest
                {
                    Name = FormName.Trim(),
                    Code = string.IsNullOrWhiteSpace(FormCode) ? null : FormCode.Trim(),
                    Phone = string.IsNullOrWhiteSpace(FormPhone) ? null : FormPhone.Trim(),
                    Phone2 = string.IsNullOrWhiteSpace(FormPhone2) ? null : FormPhone2.Trim(),
                    Notes = string.IsNullOrWhiteSpace(FormNotes) ? null : FormNotes.Trim()
                };
                var result = await ApiClient.PutAsync<SupplierDto>($"/api/v1/suppliers/{_editingId}", body);
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
    private async Task DeactivateAsync(SupplierDto? supplier)
    {
        if (supplier is null) return;
        if (!_messageService.ShowConfirm(Localization.Strings.Dialog_ConfirmDeactivate))
            return;

        var result = await ApiClient.DeleteAsync<object>($"/api/v1/suppliers/{supplier.Id}");
        if (!result.IsSuccess)
        {
            _messageService.ShowError(result.ErrorMessage ?? Localization.Strings.State_UnexpectedError);
            return;
        }

        await LoadPageAsync();
    }

    [RelayCommand]
    private void ViewPurchases(SupplierDto? supplier)
    {
        if (supplier is null) return;
        _navigationService.NavigateTo<PurchasesViewModel>();
    }
}
