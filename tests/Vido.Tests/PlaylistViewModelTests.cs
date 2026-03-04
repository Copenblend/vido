using System.IO;
using NSubstitute;
using Vido.Core.Events;
using Vido.Core.Models.Playlists;
using Vido.Core.Playback;
using Vido.Core.Settings;
using Vido.Services.Playlists;
using Vido.ViewModels.Playlists;
using Xunit;

namespace Vido.Tests;

/// <summary>
/// Unit tests for <see cref="PlaylistViewModel"/> — PI-018.
/// Covers construction, add/remove/move items, commands, settings integration,
/// status text, toast delegation, recent playlists, dirty-prompt, file drop,
/// event handling, and disposal.
/// </summary>
public sealed class PlaylistViewModelTests : IDisposable
{
    private readonly IVideoEngine _videoEngine;
    private readonly IEventBus _eventBus;
    private readonly IDialogService _dialogService;
    private readonly ISettingsService _settingsService;
    private readonly IToastService _toastService;
    private readonly PlaylistFileService _fileService;
    private readonly PlaylistProvider _playlistProvider;
    private readonly PlaylistViewModel _vm;
    private readonly string _tempDir;

    // Captured VideoLoadedEvent handler registered via Subscribe
    private Action<VideoLoadedEvent>? _videoLoadedHandler;

    public PlaylistViewModelTests()
    {
        _videoEngine = Substitute.For<IVideoEngine>();
        _eventBus = Substitute.For<IEventBus>();
        _dialogService = Substitute.For<IDialogService>();
        _settingsService = Substitute.For<ISettingsService>();
        _toastService = Substitute.For<IToastService>();

        _settingsService.Current.Returns(new AppSettings());

        // Capture the VideoLoadedEvent subscription handler
        _eventBus.Subscribe(Arg.Any<Action<VideoLoadedEvent>>())
            .Returns(ci =>
            {
                _videoLoadedHandler = ci.Arg<Action<VideoLoadedEvent>>();
                return Substitute.For<IDisposable>();
            });

        _fileService = new PlaylistFileService();
        _playlistProvider = new PlaylistProvider();

        _vm = new PlaylistViewModel(
            _fileService,
            _videoEngine,
            _eventBus,
            _dialogService,
            _settingsService,
            _toastService,
            _playlistProvider);

        _tempDir = Path.Combine(Path.GetTempPath(), "PlaylistVmTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        _vm.Dispose();
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    // ══════════════════════════════════════════════════════════════
    //  Constructor
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Verifies that Constructor creates a default "Untitled Playlist" state.
    /// </summary>
    [Fact]
    public void Constructor_CreatesDefaultPlaylist()
    {
        Assert.NotNull(_vm.CurrentPlaylist);
        Assert.Equal("Untitled Playlist", _vm.PlaylistName);
        Assert.Empty(_vm.Items);
        Assert.False(_vm.HasItems);
    }

    /// <summary>
    /// Verifies that Constructor subscribes to VideoLoadedEvent.
    /// </summary>
    [Fact]
    public void Constructor_SubscribesToVideoLoadedEvent()
    {
        _eventBus.Received(1).Subscribe(Arg.Any<Action<VideoLoadedEvent>>());
        Assert.NotNull(_videoLoadedHandler);
    }

    /// <summary>
    /// Verifies that Constructor initializes all commands.
    /// </summary>
    [Fact]
    public void Constructor_InitializesAllCommands()
    {
        Assert.NotNull(_vm.NewPlaylistCommand);
        Assert.NotNull(_vm.PlayItemCommand);
        Assert.NotNull(_vm.OpenPlaylistCommand);
        Assert.NotNull(_vm.SavePlaylistCommand);
        Assert.NotNull(_vm.SavePlaylistAsCommand);
        Assert.NotNull(_vm.OpenRecentPlaylistCommand);
        Assert.NotNull(_vm.RemoveItemCommand);
        Assert.NotNull(_vm.MoveUpCommand);
        Assert.NotNull(_vm.MoveDownCommand);
        Assert.NotNull(_vm.MoveToTopCommand);
        Assert.NotNull(_vm.MoveToBottomCommand);
    }

    /// <summary>
    /// Verifies that Constructor throws on null required arguments.
    /// </summary>
    [Fact]
    public void Constructor_ThrowsOnNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PlaylistViewModel(null!, _videoEngine, _eventBus, _dialogService, _settingsService));
        Assert.Throws<ArgumentNullException>(() =>
            new PlaylistViewModel(_fileService, null!, _eventBus, _dialogService, _settingsService));
        Assert.Throws<ArgumentNullException>(() =>
            new PlaylistViewModel(_fileService, _videoEngine, null!, _dialogService, _settingsService));
        Assert.Throws<ArgumentNullException>(() =>
            new PlaylistViewModel(_fileService, _videoEngine, _eventBus, null!, _settingsService));
        Assert.Throws<ArgumentNullException>(() =>
            new PlaylistViewModel(_fileService, _videoEngine, _eventBus, _dialogService, null!));
    }

