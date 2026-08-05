using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DistributionHelper.Models;
using Microsoft.Win32;

namespace DistributionHelper.Services;

public static class ThemeManager
{
	private struct DwmMargins
	{
		public int Left;

		public int Right;

		public int Top;

		public int Bottom;
	}

	private static bool _acrylicUnavailable;

	private static readonly List<string> _accentOverrideKeys = new List<string>();

	private const int DwmwaUseImmersiveDarkMode = 20;

	private const int DwmwaWindowCornerPreference = 33;

	private const int DwmwaSystemBackdropType = 38;

	private const int DwmsbtNone = 1;

	private const int DwmsbtTransientWindow = 3;

	private const int DwmwcpRound = 2;

	private const int WmActivate = 6;

	private const int WmNcActivate = 134;

	private const int WmDwmCompositionChanged = 798;

	public static AppTheme Current { get; private set; } = AppTheme.Acrylic;

	public static event EventHandler? ThemeChanged;

	private static bool IsAcrylicTheme(AppTheme theme)
	{
		if ((uint)theme <= 1u || theme == AppTheme.LiquidGlass)
		{
			return true;
		}
		return false;
	}

	public static void Apply(AppTheme theme)
	{
		Application current = Application.Current;
		if (current == null)
		{
			return;
		}
		ResourceDictionary resourceDictionary = new ResourceDictionary
		{
			Source = new Uri($"pack://application:,,,/Themes/{theme}.xaml")
		};
		Collection<ResourceDictionary> mergedDictionaries = current.Resources.MergedDictionaries;
		ResourceDictionary resourceDictionary2 = mergedDictionaries.FirstOrDefault((ResourceDictionary item) => item.Source?.OriginalString.Contains("/Themes/", StringComparison.OrdinalIgnoreCase) ?? false);
		if (resourceDictionary2 != null)
		{
			mergedDictionaries[mergedDictionaries.IndexOf(resourceDictionary2)] = resourceDictionary;
		}
		else
		{
			mergedDictionaries.Add(resourceDictionary);
		}
		foreach (string accentOverrideKey in _accentOverrideKeys)
		{
			current.Resources.Remove(accentOverrideKey);
		}
		_accentOverrideKeys.Clear();
		current.Resources.Remove("AppBackgroundBrush");
		current.Resources.Remove("WindowChromeBackgroundBrush");
		if (theme == AppTheme.AcrylicAccent)
		{
			ApplyAcrylicAccentTint(current);
		}
		Current = theme;
		foreach (Window window in current.Windows)
		{
			ApplyWindowEffects(window);
		}
		ThemeManager.ThemeChanged?.Invoke(null, EventArgs.Empty);
	}

	public static void ApplyWindowEffects(Window window)
	{
		if (!window.AllowsTransparency)
		{
			bool flag = IsAcrylicTheme(Current) && !_acrylicUnavailable;
			bool flag2 = TryApplyBackdrop(window, flag);
			if (flag && !flag2)
			{
				_acrylicUnavailable = true;
				UseOpaqueAcrylicFallback();
			}
		}
	}

	public static Color? GetWindowsAccentColor()
	{
		try
		{
			if ((Registry.GetValue("HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Accent", "AccentColorMenu", null) ?? Registry.GetValue("HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\DWM", "AccentColor", null)) is int num)
			{
				return Color.FromRgb((byte)(num & 0xFF), (byte)((num >> 8) & 0xFF), (byte)((num >> 16) & 0xFF));
			}
		}
		catch (Exception exception)
		{
			ErrorLog.Write(exception);
		}
		return null;
	}

	private static void SetAccentOverride(Application application, string key, Brush brush)
	{
		((Freezable)brush).Freeze();
		application.Resources[key] = brush;
		_accentOverrideKeys.Add(key);
	}

	private static Color Mix(Color from, Color to, double amount)
	{
		return Color.FromRgb((byte)Math.Round((double)(int)from.R + (double)(to.R - from.R) * amount), (byte)Math.Round((double)(int)from.G + (double)(to.G - from.G) * amount), (byte)Math.Round((double)(int)from.B + (double)(to.B - from.B) * amount));
	}

	private static Color WithAlpha(Color color, byte alpha)
	{
		return Color.FromArgb(alpha, color.R, color.G, color.B);
	}

	private static void UseOpaqueAcrylicFallback()
	{
		Application current = Application.Current;
		AppTheme current2 = Current;
		Color color;
		if (current2 != AppTheme.AcrylicAccent)
		{
			if (current2 != AppTheme.LiquidGlass)
			{
				goto IL_0058;
			}
			color = Color.FromRgb(242, 242, 247);
		}
		else
		{
			Color? windowsAccentColor = GetWindowsAccentColor();
			if (!windowsAccentColor.HasValue)
			{
				goto IL_0058;
			}
			color = Mix(windowsAccentColor.GetValueOrDefault(), Colors.Black, 0.8);
		}
		goto IL_0064;
		IL_0058:
		color = Color.FromRgb(20, 25, 34);
		goto IL_0064;
		IL_0064:
		Color color2 = color;
		SolidColorBrush solidColorBrush = new SolidColorBrush(color2);
		((Freezable)solidColorBrush).Freeze();
		current.Resources["AppBackgroundBrush"] = solidColorBrush;
		SolidColorBrush solidColorBrush2 = new SolidColorBrush(Mix(color2, Colors.Black, 0.2));
		((Freezable)solidColorBrush2).Freeze();
		current.Resources["WindowChromeBackgroundBrush"] = solidColorBrush2;
	}

