using HomeSpeaker.Maui.ViewModels;

namespace HomeSpeaker.Maui.Views;

public partial class ManageDevicesView : ContentPage
{
	public ManageDevicesView(ManageDevicesViewModel vm)
	{
		InitializeComponent();
        BindingContext = vm;
    }
}