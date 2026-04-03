using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ElshazlyStore.Desktop.Models;
using ElshazlyStore.Desktop.Models.Dtos;
using ElshazlyStore.Desktop.Services;
using ElshazlyStore.Desktop.Services.Api;
using System.Collections.ObjectModel;

namespace ElshazlyStore.Desktop.ViewModels;

public sealed partial class CustomersViewModel : PagedListViewModelBase<CustomerDto>
{
    private readonly IPermissionService _permissionService;
    private readonly IMessageService _messageService;
    private readonly INavigationService _navigationService;

    public CustomersViewModel(
        ApiClient apiClient,
        IPermissionService permissionService,
        IMessageService messageService,
        INavigationService navigationService)
        : base(apiClient)
    {
        _permissionService = permissionService;
        _messageService = messageService;
        _navigationService = navigationService;
        Title = Localization.Strings.Nav_Customers;
        CanWrite = _permissionService.HasPermission(PermissionCodes.CustomersWrite);
        CanViewPayments = _permissionService.HasPermission(PermissionCodes.PaymentsRead);
    }

    [ObservableProperty] private bool _canWrite;
    [ObservableProperty] private bool _canViewPayments;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private bool _isViewingDetails;
    [ObservableProperty] private CustomerDto? _detailCustomer;
    [ObservableProperty] private string _notificationMessage = string.Empty;
    [ObservableProperty] private string _notificationType = "Info";

    [ObservableProperty] private string _formTitle = string.Empty;
    [ObservableProperty] private string _formName = string.Empty;
    [ObservableProperty] private string _formCode = string.Empty;
    [ObservableProperty] private string _formPhone = string.Empty;
    [ObservableProperty] private string _formPhone2 = string.Empty;
    [ObservableProperty] private string _formNotes = string.Empty;
    [ObservableProperty] private string _formError = string.Empty;
    [ObservableProperty] private bool _isSaving;

    // CAFS-1-R1: Code read-only in edit mode
    [ObservableProperty] private bool _isCodeReadOnly;

    // CP-3A: Credit profile form fields
    [ObservableProperty] private string _formWhatsApp = string.Empty;
    [ObservableProperty] private string _formWalletNumber = string.Empty;
    [ObservableProperty] private string _formInstaPayId = string.Empty;
    [ObservableProperty] private string _formCommercialName = string.Empty;
    [ObservableProperty] private string _formCommercialAddress = string.Empty;
    [ObservableProperty] private string _formNationalIdNumber = string.Empty;

    // CP-3A: Attachments
    [ObservableProperty] private ObservableCollection<CustomerAttachmentDto> _detailAttachments = new();
    [ObservableProperty] private bool _isLoadingAttachments;
    [ObservableProperty] private bool _isUploadingAttachment;

    private Guid? _editingId;

    public string DetailCustomerCreatedDateDisplay =>
        DetailCustomer?.CreatedAtUtc.ToLocalTime().ToString("yyyy/MM/dd") ?? string.Empty;

    partial void OnDetailCustomerChanged(CustomerDto? value) =>
        OnPropertyChanged(nameof(DetailCustomerCreatedDateDisplay));

    protected override Task<ApiResult<PagedResponse<CustomerDto>>> FetchPageAsync(
        int page, int pageSize, string? search, string? sort)
    {
        var url = BuildQueryString("/api/v1/customers", page, pageSize, search, sort);
        return ApiClient.GetAsync<PagedResponse<CustomerDto>>(url);
    }

    [RelayCommand]
    private void OpenCreate()
    {
        _editingId = null;
        FormTitle = Localization.Strings.Customers_FormTitleCreate;
        FormName = string.Empty;
        FormCode = string.Empty;
        FormPhone = string.Empty;
        FormPhone2 = string.Empty;
        FormNotes = string.Empty;
        FormWhatsApp = string.Empty;
        FormWalletNumber = string.Empty;
        FormInstaPayId = string.Empty;
        FormCommercialName = string.Empty;
        FormCommercialAddress = string.Empty;
        FormNationalIdNumber = string.Empty;
        FormError = string.Empty;
        IsCodeReadOnly = false; // Code auto-generated on create, field hidden
        IsEditing = true;
    }

