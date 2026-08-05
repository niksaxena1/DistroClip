using System;
using System.Windows.Media.Imaging;

namespace DistributionHelper.Services;

public sealed class WallpaperInfo
{
	public required BitmapSource Image { get; init; }

	public required WallpaperFit Fit { get; init; }

	public required string SourcePath { get; init; }

	public required DateTime SourceWriteTimeUtc { get; init; }
}
