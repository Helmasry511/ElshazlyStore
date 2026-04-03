using ElshazlyStore.Desktop.Localization;

namespace ElshazlyStore.Desktop.ViewModels;

public sealed partial class HomeViewModel : ViewModelBase
{
    public HomeViewModel()
    {
        Title = Strings.Nav_Home;
    }
}