    [RelayCommand]
    private void OpenEdit(CustomerDto? customer)
    {
        if (customer is null) return;
        _editingId = customer.Id;
        FormTitle = Localization.Strings.Customers_FormTitleEdit;
        FormName = customer.Name;
        FormCode = customer.Code;
        FormPhone = customer.Phone ?? string.Empty;
        FormPhone2 = customer.Phone2 ?? string.Empty;
        FormNotes = customer.Notes ?? string.Empty;
        FormWhatsApp = customer.WhatsApp ?? string.Empty;
        FormWalletNumber = customer.WalletNumber ?? string.Empty;
        FormInstaPayId = customer.InstaPayId ?? string.Empty;
        FormCommercialName = customer.CommercialName ?? string.Empty;
        FormCommercialAddress = customer.CommercialAddress ?? string.Empty;
        FormNationalIdNumber = customer.NationalIdNumber ?? string.Empty;
        FormError = string.Empty;
        IsCodeReadOnly = true; // CAFS-1-R1: Code is immutable after creation
        IsEditing = true;
    }

    [RelayCommand]
    private async Task OpenDetailsAsync(CustomerDto? customer)
    {
        if (customer is null) return;
        DetailCustomer = customer;
        IsViewingDetails = true;
        await LoadAttachmentsAsync(customer.Id);
    }

    [RelayCommand]
    private void CloseDetails()
    {
        IsViewingDetails = false;
        DetailCustomer = null;
    }

    [RelayCommand]
    private void OpenCustomerPayments(CustomerDto? customer)
    {
        if (customer is null) return;
        // Close the details overlay before navigating
        IsViewingDetails = false;
        DetailCustomer = null;
        // Navigate to CustomerPaymentsPage with this customer pre-filtered
        _navigationService.NavigateTo<CustomerPaymentsViewModel>(vm =>
        {
            vm.SetCustomerFilter(customer.Id, customer.Name);
        });
    }

    [RelayCommand]
    private void EditFromDetails()
    {
        if (DetailCustomer is null) return;
        var customer = DetailCustomer;
        IsViewingDetails = false;
        DetailCustomer = null;
        OpenEdit(customer);
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
                var body = new CreateCustomerRequest
                {
                    Name = FormName.Trim(),
                    Code = null, // CAFS-1-R1: Always auto-generated by server
                    Phone = string.IsNullOrWhiteSpace(FormPhone) ? null : FormPhone.Trim(),
                    Phone2 = string.IsNullOrWhiteSpace(FormPhone2) ? null : FormPhone2.Trim(),
                    Notes = string.IsNullOrWhiteSpace(FormNotes) ? null : FormNotes.Trim(),
                    WhatsApp = string.IsNullOrWhiteSpace(FormWhatsApp) ? null : FormWhatsApp.Trim(),
                    WalletNumber = string.IsNullOrWhiteSpace(FormWalletNumber) ? null : FormWalletNumber.Trim(),
                    InstaPayId = string.IsNullOrWhiteSpace(FormInstaPayId) ? null : FormInstaPayId.Trim(),
                    CommercialName = string.IsNullOrWhiteSpace(FormCommercialName) ? null : FormCommercialName.Trim(),
                    CommercialAddress = string.IsNullOrWhiteSpace(FormCommercialAddress) ? null : FormCommercialAddress.Trim(),
                    NationalIdNumber = string.IsNullOrWhiteSpace(FormNationalIdNumber) ? null : FormNationalIdNumber.Trim()
                };
                var result = await ApiClient.PostAsync<CustomerDto>("/api/v1/customers", body);
                if (!result.IsSuccess)
                {
                    FormError = result.ErrorMessage ?? Localization.Strings.State_UnexpectedError;
                    return;
                }
            }
            else
            {
                var body = new UpdateCustomerRequest
                {
                    Name = FormName.Trim(),
                    Code = null, // CAFS-1-R1: Code is immutable, not sent on update
                    Phone = string.IsNullOrWhiteSpace(FormPhone) ? null : FormPhone.Trim(),
                    Phone2 = string.IsNullOrWhiteSpace(FormPhone2) ? null : FormPhone2.Trim(),
                    Notes = string.IsNullOrWhiteSpace(FormNotes) ? null : FormNotes.Trim(),
                    WhatsApp = string.IsNullOrWhiteSpace(FormWhatsApp) ? null : FormWhatsApp.Trim(),
                    WalletNumber = string.IsNullOrWhiteSpace(FormWalletNumber) ? null : FormWalletNumber.Trim(),
                    InstaPayId = string.IsNullOrWhiteSpace(FormInstaPayId) ? null : FormInstaPayId.Trim(),
                    CommercialName = string.IsNullOrWhiteSpace(FormCommercialName) ? null : FormCommercialName.Trim(),
                    CommercialAddress = string.IsNullOrWhiteSpace(FormCommercialAddress) ? null : FormCommercialAddress.Trim(),
                    NationalIdNumber = string.IsNullOrWhiteSpace(FormNationalIdNumber) ? null : FormNationalIdNumber.Trim()
                };
                var result = await ApiClient.PutAsync<CustomerDto>($"/api/v1/customers/{_editingId}", body);
                if (!result.IsSuccess)
                {
                    FormError = result.ErrorMessage ?? Localization.Strings.State_UnexpectedError;
                    return;
                }
            }

