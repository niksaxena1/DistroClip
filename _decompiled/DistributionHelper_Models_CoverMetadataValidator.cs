using System.Collections.Generic;
using System.Text.RegularExpressions;
using DistributionHelper.Services;

namespace DistributionHelper.Models;

public static class CoverMetadataValidator
{
	public static CoverMetadataCheck Compare(ReleaseSummary release, LicenseReleaseMetadata metadata)
	{
		if (string.IsNullOrWhiteSpace(metadata.Artist) || string.IsNullOrWhiteSpace(metadata.Title))
		{
			return new CoverMetadataCheck(CoverMetadataStatus.NeedsReview, metadata.Artist, metadata.Title, "Proof artist or title could not be read; verify the licence");
		}
		string text = TextNormalizer.ForSearch(release.ArtistsText);
		string text2 = TextNormalizer.ForSearch(metadata.Artist);
		string text3 = NormalizeTitle(release.TrackTitle);
		string text4 = NormalizeTitle(metadata.Title);
		bool flag = text == text2;
		bool flag2 = text3 == text4;
		if (flag && flag2)
		{
			return new CoverMetadataCheck(CoverMetadataStatus.Matches, metadata.Artist, metadata.Title, "Proof matches " + release.ArtistsText + " — " + release.TrackTitle);
		}
		List<string> list = new List<string>();
		if (!flag)
		{
			list.Add("artist is “" + metadata.Artist + "”");
		}
		if (!flag2)
		{
			list.Add("title is “" + metadata.Title + "”");
		}
		return new CoverMetadataCheck(CoverMetadataStatus.NeedsReview, metadata.Artist, metadata.Title, "Proof does not match the release folder: " + string.Join("; ", list));
	}

	private static string NormalizeTitle(string title)
	{
		return TextNormalizer.ForSearch(Regex.Replace(Regex.Replace(title, "\\s*[\\(\\[]\\s*(?:feat(?:uring)?|ft)\\.?\\s+.*?[\\)\\]]\\s*$", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "\\s+(?:feat(?:uring)?|ft)\\.?\\s+.+$", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
	}
}
