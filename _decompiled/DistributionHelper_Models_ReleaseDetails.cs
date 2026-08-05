using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;

namespace DistributionHelper.Models;

public sealed class ReleaseDetails
{
	public required ReleaseSummary Summary { get; init; }

	public IReadOnlyList<string> Payees { get; init; } = Array.Empty<string>();

	public IReadOnlyList<PayeeMapping> PayeeMappings { get; init; } = Array.Empty<PayeeMapping>();

	public IReadOnlyList<string> Songwriters { get; init; } = Array.Empty<string>();

	public string? Credits { get; init; }

	public string? ArtworkPath { get; init; }

	public BitmapSource? ArtworkThumbnail { get; init; }

	public string? AudioPath { get; init; }

	public long? AudioFileSize { get; init; }

	public TimeSpan? AudioDuration { get; init; }

	public IReadOnlyList<string> OtherAudioVersions { get; init; } = Array.Empty<string>();

	public string? ContractPath { get; init; }

	public bool PayeeCountMismatchExpected { get; init; }

	public string? LicensePath { get; init; }

	public CoverMetadataCheck? CoverMetadataCheck { get; init; }

	public bool IsCover => LicensePath != null;

	public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
