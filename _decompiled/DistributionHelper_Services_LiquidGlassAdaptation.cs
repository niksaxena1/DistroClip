using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DistributionHelper.Services;

public sealed class LiquidGlassAdaptation
{
	private readonly Window _window;

	private readonly DispatcherTimer _probeTimer = new DispatcherTimer
	{
		Interval = TimeSpan.FromSeconds(2.0)
	};

	private readonly DispatcherTimer _driftTimer = new DispatcherTimer
	{
		Interval = TimeSpan.FromMilliseconds(66.0)
	};

	private readonly List<string> _overrideKeys = new List<string>();

	private readonly List<string> _windowOverrideKeys = new List<string>();

	private double _driftPhase;

	private RadialGradientBrush? _rimBrush;

	private RadialGradientBrush? _chipRimBrush;

	private DateTime _lastPointerUpdate = DateTime.MinValue;

	private bool _running;

	private bool _darkBackdrop;

	private Color _lastMilkTint = Colors.Transparent;

	public LiquidGlassAdaptation(Window window)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Expected O, but got Unknown
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Expected O, but got Unknown
		_window = window;
		_probeTimer.Tick += delegate
		{
			ProbeBackdrop();
		};
		_driftTimer.Tick += delegate
		{
			DriftRim();
		};
	}

	public void Start()
	{
		if (!_running)
		{
			_running = true;
			_darkBackdrop = false;
			InstallPointerRim();
			_window.PreviewMouseMove += Window_PreviewMouseMove;
			_window.LocationChanged += Window_Moved;
			_window.SizeChanged += Window_Resized;
			_probeTimer.Start();
			_driftTimer.Start();
			ProbeBackdrop();
		}
	}

	public void Stop()
	{
		if (!_running)
		{
			return;
		}
		_running = false;
		_probeTimer.Stop();
		_driftTimer.Stop();
		_window.PreviewMouseMove -= Window_PreviewMouseMove;
		_window.LocationChanged -= Window_Moved;
		_window.SizeChanged -= Window_Resized;
		Application current = Application.Current;
		if (current != null)
		{
			foreach (string overrideKey in _overrideKeys)
			{
				current.Resources.Remove(overrideKey);
			}
		}
		_overrideKeys.Clear();
		foreach (string windowOverrideKey in _windowOverrideKeys)
		{
			_window.Resources.Remove(windowOverrideKey);
		}
		_windowOverrideKeys.Clear();
		_rimBrush = null;
		_chipRimBrush = null;
		_lastMilkTint = Colors.Transparent;
	}

	private void Window_Moved(object? sender, EventArgs e)
	{
		ProbeBackdrop();
	}

	private void Window_Resized(object sender, SizeChangedEventArgs e)
	{
		ProbeBackdrop();
	}

	private void Window_PreviewMouseMove(object sender, MouseEventArgs e)
	{
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		if (_rimBrush != null && !((Freezable)_rimBrush).IsFrozen && !((DateTime.UtcNow - _lastPointerUpdate).TotalMilliseconds < 33.0))
		{
			_lastPointerUpdate = DateTime.UtcNow;
			Point position = e.GetPosition(_window);
			double num = Math.Clamp(((Point)(ref position)).X / Math.Max(1.0, _window.ActualWidth), 0.0, 1.0);
			double num2 = Math.Clamp(((Point)(ref position)).Y / Math.Max(1.0, _window.ActualHeight), 0.0, 1.0);
			Point val = default(Point);
			((Point)(ref val))..ctor(num, num2);
			_rimBrush.GradientOrigin = val;
			_rimBrush.Center = val;
			RadialGradientBrush chipRimBrush = _chipRimBrush;
			if (chipRimBrush != null && !((Freezable)chipRimBrush).IsFrozen)
			{
				_chipRimBrush.GradientOrigin = val;
				_chipRimBrush.Center = val;
			}
		}
	}

	private void DriftRim()
	{
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		if (_rimBrush != null && !((Freezable)_rimBrush).IsFrozen && !((DateTime.UtcNow - _lastPointerUpdate).TotalSeconds < 3.5))
		{
			_driftPhase += 0.066;
			Point val = default(Point);
			((Point)(ref val))..ctor(0.5 + 0.34 * Math.Sin(_driftPhase * 0.23), 0.4 + 0.3 * Math.Sin(_driftPhase * 0.157 + 1.2));
			_rimBrush.GradientOrigin = val;
			_rimBrush.Center = val;
			RadialGradientBrush chipRimBrush = _chipRimBrush;
			if (chipRimBrush != null && !((Freezable)chipRimBrush).IsFrozen)
			{
				_chipRimBrush.GradientOrigin = val;
				_chipRimBrush.Center = val;
			}
		}
	}

	private void InstallPointerRim()
	{
		_rimBrush = CreateRimBrush(250, 89);
		_chipRimBrush = CreateRimBrush(237, 77);
		SetWindowOverride("BorderBrush", _rimBrush);
		SetWindowOverride("ChipBorderBrush", _chipRimBrush);
	}

	private void SetWindowOverride(string key, Brush brush)
	{
		_window.Resources[key] = brush;
		if (!_windowOverrideKeys.Contains(key))
		{
			_windowOverrideKeys.Add(key);
		}
	}

	private RadialGradientBrush CreateRimBrush(byte brightAlpha, byte shadeAlpha)
	{
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		Color color = (_darkBackdrop ? Colors.White : Colors.White);
		Color color2 = (_darkBackdrop ? Color.FromRgb(154, 166, 192) : Color.FromRgb(135, 152, 184));
		return new RadialGradientBrush
		{
			MappingMode = BrushMappingMode.RelativeToBoundingBox,
			RadiusX = 1.15,
			RadiusY = 1.15,
			GradientOrigin = new Point(0.3, 0.0),
			Center = new Point(0.3, 0.0),
			GradientStops = new GradientStopCollection
			{
				new GradientStop(Color.FromArgb(brightAlpha, color.R, color.G, color.B), 0.0),
				new GradientStop(Color.FromArgb(102, byte.MaxValue, byte.MaxValue, byte.MaxValue), 0.55),
				new GradientStop(Color.FromArgb(shadeAlpha, color2.R, color2.G, color2.B), 1.0)
			}
		};
	}

	[DllImport("user32.dll")]
	private static extern nint GetDC(nint hWnd);

	[DllImport("user32.dll")]
	private static extern int ReleaseDC(nint hWnd, nint hdc);

	[DllImport("gdi32.dll")]
	private static extern uint GetPixel(nint hdc, int x, int y);

	private void ProbeBackdrop()
	{
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		if (!_running)
		{
			return;
		}
		try
		{
			PresentationSource presentationSource = PresentationSource.FromVisual(_window);
			double? num;
			if (presentationSource == null)
			{
				num = null;
			}
			else
			{
				CompositionTarget compositionTarget = presentationSource.CompositionTarget;
				if (compositionTarget == null)
				{
					num = null;
				}
				else
				{
					Matrix transformToDevice = compositionTarget.TransformToDevice;
					num = ((Matrix)(ref transformToDevice)).M11;
				}
			}
			double num2 = num ?? 1.0;
			int num3 = (int)(_window.Left * num2);
			int num4 = (int)(_window.Top * num2);
			int num5 = (int)((_window.Left + _window.ActualWidth) * num2);
			int num6 = (int)((_window.Top + _window.ActualHeight) * num2);
			_003C_003Ey__InlineArray8<(int, int)> buffer = default(_003C_003Ey__InlineArray8<(int, int)>);
			global::<PrivateImplementationDetails>.InlineArrayFirstElementRef<_003C_003Ey__InlineArray8<(int, int)>, (int, int)>(ref buffer) = (num3 - 12, num4 + (num6 - num4) / 4);
			global::<PrivateImplementationDetails>.InlineArrayElementRef<_003C_003Ey__InlineArray8<(int, int)>, (int, int)>(ref buffer, 1) = (num3 - 12, num4 + (num6 - num4) / 2);
			global::<PrivateImplementationDetails>.InlineArrayElementRef<_003C_003Ey__InlineArray8<(int, int)>, (int, int)>(ref buffer, 2) = (num3 - 12, num6 - (num6 - num4) / 4);
			global::<PrivateImplementationDetails>.InlineArrayElementRef<_003C_003Ey__InlineArray8<(int, int)>, (int, int)>(ref buffer, 3) = (num5 + 12, num4 + (num6 - num4) / 4);
			global::<PrivateImplementationDetails>.InlineArrayElementRef<_003C_003Ey__InlineArray8<(int, int)>, (int, int)>(ref buffer, 4) = (num5 + 12, num4 + (num6 - num4) / 2);
			global::<PrivateImplementationDetails>.InlineArrayElementRef<_003C_003Ey__InlineArray8<(int, int)>, (int, int)>(ref buffer, 5) = (num5 + 12, num6 - (num6 - num4) / 4);
			global::<PrivateImplementationDetails>.InlineArrayElementRef<_003C_003Ey__InlineArray8<(int, int)>, (int, int)>(ref buffer, 6) = (num3 + (num5 - num3) / 2, num4 - 12);
			global::<PrivateImplementationDetails>.InlineArrayElementRef<_003C_003Ey__InlineArray8<(int, int)>, (int, int)>(ref buffer, 7) = (num3 + (num5 - num3) / 2, num6 + 12);
			Span<(int, int)> span = global::<PrivateImplementationDetails>.InlineArrayAsSpan<_003C_003Ey__InlineArray8<(int, int)>, (int, int)>(ref buffer, 8);
			nint dC = GetDC(IntPtr.Zero);
			try
			{
				long num7 = 0L;
				long num8 = 0L;
				long num9 = 0L;
				int num10 = 0;
				Span<(int, int)> span2 = span;
				for (int i = 0; i < span2.Length; i++)
				{
					(int, int) tuple = span2[i];
					int item = tuple.Item1;
					int item2 = tuple.Item2;
					uint pixel = GetPixel(dC, item, item2);
					if (pixel != uint.MaxValue)
					{
						num7 += pixel & 0xFF;
						num8 += (pixel >> 8) & 0xFF;
						num9 += (pixel >> 16) & 0xFF;
						num10++;
					}
				}
				if (num10 != 0)
				{
					Color backdrop = Color.FromRgb((byte)(num7 / num10), (byte)(num8 / num10), (byte)(num9 / num10));
					Apply(backdrop);
				}
			}
			finally
			{
				ReleaseDC(IntPtr.Zero, dC);
			}
		}
		catch (Exception exception)
		{
			ErrorLog.Write(exception);
		}
	}

	private void Apply(Color backdrop)
	{
		double num = 0.2126 * (double)(int)backdrop.R + 0.7152 * (double)(int)backdrop.G + 0.0722 * (double)(int)backdrop.B;
		bool darkBackdrop = _darkBackdrop;
		if (!_darkBackdrop && num < 85.0)
		{
			_darkBackdrop = true;
		}
		else if (_darkBackdrop && num > 115.0)
		{
			_darkBackdrop = false;
		}
		if (_darkBackdrop != darkBackdrop)
		{
			if (_darkBackdrop)
			{
				ApplyDarkInk();
			}
			else
			{
				ClearInkOverrides();
			}
			_lastMilkTint = Colors.Transparent;
		}
		Color color = Color.FromRgb((byte)Math.Min(255, backdrop.R + 40), (byte)Math.Min(255, backdrop.G + 40), (byte)Math.Min(255, backdrop.B + 40));
		if (ColorDistance(color, _lastMilkTint) > 12.0)
		{
			_lastMilkTint = color;
			SetOverride("AppBackgroundBrush", CreateMilk(color));
		}
	}

	private Brush CreateMilk(Color tint)
	{
		Color color2;
		Color color3;
		if (!_darkBackdrop)
		{
			Color color = Color.FromArgb(181, 251, 251, 253);
			color2 = Color.FromArgb(166, 241, 242, 247);
			color3 = color;
		}
		else
		{
			Color color4 = Color.FromArgb(196, 36, 36, 40);
			color2 = Color.FromArgb(184, 26, 26, 30);
			color3 = color4;
		}
		LinearGradientBrush linearGradientBrush = new LinearGradientBrush(Mix(color3, _darkBackdrop ? 0.06 : 0.12), Mix(color2, _darkBackdrop ? 0.05 : 0.1), 90.0);
		((Freezable)linearGradientBrush).Freeze();
		return linearGradientBrush;
		Color Mix(Color color5, double amount)
		{
			return Color.FromArgb(color5.A, (byte)((double)(int)color5.R + (double)(tint.R - color5.R) * amount), (byte)((double)(int)color5.G + (double)(tint.G - color5.G) * amount), (byte)((double)(int)color5.B + (double)(tint.B - color5.B) * amount));
		}
	}

	private void ApplyDarkInk()
	{
		SetOverride("TextBrush", Frozen(Color.FromRgb(245, 245, 247)));
		SetOverride("AppTitleBrush", Frozen(Color.FromRgb(245, 245, 247)));
		SetOverride("MutedTextBrush", Frozen(Color.FromRgb(185, 185, 194)));
		SetOverride("FaintTextBrush", Frozen(Color.FromRgb(152, 152, 159)));
		SetOverride("DividerBrush", Frozen(Color.FromArgb(48, byte.MaxValue, byte.MaxValue, byte.MaxValue)));
		SetOverride("SubtleHoverBrush", Frozen(Color.FromArgb(33, byte.MaxValue, byte.MaxValue, byte.MaxValue)));
		SetOverride("SubtlePressedBrush", Frozen(Color.FromArgb(51, byte.MaxValue, byte.MaxValue, byte.MaxValue)));
		SetOverride("PanelBrush", FrozenGradient(64, 43));
		SetOverride("RaisedPanelBrush", FrozenGradient(77, 51));
		SetOverride("PanelHoverBrush", FrozenGradient(97, 71));
		SetOverride("ChipBrush", FrozenGradient(69, 48));
		SetOverride("CardScrimBrush", Frozen(Color.FromArgb(217, 26, 26, 30)));
		SetOverride("ToastBackgroundBrush", Frozen(Color.FromArgb(242, 42, 42, 46)));
		SetOverride("TooltipBackgroundBrush", Frozen(Color.FromArgb(245, 42, 42, 46)));
		SetOverride("ProgressTrackBrush", Frozen(Color.FromArgb(51, byte.MaxValue, byte.MaxValue, byte.MaxValue)));
		SetOverride("ScrollThumbBrush", Frozen(Color.FromArgb(89, byte.MaxValue, byte.MaxValue, byte.MaxValue)));
		SetOverride("OverlayScrollThumbBrush", Frozen(Color.FromArgb(140, byte.MaxValue, byte.MaxValue, byte.MaxValue)));
		SetOverride("ThumbnailGlyphBrush", Frozen(Color.FromRgb(185, 185, 194)));
		SetOverride("PlumBrush", Frozen(Color.FromRgb(185, 185, 194)));
		SetOverride("AccentBrush", Frozen(Color.FromRgb(10, 132, byte.MaxValue)));
		SetOverride("CoverBrush", Frozen(Color.FromRgb(byte.MaxValue, 159, 10)));
		SetOverride("DangerBrush", Frozen(Color.FromRgb(byte.MaxValue, 69, 58)));
		SetOverride("TealBrush", Frozen(Color.FromRgb(48, 209, 88)));
		SetOverride("AttentionTextBrush", Frozen(Color.FromRgb(byte.MaxValue, 192, 102)));
		SetOverride("WarningTextBrush", Frozen(Color.FromRgb(byte.MaxValue, 217, 158)));
	}

	private void ClearInkOverrides()
	{
		Application current = Application.Current;
		if (current == null)
		{
			return;
		}
		foreach (string item in _overrideKeys.Where(delegate(string key)
		{
			bool flag;
			switch (key)
			{
			case "BorderBrush":
			case "ChipBorderBrush":
			case "AppBackgroundBrush":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			return !flag;
		}).ToList())
		{
			current.Resources.Remove(item);
			_overrideKeys.Remove(item);
		}
	}

	private static LinearGradientBrush FrozenGradient(byte topAlpha, byte bottomAlpha)
	{
		LinearGradientBrush linearGradientBrush = new LinearGradientBrush(Color.FromArgb(topAlpha, byte.MaxValue, byte.MaxValue, byte.MaxValue), Color.FromArgb(bottomAlpha, byte.MaxValue, byte.MaxValue, byte.MaxValue), 90.0);
		((Freezable)linearGradientBrush).Freeze();
		return linearGradientBrush;
	}

	private static SolidColorBrush Frozen(Color color)
	{
		SolidColorBrush solidColorBrush = new SolidColorBrush(color);
		((Freezable)solidColorBrush).Freeze();
		return solidColorBrush;
	}

	private void SetOverride(string key, Brush brush)
	{
		Application current = Application.Current;
		if (current != null)
		{
			current.Resources[key] = brush;
			if (!_overrideKeys.Contains(key))
			{
				_overrideKeys.Add(key);
			}
		}
	}

	private static double ColorDistance(Color a, Color b)
	{
		return Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);
	}
}
