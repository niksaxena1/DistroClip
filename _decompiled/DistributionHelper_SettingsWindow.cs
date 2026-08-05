using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using DistributionHelper.Models;
using DistributionHelper.Services;
using Microsoft.Win32;

namespace DistributionHelper;

public class SettingsWindow : Window, IComponentConnector
{
	private readonly ObservableCollection<string> _folders;

	private readonly AppTheme _originalTheme;

	private bool _isOpeningFolderPicker;

	private bool _isClosing;

	private static readonly string[] InnerModes = new string[5] { "DynamicRms", "TrueRms", "ShortTermLufs", "Crest", "Brightness" };

	private static readonly string[] InnerModeLabels = new string[5] { "RMS · dynamic", "RMS · true", "Loudness · 3s", "Crest · punch", "Brightness" };

	private int _innerModeIndex;

	internal ScaleTransform SettingsScaleTransform;

	internal ListBox ThemeList;

	internal Slider InterfaceScaleSlider;

	internal TextBlock InterfaceScaleValueText;

	internal CheckBox SearchArtworkThumbnailsCheckBox;

	internal CheckBox ValidateSearchResultsCheckBox;

	internal CheckBox SplitNamesByDefaultCheckBox;

	internal CheckBox RealWaveformCheckBox;

	internal Button InnerWaveformButton;

	internal ListBox FoldersList;

	internal TextBlock SettingsHint;

	private bool _contentLoaded;

	public IReadOnlyList<string> SearchFolders => _folders.ToArray();

	public double InterfaceScale { get; private set; } = 1.0;

	public bool ShowSearchArtworkThumbnails => SearchArtworkThumbnailsCheckBox.IsChecked == true;

	public bool ValidateSearchResultsLive => ValidateSearchResultsCheckBox.IsChecked == true;

	public bool SplitNamesByDefault => SplitNamesByDefaultCheckBox.IsChecked == true;

	public bool ShowRealWaveform => RealWaveformCheckBox.IsChecked == true;

	public string WaveformInnerMode => InnerModes[_innerModeIndex];

	public AppTheme SelectedTheme => (ThemeList.SelectedItem as ThemeOption)?.Theme ?? _originalTheme;

	public bool WasSaved { get; private set; }

	public SettingsWindow(IEnumerable<string> folders, double interfaceScale, bool showSearchArtworkThumbnails, bool validateSearchResultsLive, bool splitNamesByDefault, bool showRealWaveform, string waveformInnerMode, AppTheme currentTheme)
	{
		InitializeComponent();
		AddSplitNamesDefaultToggle(splitNamesByDefault);
		_folders = new ObservableCollection<string>(folders.Distinct<string>(StringComparer.OrdinalIgnoreCase));
		FoldersList.ItemsSource = _folders;
		double num = Math.Clamp(interfaceScale, 0.8, 1.35);
		SettingsScaleTransform.ScaleX = num;
		SettingsScaleTransform.ScaleY = num;
		base.Width = 350.0 * num + 20.0;
		base.Height = 607.0 * num + 20.0;
		InterfaceScaleSlider.Value = Math.Clamp(interfaceScale, 0.8, 1.35);
		SearchArtworkThumbnailsCheckBox.IsChecked = showSearchArtworkThumbnails;
		ValidateSearchResultsCheckBox.IsChecked = validateSearchResultsLive;
		RealWaveformCheckBox.IsChecked = showRealWaveform;
		_innerModeIndex = Math.Max(0, Array.IndexOf(InnerModes, waveformInnerMode));
		UpdateInnerWaveformButton();
		_originalTheme = currentTheme;
		IReadOnlyList<ThemeOption> readOnlyList = CreateThemeOptions();
		ThemeList.ItemsSource = readOnlyList;
		ThemeList.SelectedItem = readOnlyList.FirstOrDefault((ThemeOption option) => option.Theme == currentTheme) ?? readOnlyList[0];
	}

