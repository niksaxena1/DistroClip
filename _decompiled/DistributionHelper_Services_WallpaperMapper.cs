using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace DistributionHelper.Services;

public static class WallpaperMapper
{
	public static WallpaperInfo? LoadCurrent()
	{
		try
		{
			string text = ReadWallpaperPath();
			if (text == null || !File.Exists(text))
			{
				return null;
			}
			BitmapImage bitmapImage = new BitmapImage();
			bitmapImage.BeginInit();
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.UriSource = new Uri(text);
			bitmapImage.EndInit();
			((Freezable)bitmapImage).Freeze();
			return new WallpaperInfo
			{
				Image = bitmapImage,
				Fit = ReadFit(),
				SourcePath = text,
				SourceWriteTimeUtc = File.GetLastWriteTimeUtc(text)
			};
		}
		catch (Exception exception)
		{
			ErrorLog.Write(exception);
			return null;
		}
	}

	public static string? ReadWallpaperPath()
	{
		return Registry.GetValue("HKEY_CURRENT_USER\\Control Panel\\Desktop", "WallPaper", null) as string;
	}

	public static WallpaperFit ReadFit()
	{
		string s = Registry.GetValue("HKEY_CURRENT_USER\\Control Panel\\Desktop", "WallpaperStyle", null) as string;
		string text = Registry.GetValue("HKEY_CURRENT_USER\\Control Panel\\Desktop", "TileWallpaper", null) as string;
		int.TryParse(s, out var result);
		if (result == 0 && text == "1")
		{
			return WallpaperFit.Tile;
		}
		return result switch
		{
			2 => WallpaperFit.Stretch, 
			6 => WallpaperFit.Fit, 
			22 => WallpaperFit.Span, 
			0 => WallpaperFit.Center, 
			_ => WallpaperFit.Fill, 
		};
	}

	public static Rect MapScreenRectToImage(Rect screenRect, Rect monitorRect, Rect virtualScreenRect, double imageWidth, double imageHeight, WallpaperFit fit)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f4: Unknown result type (might be due to invalid IL or missing references)
		Rect val = (((uint)(fit - 4) <= 1u) ? virtualScreenRect : monitorRect);
		if (((Rect)(ref val)).Width < 1.0 || ((Rect)(ref val)).Height < 1.0 || imageWidth < 1.0 || imageHeight < 1.0)
		{
			return Rect.Empty;
		}
		double num2;
		double num3;
		double num4;
		double num5;
		switch (fit)
		{
		case WallpaperFit.Tile:
			return new Rect(Mod(((Rect)(ref screenRect)).X - ((Rect)(ref val)).X, imageWidth), Mod(((Rect)(ref screenRect)).Y - ((Rect)(ref val)).Y, imageHeight), ((Rect)(ref screenRect)).Width, ((Rect)(ref screenRect)).Height);
		case WallpaperFit.Stretch:
			num2 = ((Rect)(ref val)).Width / imageWidth;
			num3 = ((Rect)(ref val)).Height / imageHeight;
			num4 = 0.0;
			num5 = 0.0;
			break;
		case WallpaperFit.Center:
			num2 = (num3 = 1.0);
			num4 = (imageWidth - ((Rect)(ref val)).Width) / 2.0;
			num5 = (imageHeight - ((Rect)(ref val)).Height) / 2.0;
			break;
		case WallpaperFit.Fit:
		{
			double num6 = Math.Min(((Rect)(ref val)).Width / imageWidth, ((Rect)(ref val)).Height / imageHeight);
			num2 = (num3 = num6);
			num4 = (imageWidth - ((Rect)(ref val)).Width / num6) / 2.0;
			num5 = (imageHeight - ((Rect)(ref val)).Height / num6) / 2.0;
			break;
		}
		default:
		{
			double num = Math.Max(((Rect)(ref val)).Width / imageWidth, ((Rect)(ref val)).Height / imageHeight);
			num2 = (num3 = num);
			num4 = (imageWidth - ((Rect)(ref val)).Width / num) / 2.0;
			num5 = (imageHeight - ((Rect)(ref val)).Height / num) / 2.0;
			break;
		}
		}
		return new Rect(num4 + (((Rect)(ref screenRect)).X - ((Rect)(ref val)).X) / num2, num5 + (((Rect)(ref screenRect)).Y - ((Rect)(ref val)).Y) / num3, ((Rect)(ref screenRect)).Width / num2, ((Rect)(ref screenRect)).Height / num3);
	}

	private static double Mod(double value, double modulus)
	{
		double num = value % modulus;
		if (!(num < 0.0))
		{
			return num;
		}
		return num + modulus;
	}
}
