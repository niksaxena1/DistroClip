using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Windows.Threading;
using DistributionHelper.Models;
using DistributionHelper.Services;

namespace DistributionHelper;

public class MainWindow : Window, IComponentConnector, IStyleConnector
{
	private readonly SettingsService _settingsService;

	private readonly ReleaseIndexer _indexer = new ReleaseIndexer();

	private readonly Dictionary<string, ReleaseDetails> _detailsCache = new Dictionary<string, ReleaseDetails>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, BitmapSource?> _searchArtworkThumbnailCache = new Dictionary<string, BitmapSource>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, ReleaseReadinessResult> _searchReadinessCache = new Dictionary<string, ReleaseReadinessResult>(StringComparer.OrdinalIgnoreCase);

	private readonly OriginalArtistCacheStore _originalArtistCacheStore = new OriginalArtistCacheStore();

	private readonly Dictionary<string, OriginalArtistResult> _originalArtistCache;

	private CancellationTokenSource? _originalArtistCancellation;

	private readonly Dictionary<string, WaveformInfo> _waveformCache = new Dictionary<string, WaveformInfo>(StringComparer.OrdinalIgnoreCase);

	private CancellationTokenSource? _waveformCancellation;

	private WaveformInfo? _currentWaveformInfo;

	private CancellationTokenSource? _artistProfileCancellation;

	private LiquidGlassAdaptation? _glassAdaptation;

	private DesktopGlassLayers? _glassLayers;

	private GlassSlosh? _slosh;

	private VisualBrush? _refractionBrush;

	private LiquidLensEffect? _lensEffect;

	private bool _lensAttempted;

	private readonly ArtworkHashStore _artworkHashStore = new ArtworkHashStore();

	private readonly Dictionary<string, ArtworkHashEntry> _artworkHashes;

	private readonly object _artworkHashLock = new object();

	private CancellationTokenSource? _artworkIndexCancellation;

	private MediaPlayer? _auditionPlayer;

	private bool _isAuditionPlaying;

	private string? _auditionPath;

	private double? _pendingAuditionSeek;

	private bool _suppressCardClickOnce;

	private readonly DispatcherTimer _playheadTimer = new DispatcherTimer((DispatcherPriority)4)
	{
		Interval = TimeSpan.FromMilliseconds(150.0)
	};

	private AppSettings _settings;

	private IReadOnlyList<ReleaseSummary> _releases = Array.Empty<ReleaseSummary>();

	private ReleaseDetails? _currentDetails;

	private CancellationTokenSource? _scanCancellation;

	private CancellationTokenSource? _detailsCancellation;

	private CancellationTokenSource? _searchArtworkThumbnailsCancellation;

	private CancellationTokenSource? _searchReadinessCancellation;

	private string? _pendingReleasePath;

	private bool _suppressSearch;

	private Point _fileMouseDownPoint;

	private FrameworkElement? _fileMouseSource;

	private bool _fileDragStarted;

	private int _toastVersion;

	private readonly Dictionary<FrameworkElement, int> _copyFeedbackVersions = new Dictionary<FrameworkElement, int>();

	private readonly DispatcherTimer _snapTimer;

	private readonly DispatcherTimer _detailsScrollBarFadeTimer = new DispatcherTimer((DispatcherPriority)4)
	{
		Interval = TimeSpan.FromMilliseconds(720.0)
	};

	private bool _syncingDetailsScrollBar;

	private int _detailsScrollBarFadeVersion;

	private double _appliedInterfaceScale = 1.0;

	private bool _restoringPinnedWindow;

	private bool _splitLegalNames;

	private bool _splitSongwriterNames;

	private Button? _legalNamesSplitButton;

	private Button? _songwritersSplitButton;

	private DataTemplate? _legalNamesFullTemplate;

	private DataTemplate? _songwritersFullTemplate;

	private Style? _legalNameChipStyle;

	private Style? _songwriterChipStyle;

	private const double IslandClearance = 106.0;

	private const int SimilarArtworkThreshold = 10;

	private const double LeadSilenceWarnSeconds = 1.0;

	private const double TailSilenceWarnSeconds = 1.5;

	private const double LeadSilenceDangerSeconds = 2.0;

	private const double TailSilenceDangerSeconds = 3.0;

	private const double QuietLufsWarnThreshold = -12.0;

	private const int SwpNoSize = 1;

	private const int SwpNoMove = 2;

	private const int SwpNoActivate = 16;

	private static readonly nint HwndTopmost = new IntPtr(-1);

	internal Grid MainLayout;

	internal ScaleTransform InterfaceScaleTransform;

	internal Rectangle WallpaperUnderlay;

	internal Rectangle WallpaperMilk;

	internal Image AmbientGlowImage;

	internal Border GlassIsland;

	internal Grid IslandContent;

	internal Grid RefractionHost;

	internal Grid RefractionWarp;

	internal Rectangle IslandWallpaperRect;

	internal Rectangle RefractionRect;

	internal Border IslandSurface;

	internal Grid TitleBar;

	internal Button AttentionBadge;

	internal Button PinButton;

	internal Border SearchBorder;

	internal TextBox SearchBox;

	internal TextBlock SearchPlaceholder;

	internal Button ClearSearchButton;

	internal Popup SearchPopup;

	internal ListBox ResultsList;

	internal Grid ContentHost;

	internal StackPanel EmptyState;

	internal TextBlock EmptyTitle;

	internal TextBlock EmptyDescription;

	internal StackPanel LoadingState;

	internal ScrollViewer DetailsScroll;

	internal StackPanel DetailsStack;

	internal Border ReleaseHeaderCard;

	internal Border ReleaseStateAccent;

	internal Image HeaderArtworkImage;

	internal System.Windows.Shapes.Path HeaderArtworkFallback;

	internal TextBlock ReleaseTitleText;

	internal TextBlock ReleaseArtistsText;

	internal StackPanel ReadinessSummary;

	internal Border MasterStatusDot;

	internal Border ArtworkStatusDot;

	internal Border ContractStatusDot;

	internal Button OpenContractButton;

	internal Button CoverDetailBadge;

	internal System.Windows.Shapes.Path CoverDetailMatchCheck;

	internal Ellipse CoverDetailReviewDot;

	internal Button TrackTitleButton;

	internal TextBlock TrackTitleCopyText;

	internal System.Windows.Shapes.Path TrackTitleCopyIcon;

	internal System.Windows.Shapes.Path TrackTitleCopiedIcon;

	internal ItemsControl ArtistsItems;

	internal System.Windows.Shapes.Path ArtistsCopyIcon;

	internal System.Windows.Shapes.Path ArtistsCopiedIcon;

	internal ItemsControl PayeesItems;

	internal TextBlock NoPayeesText;

	internal Button CopyAllPayeesButton;

	internal System.Windows.Shapes.Path PayeesCopyIcon;

	internal System.Windows.Shapes.Path PayeesCopiedIcon;

	internal Grid SongwritersSection;

	internal ItemsControl SongwritersItems;

	internal TextBlock NoSongwritersText;

	internal Button CopyAllSongwritersButton;

	internal System.Windows.Shapes.Path SongwritersCopyIcon;

	internal System.Windows.Shapes.Path SongwritersCopiedIcon;

	internal StackPanel OriginalArtistSection;

	internal Button OriginalArtistChip;

	internal Button FindOriginalArtistButton;

	internal TextBlock OriginalArtistStatusText;

	internal StackPanel CreditsSection;

	internal Button CreditsButton;

	internal TextBlock CreditsText;

	internal Border ArtworkCard;

	internal Image ArtworkImage;

	internal TextBlock ArtworkFileText;

	internal Border SimilarArtworkBadge;

	internal Border AudioCard;

	internal StackPanel AudioPlaceholderHeader;

	internal Grid WaveformDisplay;

	internal System.Windows.Shapes.Path WaveformPeakPath;

	internal System.Windows.Shapes.Path WaveformRmsPath;

	internal System.Windows.Shapes.Path WaveformTruePeakPath;

	internal System.Windows.Shapes.Path WaveformClipPath;

	internal Border PlayheadLine;

	internal Border PauseBadge;

	internal TextBlock AudioMasterText;

	internal TextBlock OtherAudioVersionsText;

	internal TextBlock LufsText;

	internal TextBlock AudioFileText;

	internal TextBlock AudioDurationText;

	internal TextBlock AudioMetaText;

	internal TextBlock SilenceText;

	internal Border WarningsPanel;

	internal ItemsControl WarningsItems;

	internal ScrollBar DetailsOverlayScrollBar;

	internal TextBox ScratchpadBox;

	internal Border IndexInfoHitArea;

	internal TextBlock StatusText;

	internal Border ToastBadge;

	internal ScaleTransform ToastScale;

	internal TextBlock ToastText;

	private bool _contentLoaded;

	public MainWindow(SettingsService settingsService, AppSettings settings)
	{
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Expected O, but got Unknown
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Expected O, but got Unknown
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_012c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0145: Expected O, but got Unknown
		//IL_034e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0353: Unknown result type (might be due to invalid IL or missing references)
		InitializeComponent();
		_settingsService = settingsService;
		_settings = settings;
		_splitLegalNames = settings.SplitNamesByDefault;
		_splitSongwriterNames = settings.SplitNamesByDefault;
		_originalArtistCache = _originalArtistCacheStore.Load();
		_artworkHashes = _artworkHashStore.Load();
		_snapTimer = new DispatcherTimer((DispatcherPriority)4)
		{
			Interval = TimeSpan.FromMilliseconds(180.0)
		};
		_snapTimer.Tick += SnapTimer_Tick;
		_detailsScrollBarFadeTimer.Tick += DetailsScrollBarFadeTimer_Tick;
		_playheadTimer.Tick += PlayheadTimer_Tick;
		ThemeManager.ThemeChanged += ThemeManager_ThemeChanged;
		GelInteraction.AttachTo(ReleaseHeaderCard);
		GelInteraction.AttachTo(ArtworkCard);
		GelInteraction.AttachTo(AudioCard);
		GlassIsland.SizeChanged += delegate
		{
			UpdateRefractionViewbox();
		};
		ContentHost.SizeChanged += delegate
		{
			UpdateRefractionViewbox();
		};
		ApplyLiquidLayout();
		ApplyInterfaceScale(resizeWindow: false);
		ScratchpadBox.Text = _settings.ScratchpadText ?? string.Empty;
		bool num = settings.LayoutVersion < 2;
		double value = (num ? (410.0 * _appliedInterfaceScale) : settings.WindowWidth);
		double value2 = (num ? (610.0 * _appliedInterfaceScale) : settings.WindowHeight);
		base.Width = Math.Clamp(value, base.MinWidth, Math.Max(base.MinWidth, SystemParameters.VirtualScreenWidth));
		base.Height = Math.Clamp(value2, base.MinHeight, Math.Max(base.MinHeight, SystemParameters.VirtualScreenHeight));
		base.Topmost = settings.AlwaysOnTop;
		UpdatePinAppearance();
		if (!num)
		{
			double? windowLeft = settings.WindowLeft;
			if (windowLeft.HasValue)
			{
				double valueOrDefault = windowLeft.GetValueOrDefault();
				windowLeft = settings.WindowTop;
				if (windowLeft.HasValue)
				{
					double valueOrDefault2 = windowLeft.GetValueOrDefault();
					base.WindowStartupLocation = WindowStartupLocation.Manual;
					base.Left = Math.Clamp(valueOrDefault, SystemParameters.VirtualScreenLeft - base.Width + 140.0, SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 140.0);
					base.Top = Math.Clamp(valueOrDefault2, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 48.0);
					return;
				}
			}
		}
		Rect workArea = SystemParameters.WorkArea;
		base.WindowStartupLocation = WindowStartupLocation.Manual;
		base.Left = Math.Max(((Rect)(ref workArea)).Left, ((Rect)(ref workArea)).Right - base.Width - 16.0);
		base.Top = ((Rect)(ref workArea)).Top + 16.0;
		_settings.LayoutVersion = 2;
	}

	protected override void OnSourceInitialized(EventArgs e)
	{
		base.OnSourceInitialized(e);
		ThemeManager.ApplyWindowEffects(this);
		ThemeManager.AttachBackdropActivationHook(this);
	}

	private void ThemeManager_ThemeChanged(object? sender, EventArgs e)
	{
		UpdatePinAppearance();
		UpdateAmbientGlow();
		ApplyLiquidLayout();
		if (_currentDetails != null)
		{
			ConfigureReadiness(_currentDetails);
		}
	}

	private void ApplyLiquidLayout()
	{
		bool flag = ThemeManager.Current == AppTheme.LiquidGlass;
		Grid.SetRow(ContentHost, (!flag) ? 2 : 0);
		Grid.SetRowSpan(ContentHost, (!flag) ? 1 : 3);
		DetailsStack.Margin = (flag ? new Thickness(11.0, 106.0, 11.0, 9.0) : new Thickness(11.0, 2.0, 11.0, 9.0));
		DetailsOverlayScrollBar.Margin = (flag ? new Thickness(0.0, 106.0, 2.0, 5.0) : new Thickness(0.0, 5.0, 2.0, 5.0));
		GlassIsland.Margin = (flag ? new Thickness(7.0, 7.0, 7.0, 0.0) : new Thickness(0.0));
		GlassIsland.CornerRadius = (flag ? new CornerRadius(24.0) : new CornerRadius(0.0));
		GlassIsland.BorderThickness = (flag ? new Thickness(1.0) : new Thickness(0.0));
		RefractionHost.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		if (flag)
		{
			if (!_lensAttempted)
			{
				_lensAttempted = true;
				_lensEffect = LiquidLensEffect.TryCreate();
			}
			RefractionWarp.Effect = _lensEffect;
			if (_refractionBrush == null)
			{
				_refractionBrush = new VisualBrush
				{
					ViewboxUnits = BrushMappingMode.Absolute,
					Stretch = Stretch.Fill,
					AlignmentX = AlignmentX.Left,
					AlignmentY = AlignmentY.Top
				};
			}
			_refractionBrush.Visual = ContentHost;
			RefractionRect.Fill = _refractionBrush;
			((DispatcherObject)this).Dispatcher.BeginInvoke((DispatcherPriority)6, (Delegate)new Action(UpdateRefractionViewbox));
			if (_glassAdaptation == null)
			{
				_glassAdaptation = new LiquidGlassAdaptation(this);
			}
			_glassAdaptation.Start();
			if (_glassLayers == null)
			{
				_glassLayers = new DesktopGlassLayers(this, WallpaperUnderlay, WallpaperMilk, IslandWallpaperRect, GlassIsland, new Border[3] { ReleaseHeaderCard, ArtworkCard, AudioCard });
			}
			_glassLayers.Start();
			if (_slosh == null)
			{
				_slosh = new GlassSlosh(this);
			}
			_slosh.Target = _lensEffect;
			_slosh.Start();
		}
		else
		{
			RefractionRect.Fill = null;
			RefractionWarp.Effect = null;
			IslandContent.Clip = null;
			if (_refractionBrush != null)
			{
				_refractionBrush.Visual = null;
			}
			_glassAdaptation?.Stop();
			_glassLayers?.Stop();
			_slosh?.Stop();
		}
	}