            IsEditing = false;
            await LoadPageAsync();
            NotificationType = "Success";
            NotificationMessage = Localization.Strings.Customers_SaveSuccess;
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
    private async Task DeactivateAsync(CustomerDto? customer)
    {
        if (customer is null) return;
        if (!_messageService.ShowConfirm(Localization.Strings.Dialog_ConfirmDeactivate))
            return;

        var result = await ApiClient.DeleteAsync<object>($"/api/v1/customers/{customer.Id}");
        if (!result.IsSuccess)
        {
            NotificationType = "Error";
            NotificationMessage = result.ErrorMessage ?? Localization.Strings.State_UnexpectedError;
            return;
        }

        await LoadPageAsync();
        NotificationType = "Success";
        NotificationMessage = Localization.Strings.Customers_DeactivateSuccess;
    }

    [RelayCommand]
    private async Task ReactivateAsync(CustomerDto? customer)
    {
        if (customer is null) return;
        if (!_messageService.ShowConfirm(Localization.Strings.Dialog_ConfirmReactivate))
            return;

        var result = await ApiClient.PutAsync<object>($"/api/v1/customers/{customer.Id}", new UpdateCustomerRequest
        {
            IsActive = true
        });

        if (!result.IsSuccess)
        {
            NotificationType = "Error";
            NotificationMessage = result.ErrorMessage ?? Localization.Strings.State_UnexpectedError;
            return;
        }

        await LoadPageAsync();
        NotificationType = "Success";
        NotificationMessage = Localization.Strings.Customers_ReactivateSuccess;
    }

    // ═══ CP-3A: Attachment commands ═══

    private async Task LoadAttachmentsAsync(Guid customerId)
    {
        IsLoadingAttachments = true;
        try
        {
            var result = await ApiClient.GetAsync<List<CustomerAttachmentDto>>($"/api/v1/customers/{customerId}/attachments");
            DetailAttachments = result.IsSuccess && result.Data is not null
                ? new ObservableCollection<CustomerAttachmentDto>(result.Data)
                : new ObservableCollection<CustomerAttachmentDto>();
        }
        finally
        {
            IsLoadingAttachments = false;
        }
    }

    [RelayCommand]
    private async Task UploadAttachmentAsync(string? category)
    {
        if (DetailCustomer is null) return;

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "المرفقات المدعومة|*.jpg;*.jpeg;*.png;*.gif;*.webp;*.pdf;*.doc;*.docx|كل الملفات|*.*",
            Title = Localization.Strings.Customers_UploadAttachment
        };

        if (dialog.ShowDialog() != true) return;

        var filePath = dialog.FileName;
        var fileInfo = new System.IO.FileInfo(filePath);
        if (fileInfo.Length > 5 * 1024 * 1024)
        {
            NotificationType = "Error";
            NotificationMessage = Localization.Strings.Customers_AttachmentTooLarge;
            return;
        }