    /// <summary>
    /// Verifies that Constructor optional parameters default to null without error.
    /// </summary>
    [Fact]
    public void Constructor_OptionalParametersDefaultToNull()
    {
        var vm = new PlaylistViewModel(
            _fileService, _videoEngine, _eventBus, _dialogService, _settingsService);

        Assert.NotNull(vm.CurrentPlaylist);
        vm.Dispose();
    }

    /// <summary>
    /// Verifies that Constructor sets initial StatusText.
    /// </summary>
    [Fact]
    public void Constructor_SetsInitialStatusText()
    {
        Assert.Contains("0 items", _vm.StatusText);
    }

    // ══════════════════════════════════════════════════════════════
    //  AddItem
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Verifies that AddItem adds a video file to the playlist.
    /// </summary>
    [Fact]
    public void AddItem_AddsVideoFile()
    {
        _vm.AddItem(@"C:\Videos\video1.mp4");

        Assert.Single(_vm.Items);
        Assert.Equal("video1.mp4", _vm.Items[0].FileName);
        Assert.True(_vm.HasItems);
    }

    /// <summary>
    /// Verifies that AddItem skips duplicate file paths (case-insensitive).
    /// </summary>
    [Fact]
    public void AddItem_SkipsDuplicates()
    {
        _vm.AddItem(@"C:\Videos\video1.mp4");
        _vm.AddItem(@"C:\Videos\video1.mp4");
        _vm.AddItem(@"c:\videos\VIDEO1.MP4");

        Assert.Single(_vm.Items);
    }

    /// <summary>
    /// Verifies that AddItem ignores non-video file extensions.
    /// </summary>
    [Fact]
    public void AddItem_IgnoresNonVideoFiles()
    {
        _vm.AddItem(@"C:\Files\document.txt");
        _vm.AddItem(@"C:\Files\image.png");
        _vm.AddItem(@"C:\Files\script.funscript");

        Assert.Empty(_vm.Items);
    }

    /// <summary>
    /// Verifies that AddItem throws on null/whitespace.
    /// </summary>
    [Fact]
    public void AddItem_ThrowsOnNullOrWhiteSpace()
    {
        Assert.ThrowsAny<ArgumentException>(() => _vm.AddItem(null!));
        Assert.ThrowsAny<ArgumentException>(() => _vm.AddItem(""));
        Assert.ThrowsAny<ArgumentException>(() => _vm.AddItem("   "));
    }

    /// <summary>
    /// Verifies that AddItem accepts all supported video extensions.
    /// </summary>
    [Theory]
    [InlineData(".mp4")]
    [InlineData(".avi")]
    [InlineData(".mkv")]
    [InlineData(".mov")]
    [InlineData(".wmv")]
    [InlineData(".flv")]
    [InlineData(".webm")]
    public void AddItem_AcceptsAllSupportedVideoExtensions(string ext)
    {
        _vm.AddItem($@"C:\Videos\video{ext}");

        Assert.Single(_vm.Items);
    }

    /// <summary>
    /// Verifies that AddItem allows re-add after remove.
    /// </summary>
    [Fact]
    public void AddItem_AllowsReAddAfterRemove()
    {
        _vm.AddItem(@"C:\Videos\video1.mp4");
        _vm.RemoveItem(_vm.Items[0]);
        _vm.AddItem(@"C:\Videos\VIDEO1.MP4");

        Assert.Single(_vm.Items);
        Assert.Equal("VIDEO1.MP4", _vm.Items[0].FileName);
    }

    // ══════════════════════════════════════════════════════════════
    //  AddItems
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Verifies that AddItems adds multiple files at once.
    /// </summary>
    [Fact]
    public void AddItems_AddsMultipleFiles()
    {
        _vm.AddItems([@"C:\Videos\a.mp4", @"C:\Videos\b.mkv", @"C:\Videos\c.avi"]);

        Assert.Equal(3, _vm.Items.Count);
    }

    /// <summary>
    /// Verifies that AddItems skips duplicates and non-video files.
    /// </summary>
    [Fact]
    public void AddItems_SkipsDuplicatesAndNonVideo()
    {
        _vm.AddItem(@"C:\Videos\existing.mp4");

        _vm.AddItems([
            @"C:\Videos\existing.mp4",     // duplicate
            @"C:\Videos\new.mp4",           // new
            @"C:\Files\document.txt",       // non-video
        ]);

        Assert.Equal(2, _vm.Items.Count);
    }