	private void UpdateRefractionViewbox()
	{
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0189: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b5: Unknown result type (might be due to invalid IL or missing references)
		if (ThemeManager.Current == AppTheme.LiquidGlass && !(GlassIsland.ActualWidth < 1.0) && !(ContentHost.ActualWidth < 1.0))
		{
			IslandContent.Clip = new RectangleGeometry(new Rect(0.0, 0.0, IslandContent.ActualWidth, IslandContent.ActualHeight), 24.0, 24.0);
			double actualWidth = GlassIsland.ActualWidth;
			double actualHeight = GlassIsland.ActualHeight;
			if (_refractionBrush != null)
			{
				Point val = GlassIsland.TranslatePoint(new Point(0.0, 0.0), ContentHost);
				_refractionBrush.Viewbox = new Rect(((Point)(ref val)).X - 26.0, ((Point)(ref val)).Y - 26.0, actualWidth + 52.0, actualHeight + 52.0);
			}
			if (_lensEffect != null)
			{
				_lensEffect.SizePx = new Point(actualWidth, actualHeight);
				_lensEffect.Geometry = new Point(24.0, 15.0);
				_lensEffect.Inset = new Point(26.0 / (actualWidth + 52.0), 26.0 / (actualHeight + 52.0));
			}
			_slosh?.Configure(new Size(actualWidth + 52.0, actualHeight + 52.0));
			_glassLayers?.RequestUpdate();
		}
	}

	private void UpdateAmbientGlow()
	{
		AppTheme current = ThemeManager.Current;
		bool flag = (current == AppTheme.AmbientGlass || current == AppTheme.LiquidGlass) && AmbientGlowImage.Source != null;
		AmbientGlowImage.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		AmbientGlowImage.Opacity = ((!flag) ? 0.0 : ((ThemeManager.Current == AppTheme.LiquidGlass) ? 0.3 : 0.5));
	}

	private async void Window_Loaded(object sender, RoutedEventArgs e)
	{
		SearchBox.Focus();
		await ScanFoldersAsync();
	}

	private void Window_Closing(object? sender, CancelEventArgs e)
	{
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		if (_settings == null)
		{
			return;
		}
		ThemeManager.ThemeChanged -= ThemeManager_ThemeChanged;
		_snapTimer.Stop();
		_detailsScrollBarFadeTimer.Stop();
		_scanCancellation?.Cancel();
		_detailsCancellation?.Cancel();
		_searchArtworkThumbnailsCancellation?.Cancel();
		_searchReadinessCancellation?.Cancel();
		_originalArtistCancellation?.Cancel();
		_waveformCancellation?.Cancel();
		_artworkIndexCancellation?.Cancel();
		_artistProfileCancellation?.Cancel();
		_glassAdaptation?.Stop();
		_glassLayers?.Stop();
		_slosh?.Stop();
		StopAudition();
		Rect val = (Rect)((base.WindowState == WindowState.Normal) ? new Rect(base.Left, base.Top, base.ActualWidth, base.ActualHeight) : base.RestoreBounds);
		_settings.WindowLeft = ((Rect)(ref val)).Left;
		_settings.WindowTop = ((Rect)(ref val)).Top;
		_settings.WindowWidth = ((Rect)(ref val)).Width;
		_settings.WindowHeight = ((Rect)(ref val)).Height;
		_settings.AlwaysOnTop = base.Topmost;
		_settings.ScratchpadText = ScratchpadBox?.Text ?? _settings.ScratchpadText;
		try
		{
			_settingsService.Save(_settings);
		}
		catch (Exception exception)
		{
			ErrorLog.Write(exception);
		}
		try
		{
			Clipboard.Flush();
		}
		catch
		{
		}
	}

	private void Window_StateChanged(object? sender, EventArgs e)
	{
		if (!base.Topmost || base.WindowState != WindowState.Minimized || _restoringPinnedWindow)
		{
			return;
		}
		_restoringPinnedWindow = true;
		((DispatcherObject)this).Dispatcher.BeginInvoke((DispatcherPriority)2, (Delegate)(Action)delegate
		{
			try
			{
				if (base.Topmost && base.WindowState == WindowState.Minimized)
				{
					base.WindowState = WindowState.Normal;
				}
			}
			finally
			{
				_restoringPinnedWindow = false;
			}
		});
	}

	private void Window_LocationChanged(object? sender, EventArgs e)
	{
		if (base.IsLoaded && base.WindowState == WindowState.Normal)
		{
			_snapTimer.Stop();
			_snapTimer.Start();
		}
	}

