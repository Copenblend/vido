using Vido.Core.Layout;
using Vido.ViewModels;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Unit tests for <see cref="SidebarViewModel"/>.
/// </summary>
public class SidebarViewModelTests
{
    /// <summary>
    /// Verifies that Default Header is explorer.
    /// </summary>
    [Fact]
    public void DefaultHeader_IsExplorer()
    {
        var vm = new SidebarViewModel();

        Assert.Equal("EXPLORER", vm.HeaderText);
    }

    /// <summary>
    /// Verifies that Set Panel updates header text.
    /// </summary>
    /// <param name="panel">The sidebar panel kind.</param>
    /// <param name="expected">The expected result value.</param>
    [Theory]
    [InlineData(SidebarPanelKind.Explorer, "EXPLORER")]
    [InlineData(SidebarPanelKind.Playlists, "PLAYLISTS")]
    [InlineData(SidebarPanelKind.Osr2Plus, "OSR2+")]
    [InlineData(SidebarPanelKind.Settings, "SETTINGS")]
    public void SetPanel_UpdatesHeaderText(SidebarPanelKind panel, string expected)
    {
        var vm = new SidebarViewModel();

        vm.SetPanel(panel);

        Assert.Equal(expected, vm.HeaderText);
    }

    /// <summary>
    /// Verifies that Set Panel raises property changed.
    /// </summary>
    [Fact]
    public void SetPanel_RaisesPropertyChanged()
    {
        var vm = new SidebarViewModel();
        var changedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        vm.SetPanel(SidebarPanelKind.Playlists);

        Assert.Contains("HeaderText", changedProperties);
    }
}