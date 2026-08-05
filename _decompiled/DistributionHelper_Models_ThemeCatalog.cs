using System;
using System.Collections.Generic;

namespace DistributionHelper.Models;

public static class ThemeCatalog
{
	public static readonly IReadOnlyList<(AppTheme Theme, string DisplayName)> All = new _003C_003Ez__ReadOnlyArray<(AppTheme, string)>(new(AppTheme, string)[6]
	{
		(AppTheme.Acrylic, "Acrylic glass"),
		(AppTheme.AcrylicAccent, "Acrylic accent"),
		(AppTheme.Studio, "Studio hardware"),
		(AppTheme.AmbientGlass, "Ambient glass"),
		(AppTheme.Reel, "Reel tape"),
		(AppTheme.LiquidGlass, "Liquid glass")
	});

	public static AppTheme Parse(string? name)
	{
		if (!Enum.TryParse<AppTheme>(name, ignoreCase: true, out var result))
		{
			return AppTheme.Acrylic;
		}
		return result;
	}
}