	private void AddSplitNamesDefaultToggle(bool isChecked)
	{
		if (VisualTreeHelper.GetParent((DependencyObject)(object)RealWaveformCheckBox) is Grid { Parent: Panel parent } grid)
		{
			Grid grid2 = (Grid)XamlReader.Parse(XamlWriter.Save(grid));
			List<TextBlock> list = FindVisualChildren<TextBlock>((DependencyObject)(object)grid2).ToList();
			foreach (TextBlock item in list)
			{
				if (item.Text.Contains("Reads the master file", StringComparison.Ordinal))
				{
					item.Text = "Starts Legal Names and Songwriters split.";
				}
				else if (item.Text.Contains("Real waveform on the WAV card", StringComparison.Ordinal))
				{
					item.Text = "Scissor names by default";
				}
			}
			if (list.Count > 1 && !list.Any((TextBlock textBlock) => textBlock.Text == "Scissor names by default"))
			{
				list[0].Text = "Starts Legal Names and Songwriters split.";
				list[1].Text = "Scissor names by default";
			}
			SplitNamesByDefaultCheckBox = FindVisualChildren<CheckBox>((DependencyObject)(object)grid2).First();
			SplitNamesByDefaultCheckBox.Name = string.Empty;
			SplitNamesByDefaultCheckBox.IsChecked = isChecked;
			UIElement uIElement = FindDirectChild(parent, (DependencyObject)(object)FoldersList);
			int val = ((uIElement == null) ? (parent.Children.IndexOf(grid) + 1) : parent.Children.IndexOf(uIElement));
			parent.Children.Insert(Math.Max(0, val), grid2);
		}
		else
		{
			SplitNamesByDefaultCheckBox = new CheckBox
			{
				Content = "Scissor names by default",
				IsChecked = isChecked,
				Style = RealWaveformCheckBox.Style
			};
		}
	}

	private static UIElement? FindDirectChild(Panel parent, DependencyObject descendant)
	{
		DependencyObject val = descendant;
		while (true)
		{
			DependencyObject parent2 = VisualTreeHelper.GetParent(val);
			if (parent2 == null)
			{
				break;
			}
			if ((object)parent2 == parent)
			{
				return val as UIElement;
			}
			val = parent2;
		}
		return null;
	}

