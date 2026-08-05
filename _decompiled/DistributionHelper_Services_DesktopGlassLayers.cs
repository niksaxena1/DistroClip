using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace DistributionHelper.Services;

public sealed class DesktopGlassLayers
{
	private struct RECT
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	private struct MONITORINFO
	{
		public int cbSize;

		public RECT rcMonitor;

		public RECT rcWork;

		public int dwFlags;
	}

	public const double IslandOverscan = 26.0;

	private readonly Window _window;

	private readonly Rectangle _underlay;

	private readonly Rectangle _underlayMilk;

	private readonly Rectangle _islandWallpaper;

	private readonly Border _island;

	private readonly Border[] _cards;

	private readonly DispatcherTimer _timer = new DispatcherTimer
	{
		Interval = TimeSpan.FromSeconds(2.0)
	};

	private readonly ImageBrush _underlayBrush = new ImageBrush
	{
		ViewboxUnits = BrushMappingMode.Absolute,
		Stretch = Stretch.Fill
	};

	private readonly ImageBrush _islandBrush = new ImageBrush
	{
		ViewboxUnits = BrushMappingMode.Absolute,
		Stretch = Stretch.Fill
	};

	private WallpaperInfo? _wallpaper;

	private bool _running;

	private bool _active;

	private bool _cardsGlassed;

	private DateTime _lastCardUpdate = DateTime.MinValue;