	private static void ApplyAcrylicAccentTint(Application application)
	{
		Color? windowsAccentColor = GetWindowsAccentColor();
		if (windowsAccentColor.HasValue)
		{
			Color valueOrDefault = windowsAccentColor.GetValueOrDefault();
			Color color = ((0.2126 * (double)(int)valueOrDefault.R + 0.7152 * (double)(int)valueOrDefault.G + 0.0722 * (double)(int)valueOrDefault.B < 110.0) ? Mix(valueOrDefault, Colors.White, 0.35) : valueOrDefault);
			double num = 0.2126 * (double)(int)color.R + 0.7152 * (double)(int)color.G + 0.0722 * (double)(int)color.B;
			Color color2 = Mix(valueOrDefault, Colors.Black, 0.8);
			SetAccentOverride(application, "AppBackgroundBrush", new SolidColorBrush(WithAlpha(color2, 153)));
			SetAccentOverride(application, "DialogBackgroundBrush", new SolidColorBrush(Mix(valueOrDefault, Colors.Black, 0.76)));
			SetAccentOverride(application, "PopupBackgroundBrush", new SolidColorBrush(WithAlpha(color2, 242)));
			SetAccentOverride(application, "ToastBackgroundBrush", new SolidColorBrush(WithAlpha(color2, 242)));
			SetAccentOverride(application, "TooltipBackgroundBrush", new SolidColorBrush(WithAlpha(Mix(valueOrDefault, Colors.Black, 0.84), 245)));
			SetAccentOverride(application, "AccentBrush", new SolidColorBrush(color));
			SetAccentOverride(application, "OnAccentBrush", new SolidColorBrush((num > 120.0) ? Mix(valueOrDefault, Colors.Black, 0.85) : Colors.White));
			SetAccentOverride(application, "AccentHoverBrush", new SolidColorBrush(Mix(color, Colors.White, 0.25)));
			SetAccentOverride(application, "AccentDarkBrush", new SolidColorBrush(WithAlpha(color, 42)));
			SetAccentOverride(application, "SelectedBrush", new SolidColorBrush(WithAlpha(color, 64)));
			SetAccentOverride(application, "FocusRingBrush", new SolidColorBrush(WithAlpha(color, 138)));
			SetAccentOverride(application, "ToastBorderBrush", new SolidColorBrush(WithAlpha(color, 102)));
			SetAccentOverride(application, "ChipBrush", new SolidColorBrush(WithAlpha(color, 31)));
			SetAccentOverride(application, "ChipBorderBrush", new SolidColorBrush(WithAlpha(color, 82)));
		}
	}

	[DllImport("dwmapi.dll", ExactSpelling = true)]
	private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);

	[DllImport("dwmapi.dll", ExactSpelling = true)]
	private static extern int DwmExtendFrameIntoClientArea(nint hwnd, ref DwmMargins margins);

	[DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "DefWindowProcW")]
	private static extern nint DefWindowProc(nint hWnd, int msg, nint wParam, nint lParam);

	public static void AttachBackdropActivationHook(Window window)
	{
		nint handle = new WindowInteropHelper(window).Handle;
		if (handle != IntPtr.Zero)
		{
			HwndSource.FromHwnd(handle)?.AddHook(AcrylicWindowHook);
		}
	}

	private static nint AcrylicWindowHook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
	{
		if (!IsAcrylicTheme(Current) || _acrylicUnavailable)
		{
			return IntPtr.Zero;
		}
		switch (msg)
		{
		case 134:
			handled = true;
			return DefWindowProc(hwnd, 134, 1, new IntPtr(-1));
		case 6:
		case 798:
		{
			Application current = Application.Current;
			if (current != null)
			{
				((DispatcherObject)current).Dispatcher.BeginInvoke((DispatcherPriority)4, (Delegate)(Action)delegate
				{
					DwmMargins margins = new DwmMargins
					{
						Left = -1,
						Right = -1,
						Top = -1,
						Bottom = -1
					};
					DwmExtendFrameIntoClientArea(hwnd, ref margins);
					int value = 3;
					DwmSetWindowAttribute(hwnd, 38, ref value, 4);
				});
			}
			break;
		}
		}
		return IntPtr.Zero;
	}

	private static bool TryApplyBackdrop(Window window, bool acrylic)
	{
		try
		{
			nint handle = new WindowInteropHelper(window).Handle;
			if (handle == IntPtr.Zero)
			{
				return false;
			}
			int value = 1;
			DwmSetWindowAttribute(handle, 20, ref value, 4);
			int value2 = 2;
			DwmSetWindowAttribute(handle, 33, ref value2, 4);
			HwndSource hwndSource = HwndSource.FromHwnd(handle);
			if (acrylic)
			{
				DwmMargins margins = new DwmMargins
				{
					Left = -1,
					Right = -1,
					Top = -1,
					Bottom = -1
				};
				if (DwmExtendFrameIntoClientArea(handle, ref margins) != 0)
				{
					return false;
				}
				HwndTarget hwndTarget = hwndSource?.CompositionTarget;
				if (hwndTarget != null)
				{
					hwndTarget.BackgroundColor = Colors.Transparent;
				}
				int value3 = 3;
				return DwmSetWindowAttribute(handle, 38, ref value3, 4) == 0;
			}
			int value4 = 1;
			DwmSetWindowAttribute(handle, 38, ref value4, 4);
			DwmMargins margins2 = default(DwmMargins);
			DwmExtendFrameIntoClientArea(handle, ref margins2);
			HwndTarget hwndTarget2 = hwndSource?.CompositionTarget;
			if (hwndTarget2 != null)
			{
				hwndTarget2.BackgroundColor = Colors.Black;
			}
			return true;
		}
		catch (Exception exception)
		{
			ErrorLog.Write(exception);
			return false;
		}
	}
}
