using NSubstitute;
using Vido.Core.Windowing;
using Vido.ViewModels;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Unit tests for <see cref="TitleBarViewModel"/>.
/// </summary>
public class TitleBarViewModelTests
{
    private readonly IWindowService _windowService;
    private readonly TitleBarViewModel _viewModel;

    public TitleBarViewModelTests()
    {
        _windowService = Substitute.For<IWindowService>();
        _viewModel = new TitleBarViewModel(_windowService);
    }

    [Fact]
    public void DefaultTitle_IsVido()
    {
        Assert.Equal("Vido", _viewModel.Title);
    }

    [Fact]
    public void DefaultIsMaximized_IsFalse()
    {
        Assert.False(_viewModel.IsMaximized);
    }

    [Fact]
    public void MinimizeCommand_CallsWindowServiceMinimize()
    {
        _viewModel.MinimizeCommand.Execute(null);

        _windowService.Received(1).Minimize();
    }

    [Fact]
    public void ToggleMaximizeCommand_CallsWindowServiceToggleMaximize()
    {
        _viewModel.ToggleMaximizeCommand.Execute(null);

        _windowService.Received(1).ToggleMaximize();
    }

    [Fact]
    public void ToggleMaximizeCommand_SetsIsMaximized_WhenStateBecomesMaximized()
    {
        _windowService.CurrentState.Returns(AppWindowState.Maximized);

        _viewModel.ToggleMaximizeCommand.Execute(null);

        Assert.True(_viewModel.IsMaximized);
    }

    [Fact]
    public void ToggleMaximizeCommand_ClearsIsMaximized_WhenStateBecomesNormal()
    {
        _viewModel.IsMaximized = true;
        _windowService.CurrentState.Returns(AppWindowState.Normal);

        _viewModel.ToggleMaximizeCommand.Execute(null);

        Assert.False(_viewModel.IsMaximized);
    }

    [Fact]
    public void CloseCommand_CallsWindowServiceClose()
    {
        _viewModel.CloseCommand.Execute(null);

        _windowService.Received(1).Close();
    }

    [Fact]
    public void SyncWindowState_Maximized_SetsIsMaximizedTrue()
    {
        _viewModel.SyncWindowState(AppWindowState.Maximized);

        Assert.True(_viewModel.IsMaximized);
    }

    [Fact]
    public void SyncWindowState_Normal_SetsIsMaximizedFalse()
    {
        _viewModel.IsMaximized = true;

        _viewModel.SyncWindowState(AppWindowState.Normal);

        Assert.False(_viewModel.IsMaximized);
    }

    [Fact]
    public void SyncWindowState_Minimized_SetsIsMaximizedFalse()
    {
        _viewModel.IsMaximized = true;

        _viewModel.SyncWindowState(AppWindowState.Minimized);

        Assert.False(_viewModel.IsMaximized);
    }

    [Fact]
    public void Title_RaisesPropertyChanged()
    {
        var raised = false;
        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(TitleBarViewModel.Title))
                raised = true;
        };

        _viewModel.Title = "New Title";

        Assert.True(raised);
    }

    [Fact]
    public void IsMaximized_RaisesPropertyChanged()
    {
        var raised = false;
        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(TitleBarViewModel.IsMaximized))
                raised = true;
        };

        _viewModel.IsMaximized = true;

        Assert.True(raised);
    }
}