	public DesktopGlassLayers(Window window, Rectangle underlay, Rectangle underlayMilk, Rectangle islandWallpaper, Border island, Border[] cards)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Expected O, but got Unknown
		_window = window;
		_underlay = underlay;
		_underlayMilk = underlayMilk;
		_islandWallpaper = islandWallpaper;
		_island = island;
		_cards = cards;
		_timer.Tick += delegate
		{
			TimerTick();
		};
	}

	public void Start()
	{
		if (!_running)
		{
			_running = true;
			_wallpaper = WallpaperMapper.LoadCurrent();
			_underlay.Fill = _underlayBrush;
			_islandWallpaper.Fill = _islandBrush;
			_underlay.Visibility = Visibility.Visible;
			_underlayMilk.Visibility = Visibility.Visible;
			_window.LocationChanged += Window_Changed;
			_window.SizeChanged += Window_SizeChanged;
			_timer.Start();
			Refresh();
		}
	}

	public void Stop()
	{
		if (_running)
		{
			_running = false;
			_timer.Stop();
			_window.LocationChanged -= Window_Changed;
			_window.SizeChanged -= Window_SizeChanged;
			SetActive(active: false);
			_underlay.BeginAnimation(UIElement.OpacityProperty, null);
			_underlayMilk.BeginAnimation(UIElement.OpacityProperty, null);
			_islandWallpaper.BeginAnimation(UIElement.OpacityProperty, null);
			_underlay.Opacity = 0.0;
			_underlayMilk.Opacity = 0.0;
			_islandWallpaper.Opacity = 0.0;
			_underlay.Visibility = Visibility.Collapsed;
			_underlayMilk.Visibility = Visibility.Collapsed;
			RestoreCards();
		}
	}

	public void NotifyScrolled()
	{
		if (_running && _active)
		{
			UpdateCards(force: false);
		}
	}

	public void RequestUpdate()
	{
		Refresh();
	}

	private void Window_Changed(object? sender, EventArgs e)
	{
		Refresh();
	}

	private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
	{
		Refresh();
	}

	private void TimerTick()
	{
		ReloadWallpaperIfChanged();
		Refresh();
	}

	private void Refresh()
	{
		if (!_running)
		{
			return;
		}
		try
		{
			SetActive(_wallpaper != null && !IsAnotherWindowBehind());
			if (_active)
			{
				UpdateViewboxes();
				UpdateCards(force: false);
			}
		}
		catch (Exception exception)
		{
			ErrorLog.Write(exception);
		}
	}

	private void ReloadWallpaperIfChanged()
	{
		try
		{
			string text = WallpaperMapper.ReadWallpaperPath();
			if (!(text == _wallpaper?.SourcePath) || (_wallpaper != null && !(File.GetLastWriteTimeUtc(text) == _wallpaper.SourceWriteTimeUtc)))
			{
				_wallpaper = WallpaperMapper.LoadCurrent();
			}
		}
		catch (Exception exception)
		{
			ErrorLog.Write(exception);
		}
	}

	private void SetActive(bool active)
	{
		if (_active != active)
		{
			_active = active;
			DoubleAnimation animation = new DoubleAnimation(active ? 1 : 0, TimeSpan.FromMilliseconds(280.0))
			{
				EasingFunction = new SineEase
				{
					EasingMode = EasingMode.EaseOut
				}
			};
			_underlay.BeginAnimation(UIElement.OpacityProperty, animation);
			_underlayMilk.BeginAnimation(UIElement.OpacityProperty, animation);
			_islandWallpaper.BeginAnimation(UIElement.OpacityProperty, animation);
			if (active)
			{
				UpdateViewboxes();
				UpdateCards(force: true);
			}
			else
			{
				RestoreCards();
			}
		}
	}

	private void UpdateViewboxes()
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		if (_wallpaper != null && _underlay.IsLoaded)
		{
			(Rect Monitor, Rect VirtualScreen) monitorRects = GetMonitorRects();
			Rect item = monitorRects.Monitor;
			Rect item2 = monitorRects.VirtualScreen;
			BitmapSource image = _wallpaper.Image;
			if (TryGetScreenRect(_underlay, out var rect))
			{
				_underlayBrush.ImageSource = image;
				_underlayBrush.Viewbox = ToImageDips(WallpaperMapper.MapScreenRectToImage(rect, item, item2, image.PixelWidth, image.PixelHeight, _wallpaper.Fit), image);
			}
			if (_island.IsVisible && TryGetScreenRect(_island, out var rect2))
			{
				double num = 26.0 * (((Rect)(ref rect2)).Width / Math.Max(1.0, _island.ActualWidth));
				((Rect)(ref rect2)).Inflate(num, num);
				_islandBrush.ImageSource = image;
				_islandBrush.Viewbox = ToImageDips(WallpaperMapper.MapScreenRectToImage(rect2, item, item2, image.PixelWidth, image.PixelHeight, _wallpaper.Fit), image);
			}
		}
	}

	private void UpdateCards(bool force)
	{
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_014e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0191: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
		if (_wallpaper == null)
		{
			return;
		}
		DateTime utcNow = DateTime.UtcNow;
		if (!force && (utcNow - _lastCardUpdate).TotalMilliseconds < 33.0)
		{
			return;
		}
		_lastCardUpdate = utcNow;
		(Rect Monitor, Rect VirtualScreen) monitorRects = GetMonitorRects();
		Rect item = monitorRects.Monitor;
		Rect item2 = monitorRects.VirtualScreen;
		BitmapSource image = _wallpaper.Image;
		Brush brush = _window.TryFindResource("RaisedPanelBrush") as Brush;
		Border[] cards = _cards;
		Rect rect = default(Rect);
		Border[] array = cards;
		foreach (Border border in array)
		{
			if (!border.IsVisible || !TryGetScreenRect(border, out var rect2))
			{
				continue;
			}
			Rect val = Rect.Intersect(WallpaperMapper.MapScreenRectToImage(rect2, item, item2, image.PixelWidth, image.PixelHeight, _wallpaper.Fit), new Rect(0.0, 0.0, (double)image.PixelWidth, (double)image.PixelHeight));
			if (!((Rect)(ref val)).IsEmpty && !(((Rect)(ref val)).Width < 2.0) && !(((Rect)(ref val)).Height < 2.0))
			{
				CroppedBitmap imageSource = new CroppedBitmap(image, new Int32Rect((int)((Rect)(ref val)).X, (int)((Rect)(ref val)).Y, (int)((Rect)(ref val)).Width, (int)((Rect)(ref val)).Height));
				((Rect)(ref rect))..ctor(0.0, 0.0, ((Rect)(ref val)).Width, ((Rect)(ref val)).Height);
				DrawingGroup drawingGroup = new DrawingGroup();
				drawingGroup.Children.Add(new ImageDrawing(imageSource, rect));
				if (brush != null)
				{
					drawingGroup.Children.Add(new GeometryDrawing(brush, null, new RectangleGeometry(rect)));
				}
				((Freezable)drawingGroup).Freeze();
				border.Background = new DrawingBrush(drawingGroup)
				{
					Stretch = Stretch.Fill
				};
			}
		}
		_cardsGlassed = true;
	}

	private void RestoreCards()
	{
		if (_cardsGlassed)
		{
			_cardsGlassed = false;
			Border[] cards = _cards;
			for (int i = 0; i < cards.Length; i++)
			{
				cards[i].SetResourceReference(Border.BackgroundProperty, "RaisedPanelBrush");
			}
		}
	}

	private static Rect ToImageDips(Rect pixelRect, BitmapSource image)
	{
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		if (((Rect)(ref pixelRect)).IsEmpty)
		{
			return new Rect(0.0, 0.0, 1.0, 1.0);
		}
		double num = image.Width / (double)image.PixelWidth;
		double num2 = image.Height / (double)image.PixelHeight;
		return new Rect(((Rect)(ref pixelRect)).X * num, ((Rect)(ref pixelRect)).Y * num2, Math.Max(0.01, ((Rect)(ref pixelRect)).Width * num), Math.Max(0.01, ((Rect)(ref pixelRect)).Height * num2));
	}

	private static bool TryGetScreenRect(FrameworkElement element, out Rect rect)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		rect = Rect.Empty;
		if (!element.IsLoaded || PresentationSource.FromVisual(element) == null || element.ActualWidth < 1.0 || element.ActualHeight < 1.0)
		{
			return false;
		}
		Point val = element.PointToScreen(new Point(0.0, 0.0));
		Point val2 = element.PointToScreen(new Point(element.ActualWidth, element.ActualHeight));
		rect = new Rect(val, val2);
		if (((Rect)(ref rect)).Width >= 1.0)
		{
			return ((Rect)(ref rect)).Height >= 1.0;
		}
		return false;
	}

	private (Rect Monitor, Rect VirtualScreen) GetMonitorRects()
	{
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		Rect val = default(Rect);
		((Rect)(ref val))..ctor((double)GetSystemMetrics(76), (double)GetSystemMetrics(77), (double)Math.Max(1, GetSystemMetrics(78)), (double)Math.Max(1, GetSystemMetrics(79)));
		nint handle = new WindowInteropHelper(_window).Handle;
		if (handle != IntPtr.Zero)
		{
			nint num = MonitorFromWindow(handle, 2u);
			MONITORINFO lpmi = new MONITORINFO
			{
				cbSize = Marshal.SizeOf<MONITORINFO>()
			};
			if (num != IntPtr.Zero && GetMonitorInfoW(num, ref lpmi))
			{
				return (Monitor: new Rect((double)lpmi.rcMonitor.Left, (double)lpmi.rcMonitor.Top, (double)Math.Max(1, lpmi.rcMonitor.Right - lpmi.rcMonitor.Left), (double)Math.Max(1, lpmi.rcMonitor.Bottom - lpmi.rcMonitor.Top)), VirtualScreen: val);
			}
		}
		return (Monitor: val, VirtualScreen: val);
	}

	private bool IsAnotherWindowBehind()
	{
		nint handle = new WindowInteropHelper(_window).Handle;
		if (handle == IntPtr.Zero || !GetFrameBounds(handle, out var rect))
		{
			return true;
		}
		nint window = GetWindow(handle, 2u);
		int num = 0;
		while (window != IntPtr.Zero && num++ < 1024)
		{
			if (IsWindowVisible(window) && !IsIconic(window) && !IsCloaked(window))
			{
				string classNameOf = GetClassNameOf(window);
				if (classNameOf == "Progman" || classNameOf == "WorkerW")
				{
					return false;
				}
				if (GetFrameBounds(window, out var rect2) && rect2.Right > rect.Left && rect2.Left < rect.Right && rect2.Bottom > rect.Top && rect2.Top < rect.Bottom)
				{
					return true;
				}
			}
			window = GetWindow(window, 2u);
		}
		return false;
	}

	private static bool GetFrameBounds(nint hwnd, out RECT rect)
	{
		if (DwmGetWindowAttribute(hwnd, 9, out rect, Marshal.SizeOf<RECT>()) == 0)
		{
			if (rect.Right > rect.Left)
			{
				return rect.Bottom > rect.Top;
			}
			return false;
		}
		if (GetWindowRect(hwnd, out rect) && rect.Right > rect.Left)
		{
			return rect.Bottom > rect.Top;
		}
		return false;
	}

	private static bool IsCloaked(nint hwnd)
	{
		if (DwmGetWindowAttribute(hwnd, 14, out int pvAttribute, 4) == 0)
		{
			return pvAttribute != 0;
		}
		return false;
	}

	private static string GetClassNameOf(nint hwnd)
	{
		StringBuilder stringBuilder = new StringBuilder(64);
		GetClassNameW(hwnd, stringBuilder, stringBuilder.Capacity);
		return stringBuilder.ToString();
	}

	[DllImport("user32.dll")]
	private static extern nint GetWindow(nint hWnd, uint uCmd);

	[DllImport("user32.dll")]
	private static extern bool IsWindowVisible(nint hWnd);

	[DllImport("user32.dll")]
	private static extern bool IsIconic(nint hWnd);

	[DllImport("user32.dll")]
	private static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetClassNameW(nint hWnd, StringBuilder lpClassName, int nMaxCount);

	[DllImport("user32.dll")]
	private static extern int GetSystemMetrics(int nIndex);

	[DllImport("user32.dll")]
	private static extern nint MonitorFromWindow(nint hwnd, uint dwFlags);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern bool GetMonitorInfoW(nint hMonitor, ref MONITORINFO lpmi);

	[DllImport("dwmapi.dll")]
	private static extern int DwmGetWindowAttribute(nint hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

	[DllImport("dwmapi.dll")]
	private static extern int DwmGetWindowAttribute(nint hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);
}
