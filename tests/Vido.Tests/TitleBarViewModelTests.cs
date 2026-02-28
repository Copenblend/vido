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

    /// <summary>
    /// Sets up test dependencies and creates the system under test.
    /// </summary>
    public TitleBarViewModelTests()
    {
        _windowService = Substitute.For<IWindowService>();
        _viewModel = new TitleBarViewModel(_windowService);
    }

    /// <summary>
    /// Verifies that Default Title is vido.
    /// </summary>
    [Fact]
    public void DefaultTitle_IsVido()
    {
        Assert.Equal("Vido", _viewModel.Title);
    }

    /// <summary>
    /// Verifies that Default Is Maximized is false.
    /// </summary>
    [Fact]
    public void DefaultIsMaximized_IsFalse()
    {
        Assert.False(_viewModel.IsMaximized);
    }

    /// <summary>
    /// Verifies that Minimize Command calls window service minimize.
    /// </summary>
    [Fact]
    public void MinimizeCommand_CallsWindowServiceMinimize()
    {
        _viewModel.MinimizeCommand.Execute(null);

        _windowService.Received(1).Minimize();
    }

    /// <summary>
    /// Verifies that Toggle Maximize Command calls window service toggle maximize.
    /// </summary>
    [Fact]
    public void ToggleMaximizeCommand_CallsWindowServiceToggleMaximize()
    {
        _viewModel.ToggleMaximizeCommand.Execute(null);

        _windowService.Received(1).ToggleMaximize();
    }

    /// <summary>
    /// Verifies that Toggle Maximize Command sets is maximized when state becomes maximized.
    /// </summary>
    [Fact]
    public void ToggleMaximizeCommand_SetsIsMaximized_WhenStateBecomesMaximized()
    {
        _windowService.CurrentState.Returns(AppWindowState.Maximized);

        _viewModel.ToggleMaximizeCommand.Execute(null);

        Assert.True(_viewModel.IsMaximized);
    }

    /// <summary>
    /// Verifies that Toggle Maximize Command clears is maximized when state becomes normal.
    /// </summary>
    [Fact]
    public void ToggleMaximizeCommand_ClearsIsMaximized_WhenStateBecomesNormal()
    {
        _viewModel.IsMaximized = true;
        _windowService.CurrentState.Returns(AppWindowState.Normal);

        _viewModel.ToggleMaximizeCommand.Execute(null);

        Assert.False(_viewModel.IsMaximized);
    }

    /// <summary>
    /// Verifies that Close Command calls window service close.
    /// </summary>
    [Fact]
    public void CloseCommand_CallsWindowServiceClose()
    {
        _viewModel.CloseCommand.Execute(null);

        _windowService.Received(1).Close();
    }

    /// <summary>
    /// Verifies that Sync Window State maximized sets is maximized true.
    /// </summary>
    [Fact]
    public void SyncWindowState_Maximized_SetsIsMaximizedTrue()
    {
        _viewModel.SyncWindowState(AppWindowState.Maximized);

        Assert.True(_viewModel.IsMaximized);
    }

    /// <summary>
    /// Verifies that Sync Window State normal sets is maximized false.
    /// </summary>
    [Fact]
    public void SyncWindowState_Normal_SetsIsMaximizedFalse()
    {
        _viewModel.IsMaximized = true;

        _viewModel.SyncWindowState(AppWindowState.Normal);

        Assert.False(_viewModel.IsMaximized);
    }

    /// <summary>
    /// Verifies that Sync Window State minimized sets is maximized false.
    /// </summary>
    [Fact]
    public void SyncWindowState_Minimized_SetsIsMaximizedFalse()
    {
        _viewModel.IsMaximized = true;

        _viewModel.SyncWindowState(AppWindowState.Minimized);

        Assert.False(_viewModel.IsMaximized);
    }

    /// <summary>
    /// Verifies that Title raises property changed.
    /// </summary>
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

    /// <summary>
    /// Verifies that Is Maximized raises property changed.
    /// </summary>
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