    /// <summary>
    /// Verifies that AddItems throws on null.
    /// </summary>
    [Fact]
    public void AddItems_ThrowsOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => _vm.AddItems(null!));
    }

    /// <summary>
    /// Verifies that AddItems with empty list is a no-op.
    /// </summary>
    [Fact]
    public void AddItems_EmptyList_NoOp()
    {
        _vm.AddItems([]);

        Assert.Empty(_vm.Items);
    }

    // ══════════════════════════════════════════════════════════════
    //  RemoveItem
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Verifies that RemoveItem removes the specified item.
    /// </summary>
    [Fact]
    public void RemoveItem_RemovesItem()
    {
        _vm.AddItem(@"C:\Videos\a.mp4");
        _vm.AddItem(@"C:\Videos\b.mp4");
        var itemToRemove = _vm.Items[0];

        _vm.RemoveItem(itemToRemove);

        Assert.Single(_vm.Items);
        Assert.Equal("b.mp4", _vm.Items[0].FileName);
    }

    /// <summary>
    /// Verifies that RemoveItem with null is a no-op.
    /// </summary>
    [Fact]
    public void RemoveItem_Null_NoOp()
    {
        _vm.AddItem(@"C:\Videos\a.mp4");

        _vm.RemoveItem(null);

        Assert.Single(_vm.Items);
    }

    /// <summary>
    /// Verifies that RemoveItem clears CurrentItem if removed item is current.
    /// </summary>
    [Fact]
    public void RemoveItem_ClearsCurrentItemIfRemoved()
    {
        _vm.AddItem(@"C:\Videos\a.mp4");
        _vm.CurrentItem = _vm.Items[0];

        _vm.RemoveItem(_vm.Items[0]);

        Assert.Null(_vm.CurrentItem);
    }

    // ══════════════════════════════════════════════════════════════
    //  MoveItem
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Verifies that MoveItem swaps positions correctly.
    /// </summary>
    [Fact]
    public void MoveItem_SwapsPositions()
    {
        _vm.AddItems([@"C:\Videos\a.mp4", @"C:\Videos\b.mp4", @"C:\Videos\c.mp4"]);

        _vm.MoveItem(0, 2);

        Assert.Equal("b.mp4", _vm.Items[0].FileName);
        Assert.Equal("c.mp4", _vm.Items[1].FileName);
        Assert.Equal("a.mp4", _vm.Items[2].FileName);
    }

    /// <summary>
    /// Verifies that MoveItem with invalid indices is a no-op.
    /// </summary>
    [Fact]
    public void MoveItem_InvalidIndices_NoOp()
    {
        _vm.AddItems([@"C:\Videos\a.mp4", @"C:\Videos\b.mp4"]);

        _vm.MoveItem(-1, 0);
        _vm.MoveItem(0, 5);
        _vm.MoveItem(0, 0);

        Assert.Equal("a.mp4", _vm.Items[0].FileName);
        Assert.Equal("b.mp4", _vm.Items[1].FileName);
    }

    // ══════════════════════════════════════════════════════════════
    //  HasItems / StatusText
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Verifies that HasItems tracks item count.
    /// </summary>
    [Fact]
    public void HasItems_TrueWhenItemsExist()
    {
        Assert.False(_vm.HasItems);

        _vm.AddItem(@"C:\Videos\a.mp4");

        Assert.True(_vm.HasItems);
    }

    /// <summary>
    /// Verifies that StatusText updates when items change.
    /// </summary>
    [Fact]
    public void StatusText_UpdatesOnItemChange()
    {
        Assert.Contains("0 items", _vm.StatusText);

        _vm.AddItem(@"C:\Videos\a.mp4");

        Assert.Contains("1 items", _vm.StatusText);
    }

    /// <summary>
    /// Verifies that StatusText includes playlist name.
    /// </summary>
    [Fact]
    public void StatusText_IncludesPlaylistName()
    {
        Assert.Contains("Untitled Playlist", _vm.StatusText);
    }

    /// <summary>
    /// Verifies that StatusText shows playing info when item is current.
    /// </summary>
    [Fact]
    public void StatusText_ShowsPlayingInfoWhenCurrentItem()
    {
        _vm.AddItems([@"C:\Videos\a.mp4", @"C:\Videos\b.mp4"]);
        _vm.CurrentItem = _vm.Items[1];
        _vm.UpdateStatusText();

        Assert.Contains("Playing 2 of 2", _vm.StatusText);
    }

    // ══════════════════════════════════════════════════════════════
    //  CurrentItem
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Verifies that CurrentItem sets IsPlaying on the selected item.
    /// </summary>
    [Fact]
    public void CurrentItem_SetsIsPlayingOnItem()
    {
        _vm.AddItems([@"C:\Videos\a.mp4", @"C:\Videos\b.mp4"]);

        _vm.CurrentItem = _vm.Items[0];

        Assert.True(_vm.Items[0].IsPlaying);
        Assert.False(_vm.Items[1].IsPlaying);
    }

    /// <summary>
    /// Verifies that CurrentItem clears previous item's IsPlaying.
    /// </summary>
    [Fact]
    public void CurrentItem_ClearsPreviousIsPlaying()
    {
        _vm.AddItems([@"C:\Videos\a.mp4", @"C:\Videos\b.mp4"]);

        _vm.CurrentItem = _vm.Items[0];
        _vm.CurrentItem = _vm.Items[1];

        Assert.False(_vm.Items[0].IsPlaying);
        Assert.True(_vm.Items[1].IsPlaying);
    }

    /// <summary>
    /// Verifies that CurrentItem raises PropertyChanged.
    /// </summary>
    [Fact]
    public void CurrentItem_RaisesPropertyChanged()
    {
        _vm.AddItem(@"C:\Videos\a.mp4");
        var raised = false;
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PlaylistViewModel.CurrentItem))
                raised = true;
        };

        _vm.CurrentItem = _vm.Items[0];

        Assert.True(raised);
    }

    // ══════════════════════════════════════════════════════════════
    //  PlaylistName
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Verifies that PlaylistName raises PropertyChanged and updates model.
    /// </summary>
    [Fact]
    public void PlaylistName_UpdatesModelAndRaisesPropertyChanged()
    {
        var raised = false;
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PlaylistViewModel.PlaylistName))
                raised = true;
        };

        _vm.PlaylistName = "My Custom Playlist";

        Assert.Equal("My Custom Playlist", _vm.PlaylistName);
        Assert.Equal("My Custom Playlist", _vm.CurrentPlaylist.Name);
        Assert.True(raised);
    }

    /// <summary>
    /// Verifies that PlaylistName same value does not raise PropertyChanged.
    /// </summary>
    [Fact]
    public void PlaylistName_SameValue_NoPropertyChanged()
    {
        var raised = false;
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PlaylistViewModel.PlaylistName))
                raised = true;
        };

        _vm.PlaylistName = "Untitled Playlist"; // same as default

        Assert.False(raised);
    }

    // ══════════════════════════════════════════════════════════════
    //  ShowToast / ShowErrorToast
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Verifies that ShowToast delegates to IToastService.Show.
    /// </summary>
    [Fact]
    public void ShowToast_DelegatesToToastService()
    {
        _vm.ShowToast("Hello", "World");

        _toastService.Received(1).Show("Hello", "World");
    }

    /// <summary>
    /// Verifies that ShowErrorToast delegates to IToastService.ShowError.
    /// </summary>
    [Fact]
    public void ShowErrorToast_DelegatesToToastService()
    {
        _vm.ShowErrorToast("Error", "Details");

        _toastService.Received(1).ShowError("Error", "Details");
    }

    /// <summary>
    /// Verifies that ShowToast without toast service does not throw.
    /// </summary>
    [Fact]
    public void ShowToast_WithoutToastService_DoesNotThrow()
    {
        var vm = new PlaylistViewModel(
            _fileService, _videoEngine, _eventBus, _dialogService, _settingsService);

        var exception = Record.Exception(() => vm.ShowToast("msg"));

        Assert.Null(exception);
        vm.Dispose();
    }

    // ══════════════════════════════════════════════════════════════
    //  AutoSaveIfEnabled
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Verifies that AutoSaveIfEnabled does nothing when auto-save is disabled.
    /// </summary>
    [Fact]
    public void AutoSaveIfEnabled_DisabledSetting_NoSave()
    {
        _settingsService.Current.PlaylistAutoSave = false;

        _vm.AddItem(@"C:\Videos\a.mp4");
        _vm.AutoSaveIfEnabled();

        // No save dialog should be shown — the playlist has no file path
        // so it would prompt if auto-save triggered
        _dialogService.DidNotReceive().ShowSaveFileDialog(Arg.Any<string>(), Arg.Any<string>());
    }

    // ══════════════════════════════════════════════════════════════
    //  PromptSaveDirtyPlaylist
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Verifies that PromptSaveDirtyPlaylist returns true when playlist is not dirty.
    /// </summary>
    [Fact]
    public void PromptSaveDirtyPlaylist_NotDirty_ReturnsTrue()
    {
        // Fresh playlist is not dirty
        var result = _vm.PromptSaveDirtyPlaylist();

        Assert.True(result);
        _dialogService.DidNotReceive().ShowConfirmationDialog(Arg.Any<string>(), Arg.Any<string>());
    }

    /// <summary>
    /// Verifies that PromptSaveDirtyPlaylist prompts when dirty and user cancels.
    /// </summary>
    [Fact]
    public void PromptSaveDirtyPlaylist_Dirty_UserCancels_ReturnsFalse()
    {
        // Make playlist dirty by adding an item
        _vm.AddItem(@"C:\Videos\a.mp4");

        _dialogService.ShowConfirmationDialog(Arg.Any<string>(), Arg.Any<string>()).Returns((bool?)null);

        var result = _vm.PromptSaveDirtyPlaylist();

        Assert.False(result);
    }

    /// <summary>
    /// Verifies that PromptSaveDirtyPlaylist user says No returns true (discard).
    /// </summary>
    [Fact]
    public void PromptSaveDirtyPlaylist_Dirty_UserSaysNo_ReturnsTrue()
    {
        _vm.AddItem(@"C:\Videos\a.mp4");

        _dialogService.ShowConfirmationDialog(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        var result = _vm.PromptSaveDirtyPlaylist();

        Assert.True(result);
    }

    // ══════════════════════════════════════════════════════════════
    //  RecentPlaylists
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Verifies that AddRecentPlaylist adds path to top.
    /// </summary>
    [Fact]
    public void AddRecentPlaylist_AddsToTop()
    {
        _vm.AddRecentPlaylist(@"C:\Playlists\first.vidpl");
        _vm.AddRecentPlaylist(@"C:\Playlists\second.vidpl");

        Assert.Equal(2, _vm.RecentPlaylists.Count);
        Assert.Equal(@"C:\Playlists\second.vidpl", _vm.RecentPlaylists[0]);
        Assert.Equal(@"C:\Playlists\first.vidpl", _vm.RecentPlaylists[1]);
    }

    /// <summary>
    /// Verifies that AddRecentPlaylist moves existing entry to top.
    /// </summary>
    [Fact]
    public void AddRecentPlaylist_MovesExistingToTop()
    {
        _vm.AddRecentPlaylist(@"C:\Playlists\first.vidpl");
        _vm.AddRecentPlaylist(@"C:\Playlists\second.vidpl");
        _vm.AddRecentPlaylist(@"C:\Playlists\first.vidpl");

        Assert.Equal(2, _vm.RecentPlaylists.Count);
        Assert.Equal(@"C:\Playlists\first.vidpl", _vm.RecentPlaylists[0]);
    }

    /// <summary>
    /// Verifies that AddRecentPlaylist trims to max 10 entries.
    /// </summary>
    [Fact]
    public void AddRecentPlaylist_TrimsToMax()
    {
        for (var i = 0; i < 15; i++)
            _vm.AddRecentPlaylist($@"C:\Playlists\list{i}.vidpl");

        Assert.Equal(10, _vm.RecentPlaylists.Count);
    }

    /// <summary>
    /// Verifies that AddRecentPlaylist persists to settings.
    /// </summary>
    [Fact]
    public void AddRecentPlaylist_PersistsToSettings()
    {
        _vm.AddRecentPlaylist(@"C:\Playlists\test.vidpl");

        _settingsService.Received().QueueSave();
        Assert.Contains(@"C:\Playlists\test.vidpl",
            _settingsService.Current.PlaylistRecentPlaylists);
    }

    // ══════════════════════════════════════════════════════════════
    //  EnsureRecentPlaylistsLoaded
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Verifies that EnsureRecentPlaylistsLoaded loads from settings on first call.
    /// </summary>
    [Fact]
    public void EnsureRecentPlaylistsLoaded_LoadsFromSettings()
    {
        // Create a temp file so it passes the File.Exists check
        var tempFile = Path.Combine(_tempDir, "recent.vidpl");
        File.WriteAllText(tempFile, "[]");

        _settingsService.Current.PlaylistRecentPlaylists = [tempFile];

        _vm.EnsureRecentPlaylistsLoaded();

        Assert.Single(_vm.RecentPlaylists);
        Assert.Equal(tempFile, _vm.RecentPlaylists[0]);
    }

    /// <summary>
    /// Verifies that EnsureRecentPlaylistsLoaded only loads once.
    /// </summary>
    [Fact]
    public void EnsureRecentPlaylistsLoaded_OnlyLoadsOnce()
    {
        var tempFile = Path.Combine(_tempDir, "recent2.vidpl");
        File.WriteAllText(tempFile, "[]");

        _settingsService.Current.PlaylistRecentPlaylists = [tempFile];

        _vm.EnsureRecentPlaylistsLoaded();
        _vm.RecentPlaylists.Clear();
        _vm.EnsureRecentPlaylistsLoaded(); // should not reload

        Assert.Empty(_vm.RecentPlaylists);
    }

    /// <summary>
    /// Verifies that EnsureRecentPlaylistsLoaded skips non-existent files.
    /// </summary>
    [Fact]
    public void EnsureRecentPlaylistsLoaded_SkipsNonExistentFiles()
    {
        _settingsService.Current.PlaylistRecentPlaylists =
            [@"C:\NonExistent\missing.vidpl"];

        _vm.EnsureRecentPlaylistsLoaded();

        Assert.Empty(_vm.RecentPlaylists);
    }

    // ══════════════════════════════════════════════════════════════
    //  VideoLoadedEvent handler
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Verifies that VideoLoadedEvent sets CurrentItem when file is in playlist.
    /// </summary>
    [Fact]
    public void OnVideoLoaded_SetsCurrentItem_WhenFileInPlaylist()
    {
        _vm.AddItems([@"C:\Videos\a.mp4", @"C:\Videos\b.mp4"]);

        _videoLoadedHandler?.Invoke(new VideoLoadedEvent { FilePath = @"C:\Videos\b.mp4" });

        Assert.NotNull(_vm.CurrentItem);
        Assert.Equal("b.mp4", _vm.CurrentItem!.FileName);
        Assert.True(_vm.CurrentItem.IsPlaying);
    }

    /// <summary>
    /// Verifies that VideoLoadedEvent clears CurrentItem when file is not in playlist.
    /// </summary>
    [Fact]
    public void OnVideoLoaded_ClearsCurrentItem_WhenFileNotInPlaylist()
    {
        _vm.AddItem(@"C:\Videos\a.mp4");
        _vm.CurrentItem = _vm.Items[0];

        _videoLoadedHandler?.Invoke(new VideoLoadedEvent { FilePath = @"C:\Videos\other.mp4" });

        Assert.Null(_vm.CurrentItem);
    }

    /// <summary>
    /// Verifies that VideoLoadedEvent updates StatusText.
    /// </summary>
    [Fact]
    public void OnVideoLoaded_UpdatesStatusText()
    {
        _vm.AddItem(@"C:\Videos\a.mp4");

        _videoLoadedHandler?.Invoke(new VideoLoadedEvent { FilePath = @"C:\Videos\a.mp4" });

        Assert.Contains("Playing 1 of 1", _vm.StatusText);
    }

    // ══════════════════════════════════════════════════════════════
    //  HandleFileDrop
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Verifies that HandleFileDrop adds video files from dropped paths.
    /// HandleFileDrop is async void, so we need a delay for the async work to complete.
    /// </summary>
    [Fact]
    public async Task HandleFileDrop_AddsVideoFiles()
    {
        var videoFile = CreateTempFile("test.mp4");

        _vm.HandleFileDrop([videoFile]);

        // HandleFileDrop is async void — wait for async work to complete
        await Task.Delay(500);

        Assert.Single(_vm.Items);
    }

    /// <summary>
    /// Verifies that HandleFileDrop opens vidpl files instead of adding.
    /// </summary>
    [Fact]
    public async Task HandleFileDrop_OpensVidplFile()
    {
        // Create a .vidpl file
        var vidplPath = Path.Combine(_tempDir, "test.vidpl");
        var playlist = new Playlist("Test", [new PlaylistItem(@"C:\Videos\a.mp4")]);
        await _fileService.SaveAsync(playlist, vidplPath);

        _vm.HandleFileDrop([vidplPath]);

        // Give async load a moment
        await Task.Delay(200);

        // The playlist should have been loaded (name changes from Untitled)
        Assert.Equal("test", _vm.PlaylistName);
    }

    // ══════════════════════════════════════════════════════════════
    //  SaveCurrentPlaylistAsync
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Verifies that SaveCurrentPlaylistAsync prompts for file path when no existing path.
    /// </summary>
    [Fact]
    public async Task SaveCurrentPlaylistAsync_NoPath_PromptsBrowse()
    {
        var savePath = Path.Combine(_tempDir, "save_test.vidpl");
        _dialogService.ShowSaveFileDialog(Arg.Any<string>(), Arg.Any<string>()).Returns(savePath);

        _vm.AddItem(@"C:\Videos\a.mp4");
        var result = await _vm.SaveCurrentPlaylistAsync(saveAs: false);

        Assert.True(result);
        Assert.True(File.Exists(savePath));
    }

    /// <summary>
    /// Verifies that SaveCurrentPlaylistAsync returns false when user cancels browse.
    /// </summary>
    [Fact]
    public async Task SaveCurrentPlaylistAsync_UserCancels_ReturnsFalse()
    {
        _dialogService.ShowSaveFileDialog(Arg.Any<string>(), Arg.Any<string>()).Returns((string?)null);

        var result = await _vm.SaveCurrentPlaylistAsync(saveAs: false);

        Assert.False(result);
    }

    /// <summary>
    /// Verifies that SaveCurrentPlaylistAsync saveAs always prompts browse.
    /// </summary>
    [Fact]
    public async Task SaveCurrentPlaylistAsync_SaveAs_AlwaysPromptsBrowse()
    {
        // First save to set a file path
        var firstPath = Path.Combine(_tempDir, "first.vidpl");
        _dialogService.ShowSaveFileDialog(Arg.Any<string>(), Arg.Any<string>()).Returns(firstPath);
        await _vm.SaveCurrentPlaylistAsync(saveAs: false);

        // Now save-as should still prompt
        var secondPath = Path.Combine(_tempDir, "second.vidpl");
        _dialogService.ShowSaveFileDialog(Arg.Any<string>(), Arg.Any<string>()).Returns(secondPath);
        var result = await _vm.SaveCurrentPlaylistAsync(saveAs: true);

        Assert.True(result);
        _dialogService.Received(2).ShowSaveFileDialog(Arg.Any<string>(), Arg.Any<string>());
    }

    /// <summary>
    /// Verifies that SaveCurrentPlaylistAsync updates playlist name from file name.
    /// </summary>
    [Fact]
    public async Task SaveCurrentPlaylistAsync_UpdatesPlaylistName()
    {
        var savePath = Path.Combine(_tempDir, "MyPlaylist.vidpl");
        _dialogService.ShowSaveFileDialog(Arg.Any<string>(), Arg.Any<string>()).Returns(savePath);

        await _vm.SaveCurrentPlaylistAsync(saveAs: false);

        Assert.Equal("MyPlaylist", _vm.PlaylistName);
    }

    // ══════════════════════════════════════════════════════════════
    //  LoadPlaylistFromPathAsync
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Verifies that LoadPlaylistFromPathAsync loads playlist and updates state.
    /// </summary>
    [Fact]
    public async Task LoadPlaylistFromPathAsync_LoadsPlaylist()
    {
        var path = Path.Combine(_tempDir, "load_test.vidpl");
        var playlist = new Playlist("Loaded",
        [
            new PlaylistItem(@"C:\Videos\x.mp4"),
            new PlaylistItem(@"C:\Videos\y.mp4"),
        ]);
        await _fileService.SaveAsync(playlist, path);

        await _vm.LoadPlaylistFromPathAsync(path);

        Assert.Equal("load_test", _vm.PlaylistName);
        Assert.Equal(2, _vm.Items.Count);
    }

    /// <summary>
    /// Verifies that LoadPlaylistFromPathAsync persists last playlist path.
    /// </summary>
    [Fact]
    public async Task LoadPlaylistFromPathAsync_PersistsLastPath()
    {
        var path = Path.Combine(_tempDir, "persist.vidpl");
        var playlist = new Playlist("P");
        await _fileService.SaveAsync(playlist, path);

        await _vm.LoadPlaylistFromPathAsync(path);

        Assert.Equal(path, _settingsService.Current.PlaylistLastPlaylistPath);
        _settingsService.Received().QueueSave();
    }

    /// <summary>
    /// Verifies that LoadPlaylistFromPathAsync with invalid path does not throw.
    /// </summary>
    [Fact]
    public async Task LoadPlaylistFromPathAsync_InvalidPath_DoesNotThrow()
    {
        var exception = await Record.ExceptionAsync(() =>
            _vm.LoadPlaylistFromPathAsync(@"C:\NonExistent\bad.vidpl"));

        Assert.Null(exception);
    }

    // ══════════════════════════════════════════════════════════════
    //  NewPlaylistCommand
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Verifies that NewPlaylistCommand creates a fresh empty playlist.
    /// </summary>
    [Fact]
    public void NewPlaylistCommand_CreatesEmptyPlaylist()
    {
        _vm.AddItems([@"C:\Videos\a.mp4", @"C:\Videos\b.mp4"]);
        Assert.Equal(2, _vm.Items.Count);

        // Adding items makes playlist dirty — dialog must return false (No = discard)
        _dialogService.ShowConfirmationDialog(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        _vm.NewPlaylistCommand.Execute(null);

        Assert.Equal("Untitled Playlist", _vm.PlaylistName);
        Assert.Empty(_vm.Items);
    }

    // ══════════════════════════════════════════════════════════════
    //  Move commands
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Verifies that MoveUpCommand moves item up one position.
    /// </summary>
    [Fact]
    public void MoveUpCommand_MovesItemUp()
    {
        _vm.AddItems([@"C:\Videos\a.mp4", @"C:\Videos\b.mp4", @"C:\Videos\c.mp4"]);
        var itemB = _vm.Items[1];

        _vm.MoveUpCommand.Execute(itemB);

        Assert.Equal("b.mp4", _vm.Items[0].FileName);
        Assert.Equal("a.mp4", _vm.Items[1].FileName);
    }

    /// <summary>
    /// Verifies that MoveDownCommand moves item down one position.
    /// </summary>
    [Fact]
    public void MoveDownCommand_MovesItemDown()
    {
        _vm.AddItems([@"C:\Videos\a.mp4", @"C:\Videos\b.mp4", @"C:\Videos\c.mp4"]);
        var itemA = _vm.Items[0];

        _vm.MoveDownCommand.Execute(itemA);

        Assert.Equal("b.mp4", _vm.Items[0].FileName);
        Assert.Equal("a.mp4", _vm.Items[1].FileName);
    }

    /// <summary>
    /// Verifies that MoveToTopCommand moves item to position 0.
    /// </summary>
    [Fact]
    public void MoveToTopCommand_MovesItemToTop()
    {
        _vm.AddItems([@"C:\Videos\a.mp4", @"C:\Videos\b.mp4", @"C:\Videos\c.mp4"]);
        var itemC = _vm.Items[2];

        _vm.MoveToTopCommand.Execute(itemC);

        Assert.Equal("c.mp4", _vm.Items[0].FileName);
    }

    /// <summary>
    /// Verifies that MoveToBottomCommand moves item to last position.
    /// </summary>
    [Fact]
    public void MoveToBottomCommand_MovesItemToBottom()
    {
        _vm.AddItems([@"C:\Videos\a.mp4", @"C:\Videos\b.mp4", @"C:\Videos\c.mp4"]);
        var itemA = _vm.Items[0];

        _vm.MoveToBottomCommand.Execute(itemA);

        Assert.Equal("a.mp4", _vm.Items[2].FileName);
    }

    /// <summary>
    /// Verifies that MoveUpCommand with null item is a no-op.
    /// </summary>
    [Fact]
    public void MoveUpCommand_NullItem_NoOp()
    {
        _vm.AddItem(@"C:\Videos\a.mp4");

        var exception = Record.Exception(() => _vm.MoveUpCommand.Execute(null));

        Assert.Null(exception);
        Assert.Single(_vm.Items);
    }

    /// <summary>
    /// Verifies that MoveUpCommand on first item is a no-op.
    /// </summary>
    [Fact]
    public void MoveUpCommand_FirstItem_NoOp()
    {
        _vm.AddItems([@"C:\Videos\a.mp4", @"C:\Videos\b.mp4"]);
        var firstItem = _vm.Items[0];

        _vm.MoveUpCommand.Execute(firstItem);

        Assert.Equal("a.mp4", _vm.Items[0].FileName);
    }

    /// <summary>
    /// Verifies that MoveDownCommand on last item is a no-op.
    /// </summary>
    [Fact]
    public void MoveDownCommand_LastItem_NoOp()
    {
        _vm.AddItems([@"C:\Videos\a.mp4", @"C:\Videos\b.mp4"]);
        var lastItem = _vm.Items[1];

        _vm.MoveDownCommand.Execute(lastItem);

        Assert.Equal("b.mp4", _vm.Items[1].FileName);
    }

    // ══════════════════════════════════════════════════════════════
    //  RemoveItemCommand
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Verifies that RemoveItemCommand removes the specified item.
    /// </summary>
    [Fact]
    public void RemoveItemCommand_RemovesItem()
    {
        _vm.AddItem(@"C:\Videos\a.mp4");
        var item = _vm.Items[0];

        _vm.RemoveItemCommand.Execute(item);

        Assert.Empty(_vm.Items);
    }

    // ══════════════════════════════════════════════════════════════
    //  Dispose
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Verifies that Dispose does not throw when called multiple times.
    /// </summary>
    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var vm = new PlaylistViewModel(
            _fileService, _videoEngine, _eventBus, _dialogService, _settingsService);

        var exception = Record.Exception(() =>
        {
            vm.Dispose();
            vm.Dispose();
        });

        Assert.Null(exception);
    }

    // ══════════════════════════════════════════════════════════════
    //  PropertyChanged
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Verifies that HasItems raises PropertyChanged when items added.
    /// </summary>
    [Fact]
    public void HasItems_RaisesPropertyChanged_WhenItemAdded()
    {
        var raised = false;
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PlaylistViewModel.HasItems))
                raised = true;
        };

        _vm.AddItem(@"C:\Videos\a.mp4");

        Assert.True(raised);
    }

    /// <summary>
    /// Verifies that StatusText raises PropertyChanged.
    /// </summary>
    [Fact]
    public void StatusText_RaisesPropertyChanged()
    {
        var raised = false;
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PlaylistViewModel.StatusText))
                raised = true;
        };

        _vm.AddItem(@"C:\Videos\a.mp4");

        Assert.True(raised);
    }

    // ══════════════════════════════════════════════════════════════
    //  Helpers
    // ══════════════════════════════════════════════════════════════

    private string CreateTempFile(string name)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, "test");
        return path;
    }
}