	private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
	{
		for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(parent, i);
			T val = (T)(object)((child is T) ? child : null);
			if (val != null)
			{
				yield return val;
			}
			foreach (T item in FindVisualChildren<T>(child))
			{
				yield return item;
			}
		}
	}

	private void InnerWaveformButton_Click(object sender, RoutedEventArgs e)
	{
		_innerModeIndex = (_innerModeIndex + 1) % InnerModes.Length;
		UpdateInnerWaveformButton();
	}

	private void UpdateInnerWaveformButton()
	{
		InnerWaveformButton.Content = InnerModeLabels[_innerModeIndex];
	}

	private static IReadOnlyList<ThemeOption> CreateThemeOptions()
	{
		Color color = ThemeManager.GetWindowsAccentColor() ?? Color.FromRgb(209, 52, 56);
		return new _003C_003Ez__ReadOnlyArray<ThemeOption>(new ThemeOption[6]
		{
			CreateOption(AppTheme.Acrylic, "Acrylic", "Real Windows glass: the desktop blurs through the window", Solid(27, 34, 44), Solid(46, 55, 68), Solid(96, 205, byte.MaxValue)),
			CreateOption(AppTheme.AcrylicAccent, "Accent", "Acrylic glass tinted with your Windows accent colour", SolidColor(Mix(color, Colors.Black, 0.72)), SolidColor(Mix(color, Colors.Black, 0.52)), SolidColor(Mix(color, Colors.White, 0.3))),
			CreateOption(AppTheme.Studio, "Studio", "Rack-unit graphite with amber VU accents", Solid(21, 22, 25), Solid(34, 35, 39), Solid(byte.MaxValue, 180, 84)),
			CreateOption(AppTheme.AmbientGlass, "Glass", "Glass panels; the artwork glows behind the header", Solid(18, 19, 34), Solid(40, 41, 62), Solid(201, 166, byte.MaxValue)),
			CreateOption(AppTheme.Reel, "Reel", "Warm cream analog console — the daylight theme", Solid(242, 235, 221), Solid(226, 215, 189), Solid(198, 75, 30)),
			CreateOption(AppTheme.LiquidGlass, "Liquid", "Apple liquid glass: real blur, milky capsules, blue ink — beauty over duty", Solid(236, 239, 245), Solid(251, 252, 254), Solid(0, 122, byte.MaxValue))
		});
	}

	private static ThemeOption CreateOption(AppTheme theme, string name, string description, Brush background, Brush panel, Brush accent)
	{
		return new ThemeOption
		{
			Theme = theme,
			Name = name,
			Description = description,
			PreviewBackground = background,
			PreviewPanel = panel,
			PreviewAccent = accent
		};
	}

	private static SolidColorBrush Solid(byte r, byte g, byte b)
	{
		return SolidColor(Color.FromRgb(r, g, b));
	}

	private static SolidColorBrush SolidColor(Color color)
	{
		SolidColorBrush solidColorBrush = new SolidColorBrush(color);
		((Freezable)solidColorBrush).Freeze();
		return solidColorBrush;
	}

	private static Color Mix(Color from, Color to, double amount)
	{
		return Color.FromRgb((byte)Math.Round((double)(int)from.R + (double)(to.R - from.R) * amount), (byte)Math.Round((double)(int)from.G + (double)(to.G - from.G) * amount), (byte)Math.Round((double)(int)from.B + (double)(to.B - from.B) * amount));
	}

	private void ThemeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (ThemeList.SelectedItem is ThemeOption themeOption && themeOption.Theme != ThemeManager.Current)
		{
			ThemeManager.Apply(themeOption.Theme);
		}
	}

	private void AddFolderButton_Click(object sender, RoutedEventArgs e)
	{
		OpenFolderDialog openFolderDialog = new OpenFolderDialog
		{
			Title = "Choose a folder containing release folders",
			Multiselect = false
		};
		_isOpeningFolderPicker = true;
		bool valueOrDefault;
		try
		{
			valueOrDefault = openFolderDialog.ShowDialog(this) == true;
		}
		finally
		{
			_isOpeningFolderPicker = false;
		}
		if (valueOrDefault)
		{
			string path = Path.TrimEndingDirectorySeparator(openFolderDialog.FolderName);
			if (ReleaseIndexer.ContainsOldPathSegment(path))
			{
				SettingsHint.Text = "Folders inside Old cannot be added.";
				return;
			}
			if (_folders.Any((string existing) => existing.Equals(path, StringComparison.OrdinalIgnoreCase)))
			{
				SettingsHint.Text = "That folder is already listed.";
				return;
			}
			_folders.Add(path);
			FoldersList.SelectedItem = path;
			FoldersList.ScrollIntoView(path);
			SettingsHint.Text = string.Empty;
		}
	}

	private void RemoveFolderButton_Click(object sender, RoutedEventArgs e)
	{
		if (FoldersList.SelectedItem is string item)
		{
			_folders.Remove(item);
			SettingsHint.Text = string.Empty;
		}
		else
		{
			SettingsHint.Text = "Select a folder first.";
		}
	}

	private void SaveButton_Click(object sender, RoutedEventArgs e)
	{
		if (_folders.Count == 0)
		{
			SettingsHint.Text = "Add at least one search folder.";
			return;
		}
		WasSaved = true;
		_isClosing = true;
		Close();
	}

	private void InterfaceScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		InterfaceScale = Math.Round(e.NewValue, 2);
		if (InterfaceScaleValueText != null)
		{
			InterfaceScaleValueText.Text = $"{InterfaceScale:P0}";
		}
	}

	private void CancelButton_Click(object sender, RoutedEventArgs e)
	{
		_isClosing = true;
		Close();
	}

	private void Window_Deactivated(object sender, EventArgs e)
	{
		if (!_isOpeningFolderPicker && !_isClosing)
		{
			_isClosing = true;
			Close();
		}
	}

	private void Window_Closing(object? sender, CancelEventArgs e)
	{
		_isClosing = true;
		if (!WasSaved && ThemeManager.Current != _originalTheme)
		{
			ThemeManager.Apply(_originalTheme);
		}
	}

	private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (e.LeftButton == MouseButtonState.Pressed && !(e.OriginalSource is Button))
		{
			DragMove();
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.22.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/DistroClip;component/settingswindow.xaml", UriKind.Relative);
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
			((SettingsWindow)target).Deactivated += Window_Deactivated;
			((SettingsWindow)target).Closing += Window_Closing;
			break;
		case 2:
			SettingsScaleTransform = (ScaleTransform)target;
			break;
		case 3:
			((Grid)target).MouseLeftButtonDown += TitleBar_MouseLeftButtonDown;
			break;
		case 4:
			((Button)target).Click += CancelButton_Click;
			break;
		case 5:
			ThemeList = (ListBox)target;
			ThemeList.SelectionChanged += ThemeList_SelectionChanged;
			break;
		case 6:
			InterfaceScaleSlider = (Slider)target;
			InterfaceScaleSlider.ValueChanged += InterfaceScaleSlider_ValueChanged;
			break;
		case 7:
			InterfaceScaleValueText = (TextBlock)target;
			break;
		case 8:
			SearchArtworkThumbnailsCheckBox = (CheckBox)target;
			break;
		case 9:
			ValidateSearchResultsCheckBox = (CheckBox)target;
			break;
		case 10:
			RealWaveformCheckBox = (CheckBox)target;
			break;
		case 11:
			InnerWaveformButton = (Button)target;
			InnerWaveformButton.Click += InnerWaveformButton_Click;
			break;
		case 12:
			FoldersList = (ListBox)target;
			break;
		case 13:
			((Button)target).Click += AddFolderButton_Click;
			break;
		case 14:
			((Button)target).Click += RemoveFolderButton_Click;
			break;
		case 15:
			SettingsHint = (TextBlock)target;
			break;
		case 16:
			((Button)target).Click += CancelButton_Click;
			break;
		case 17:
			((Button)target).Click += SaveButton_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