        IsUploadingAttachment = true;
        try
        {
            var cat = category ?? "other";
            var result = await ApiClient.PostMultipartAsync<CustomerAttachmentDto>(
                $"/api/v1/customers/{DetailCustomer.Id}/attachments?category={cat}", filePath);

            if (result.IsSuccess)
            {
                await LoadAttachmentsAsync(DetailCustomer.Id);
                NotificationType = "Success";
                NotificationMessage = Localization.Strings.Customers_AttachmentUploaded;
            }
            else
            {
                NotificationType = "Error";
                NotificationMessage = result.ErrorMessage ?? Localization.Strings.State_UnexpectedError;
            }
        }
        finally
        {
            IsUploadingAttachment = false;
        }
    }

    [RelayCommand]
    private async Task DownloadAttachmentAsync(CustomerAttachmentDto? attachment)
    {
        if (DetailCustomer is null || attachment is null) return;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = attachment.FileName,
            Title = Localization.Strings.Customers_DownloadAttachment
        };

        if (dialog.ShowDialog() != true) return;

        var (data, _, error) = await ApiClient.GetBytesAsync(
            $"/api/v1/customers/{DetailCustomer.Id}/attachments/{attachment.Id}");

        if (data is not null)
        {
            await System.IO.File.WriteAllBytesAsync(dialog.FileName, data);
            // Auto-open after saving so the user can immediately view the file
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = dialog.FileName,
                    UseShellExecute = true
                });
            }
            catch { /* silently ignore if shell open fails */ }
            NotificationType = "Success";
            NotificationMessage = Localization.Strings.Customers_AttachmentDownloaded;
        }
        else
        {
            NotificationType = "Error";
            NotificationMessage = error ?? Localization.Strings.State_UnexpectedError;
        }
    }

    [RelayCommand]
    private async Task OpenAttachmentAsync(CustomerAttachmentDto? attachment)
    {
        if (DetailCustomer is null || attachment is null) return;

        var (data, _, error) = await ApiClient.GetBytesAsync(
            $"/api/v1/customers/{DetailCustomer.Id}/attachments/{attachment.Id}");

        if (data is not null)
        {
            // Write to a unique temp path to avoid file-lock conflicts across multiple opens
            var ext = System.IO.Path.GetExtension(attachment.FileName);
            var tempName = $"elshazly_{attachment.Id}{ext}";
            var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), tempName);
            await System.IO.File.WriteAllBytesAsync(tempPath, data);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = tempPath,
                UseShellExecute = true
            });
        }
        else
        {
            NotificationType = "Error";
            NotificationMessage = error ?? Localization.Strings.State_UnexpectedError;
        }
    }

    [RelayCommand]
    private async Task DeleteAttachmentAsync(CustomerAttachmentDto? attachment)
    {
        if (DetailCustomer is null || attachment is null) return;
        if (!_messageService.ShowConfirm(Localization.Strings.Customers_ConfirmDeleteAttachment))
            return;

        var result = await ApiClient.DeleteAsync<object>(
            $"/api/v1/customers/{DetailCustomer.Id}/attachments/{attachment.Id}");

        if (result.IsSuccess)
        {
            await LoadAttachmentsAsync(DetailCustomer.Id);
            NotificationType = "Success";
            NotificationMessage = Localization.Strings.Customers_AttachmentDeleted;
        }
        else
        {
            NotificationType = "Error";
            NotificationMessage = result.ErrorMessage ?? Localization.Strings.State_UnexpectedError;
        }
    }

    public string GetAttachmentCategoryDisplay(string category) => category switch
    {
        "national_id" => "بطاقة رقم قومي",
        "contract" => "تعاقد",
        _ => "مرفق آخر"
    };

    // ═══ CAFS-1-R1: Open Attachments Folder ═══

    [RelayCommand]
    private async Task OpenAttachmentsFolderAsync(CustomerDto? customer)
    {
        if (customer is null) return;

        try
        {
            var result = await ApiClient.GetAsync<AttachmentsFolderResponse>(
                $"/api/v1/customers/{customer.Id}/attachments-folder");

            if (!result.IsSuccess || result.Data is null)
            {
                NotificationType = "Error";
                NotificationMessage = result.ErrorMessage ?? Localization.Strings.Customers_FolderNotAccessible;
                return;
            }

            var folderPath = result.Data.FolderPath;

            if (!result.Data.Exists)
            {
                NotificationType = "Info";
                NotificationMessage = Localization.Strings.Customers_FolderNotCreatedYet;
                return;
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = folderPath,
                UseShellExecute = true
            });
        }
        catch
        {
            NotificationType = "Error";
            NotificationMessage = Localization.Strings.Customers_FolderNotAccessible;
        }
    }
}