	private void SnapTimer_Tick(object? sender, EventArgs e)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		_snapTimer.Stop();
		if (base.WindowState == WindowState.Normal)
		{
			Rect workArea = SystemParameters.WorkArea;
			double left = ((Rect)(ref workArea)).Left;
			double top = ((Rect)(ref workArea)).Top;
			double right = ((Rect)(ref workArea)).Right;
			double bottom = ((Rect)(ref workArea)).Bottom;
			double left2 = ((Math.Abs(base.Left - left) <= 20.0) ? left : ((Math.Abs(base.Left + base.ActualWidth - right) <= 20.0) ? (right - base.ActualWidth) : base.Left));
			double top2 = ((Math.Abs(base.Top - top) <= 20.0) ? top : ((Math.Abs(base.Top + base.ActualHeight - bottom) <= 20.0) ? (bottom - base.ActualHeight) : base.Top));
			if (!left2.Equals(base.Left))
			{
				base.Left = left2;
			}
			if (!top2.Equals(base.Top))
			{
				base.Top = top2;
			}
		}
	}

	private async Task<bool> ScanFoldersAsync()
	{
		_detailsCancellation?.Cancel();
		_searchArtworkThumbnailsCancellation?.Cancel();
		_searchArtworkThumbnailsCancellation?.Dispose();
		_searchArtworkThumbnailsCancellation = null;
		_searchReadinessCancellation?.Cancel();
		_searchReadinessCancellation?.Dispose();
		_searchReadinessCancellation = null;
		_scanCancellation?.Cancel();
		_scanCancellation?.Dispose();
		_scanCancellation = new CancellationTokenSource();
		CancellationToken token = _scanCancellation.Token;
		StatusText.Text = "Indexing release folders…";
		try
		{
			IndexResult indexResult = await _indexer.ScanAsync(_settings.SearchFolders, token);
			if (token.IsCancellationRequested)
			{
				return false;
			}
			_releases = indexResult.Releases;
			_detailsCache.Clear();
			_searchArtworkThumbnailCache.Clear();
			_searchReadinessCache.Clear();
			StatusText.Text = ((indexResult.UnavailableFolders.Count == 0) ? $"{_releases.Count:N0} releases indexed" : $"{_releases.Count:N0} releases · {indexResult.UnavailableFolders.Count} folder(s) unavailable");
			StartArtworkIndexing();
			if (!string.IsNullOrWhiteSpace(SearchBox.Text))
			{
				UpdateSearchResults();
			}
			else if (_releases.Count == 0)
			{
				EmptyTitle.Text = "No release folders found";
				EmptyDescription.Text = "Open Settings and add a parent folder that contains your release folders.";
			}
			return true;
		}
		catch (OperationCanceledException)
		{
			return false;
		}
		catch (Exception ex2)
		{
			ErrorLog.Write(ex2);
			StatusText.Text = "Indexing failed";
			EmptyTitle.Text = "Could not index the folders";
			EmptyDescription.Text = ex2.Message;
			return false;
		}
	}

	private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		SearchPlaceholder.Visibility = ((!string.IsNullOrEmpty(SearchBox.Text)) ? Visibility.Collapsed : Visibility.Visible);
		ClearSearchButton.Visibility = (string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Collapsed : Visibility.Visible);
		if (!_suppressSearch)
		{
			UpdateSearchResults();
		}
	}

	private void UpdateSearchResults()
	{
		_searchArtworkThumbnailsCancellation?.Cancel();
		_searchArtworkThumbnailsCancellation?.Dispose();
		_searchArtworkThumbnailsCancellation = null;
		_searchReadinessCancellation?.Cancel();
		_searchReadinessCancellation?.Dispose();
		_searchReadinessCancellation = null;
		IReadOnlyList<ReleaseSummary> readOnlyList = ReleaseIndexer.Search(_releases, SearchBox.Text);
		foreach (ReleaseSummary item in readOnlyList)
		{
			item.ShowSearchArtworkThumbnail = _settings.ShowSearchArtworkThumbnails;
			item.ShowSearchReadinessStatus = _settings.ValidateSearchResultsLive;
			if (_settings.ShowSearchArtworkThumbnails && _searchArtworkThumbnailCache.TryGetValue(item.FolderPath, out BitmapSource value))
			{
				item.SearchArtworkThumbnail = value;
			}
			if (_settings.ValidateSearchResultsLive && _searchReadinessCache.TryGetValue(item.FolderPath, out var value2))
			{
				item.SearchReadinessStatus = value2.Status;
				item.SearchReadinessToolTip = value2.Message;
			}
			else
			{
				item.SearchReadinessStatus = SearchReadinessStatus.Unknown;
				item.SearchReadinessToolTip = null;
			}
		}
		ResultsList.ItemsSource = readOnlyList;
		ResultsList.SelectedIndex = ((readOnlyList.Count <= 0) ? (-1) : 0);
		SearchPopup.IsOpen = !string.IsNullOrWhiteSpace(SearchBox.Text) && readOnlyList.Count > 0;
		if (_settings.ValidateSearchResultsLive && readOnlyList.Count > 0)
		{
			_searchReadinessCancellation = new CancellationTokenSource();
			ValidateSearchResultsAsync(readOnlyList, _searchReadinessCancellation.Token);
		}
		else if (_settings.ShowSearchArtworkThumbnails && readOnlyList.Count > 0)
		{
			_searchArtworkThumbnailsCancellation = new CancellationTokenSource();
			LoadSearchArtworkThumbnailsAsync(readOnlyList, _searchArtworkThumbnailsCancellation.Token);
		}
	}

	private async Task ValidateSearchResultsAsync(IReadOnlyList<ReleaseSummary> results, CancellationToken cancellationToken)
	{
		_ = 1;
		try
		{
			await Task.Delay(240, cancellationToken);
			foreach (ReleaseSummary release in results)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (_searchReadinessCache.TryGetValue(release.FolderPath, out var value))
				{
					release.SearchReadinessStatus = value.Status;
					release.SearchReadinessToolTip = value.Message;
					continue;
				}
				ReleaseReadinessResult releaseReadinessResult;
				try
				{
					if (!_detailsCache.TryGetValue(release.FolderPath, out var value2))
					{
						value2 = await ReleaseDetailsService.LoadAsync(release, cancellationToken);
						cancellationToken.ThrowIfCancellationRequested();
						_detailsCache[release.FolderPath] = value2;
					}
					releaseReadinessResult = ReleaseReadinessEvaluator.Evaluate(value2);
					if (_settings.ShowSearchArtworkThumbnails)
					{
						_searchArtworkThumbnailCache[release.FolderPath] = value2.ArtworkThumbnail;
						release.SearchArtworkThumbnail = value2.ArtworkThumbnail;
					}
				}
				catch (OperationCanceledException)
				{
					throw;
				}
				catch (Exception exception)
				{
					ErrorLog.Write(exception);
					releaseReadinessResult = new ReleaseReadinessResult(SearchReadinessStatus.NeedsAttention, "Needs attention — release files could not be inspected");
				}
				_searchReadinessCache[release.FolderPath] = releaseReadinessResult;
				release.SearchReadinessStatus = releaseReadinessResult.Status;
				release.SearchReadinessToolTip = releaseReadinessResult.Message;
			}
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception exception2)
		{
			ErrorLog.Write(exception2);
		}
	}

	private async Task LoadSearchArtworkThumbnailsAsync(IReadOnlyList<ReleaseSummary> results, CancellationToken cancellationToken)
	{
		try
		{
			await Task.Delay(160, cancellationToken);
			foreach (ReleaseSummary release in results)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (_searchArtworkThumbnailCache.TryGetValue(release.FolderPath, out BitmapSource value))
				{
					release.SearchArtworkThumbnail = value;
					continue;
				}
				ReleaseDetails value2;
				BitmapSource bitmapSource = (_detailsCache.TryGetValue(release.FolderPath, out value2) ? value2.ArtworkThumbnail : (await Task.Run(() => ReleaseDetailsService.LoadSearchArtworkThumbnail(release.FolderPath, cancellationToken), cancellationToken)));
				BitmapSource bitmapSource2 = bitmapSource;
				cancellationToken.ThrowIfCancellationRequested();
				_searchArtworkThumbnailCache[release.FolderPath] = bitmapSource2;
				release.SearchArtworkThumbnail = bitmapSource2;
			}
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception exception)
		{
			ErrorLog.Write(exception);
		}
	}

	private async void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		Key key = e.Key;
		if ((int)key <= 13)
		{
			if ((int)key != 6)
			{
				if ((int)key == 13)
				{
					SearchPopup.IsOpen = false;
					CancelSearchEnrichment();
					e.Handled = true;
				}
				return;
			}
			if (!SearchPopup.IsOpen)
			{
				UpdateSearchResults();
			}
			if (ResultsList.SelectedIndex < 0 && ResultsList.Items.Count > 0)
			{
				ResultsList.SelectedIndex = 0;
			}
			if (ResultsList.SelectedItem is ReleaseSummary release)
			{
				e.Handled = true;
				await SelectReleaseAsync(release);
			}
		}
		else if ((int)key != 24)
		{
			if ((int)key == 26)
			{
				if (!SearchPopup.IsOpen)
				{
					UpdateSearchResults();
				}
				if (ResultsList.Items.Count > 0)
				{
					ResultsList.SelectedIndex = ((ResultsList.SelectedIndex >= 0) ? Math.Min(ResultsList.SelectedIndex + 1, ResultsList.Items.Count - 1) : 0);
					ResultsList.ScrollIntoView(ResultsList.SelectedItem);
				}
				e.Handled = true;
			}
		}
		else
		{
			if (!SearchPopup.IsOpen)
			{
				UpdateSearchResults();
			}
			if (ResultsList.Items.Count > 0)
			{
				ResultsList.SelectedIndex = ((ResultsList.SelectedIndex < 0) ? (ResultsList.Items.Count - 1) : Math.Max(ResultsList.SelectedIndex - 1, 0));
				ResultsList.ScrollIntoView(ResultsList.SelectedItem);
			}
			e.Handled = true;
		}
	}

	private async void ResultsList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		ListBox resultsList = ResultsList;
		object originalSource = e.OriginalSource;
		if ((ItemsControl.ContainerFromElement(resultsList, (DependencyObject)((originalSource is DependencyObject) ? originalSource : null)) as ListBoxItem)?.DataContext is ReleaseSummary release)
		{
			await SelectReleaseAsync(release);
		}
	}

	private async Task SelectReleaseAsync(ReleaseSummary release)
	{
		_pendingReleasePath = release.FolderPath;
		SearchPopup.IsOpen = false;
		CancelSearchEnrichment();
		_suppressSearch = true;
		SearchBox.Text = release.FolderName;
		SearchBox.CaretIndex = SearchBox.Text.Length;
		SearchBox.SelectAll();
		_suppressSearch = false;
		_detailsCancellation?.Cancel();
		_detailsCancellation?.Dispose();
		_detailsCancellation = new CancellationTokenSource();
		CancellationToken token = _detailsCancellation.Token;
		EmptyState.Visibility = Visibility.Collapsed;
		DetailsScroll.Visibility = Visibility.Collapsed;
		HideDetailsOverlayScrollBar();
		LoadingState.Visibility = Visibility.Visible;
		StatusText.Text = "Reading contract and deliverables…";
		try
		{
			if (!_detailsCache.TryGetValue(release.FolderPath, out var value))
			{
				value = await ReleaseDetailsService.LoadAsync(release, token);
				token.ThrowIfCancellationRequested();
				_detailsCache[release.FolderPath] = value;
			}
			token.ThrowIfCancellationRequested();
			_currentDetails = value;
			_pendingReleasePath = null;
			RenderDetails(value);
			StatusText.Text = $"Ready · {_releases.Count:N0} releases indexed";
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception ex2)
		{
			if (_pendingReleasePath?.Equals(release.FolderPath, StringComparison.OrdinalIgnoreCase) ?? false)
			{
				_pendingReleasePath = null;
			}
			ErrorLog.Write(ex2);
			LoadingState.Visibility = Visibility.Collapsed;
			EmptyState.Visibility = Visibility.Visible;
			EmptyTitle.Text = "Could not read this release";
			EmptyDescription.Text = ex2.Message;
			StatusText.Text = "Release read failed";
		}
	}

	private void RenderDetails(ReleaseDetails details)
	{
		LoadingState.Visibility = Visibility.Collapsed;
		EmptyState.Visibility = Visibility.Collapsed;
		DetailsScroll.Visibility = Visibility.Visible;
		DetailsScroll.ScrollToTop();
		ReleaseTitleText.Text = details.Summary.TrackTitle;
		ReleaseArtistsText.Text = details.Summary.ArtistsText;
		TrackTitleCopyText.Text = details.Summary.TrackTitle;
		ArtistChipItem[] array = details.Summary.Artists.Select((string artist) => new ArtistChipItem(artist)).ToArray();
		ArtistsItems.ItemsSource = array;
		_artistProfileCancellation?.Cancel();
		_artistProfileCancellation?.Dispose();
		_artistProfileCancellation = new CancellationTokenSource();
		LoadArtistProfilesAsync(array, _artistProfileCancellation.Token);
		RenderLegalNames(details);
		NoPayeesText.Visibility = ((details.Payees.Count != 0) ? Visibility.Collapsed : Visibility.Visible);
		CopyAllPayeesButton.Visibility = ((details.Payees.Count <= 0) ? Visibility.Collapsed : Visibility.Visible);
		CoverDetailBadge.Visibility = ((!details.IsCover) ? Visibility.Collapsed : Visibility.Visible);
		CoverDetailBadge.Tag = details.LicensePath;
		ConfigureCoverMetadataStatus(details);
		SongwritersSection.Visibility = ((!details.IsCover) ? Visibility.Collapsed : Visibility.Visible);
		RenderSongwriters(details);
		NoSongwritersText.Visibility = ((!details.IsCover || details.Songwriters.Count != 0) ? Visibility.Collapsed : Visibility.Visible);
		CopyAllSongwritersButton.Visibility = ((details.Songwriters.Count <= 0) ? Visibility.Collapsed : Visibility.Visible);
		ConfigureOriginalArtistSection(details);
		CreditsSection.Visibility = (string.IsNullOrWhiteSpace(details.Credits) ? Visibility.Collapsed : Visibility.Visible);
		CreditsText.Text = details.Credits ?? string.Empty;
		ConfigureArtwork(details.ArtworkPath, details.ArtworkThumbnail);
		ConfigureAudio(details.AudioPath, details.AudioFileSize, details.AudioDuration, details.OtherAudioVersions);
		ConfigureReadiness(details);
		WarningsItems.ItemsSource = details.Warnings;
		WarningsPanel.Visibility = ((details.Warnings.Count <= 0) ? Visibility.Collapsed : Visibility.Visible);
		AttentionBadge.Content = $"● {details.Warnings.Count}";
		AttentionBadge.ToolTip = ((details.Warnings.Count == 1) ? "1 item needs attention" : $"{details.Warnings.Count} items need attention");
		AttentionBadge.Visibility = ((details.Warnings.Count <= 0) ? Visibility.Collapsed : Visibility.Visible);
		ReleaseHeaderCard.ToolTip = "Open " + details.Summary.FolderPath;
		OpenContractButton.Tag = details.ContractPath;
		OpenContractButton.Visibility = ((details.ContractPath == null) ? Visibility.Collapsed : Visibility.Visible);
		((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)new Action(ConfigureNameSplitToggles), (DispatcherPriority)6, Array.Empty<object>());
	}

	private void ConfigureCoverMetadataStatus(ReleaseDetails details)
	{
		CoverDetailMatchCheck.Visibility = Visibility.Collapsed;
		CoverDetailReviewDot.Visibility = Visibility.Collapsed;
		if (details.IsCover)
		{
			CoverMetadataCheck coverMetadataCheck = details.CoverMetadataCheck;
			string text = coverMetadataCheck?.Message ?? "Proof artist or title could not be verified";
			CoverDetailBadge.ToolTip = text + "\nClick to open Proof of Licensing";
			if ((object)coverMetadataCheck != null && coverMetadataCheck.Status == CoverMetadataStatus.Matches)
			{
				CoverDetailMatchCheck.Visibility = Visibility.Visible;
			}
			else
			{
				CoverDetailReviewDot.Visibility = Visibility.Visible;
			}
		}
	}

	private void ConfigureOriginalArtistSection(ReleaseDetails details)
	{
		_originalArtistCancellation?.Cancel();
		OriginalArtistSection.Visibility = ((!details.IsCover) ? Visibility.Collapsed : Visibility.Visible);
		OriginalArtistChip.Visibility = Visibility.Collapsed;
		OriginalArtistStatusText.Visibility = Visibility.Collapsed;
		FindOriginalArtistButton.Visibility = Visibility.Collapsed;
		if (details.IsCover)
		{
			if (_originalArtistCache.TryGetValue(OriginalArtistCacheKey(details), out var value) && value.ArtistCredit != null)
			{
				ShowOriginalArtistResult(value);
			}
			else
			{
				LookUpOriginalArtistAsync(details);
			}
		}
	}

	private static string OriginalArtistCacheKey(ReleaseDetails details)
	{
		return TextNormalizer.ForSearch(details.CoverMetadataCheck?.LicensedTitle ?? details.Summary.TrackTitle) + "|" + string.Join(",", details.Songwriters.Select(TextNormalizer.ForSearch));
	}

	private async Task LookUpOriginalArtistAsync(ReleaseDetails details)
	{
		_originalArtistCancellation?.Cancel();
		_originalArtistCancellation?.Dispose();
		_originalArtistCancellation = new CancellationTokenSource();
		CancellationToken token = _originalArtistCancellation.Token;
		string folderPath = details.Summary.FolderPath;
		string songTitle = details.CoverMetadataCheck?.LicensedTitle ?? details.Summary.TrackTitle;
		OriginalArtistStatusText.Text = "Looking up original artist…";
		OriginalArtistStatusText.Visibility = Visibility.Visible;
		try
		{
			OriginalArtistResult originalArtistResult = await OriginalArtistLookup.FindAsync(songTitle, details.Songwriters, token);
			if (token.IsCancellationRequested)
			{
				return;
			}
			ReleaseDetails currentDetails = _currentDetails;
			if (currentDetails != null && currentDetails.Summary.FolderPath.Equals(folderPath, StringComparison.OrdinalIgnoreCase))
			{
				if (originalArtistResult.ArtistCredit != null)
				{
					_originalArtistCache[OriginalArtistCacheKey(details)] = originalArtistResult;
					_originalArtistCacheStore.Save(_originalArtistCache);
					ShowOriginalArtistResult(originalArtistResult);
				}
				else
				{
					OriginalArtistStatusText.Text = originalArtistResult.Error;
					OriginalArtistStatusText.Visibility = Visibility.Visible;
					FindOriginalArtistButton.Visibility = Visibility.Visible;
				}
			}
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception exception)
		{
			ErrorLog.Write(exception);
		}
	}

	private void ShowOriginalArtistResult(OriginalArtistResult result)
	{
		OriginalArtistChip.Content = result.ArtistCredit;
		OriginalArtistChip.Tag = result.ArtistCredit;
		OriginalArtistChip.ToolTip = ((result.FirstReleaseDate == null) ? ("“" + result.MatchedTitle + "” (MusicBrainz) — click to copy") : $"“{result.MatchedTitle}”, first released {result.FirstReleaseDate} (MusicBrainz) — click to copy");
		OriginalArtistChip.Visibility = Visibility.Visible;
		FindOriginalArtistButton.Visibility = Visibility.Collapsed;
		OriginalArtistStatusText.Visibility = Visibility.Collapsed;
	}

	private void FindOriginalArtist_Click(object sender, RoutedEventArgs e)
	{
		ReleaseDetails currentDetails = _currentDetails;
		if (currentDetails != null && currentDetails.IsCover)
		{
			string value = currentDetails.CoverMetadataCheck?.LicensedTitle ?? currentDetails.Summary.TrackTitle;
			string stringToEscape = $"\"{value}\" {string.Join(' ', currentDetails.Songwriters)} original artist";
			OpenPath("https://www.google.com/search?q=" + Uri.EscapeDataString(stringToEscape));
		}
	}

	private void DetailsScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
	{
		if (ThemeManager.Current == AppTheme.LiquidGlass && e.VerticalChange != 0.0)
		{
			_slosh?.NudgeScroll(e.VerticalChange);
			_glassLayers?.NotifyScrolled();
		}
		if (DetailsOverlayScrollBar != null)
		{
			_syncingDetailsScrollBar = true;
			try
			{
				DetailsOverlayScrollBar.Maximum = Math.Max(0.0, DetailsScroll.ScrollableHeight);
				DetailsOverlayScrollBar.ViewportSize = Math.Max(0.0, DetailsScroll.ViewportHeight);
				DetailsOverlayScrollBar.Value = Math.Clamp(DetailsScroll.VerticalOffset, DetailsOverlayScrollBar.Minimum, DetailsOverlayScrollBar.Maximum);
			}
			finally
			{
				_syncingDetailsScrollBar = false;
			}
			if (DetailsOverlayScrollBar.Maximum <= 0.0)
			{
				HideDetailsOverlayScrollBar();
			}
			else if (Math.Abs(e.VerticalChange) > 0.01)
			{
				ShowDetailsOverlayScrollBar();
			}
		}
	}

	private void DetailsOverlayScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!_syncingDetailsScrollBar && DetailsScroll != null)
		{
			DetailsScroll.ScrollToVerticalOffset(e.NewValue);
			ShowDetailsOverlayScrollBar();
		}
	}

	private void DetailsOverlayScrollBar_MouseEnter(object sender, MouseEventArgs e)
	{
		ShowDetailsOverlayScrollBar();
		_detailsScrollBarFadeTimer.Stop();
	}

	private void DetailsOverlayScrollBar_MouseLeave(object sender, MouseEventArgs e)
	{
		if (DetailsOverlayScrollBar.Maximum > 0.0)
		{
			_detailsScrollBarFadeTimer.Stop();
			_detailsScrollBarFadeTimer.Start();
		}
	}

	private void ShowDetailsOverlayScrollBar()
	{
		if (DetailsOverlayScrollBar != null && !(DetailsOverlayScrollBar.Maximum <= 0.0))
		{
			_detailsScrollBarFadeVersion++;
			_detailsScrollBarFadeTimer.Stop();
			DetailsOverlayScrollBar.BeginAnimation(UIElement.OpacityProperty, null);
			DetailsOverlayScrollBar.Visibility = Visibility.Visible;
			DetailsOverlayScrollBar.IsHitTestVisible = true;
			DetailsOverlayScrollBar.Opacity = 0.58;
			_detailsScrollBarFadeTimer.Start();
		}
	}

	private void DetailsScrollBarFadeTimer_Tick(object? sender, EventArgs e)
	{
		_detailsScrollBarFadeTimer.Stop();
		if (DetailsOverlayScrollBar.IsMouseOver)
		{
			return;
		}
		int fadeVersion = ++_detailsScrollBarFadeVersion;
		DoubleAnimation doubleAnimation = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(180.0))
		{
			EasingFunction = new QuadraticEase
			{
				EasingMode = EasingMode.EaseOut
			}
		};
		doubleAnimation.Completed += delegate
		{
			if (fadeVersion == _detailsScrollBarFadeVersion && !DetailsOverlayScrollBar.IsMouseOver)
			{
				DetailsOverlayScrollBar.Visibility = Visibility.Collapsed;
				DetailsOverlayScrollBar.IsHitTestVisible = false;
			}
		};
		DetailsOverlayScrollBar.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
	}

	private void HideDetailsOverlayScrollBar()
	{
		if (DetailsOverlayScrollBar != null)
		{
			_detailsScrollBarFadeVersion++;
			_detailsScrollBarFadeTimer.Stop();
			DetailsOverlayScrollBar.BeginAnimation(UIElement.OpacityProperty, null);
			DetailsOverlayScrollBar.Opacity = 0.0;
			DetailsOverlayScrollBar.IsHitTestVisible = false;
			DetailsOverlayScrollBar.Visibility = Visibility.Collapsed;
		}
	}

	private void ConfigureArtwork(string? path, BitmapSource? thumbnail)
	{
		ArtworkCard.Tag = path;
		ArtworkCard.Cursor = ((path == null) ? Cursors.Arrow : Cursors.Hand);
		ArtworkCard.ToolTip = ((path == null) ? "No artwork found" : path);
		ArtworkFileText.Text = ((path == null) ? "Not found" : System.IO.Path.GetFileName(path));
		ArtworkImage.Source = null;
		ArtworkImage.Visibility = Visibility.Collapsed;
		HeaderArtworkImage.Source = null;
		HeaderArtworkImage.Visibility = Visibility.Collapsed;
		HeaderArtworkFallback.Visibility = Visibility.Visible;
		AmbientGlowImage.Source = null;
		UpdateAmbientGlow();
		SimilarArtworkBadge.Visibility = Visibility.Collapsed;
		if (path != null)
		{
			CheckArtworkSimilarityAsync(path);
		}
		if (path == null || thumbnail == null)
		{
			if (path != null)
			{
				ArtworkCard.ToolTip = path;
			}
			return;
		}
		ArtworkImage.Source = thumbnail;
		ArtworkImage.Visibility = Visibility.Visible;
		HeaderArtworkImage.Source = thumbnail;
		HeaderArtworkImage.Visibility = Visibility.Visible;
		HeaderArtworkFallback.Visibility = Visibility.Collapsed;
		AmbientGlowImage.Source = thumbnail;
		UpdateAmbientGlow();
	}

	private void ConfigureAudio(string? path, long? fileSize, TimeSpan? duration, IReadOnlyList<string> otherVersions)
	{
		AudioCard.Tag = path;
		AudioCard.Cursor = ((path == null) ? Cursors.Arrow : Cursors.Hand);
		AudioCard.ToolTip = ((path == null) ? "No M-numbered WAV found" : path);
		AudioMasterText.Text = ((path == null) ? "—" : (ReleaseDetailsService.GetMasterVersion(System.IO.Path.GetFileName(path))?.ToString() ?? "WAV"));
		OtherAudioVersionsText.Text = ((otherVersions.Count == 0) ? string.Empty : string.Join("  ", otherVersions));
		AudioFileText.Text = ((path == null) ? "Not found" : System.IO.Path.GetFileName(path));
		AudioDurationText.Text = ((path == null || !duration.HasValue) ? string.Empty : FormatDuration(duration.Value));
		AudioMetaText.Text = ((path == null || !fileSize.HasValue) ? string.Empty : ((!duration.HasValue) ? FormatBytes(fileSize.Value) : (" • " + FormatBytes(fileSize.Value))));
		UpdateDurationFlag(duration?.TotalSeconds);
		ConfigureWaveform(path);
	}

	private void UpdateDurationFlag(double? seconds)
	{
		int num;
		if (seconds.HasValue)
		{
			double valueOrDefault = seconds.GetValueOrDefault();
			num = ((valueOrDefault <= 60.0 || valueOrDefault >= 240.0) ? 1 : 0);
		}
		else
		{
			num = 0;
		}
		bool flag = (byte)num != 0;
		int num2;
		if (seconds.HasValue)
		{
			double valueOrDefault2 = seconds.GetValueOrDefault();
			num2 = ((valueOrDefault2 <= 120.0 || valueOrDefault2 >= 180.0) ? 1 : 0);
		}
		else
		{
			num2 = 0;
		}
		bool flag2 = (byte)num2 != 0;
		AudioDurationText.SetResourceReference(Control.ForegroundProperty, flag ? "DangerBrush" : (flag2 ? "CoverBrush" : "MutedTextBrush"));
		AudioDurationText.ToolTip = (flag ? "Track length far outside the usual 2:00–3:00 range — check the file" : (flag2 ? "Track length outside the usual 2:00–3:00 range" : null));
	}

	private void StartArtworkIndexing()
	{
		_artworkIndexCancellation?.Cancel();
		_artworkIndexCancellation?.Dispose();
		_artworkIndexCancellation = new CancellationTokenSource();
		IndexArtworkHashesAsync(_releases, _artworkIndexCancellation.Token);
	}

	private async Task IndexArtworkHashesAsync(IReadOnlyList<ReleaseSummary> releases, CancellationToken token)
	{
		try
		{
			int updates = 0;
			foreach (ReleaseSummary release in releases)
			{
				token.ThrowIfCancellationRequested();
				if (await Task.Run(() => RefreshArtworkHash(release.FolderPath, token), token))
				{
					int num = updates + 1;
					updates = num;
					if (num % 25 == 0)
					{
						_artworkHashStore.Save(SnapshotArtworkHashes());
					}
				}
			}
			if (updates > 0)
			{
				_artworkHashStore.Save(SnapshotArtworkHashes());
			}
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception exception)
		{
			ErrorLog.Write(exception);
		}
	}

	private bool RefreshArtworkHash(string folderPath, CancellationToken token)
	{
		string text = ReleaseDetailsService.FindArtworkPath(folderPath, token);
		if (text == null)
		{
			return false;
		}
		FileInfo fileInfo;
		try
		{
			fileInfo = new FileInfo(text);
			if (!fileInfo.Exists)
			{
				return false;
			}
		}
		catch
		{
			return false;
		}
		lock (_artworkHashLock)
		{
			if (_artworkHashes.TryGetValue(text, out var value) && value.FileLength == fileInfo.Length && value.LastWriteUtc == fileInfo.LastWriteTimeUtc)
			{
				return false;
			}
		}
		ulong? num = ArtworkHasher.ComputeHash(text, token);
		if (num.HasValue)
		{
			ulong valueOrDefault = num.GetValueOrDefault();
			lock (_artworkHashLock)
			{
				_artworkHashes[text] = new ArtworkHashEntry(valueOrDefault, fileInfo.Length, fileInfo.LastWriteTimeUtc);
			}
			return true;
		}
		return false;
	}

	private Dictionary<string, ArtworkHashEntry> SnapshotArtworkHashes()
	{
		lock (_artworkHashLock)
		{
			return new Dictionary<string, ArtworkHashEntry>(_artworkHashes, StringComparer.OrdinalIgnoreCase);
		}
	}

	private async Task CheckArtworkSimilarityAsync(string artworkPath)
	{
		try
		{
			string folder = System.IO.Path.GetDirectoryName(artworkPath) ?? string.Empty;
			(string, int) obj = await Task.Run(delegate
			{
				RefreshArtworkHash(folder, CancellationToken.None);
				ulong hash;
				lock (_artworkHashLock)
				{
					if (!_artworkHashes.TryGetValue(artworkPath, out var value))
					{
						return ((string, int))(null, 0);
					}
					hash = value.Hash;
				}
				string text = null;
				int num = int.MaxValue;
				lock (_artworkHashLock)
				{
					foreach (var (text3, artworkHashEntry2) in _artworkHashes)
					{
						if (!text3.StartsWith(folder + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
						{
							int num2 = ArtworkHasher.HammingDistance(hash, artworkHashEntry2.Hash);
							if (num2 < num)
							{
								num = num2;
								text = text3;
							}
						}
					}
				}
				return ((num <= 10) ? text : null, num);
			});
			string item = obj.Item1;
			int item2 = obj.Item2;
			ReleaseDetails currentDetails = _currentDetails;
			if (currentDetails != null && (currentDetails.ArtworkPath?.Equals(artworkPath, StringComparison.OrdinalIgnoreCase) ?? false) && item != null)
			{
				string directoryName = System.IO.Path.GetDirectoryName(item);
				SimilarArtworkBadge.Tag = directoryName;
				SimilarArtworkBadge.ToolTip = $"Artwork looks like {System.IO.Path.GetFileName(directoryName)} (difference {item2}/64) — click to open that folder";
				SimilarArtworkBadge.Visibility = Visibility.Visible;
			}
		}
		catch (Exception exception)
		{
			ErrorLog.Write(exception);
		}
	}

	private void SimilarArtworkBadge_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		e.Handled = true;
		if (SimilarArtworkBadge.Tag is string path)
		{
			OpenPath(path);
		}
	}

	private void ConfigureWaveform(string? path)
	{
		_waveformCancellation?.Cancel();
		StopAudition();
		_currentWaveformInfo = null;
		WaveformDisplay.Visibility = Visibility.Collapsed;
		AudioPlaceholderHeader.Visibility = Visibility.Visible;
		LufsText.Text = string.Empty;
		SilenceText.Text = string.Empty;
		if (_settings.ShowRealWaveform && path != null)
		{
			if (_waveformCache.TryGetValue(path, out var value))
			{
				ShowWaveform(value);
				return;
			}
			_waveformCancellation?.Dispose();
			_waveformCancellation = new CancellationTokenSource();
			CancellationToken token = _waveformCancellation.Token;
			LoadWaveformAsync(path, token);
		}
	}

	private async Task LoadWaveformAsync(string path, CancellationToken token)
	{
		try
		{
			WaveformInfo waveformInfo = await Task.Run(() => WaveformAnalyzer.Analyze(path, 140, token), token);
			if ((object)waveformInfo != null && !token.IsCancellationRequested)
			{
				_waveformCache[path] = waveformInfo;
				ReleaseDetails currentDetails = _currentDetails;
				if (currentDetails != null && (currentDetails.AudioPath?.Equals(path, StringComparison.OrdinalIgnoreCase) ?? false) && _settings.ShowRealWaveform)
				{
					ShowWaveform(waveformInfo);
				}
			}
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception exception)
		{
			ErrorLog.Write(exception);
		}
	}

	private void ShowWaveform(WaveformInfo info)
	{
		_currentWaveformInfo = info;
		WaveformPeakPath.Data = BuildWaveformGeometry(info.Peaks);
		System.Windows.Shapes.Path waveformRmsPath = WaveformRmsPath;
		waveformRmsPath.Data = BuildWaveformGeometry(_settings.WaveformInnerMode switch
		{
			"TrueRms" => info.Rms, 
			"ShortTermLufs" => BuildLoudnessDisplay(info), 
			"Crest" => BuildCrestDisplay(info), 
			"Brightness" => BuildBrightnessDisplay(info), 
			_ => BuildRmsDisplay(info), 
		});
		Grid waveformDisplay = WaveformDisplay;
		waveformDisplay.ToolTip = "Waveform of the master WAV — outline is peak level, solid core is " + _settings.WaveformInnerMode switch
		{
			"ShortTermLufs" => "short-term loudness (K-weighted)", 
			"Crest" => "crest factor (taller = punchier, shorter = more limited)", 
			"Brightness" => "brightness (estimated dominant frequency, log scale)", 
			"TrueRms" => "RMS energy (true proportions)", 
			_ => "RMS energy", 
		} + ". Ctrl+click to listen from that spot.";
		bool flag = info.ClippedBuckets.Any((bool clipped) => clipped);
		WaveformClipPath.Data = (flag ? BuildCapGeometry(info.Peaks, info.ClippedBuckets, null) : null);
		WaveformClipPath.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		bool[] array = new bool[info.TruePeakOverBuckets.Length];
		for (int num = 0; num < array.Length; num++)
		{
			array[num] = info.TruePeakOverBuckets[num] && !info.ClippedBuckets[num];
		}
		bool flag2 = array.Any((bool over) => over);
		WaveformTruePeakPath.Data = (flag2 ? BuildCapGeometry(info.Peaks, array, info.ClippedBuckets) : null);
		WaveformTruePeakPath.Visibility = ((!flag2) ? Visibility.Collapsed : Visibility.Visible);
		WaveformDisplay.Visibility = Visibility.Visible;
		AudioPlaceholderHeader.Visibility = Visibility.Collapsed;
		double? integratedLufs = info.IntegratedLufs;
		if (integratedLufs.HasValue)
		{
			double valueOrDefault = integratedLufs.GetValueOrDefault();
			bool flag3 = info.SamplePeakDb >= -0.001;
			bool flag4 = info.TruePeakDb > 0.0;
			bool flag5 = valueOrDefault < -12.0;
			LufsText.Text = $"{valueOrDefault:0.0} LUFS";
			string text = $"Integrated loudness (BS.1770): {valueOrDefault:0.0} LUFS · sample peak {info.SamplePeakDb:0.00} dBFS · true peak {info.TruePeakDb:0.00} dBTP";
			LufsText.ToolTip = (flag3 ? (text + " — samples at/over full scale (clipping; red marks show where)") : (flag4 ? (text + " — intersample peaks exceed 0 dBTP (amber marks show where)") : (flag5 ? (text + " — quieter than this library's usual −7 to −11 LUFS") : text)));
			LufsText.SetResourceReference(Control.ForegroundProperty, flag3 ? "DangerBrush" : ((flag4 || flag5) ? "CoverBrush" : "MutedTextBrush"));
		}
		else
		{
			LufsText.Text = string.Empty;
		}
		if (AudioDurationText.Text.Length == 0)
		{
			AudioDurationText.Text = FormatDuration(TimeSpan.FromSeconds(info.DurationSeconds));
			UpdateDurationFlag(info.DurationSeconds);
		}
		SilenceText.Text = FormatSeconds(info.LeadingSilenceSeconds) + " / " + FormatSeconds(info.TrailingSilenceSeconds);
		bool flag6 = info.LeadingSilenceSeconds > 2.0 || info.TrailingSilenceSeconds > 3.0;
		bool flag7 = info.LeadingSilenceSeconds > 1.0 || info.TrailingSilenceSeconds > 1.5;
		SilenceText.SetResourceReference(Control.ForegroundProperty, flag6 ? "DangerBrush" : (flag7 ? "CoverBrush" : "MutedTextBrush"));
		SilenceText.ToolTip = (flag6 ? "Silence at the start / end of the file — far beyond normal, the export is probably wrong" : (flag7 ? "Silence at the start / end of the file — unusually long, check the export" : "Silence at the start / end of the file"));
	}

	private static string FormatSeconds(double seconds)
	{
		if (!(seconds < 0.05))
		{
			return $"{seconds:0.0}s";
		}
		return "0s";
	}

	private static Geometry BuildWaveformGeometry(IReadOnlyList<double> peaks)
	{
		return BuildBarGeometry(peaks, (int index) => true);
	}

	private static double[] BuildLoudnessDisplay(WaveformInfo info)
	{
		double[] array = new double[info.ShortTermLufs.Length];
		double num = info.ShortTermLufs.Max();
		if (num <= -69.0)
		{
			return array;
		}
		for (int i = 0; i < array.Length; i++)
		{
			double val = Math.Clamp((info.ShortTermLufs[i] - (num - 25.0)) / 25.0, 0.0, 1.0);
			array[i] = Math.Min(info.Peaks[i], val);
		}
		return array;
	}

	private static double[] BuildCrestDisplay(WaveformInfo info)
	{
		double[] array = new double[info.Peaks.Length];
		for (int i = 0; i < array.Length; i++)
		{
			if (info.Rms[i] > 0.0001 && info.Peaks[i] > 0.0001)
			{
				double num = 20.0 * Math.Log10(info.Peaks[i] / info.Rms[i]);
				array[i] = Math.Clamp((num - 2.0) / 16.0, 0.0, 1.0);
			}
		}
		return array;
	}

	private static double[] BuildBrightnessDisplay(WaveformInfo info)
	{
		double num = Math.Log2(80.0);
		double num2 = Math.Log2(8000.0);
		double[] array = new double[info.BrightnessHz.Length];
		for (int i = 0; i < array.Length; i++)
		{
			if (info.BrightnessHz[i] > 1.0)
			{
				array[i] = Math.Clamp((Math.Log2(info.BrightnessHz[i]) - num) / (num2 - num), 0.0, 1.0);
			}
		}
		return array;
	}

	private static double[] BuildRmsDisplay(WaveformInfo info)
	{
		double[] array = new double[info.Rms.Length];
		double num = info.Rms.Max();
		if (num <= 0.0)
		{
			return array;
		}
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = Math.Min(info.Peaks[i], Math.Pow(info.Rms[i] / num, 1.25));
		}
		return array;
	}

	private static Geometry BuildCapGeometry(IReadOnlyList<double> peaks, bool[] marks, bool[]? blocked)
	{
		//IL_0180: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a7: Unknown result type (might be due to invalid IL or missing references)
		double num = 0.19;
		StreamGeometry streamGeometry = new StreamGeometry();
		using (StreamGeometryContext context = streamGeometry.Open())
		{
			for (int i = 0; i < peaks.Count; i++)
			{
				if (!marks[i])
				{
					continue;
				}
				double num2 = Math.Max(1.6, peaks[i] * 49.0);
				double num3 = Math.Min(6.0, num2);
				bool num4 = (i == 0 || !Occupied(i - 1)) && (i == peaks.Count - 1 || !Occupied(i + 1));
				double left = (double)i + num;
				double width = 0.62;
				if (!num4)
				{
					goto IL_00de;
				}
				int num5;
				if (i > 0)
				{
					bool[] array = blocked;
					if (array == null || !array[i - 1])
					{
						num5 = i - 1;
						goto IL_0113;
					}
				}
				num5 = i;
				goto IL_0113;
				IL_00de:
				AddRect(context, left, 50.0 - num2, width, num3);
				AddRect(context, left, 50.0 + num2 - num3, width, num3);
				continue;
				IL_0113:
				int num6 = num5;
				int num7;
				if (i < peaks.Count - 1)
				{
					bool[] array2 = blocked;
					if (array2 == null || !array2[i + 1])
					{
						num7 = i + 1;
						goto IL_0144;
					}
				}
				num7 = i;
				goto IL_0144;
				IL_0144:
				left = (double)num6 + num;
				width = (double)(num7 - num6) + 0.62;
				goto IL_00de;
			}
			AddBoundsAnchor(context, new Point(num, 1.0));
			AddBoundsAnchor(context, new Point((double)peaks.Count - num - 0.02, 98.98));
		}
		((Freezable)streamGeometry).Freeze();
		return streamGeometry;
		bool Occupied(int index)
		{
			if (!marks[index])
			{
				bool[] array3 = blocked;
				if (array3 == null)
				{
					return false;
				}
				return array3[index];
			}
			return true;
		}
	}

	private static void AddRect(StreamGeometryContext context, double left, double top, double width, double height)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		context.BeginFigure(new Point(left, top), isFilled: true, isClosed: true);
		context.LineTo(new Point(left + width, top), isStroked: true, isSmoothJoin: false);
		context.LineTo(new Point(left + width, top + height), isStroked: true, isSmoothJoin: false);
		context.LineTo(new Point(left, top + height), isStroked: true, isSmoothJoin: false);
	}

	private static Geometry BuildBarGeometry(IReadOnlyList<double> peaks, Func<int, bool> includeBar)
	{
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		double num = 0.19;
		StreamGeometry streamGeometry = new StreamGeometry();
		using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
		{
			for (int i = 0; i < peaks.Count; i++)
			{
				if (includeBar(i))
				{
					double num2 = Math.Max(1.6, peaks[i] * 49.0);
					double num3 = (double)i + num;
					streamGeometryContext.BeginFigure(new Point(num3, 50.0 - num2), isFilled: true, isClosed: true);
					streamGeometryContext.LineTo(new Point(num3 + 0.62, 50.0 - num2), isStroked: true, isSmoothJoin: false);
					streamGeometryContext.LineTo(new Point(num3 + 0.62, 50.0 + num2), isStroked: true, isSmoothJoin: false);
					streamGeometryContext.LineTo(new Point(num3, 50.0 + num2), isStroked: true, isSmoothJoin: false);
				}
			}
			AddBoundsAnchor(streamGeometryContext, new Point(num, 1.0));
			AddBoundsAnchor(streamGeometryContext, new Point((double)peaks.Count - num - 0.02, 98.98));
		}
		((Freezable)streamGeometry).Freeze();
		return streamGeometry;
	}

	private static void AddBoundsAnchor(StreamGeometryContext context, Point corner)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		context.BeginFigure(corner, isFilled: false, isClosed: true);
		context.LineTo(new Point(((Point)(ref corner)).X + 0.02, ((Point)(ref corner)).Y), isStroked: false, isSmoothJoin: false);
		context.LineTo(new Point(((Point)(ref corner)).X + 0.02, ((Point)(ref corner)).Y + 0.02), isStroked: false, isSmoothJoin: false);
	}

	private void ConfigureReadiness(ReleaseDetails details)
	{
		SetReadinessDot(MasterStatusDot, details.AudioPath != null, "Master WAV");
		SetReadinessDot(ArtworkStatusDot, details.ArtworkPath != null, "Artwork");
		SetReadinessDot(ContractStatusDot, details.ContractPath != null, "Contract");
		ReadinessSummary.ToolTip = $"Master: {((details.AudioPath == null) ? "missing" : "ready")} · Artwork: {((details.ArtworkPath == null) ? "missing" : "ready")} · Contract: {((details.ContractPath == null) ? "missing" : "ready")}";
		ReleaseStateAccent.Background = ((details.Warnings.Count == 0) ? ((Brush)FindResource("TealBrush")) : ((Brush)FindResource("CoverBrush")));
	}

	private void SetReadinessDot(Border dot, bool ready, string label)
	{
		dot.Background = (Brush)FindResource(ready ? "TealBrush" : "CoverBrush");
		dot.ToolTip = (ready ? (label + " ready") : (label + " missing"));
	}

	private async Task LoadArtistProfilesAsync(IReadOnlyList<ArtistChipItem> chips, CancellationToken cancellationToken)
	{
		foreach (ArtistChipItem chip in chips)
		{
			try
			{
				string[] coArtists = (from other in chips
					where other != chip
					select other.Value).ToArray();
				SpotifyArtistProfile spotifyArtistProfile = await SpotifyArtistService.LookupAsync(chip.Value, coArtists, cancellationToken);
				if (!cancellationToken.IsCancellationRequested && (object)spotifyArtistProfile != null)
				{
					chip.ProfileUrl = spotifyArtistProfile.ProfileUrl;
					if (spotifyArtistProfile.ThumbnailPath != null && File.Exists(spotifyArtistProfile.ThumbnailPath))
					{
						BitmapImage bitmapImage = new BitmapImage();
						bitmapImage.BeginInit();
						bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
						bitmapImage.DecodePixelWidth = 32;
						bitmapImage.UriSource = new Uri(spotifyArtistProfile.ThumbnailPath);
						bitmapImage.EndInit();
						((Freezable)bitmapImage).Freeze();
						chip.Thumbnail = bitmapImage;
					}
				}
			}
			catch (OperationCanceledException)
			{
				return;
			}
			catch (Exception exception)
			{
				ErrorLog.Write(exception);
			}
		}
	}

	private void ArtistChip_Click(object sender, RoutedEventArgs e)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		if (((Enum)Keyboard.Modifiers).HasFlag((Enum)(object)(ModifierKeys)2) && sender is Button { DataContext: ArtistChipItem dataContext })
		{
			string profileUrl = dataContext.ProfileUrl;
			if (profileUrl != null && profileUrl.Length > 0)
			{
				try
				{
					Process.Start(new ProcessStartInfo(dataContext.ProfileUrl)
					{
						UseShellExecute = true
					});
					return;
				}
				catch (Exception exception)
				{
					ErrorLog.Write(exception);
					return;
				}
			}
		}
		CopyChip_Click(sender, e);
	}

	private async void CopyChip_Click(object sender, RoutedEventArgs e)
	{
		string text = default(string);
		int num;
		if (sender is Button button)
		{
			text = button.Tag as string;
			num = ((text != null) ? 1 : 0);
		}
		else
		{
			num = 0;
		}
		bool flag = (byte)num != 0;
		if (flag)
		{
			flag = await CopyTextAsync(text, "Copied");
		}
		if (flag)
		{
			ShowCopiedState((Button)sender);
		}
	}

	private async void TrackTitleButton_Click(object sender, RoutedEventArgs e)
	{
		bool flag = _currentDetails != null;
		if (flag)
		{
			flag = await CopyTextAsync(_currentDetails.Summary.TrackTitle, "Track title copied");
		}
		if (flag)
		{
			ShowCopiedState(TrackTitleButton, TrackTitleCopyIcon, TrackTitleCopiedIcon);
		}
	}

	private async void CopyAllArtists_Click(object sender, RoutedEventArgs e)
	{
		bool flag = _currentDetails != null;
		if (flag)
		{
			flag = await CopyTextAsync(string.Join(", ", _currentDetails.Summary.Artists), "Artist line copied");
		}
		if (flag)
		{
			ShowCopiedState(sender as FrameworkElement, ArtistsCopyIcon, ArtistsCopiedIcon);
		}
	}

	private async void CopyAllPayees_Click(object sender, RoutedEventArgs e)
	{
		ReleaseDetails currentDetails = _currentDetails;
		if (currentDetails != null)
		{
			IReadOnlyList<string> payees = currentDetails.Payees;
			bool flag = payees != null && payees.Count > 0;
			if (flag)
			{
				flag = await CopyTextAsync(string.Join(", ", _currentDetails.Payees), "Legal-name line copied");
			}
			if (flag)
			{
				ShowCopiedState(sender as FrameworkElement, PayeesCopyIcon, PayeesCopiedIcon);
			}
		}
	}

	private async void CopyAllSongwriters_Click(object sender, RoutedEventArgs e)
	{
		ReleaseDetails currentDetails = _currentDetails;
		if (currentDetails != null)
		{
			IReadOnlyList<string> songwriters = currentDetails.Songwriters;
			bool flag = songwriters != null && songwriters.Count > 0;
			if (flag)
			{
				flag = await CopyTextAsync(string.Join(", ", _currentDetails.Songwriters), "Songwriter line copied");
			}
			if (flag)
			{
				ShowCopiedState(sender as FrameworkElement, SongwritersCopyIcon, SongwritersCopiedIcon);
			}
		}
	}

	private async void CreditsButton_Click(object sender, RoutedEventArgs e)
	{
		bool flag = !string.IsNullOrWhiteSpace(_currentDetails?.Credits);
		if (flag)
		{
			flag = await CopyTextAsync(_currentDetails.Credits, "Credits copied");
		}
		if (flag)
		{
			ShowCopiedState(CreditsButton);
		}
	}

	private async Task<bool> CopyTextAsync(string value, string confirmation)
	{
		try
		{
			await SetClipboardWithRetryAsync(delegate
			{
				Clipboard.SetDataObject(new DataObject(DataFormats.UnicodeText, value), copy: false);
			});
			ShowToast(confirmation);
			return true;
		}
		catch (Exception exception)
		{
			ErrorLog.Write(exception);
			ShowToast("Clipboard is busy");
			return false;
		}
	}

	private async void ShowCopiedState(FrameworkElement? element, System.Windows.Shapes.Path? copyIcon = null, System.Windows.Shapes.Path? copiedIcon = null)
	{
		if (element == null)
		{
			return;
		}
		int value;
		int version = ((!_copyFeedbackVersions.TryGetValue(element, out value)) ? 1 : (value + 1));
		_copyFeedbackVersions[element] = version;
		if (copyIcon != null && copiedIcon != null)
		{
			copyIcon.Visibility = Visibility.Collapsed;
			copiedIcon.Visibility = Visibility.Visible;
		}
		Brush background = (Brush)FindResource("CopiedBackgroundBrush");
		Brush borderBrush = (Brush)FindResource("TealBrush");
		if (!(element is Control control))
		{
			if (element is Border border)
			{
				border.Background = background;
				border.BorderBrush = borderBrush;
			}
		}
		else
		{
			control.Background = background;
			control.BorderBrush = borderBrush;
		}
		await Task.Delay(850);
		if (!_copyFeedbackVersions.TryGetValue(element, out var value2) || value2 != version)
		{
			return;
		}
		_copyFeedbackVersions.Remove(element);
		if (copyIcon != null && copiedIcon != null)
		{
			copyIcon.Visibility = Visibility.Visible;
			copiedIcon.Visibility = Visibility.Collapsed;
		}
		if (!(element is Control control2))
		{
			if (element is Border border2)
			{
				((DependencyObject)border2).ClearValue(Border.BackgroundProperty);
				((DependencyObject)border2).ClearValue(Border.BorderBrushProperty);
			}
		}
		else
		{
			((DependencyObject)control2).ClearValue(Control.BackgroundProperty);
			((DependencyObject)control2).ClearValue(Control.BorderBrushProperty);
		}
	}

	private void AudioCard_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		bool flag = _settings.ShowRealWaveform && WaveformDisplay.Visibility == Visibility.Visible && AudioCard.Tag is string path && File.Exists(path);
		if (((Enum)Keyboard.Modifiers).HasFlag((Enum)(object)(ModifierKeys)2) && flag)
		{
			e.Handled = true;
			_suppressCardClickOnce = true;
			if (_isAuditionPlaying)
			{
				PauseAudition();
			}
			else
			{
				StartAudition((string)AudioCard.Tag, GetAuditionFraction(e));
			}
		}
		else if (_isAuditionPlaying)
		{
			e.Handled = true;
			_suppressCardClickOnce = true;
			if (IsOverPauseBadge(e))
			{
				PauseAudition();
			}
			else
			{
				SeekAudition(GetAuditionFraction(e));
			}
		}
		else
		{
			FileCard_PreviewMouseLeftButtonDown(sender, e);
		}
	}

	private void AudioCard_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (_suppressCardClickOnce)
		{
			_suppressCardClickOnce = false;
			e.Handled = true;
		}
		else
		{
			FileCard_PreviewMouseLeftButtonUp(sender, e);
		}
	}

	private void AudioCard_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
	{
		if (_isAuditionPlaying && _auditionPlayer != null)
		{
			e.Handled = true;
			double num = CurrentAuditionDurationSeconds();
			if (!(num <= 0.0))
			{
				double num2 = _auditionPlayer.Position.TotalSeconds + (double)((e.Delta > 0) ? 3 : (-3));
				SeekAudition(Math.Clamp(num2 / num, 0.0, 0.999));
			}
		}
	}

	private double GetAuditionFraction(MouseEventArgs e)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		if (WaveformDisplay.ActualWidth <= 0.0)
		{
			return 0.0;
		}
		Point position = e.GetPosition(WaveformDisplay);
		return Math.Clamp(((Point)(ref position)).X / WaveformDisplay.ActualWidth, 0.0, 0.999);
	}

	private bool IsOverPauseBadge(MouseEventArgs e)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		if (PauseBadge.Visibility != Visibility.Visible)
		{
			return false;
		}
		Point position = e.GetPosition(PauseBadge);
		if (((Point)(ref position)).X >= -2.0 && ((Point)(ref position)).Y >= -2.0 && ((Point)(ref position)).X <= PauseBadge.ActualWidth + 2.0)
		{
			return ((Point)(ref position)).Y <= PauseBadge.ActualHeight + 2.0;
		}
		return false;
	}

	private void StartAudition(string path, double fraction)
	{
		if (_auditionPlayer == null)
		{
			_auditionPlayer = new MediaPlayer
			{
				Volume = 0.9
			};
			_auditionPlayer.MediaOpened += delegate
			{
				double? pendingAuditionSeek = _pendingAuditionSeek;
				if (pendingAuditionSeek.HasValue)
				{
					double valueOrDefault = pendingAuditionSeek.GetValueOrDefault();
					_pendingAuditionSeek = null;
					ApplyAuditionSeek(valueOrDefault);
				}
			};
			_auditionPlayer.MediaEnded += delegate
			{
				StopAudition();
			};
			_auditionPlayer.MediaFailed += delegate
			{
				StopAudition();
				ShowToast("Could not play the WAV");
			};
		}
		if (!string.Equals(_auditionPath, path, StringComparison.OrdinalIgnoreCase))
		{
			_auditionPath = path;
			_pendingAuditionSeek = fraction;
			_auditionPlayer.Open(new Uri(path));
		}
		else
		{
			ApplyAuditionSeek(fraction);
		}
		_auditionPlayer.Play();
		_isAuditionPlaying = true;
		PauseBadge.Visibility = Visibility.Visible;
		PlayheadLine.Visibility = Visibility.Visible;
		PlayheadLine.Opacity = 0.9;
		_playheadTimer.Start();
	}

	private void PauseAudition()
	{
		_auditionPlayer?.Pause();
		_isAuditionPlaying = false;
		_playheadTimer.Stop();
		PauseBadge.Visibility = Visibility.Collapsed;
		PlayheadLine.Opacity = 0.45;
	}

	private void StopAudition()
	{
		_playheadTimer.Stop();
		_isAuditionPlaying = false;
		_auditionPath = null;
		_pendingAuditionSeek = null;
		_auditionPlayer?.Close();
		if (PauseBadge != null)
		{
			PauseBadge.Visibility = Visibility.Collapsed;
			PlayheadLine.Visibility = Visibility.Collapsed;
		}
	}

	private void SeekAudition(double fraction)
	{
		ApplyAuditionSeek(fraction);
	}

	private void ApplyAuditionSeek(double fraction)
	{
		if (_auditionPlayer != null)
		{
			double num = CurrentAuditionDurationSeconds();
			if (num > 0.0)
			{
				_auditionPlayer.Position = TimeSpan.FromSeconds(fraction * num);
				UpdatePlayheadPosition();
			}
		}
	}

	private double CurrentAuditionDurationSeconds()
	{
		MediaPlayer auditionPlayer = _auditionPlayer;
		if (auditionPlayer != null && auditionPlayer.NaturalDuration.HasTimeSpan)
		{
			return _auditionPlayer.NaturalDuration.TimeSpan.TotalSeconds;
		}
		return _currentWaveformInfo?.DurationSeconds ?? 0.0;
	}

	private void PlayheadTimer_Tick(object? sender, EventArgs e)
	{
		UpdatePlayheadPosition();
	}

	private void UpdatePlayheadPosition()
	{
		double num = CurrentAuditionDurationSeconds();
		if (_auditionPlayer != null && !(num <= 0.0) && !(WaveformDisplay.ActualWidth <= 0.0))
		{
			double num2 = Math.Clamp(_auditionPlayer.Position.TotalSeconds / num, 0.0, 1.0);
			PlayheadLine.Margin = new Thickness(num2 * WaveformDisplay.ActualWidth - 0.8, 0.0, 0.0, 0.0);
		}
	}

	private void FileCard_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Expected O, but got Unknown
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		if (!IsWithinElement(e.OriginalSource, (DependencyObject)SimilarArtworkBadge) && sender is FrameworkElement { Tag: string tag } frameworkElement && File.Exists(tag))
		{
			if (e.ClickCount >= 2)
			{
				_fileMouseSource = null;
				_fileDragStarted = false;
				e.Handled = true;
				OpenPath(tag);
			}
			else
			{
				_fileMouseSource = frameworkElement;
				_fileMouseDownPoint = e.GetPosition(this);
				_fileDragStarted = false;
			}
		}
	}

	private void FileCard_PreviewMouseMove(object sender, MouseEventArgs e)
	{
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Expected O, but got Unknown
		if (e.LeftButton == MouseButtonState.Pressed && sender is FrameworkElement frameworkElement && frameworkElement == _fileMouseSource && frameworkElement.Tag is string path && File.Exists(path))
		{
			Point position = e.GetPosition(this);
			if (!(Math.Abs(((Point)(ref position)).X - ((Point)(ref _fileMouseDownPoint)).X) < SystemParameters.MinimumHorizontalDragDistance) || !(Math.Abs(((Point)(ref position)).Y - ((Point)(ref _fileMouseDownPoint)).Y) < SystemParameters.MinimumVerticalDragDistance))
			{
				_fileDragStarted = true;
				DataObject data = CreateFileData(path);
				DragDrop.DoDragDrop((DependencyObject)frameworkElement, data, DragDropEffects.Copy);
				_fileMouseSource = null;
			}
		}
	}

	private async void FileCard_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (IsWithinElement(e.OriginalSource, (DependencyObject)SimilarArtworkBadge))
		{
			return;
		}
		if (sender is FrameworkElement element && element == _fileMouseSource && !_fileDragStarted && element.Tag is string path && File.Exists(path))
		{
			try
			{
				DataObject data = CreateFileData(path);
				await SetClipboardWithRetryAsync(delegate
				{
					Clipboard.SetDataObject(data, copy: false);
				});
				ShowToast("File copied");
				ShowCopiedState(element);
			}
			catch (Exception exception)
			{
				ErrorLog.Write(exception);
				ShowToast("Clipboard is busy");
			}
		}
		_fileMouseSource = null;
	}

	private static DataObject CreateFileData(string path)
	{
		StringCollection fileDropList = new StringCollection { path };
		DataObject dataObject = new DataObject();
		dataObject.SetFileDropList(fileDropList);
		return dataObject;
	}

	private static Task SetClipboardWithRetryAsync(Action action)
	{
		try
		{
			action();
			return Task.CompletedTask;
		}
		catch (Exception exception) when (IsClipboardBusyException(exception))
		{
			return RetryClipboardAsync(action);
		}
	}

	private static async Task RetryClipboardAsync(Action action)
	{
		int attempt = 0;
		while (true)
		{
			try
			{
				action();
				break;
			}
			catch (Exception exception) when (attempt < 2 && IsClipboardBusyException(exception))
			{
				await Task.Delay(60 * (attempt + 1));
			}
			attempt++;
		}
	}

	private static bool IsClipboardBusyException(Exception exception)
	{
		if (exception is ExternalException || exception is NotImplementedException)
		{
			return true;
		}
		return false;
	}

	private async void ShowToast(string message)
	{
		int version = ++_toastVersion;
		ToastText.Text = message;
		ToastBadge.Visibility = Visibility.Visible;
		ToastScale.BeginAnimation(ScaleTransform.ScaleXProperty, CreateToastPopAnimation());
		ToastScale.BeginAnimation(ScaleTransform.ScaleYProperty, CreateToastPopAnimation());
		await Task.Delay(1500);
		if (version == _toastVersion)
		{
			ToastBadge.Visibility = Visibility.Collapsed;
		}
	}

	private static DoubleAnimationUsingKeyFrames CreateToastPopAnimation()
	{
		return new DoubleAnimationUsingKeyFrames
		{
			KeyFrames = 
			{
				(DoubleKeyFrame)new EasingDoubleKeyFrame(0.92, KeyTime.FromTimeSpan(TimeSpan.Zero)),
				(DoubleKeyFrame)new EasingDoubleKeyFrame(1.06, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(110.0))),
				(DoubleKeyFrame)new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(210.0)))
			}
		};
	}

	private void ReleaseHeaderCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (_currentDetails != null && !IsWithinButton(e.OriginalSource))
		{
			OpenPath(_currentDetails.Summary.FolderPath);
		}
	}

	private static bool IsWithinButton(object? source)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Expected O, but got Unknown
		for (DependencyObject val = (DependencyObject)((source is DependencyObject) ? source : null); val != null; val = ((val is Visual || val is Visual3D) ? VisualTreeHelper.GetParent(val) : LogicalTreeHelper.GetParent(val)))
		{
			if (val is Button)
			{
				return true;
			}
		}
		return false;
	}

	private static bool IsWithinElement(object? source, DependencyObject target)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Expected O, but got Unknown
		for (DependencyObject val = (DependencyObject)((source is DependencyObject) ? source : null); val != null; val = ((val is Visual || val is Visual3D) ? VisualTreeHelper.GetParent(val) : LogicalTreeHelper.GetParent(val)))
		{
			if (val == target)
			{
				return true;
			}
		}
		return false;
	}

	private void AttentionBadge_Click(object sender, RoutedEventArgs e)
	{
		WarningsPanel.BringIntoView();
	}

	private void OpenContractButton_Click(object sender, RoutedEventArgs e)
	{
		if (OpenContractButton.Tag is string path)
		{
			OpenPath(path);
		}
	}

	private void CoverBadge_Click(object sender, RoutedEventArgs e)
	{
		if (_fileDragStarted)
		{
			_fileDragStarted = false;
		}
		else if (sender is FrameworkElement { Tag: string tag })
		{
			OpenPath(tag);
		}
	}

	private void CoverBadge_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		if (sender is FrameworkElement { Tag: string tag } frameworkElement && File.Exists(tag))
		{
			_fileMouseSource = frameworkElement;
			_fileMouseDownPoint = e.GetPosition(this);
			_fileDragStarted = false;
		}
	}

	private void OpenPath(string path)
	{
		try
		{
			Process.Start(new ProcessStartInfo(path)
			{
				UseShellExecute = true
			});
		}
		catch (Exception exception)
		{
			ErrorLog.Write(exception);
			ShowToast("Could not open path");
		}
	}

	private async void RefreshButton_Click(object sender, RoutedEventArgs e)
	{
		string currentPath = _currentDetails?.Summary.FolderPath ?? _pendingReleasePath;
		if (await ScanFoldersAsync())
		{
			await ReloadCurrentReleaseAsync(currentPath);
			ShowToast("Folder index refreshed");
		}
		else
		{
			ShowEmptyAfterFailedScan();
			ShowToast("Folder refresh failed");
		}
	}

	private void SettingsButton_Click(object sender, RoutedEventArgs e)
	{
		SettingsWindow settingsWindow = new SettingsWindow(_settings.SearchFolders, _settings.InterfaceScale, _settings.ShowSearchArtworkThumbnails, _settings.ValidateSearchResultsLive, _settings.SplitNamesByDefault, _settings.ShowRealWaveform, _settings.WaveformInnerMode, ThemeCatalog.Parse(_settings.Theme))
		{
			Owner = this,
			Topmost = base.Topmost
		};
		settingsWindow.Closed += async delegate
		{
			if (settingsWindow.WasSaved)
			{
				_settings.SearchFolders = settingsWindow.SearchFolders.ToList();
				_settings.InterfaceScale = settingsWindow.InterfaceScale;
				_settings.ShowSearchArtworkThumbnails = settingsWindow.ShowSearchArtworkThumbnails;
				_settings.ValidateSearchResultsLive = settingsWindow.ValidateSearchResultsLive;
				_settings.SplitNamesByDefault = settingsWindow.SplitNamesByDefault;
				_settings.ShowRealWaveform = settingsWindow.ShowRealWaveform;
				_settings.WaveformInnerMode = settingsWindow.WaveformInnerMode;
				_settings.Theme = settingsWindow.SelectedTheme.ToString();
				_splitLegalNames = _settings.SplitNamesByDefault;
				_splitSongwriterNames = _settings.SplitNamesByDefault;
				UpdateNameSplitButton(_legalNamesSplitButton, _splitLegalNames, "legal names");
				UpdateNameSplitButton(_songwritersSplitButton, _splitSongwriterNames, "songwriters");
				if (_currentDetails != null)
				{
					RenderLegalNames(_currentDetails);
					RenderSongwriters(_currentDetails);
				}
				ApplyInterfaceScale(resizeWindow: true);
				_settingsService.Save(_settings);
				string currentPath = _currentDetails?.Summary.FolderPath ?? _pendingReleasePath;
				if (await ScanFoldersAsync())
				{
					await ReloadCurrentReleaseAsync(currentPath);
				}
				else
				{
					ShowEmptyAfterFailedScan();
				}
			}
		};
		settingsWindow.Show();
	}

	private void PinButton_Click(object sender, RoutedEventArgs e)
	{
		base.Topmost = !base.Topmost;
		_settings.AlwaysOnTop = base.Topmost;
		UpdatePinAppearance();
		ShowToast(base.Topmost ? "Pinned on top" : "Pin released");
	}

	private void UpdatePinAppearance()
	{
		PinButton.Foreground = (base.Topmost ? ((Brush)FindResource("AccentBrush")) : ((Brush)FindResource("MutedTextBrush")));
		PinButton.Opacity = (base.Topmost ? 1.0 : 0.72);
	}

	private void ApplyInterfaceScale(bool resizeWindow)
	{
		double num = Math.Clamp(_settings.InterfaceScale, 0.8, 1.35);
		double appliedInterfaceScale = _appliedInterfaceScale;
		_appliedInterfaceScale = num;
		InterfaceScaleTransform.ScaleX = num;
		InterfaceScaleTransform.ScaleY = num;
		base.MinWidth = 370.0 * num;
		base.MinHeight = 430.0 * num;
		if (resizeWindow && base.IsLoaded)
		{
			base.Width = Math.Max(base.MinWidth, base.ActualWidth * num / appliedInterfaceScale);
			base.Height = Math.Max(base.MinHeight, base.ActualHeight * num / appliedInterfaceScale);
		}
	}

	private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
	{
		SearchBox.Clear();
		SearchBox.Focus();
	}

	private void SearchBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
	{
		((DispatcherObject)this).Dispatcher.BeginInvoke((DispatcherPriority)5, (Delegate)new Action(SearchBox.SelectAll));
	}

	private void SearchBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (e.ClickCount == 1)
		{
			e.Handled = true;
			SearchBox.Focus();
			SearchBox.SelectAll();
			if (!string.IsNullOrWhiteSpace(SearchBox.Text))
			{
				UpdateSearchResults();
			}
		}
	}

	private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
	{
		if (SearchPopup.IsOpen && !SearchBorder.IsMouseOver && !ResultsList.IsMouseOver)
		{
			SearchPopup.IsOpen = false;
			CancelSearchEnrichment();
		}
	}

	private void Window_Deactivated(object? sender, EventArgs e)
	{
		SearchPopup.IsOpen = false;
		CancelSearchEnrichment();
		ReassertTopmost();
	}

	[DllImport("user32.dll")]
	private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, int flags);

	private void ReassertTopmost()
	{
		if (base.Topmost)
		{
			nint handle = new WindowInteropHelper(this).Handle;
			if (handle != IntPtr.Zero)
			{
				SetWindowPos(handle, HwndTopmost, 0, 0, 0, 0, 19);
			}
		}
	}

	private void CancelSearchEnrichment()
	{
		_searchArtworkThumbnailsCancellation?.Cancel();
		_searchReadinessCancellation?.Cancel();
	}

	private void CloseButton_Click(object sender, RoutedEventArgs e)
	{
		Close();
	}

	private async Task ReloadCurrentReleaseAsync(string? folderPath)
	{
		if (folderPath != null)
		{
			ReleaseSummary releaseSummary = _releases.FirstOrDefault((ReleaseSummary item) => item.FolderPath.Equals(folderPath, StringComparison.OrdinalIgnoreCase));
			if (releaseSummary != null)
			{
				await SelectReleaseAsync(releaseSummary);
				return;
			}
			_currentDetails = null;
			_pendingReleasePath = null;
			DetailsScroll.Visibility = Visibility.Collapsed;
			LoadingState.Visibility = Visibility.Collapsed;
			EmptyState.Visibility = Visibility.Visible;
			EmptyTitle.Text = "Release is outside the current search folders";
			EmptyDescription.Text = "Choose another result or add its parent folder in Settings.";
		}
	}

	private void ShowEmptyAfterFailedScan()
	{
		if (_currentDetails == null)
		{
			LoadingState.Visibility = Visibility.Collapsed;
			DetailsScroll.Visibility = Visibility.Collapsed;
			EmptyState.Visibility = Visibility.Visible;
		}
	}

	private void ConfigureNameSplitToggles()
	{
		if (_legalNamesSplitButton == null)
		{
			_legalNamesSplitButton = AttachNameSplitToggle("LEGAL NAMES", CopyAllPayeesButton, _splitLegalNames, delegate
			{
				_splitLegalNames = !_splitLegalNames;
				UpdateNameSplitButton(_legalNamesSplitButton, _splitLegalNames, "legal names");
				if (_currentDetails != null)
				{
					RenderLegalNames(_currentDetails);
				}
			});
		}
		if (_songwritersSplitButton != null)
		{
			return;
		}
		_songwritersSplitButton = AttachNameSplitToggle("SONGWRITERS", CopyAllSongwritersButton, _splitSongwriterNames, delegate
		{
			_splitSongwriterNames = !_splitSongwriterNames;
			UpdateNameSplitButton(_songwritersSplitButton, _splitSongwriterNames, "songwriters");
			if (_currentDetails != null)
			{
				RenderSongwriters(_currentDetails);
			}
		});
	}

	private Button? AttachNameSplitToggle(string headingText, Button appearanceSource, bool isSplit, RoutedEventHandler clickHandler)
	{
		TextBlock textBlock = FindTextBlock((DependencyObject)(object)this, headingText);
		if (!(textBlock?.Parent is Panel panel))
		{
			return null;
		}
		int index = panel.Children.IndexOf(textBlock);
		Button button = CreateNameSplitButton(appearanceSource, clickHandler);
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = textBlock.HorizontalAlignment,
			VerticalAlignment = textBlock.VerticalAlignment,
			Margin = textBlock.Margin
		};
		Grid.SetRow(stackPanel, Grid.GetRow(textBlock));
		Grid.SetColumn(stackPanel, Grid.GetColumn(textBlock));
		Grid.SetRowSpan(stackPanel, Grid.GetRowSpan(textBlock));
		Grid.SetColumnSpan(stackPanel, Grid.GetColumnSpan(textBlock));
		DockPanel.SetDock(stackPanel, DockPanel.GetDock(textBlock));
		textBlock.Margin = new Thickness(0.0);
		button.Margin = new Thickness(5.0, -2.0, 0.0, -2.0);
		panel.Children.RemoveAt(index);
		stackPanel.Children.Add(textBlock);
		stackPanel.Children.Add(button);
		panel.Children.Insert(index, stackPanel);
		UpdateNameSplitButton(button, isSplit, headingText.ToLowerInvariant());
		return button;
	}

	private static Button CreateNameSplitButton(Button appearanceSource, RoutedEventHandler clickHandler)
	{
		Button button = new Button
		{
			Content = new TextBlock
			{
				Text = "✂",
				FontFamily = new FontFamily("Segoe UI Symbol"),
				FontSize = 11.0,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			},
			Width = 22.0,
			Height = 20.0,
			Padding = new Thickness(0.0),
			Margin = new Thickness(5.0, -2.0, 0.0, -2.0),
			VerticalAlignment = VerticalAlignment.Center,
			Focusable = false,
			Opacity = 0.58,
			RenderTransform = new TranslateTransform(0.0, -2.0)
		};
		Style style = appearanceSource.Style ?? (appearanceSource.TryFindResource(typeof(Button)) as Style);
		if (style != null)
		{
			button.Style = style;
		}
		button.Click += clickHandler;
		return button;
	}

	private static TextBlock? FindTextBlock(DependencyObject root, string text)
	{
		int childrenCount = VisualTreeHelper.GetChildrenCount(root);
		for (int i = 0; i < childrenCount; i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(root, i);
			if (child is TextBlock textBlock && textBlock.Text.Equals(text, StringComparison.OrdinalIgnoreCase))
			{
				return textBlock;
			}
			TextBlock textBlock2 = FindTextBlock(child, text);
			if (textBlock2 != null)
			{
				return textBlock2;
			}
		}
		return null;
	}

	private static void UpdateNameSplitButton(Button? button, bool isSplit, string sectionName)
	{
		if (button == null)
		{
			return;
		}
		button.Opacity = (isSplit ? 1.0 : 0.58);
		button.FontWeight = (isSplit ? FontWeights.Bold : FontWeights.Normal);
		if (button.Content is TextBlock textBlock)
		{
			if (isSplit)
			{
				textBlock.Foreground = Brushes.White;
			}
			else
			{
				((DependencyObject)textBlock).ClearValue(TextBlock.ForegroundProperty);
			}
		}
		button.ToolTip = (isSplit ? ("Show full " + sectionName) : ("Split " + sectionName + " into first and last names"));
	}

	private void RenderLegalNames(ReleaseDetails details)
	{
		if (_legalNamesFullTemplate == null)
		{
			_legalNamesFullTemplate = PayeesItems.ItemTemplate;
		}
		if (_legalNameChipStyle == null)
		{
			_legalNameChipStyle = GetChipButtonStyle(_legalNamesFullTemplate);
		}
		if (_splitLegalNames)
		{
			PayeesItems.ItemTemplate = null;
			PayeesItems.ItemsSource = details.PayeeMappings.Select((PayeeMapping mapping) => CreateSplitNameControl(mapping.LegalName, _legalNameChipStyle, mapping.Artist)).ToArray();
		}
		else
		{
			PayeesItems.ItemTemplate = _legalNamesFullTemplate;
			PayeesItems.ItemsSource = details.PayeeMappings.Select((PayeeMapping mapping) => new CopyToken(mapping.LegalName, mapping.LegalName, mapping.Artist ?? "Songwriter")).ToArray();
		}
	}

	private void RenderSongwriters(ReleaseDetails details)
	{
		if (_songwritersFullTemplate == null)
		{
			_songwritersFullTemplate = SongwritersItems.ItemTemplate;
		}
		if (_songwriterChipStyle == null)
		{
			_songwriterChipStyle = GetChipButtonStyle(_songwritersFullTemplate);
		}
		if (_splitSongwriterNames)
		{
			SongwritersItems.ItemTemplate = null;
			SongwritersItems.ItemsSource = details.Songwriters.Select((string name) => CreateSplitNameControl(name, _songwriterChipStyle, null)).ToArray();
		}
		else
		{
			SongwritersItems.ItemTemplate = _songwritersFullTemplate;
			SongwritersItems.ItemsSource = CreateCopyTokens(details.Songwriters);
		}
	}

	private static Style? GetChipButtonStyle(DataTemplate? template)
	{
		return (template?.LoadContent() as Button)?.Style;
	}

	private Button CreateSplitNameControl(string fullName, Style? chipStyle, string? tooltip)
	{
		string[] array = fullName.Trim().Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
		string firstName = ((array.Length != 0) ? array[0] : string.Empty);
		string lastName = ((array.Length > 1) ? array[1] : string.Empty);
		Button button = new Button
		{
			Style = chipStyle,
			Padding = new Thickness(0.0),
			ToolTip = (string.IsNullOrWhiteSpace(tooltip) ? null : tooltip),
			Focusable = false,
			Height = 22.0
		};
		Grid grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(9.0)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		Border firstHitArea = CreateNameHalf(firstName);
		Border lastHitArea = CreateNameHalf((lastName.Length > 0) ? lastName : "—");
		Grid.SetColumn(firstHitArea, 0);
		Grid.SetColumn(lastHitArea, 2);
		Line line = new Line();
		line.X1 = 0.5;
		line.X2 = 0.5;
		line.Y1 = 0.0;
		line.Y2 = 16.0;
		line.Height = 16.0;
		line.Stretch = Stretch.Fill;
		line.StrokeThickness = 1.0;
		line.StrokeDashArray = new DoubleCollection(new double[2] { 2.0, 2.0 });
		line.Opacity = 0.5;
		line.HorizontalAlignment = HorizontalAlignment.Center;
		line.VerticalAlignment = VerticalAlignment.Center;
		Line line2 = line;
		line2.SetBinding(Shape.StrokeProperty, new Binding("Foreground")
		{
			Source = button
		});
		Grid.SetColumn(line2, 1);
		bool copyFirstName = true;
		button.PreviewMouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			Point position = e.GetPosition(grid);
			copyFirstName = ((Point)(ref position)).X < firstHitArea.ActualWidth + 4.5;
		};
		button.Click += async delegate
		{
			string value = (copyFirstName ? firstName : lastName);
			string confirmation = (copyFirstName ? "First name copied" : "Last name copied");
			if (await CopyTextAsync(value, confirmation))
			{
				ShowCopiedState(copyFirstName ? firstHitArea : lastHitArea);
			}
		};
		grid.Children.Add(firstHitArea);
		grid.Children.Add(line2);
		grid.Children.Add(lastHitArea);
		button.Content = grid;
		return button;
	}

	private static Border CreateNameHalf(string value)
	{
		return new Border
		{
			Background = Brushes.Transparent,
			Padding = new Thickness(9.0, 5.0, 9.0, 5.0),
			Cursor = Cursors.Hand,
			Child = new TextBlock
			{
				Text = value,
				VerticalAlignment = VerticalAlignment.Center
			}
		};
	}

	private static IReadOnlyList<CopyToken> CreateCopyTokens(IReadOnlyList<string> values)
	{
		return values.Select((string value) => new CopyToken(value, value)).ToArray();
	}

	private static string FormatBytes(long bytes)
	{
		string[] array = new string[4] { "B", "KB", "MB", "GB" };
		double num = bytes;
		int num2 = 0;
		while (num >= 1024.0 && num2 < array.Length - 1)
		{
			num /= 1024.0;
			num2++;
		}
		return $"{num:0.#} {array[num2]}";
	}

	private static string FormatDuration(TimeSpan duration)
	{
		if (!(duration.TotalHours >= 1.0))
		{
			return $"{(int)duration.TotalMinutes}:{duration.Seconds:D2}";
		}
		return $"{(int)duration.TotalHours}:{duration.Minutes:D2}:{duration.Seconds:D2}";
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.22.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/DistroClip;component/mainwindow.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.22.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 1:
			((MainWindow)target).Loaded += Window_Loaded;
			((MainWindow)target).PreviewMouseDown += Window_PreviewMouseDown;
			((MainWindow)target).Deactivated += Window_Deactivated;
			((MainWindow)target).LocationChanged += Window_LocationChanged;
			((MainWindow)target).StateChanged += Window_StateChanged;
			((MainWindow)target).Closing += Window_Closing;
			break;
		case 2:
			MainLayout = (Grid)target;
			break;
		case 3:
			InterfaceScaleTransform = (ScaleTransform)target;
			break;
		case 4:
			WallpaperUnderlay = (Rectangle)target;
			break;
		case 5:
			WallpaperMilk = (Rectangle)target;
			break;
		case 6:
			AmbientGlowImage = (Image)target;
			break;
		case 7:
			GlassIsland = (Border)target;
			break;
		case 8:
			IslandContent = (Grid)target;
			break;
		case 9:
			RefractionHost = (Grid)target;
			break;
		case 10:
			RefractionWarp = (Grid)target;
			break;
		case 11:
			IslandWallpaperRect = (Rectangle)target;
			break;
		case 12:
			RefractionRect = (Rectangle)target;
			break;
		case 13:
			IslandSurface = (Border)target;
			break;
		case 14:
			TitleBar = (Grid)target;
			break;
		case 15:
			AttentionBadge = (Button)target;
			AttentionBadge.Click += AttentionBadge_Click;
			break;
		case 16:
			PinButton = (Button)target;
			PinButton.Click += PinButton_Click;
			break;
		case 17:
			((Button)target).Click += RefreshButton_Click;
			break;
		case 18:
			((Button)target).Click += SettingsButton_Click;
			break;
		case 19:
			((Button)target).Click += CloseButton_Click;
			break;
		case 20:
			SearchBorder = (Border)target;
			break;
		case 21:
			SearchBox = (TextBox)target;
			SearchBox.TextChanged += SearchBox_TextChanged;
			SearchBox.PreviewKeyDown += SearchBox_PreviewKeyDown;
			SearchBox.GotKeyboardFocus += SearchBox_GotKeyboardFocus;
			SearchBox.PreviewMouseLeftButtonDown += SearchBox_PreviewMouseLeftButtonDown;
			break;
		case 22:
			SearchPlaceholder = (TextBlock)target;
			break;
		case 23:
			ClearSearchButton = (Button)target;
			ClearSearchButton.Click += ClearSearchButton_Click;
			break;
		case 24:
			SearchPopup = (Popup)target;
			break;
		case 25:
			ResultsList = (ListBox)target;
			ResultsList.PreviewMouseLeftButtonUp += ResultsList_PreviewMouseLeftButtonUp;
			break;
		case 26:
			ContentHost = (Grid)target;
			break;
		case 27:
			EmptyState = (StackPanel)target;
			break;
		case 28:
			EmptyTitle = (TextBlock)target;
			break;
		case 29:
			EmptyDescription = (TextBlock)target;
			break;
		case 30:
			LoadingState = (StackPanel)target;
			break;
		case 31:
			DetailsScroll = (ScrollViewer)target;
			DetailsScroll.ScrollChanged += DetailsScroll_ScrollChanged;
			break;
		case 32:
			DetailsStack = (StackPanel)target;
			break;
		case 33:
			ReleaseHeaderCard = (Border)target;
			ReleaseHeaderCard.MouseLeftButtonUp += ReleaseHeaderCard_MouseLeftButtonUp;
			break;
		case 34:
			ReleaseStateAccent = (Border)target;
			break;
		case 35:
			HeaderArtworkImage = (Image)target;
			break;
		case 36:
			HeaderArtworkFallback = (System.Windows.Shapes.Path)target;
			break;
		case 37:
			ReleaseTitleText = (TextBlock)target;
			break;
		case 38:
			ReleaseArtistsText = (TextBlock)target;
			break;
		case 39:
			ReadinessSummary = (StackPanel)target;
			break;
		case 40:
			MasterStatusDot = (Border)target;
			break;
		case 41:
			ArtworkStatusDot = (Border)target;
			break;
		case 42:
			ContractStatusDot = (Border)target;
			break;
		case 43:
			OpenContractButton = (Button)target;
			OpenContractButton.Click += OpenContractButton_Click;
			break;
		case 44:
			CoverDetailBadge = (Button)target;
			CoverDetailBadge.Click += CoverBadge_Click;
			CoverDetailBadge.PreviewMouseLeftButtonDown += CoverBadge_PreviewMouseLeftButtonDown;
			CoverDetailBadge.PreviewMouseMove += FileCard_PreviewMouseMove;
			break;
		case 45:
			CoverDetailMatchCheck = (System.Windows.Shapes.Path)target;
			break;
		case 46:
			CoverDetailReviewDot = (Ellipse)target;
			break;
		case 47:
			TrackTitleButton = (Button)target;
			TrackTitleButton.Click += TrackTitleButton_Click;
			break;
		case 48:
			TrackTitleCopyText = (TextBlock)target;
			break;
		case 49:
			TrackTitleCopyIcon = (System.Windows.Shapes.Path)target;
			break;
		case 50:
			TrackTitleCopiedIcon = (System.Windows.Shapes.Path)target;
			break;
		case 51:
			ArtistsItems = (ItemsControl)target;
			break;
		case 53:
			((Button)target).Click += CopyAllArtists_Click;
			break;
		case 54:
			ArtistsCopyIcon = (System.Windows.Shapes.Path)target;
			break;
		case 55:
			ArtistsCopiedIcon = (System.Windows.Shapes.Path)target;
			break;
		case 56:
			PayeesItems = (ItemsControl)target;
			break;
		case 58:
			NoPayeesText = (TextBlock)target;
			break;
		case 59:
			CopyAllPayeesButton = (Button)target;
			CopyAllPayeesButton.Click += CopyAllPayees_Click;
			break;
		case 60:
			PayeesCopyIcon = (System.Windows.Shapes.Path)target;
			break;
		case 61:
			PayeesCopiedIcon = (System.Windows.Shapes.Path)target;
			break;
		case 62:
			SongwritersSection = (Grid)target;
			break;
		case 63:
			SongwritersItems = (ItemsControl)target;
			break;
		case 65:
			NoSongwritersText = (TextBlock)target;
			break;
		case 66:
			CopyAllSongwritersButton = (Button)target;
			CopyAllSongwritersButton.Click += CopyAllSongwriters_Click;
			break;
		case 67:
			SongwritersCopyIcon = (System.Windows.Shapes.Path)target;
			break;
		case 68:
			SongwritersCopiedIcon = (System.Windows.Shapes.Path)target;
			break;
		case 69:
			OriginalArtistSection = (StackPanel)target;
			break;
		case 70:
			OriginalArtistChip = (Button)target;
			OriginalArtistChip.Click += CopyChip_Click;
			break;
		case 71:
			FindOriginalArtistButton = (Button)target;
			FindOriginalArtistButton.Click += FindOriginalArtist_Click;
			break;
		case 72:
			OriginalArtistStatusText = (TextBlock)target;
			break;
		case 73:
			CreditsSection = (StackPanel)target;
			break;
		case 74:
			CreditsButton = (Button)target;
			CreditsButton.Click += CreditsButton_Click;
			break;
		case 75:
			CreditsText = (TextBlock)target;
			break;
		case 76:
			ArtworkCard = (Border)target;
			ArtworkCard.PreviewMouseLeftButtonDown += FileCard_PreviewMouseLeftButtonDown;
			ArtworkCard.PreviewMouseMove += FileCard_PreviewMouseMove;
			ArtworkCard.PreviewMouseLeftButtonUp += FileCard_PreviewMouseLeftButtonUp;
			break;
		case 77:
			ArtworkImage = (Image)target;
			break;
		case 78:
			ArtworkFileText = (TextBlock)target;
			break;
		case 79:
			SimilarArtworkBadge = (Border)target;
			SimilarArtworkBadge.MouseLeftButtonUp += SimilarArtworkBadge_MouseLeftButtonUp;
			break;
		case 80:
			AudioCard = (Border)target;
			AudioCard.PreviewMouseLeftButtonDown += AudioCard_PreviewMouseLeftButtonDown;
			AudioCard.PreviewMouseMove += FileCard_PreviewMouseMove;
			AudioCard.PreviewMouseLeftButtonUp += AudioCard_PreviewMouseLeftButtonUp;
			AudioCard.PreviewMouseWheel += AudioCard_PreviewMouseWheel;
			break;
		case 81:
			AudioPlaceholderHeader = (StackPanel)target;
			break;
		case 82:
			WaveformDisplay = (Grid)target;
			break;
		case 83:
			WaveformPeakPath = (System.Windows.Shapes.Path)target;
			break;
		case 84:
			WaveformRmsPath = (System.Windows.Shapes.Path)target;
			break;
		case 85:
			WaveformTruePeakPath = (System.Windows.Shapes.Path)target;
			break;
		case 86:
			WaveformClipPath = (System.Windows.Shapes.Path)target;
			break;
		case 87:
			PlayheadLine = (Border)target;
			break;
		case 88:
			PauseBadge = (Border)target;
			break;
		case 89:
			AudioMasterText = (TextBlock)target;
			break;
		case 90:
			OtherAudioVersionsText = (TextBlock)target;
			break;
		case 91:
			LufsText = (TextBlock)target;
			break;
		case 92:
			AudioFileText = (TextBlock)target;
			break;
		case 93:
			AudioDurationText = (TextBlock)target;
			break;
		case 94:
			AudioMetaText = (TextBlock)target;
			break;
		case 95:
			SilenceText = (TextBlock)target;
			break;
		case 96:
			WarningsPanel = (Border)target;
			break;
		case 97:
			WarningsItems = (ItemsControl)target;
			break;
		case 98:
			DetailsOverlayScrollBar = (ScrollBar)target;
			DetailsOverlayScrollBar.ValueChanged += DetailsOverlayScrollBar_ValueChanged;
			DetailsOverlayScrollBar.MouseEnter += DetailsOverlayScrollBar_MouseEnter;
			DetailsOverlayScrollBar.MouseLeave += DetailsOverlayScrollBar_MouseLeave;
			break;
		case 99:
			ScratchpadBox = (TextBox)target;
			break;
		case 100:
			IndexInfoHitArea = (Border)target;
			break;
		case 101:
			StatusText = (TextBlock)target;
			break;
		case 102:
			ToastBadge = (Border)target;
			break;
		case 103:
			ToastScale = (ScaleTransform)target;
			break;
		case 104:
			ToastText = (TextBlock)target;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.22.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IStyleConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 52:
			((Button)target).Click += ArtistChip_Click;
			break;
		case 57:
			((Button)target).Click += CopyChip_Click;
			break;
		case 64:
			((Button)target).Click += CopyChip_Click;
			break;
		}
	}
}
