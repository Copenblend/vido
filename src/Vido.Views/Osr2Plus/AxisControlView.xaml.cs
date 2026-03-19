using System.Windows;
using System.Windows.Controls;
using Vido.ViewModels.Osr2Plus;
using Vido.Views.Controls;

namespace Vido.Views.Osr2Plus;

/// <summary>
/// View for the axis control panel, displaying axis cards and a test button.
/// DataContext: <see cref="Vido.ViewModels.Osr2Plus.AxisControlViewModel"/>.
/// </summary>
public partial class AxisControlView : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="AxisControlView"/> class.</summary>
    public AxisControlView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is AxisControlViewModel oldVm)
        {
            oldVm.RequestProfileName -= OnRequestProfileName;
            oldVm.RequestProfileRename -= OnRequestProfileRename;
        }

        if (e.NewValue is AxisControlViewModel newVm)
        {
            newVm.RequestProfileName += OnRequestProfileName;
            newVm.RequestProfileRename += OnRequestProfileRename;
        }
    }

    private void OnRequestProfileName(object? sender, EventArgs e)
    {
        var vm = DataContext as AxisControlViewModel;
        if (vm is null) return;

        var owner = Window.GetWindow(this);
        if (owner is null) return;

        var defaultName = vm.GenerateDefaultProfileName();
        var name = InputDialog.ShowInputDialog(owner, "Save Profile", "Profile name:", defaultName);
        if (!string.IsNullOrWhiteSpace(name))
            vm.CompleteSaveProfile(name);
    }

    private void OnRequestProfileRename(object? sender, EventArgs e)
    {
        var vm = DataContext as AxisControlViewModel;
        if (vm is null || vm.SelectedProfile is null) return;

        var owner = Window.GetWindow(this);
        if (owner is null) return;

        var newName = InputDialog.ShowInputDialog(owner, "Rename Profile", "New name:", vm.SelectedProfile.Name);
        if (!string.IsNullOrWhiteSpace(newName))
            vm.CompleteRenameProfile(newName);
    }
}
