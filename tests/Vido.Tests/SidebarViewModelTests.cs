using Vido.Core.Layout;
using Vido.ViewModels;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Unit tests for <see cref="SidebarViewModel"/>.
/// </summary>
public class SidebarViewModelTests
{
    [Fact]
    public void DefaultHeader_IsExplorer()
    {
        var vm = new SidebarViewModel();

        Assert.Equal("EXPLORER", vm.HeaderText);
    }

    [Theory]
    [InlineData(SidebarPanelKind.Explorer, "EXPLORER")]
    [InlineData(SidebarPanelKind.Extensions, "EXTENSIONS")]
    [InlineData(SidebarPanelKind.Settings, "SETTINGS")]
    public void SetPanel_UpdatesHeaderText(SidebarPanelKind panel, string expected)
    {
        var vm = new SidebarViewModel();

        vm.SetPanel(panel);

        Assert.Equal(expected, vm.HeaderText);
    }

    [Fact]
    public void SetPanel_RaisesPropertyChanged()
    {
        var vm = new SidebarViewModel();
        var changedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        vm.SetPanel(SidebarPanelKind.Extensions);

        Assert.Contains("HeaderText", changedProperties);
    }
}
