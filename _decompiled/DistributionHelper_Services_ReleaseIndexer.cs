using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DistributionHelper.Models;

namespace DistributionHelper.Services;

public sealed class ReleaseIndexer
{
	public Task<IndexResult> ScanAsync(IEnumerable<string> searchFolders, CancellationToken cancellationToken = default(CancellationToken))
	{
		return Task.Run(() => Scan(searchFolders, cancellationToken), cancellationToken);
	}

	public static ReleaseSummary? ParseReleaseFolder(string folderPath, string rootLabel)
	{
		string name = new DirectoryInfo(folderPath).Name;
		int num = name.IndexOf(" - ", StringComparison.Ordinal);
		if (num <= 0 || num >= name.Length - 3)
		{
			return null;
		}
		string text = name.Substring(0, num).Trim();
		string text2 = name;
		int num2 = num + 3;
		string text3 = text2.Substring(num2, text2.Length - num2).Trim();
		string[] array = (from artist in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			where artist.Length > 0
			select artist).ToArray();
		if (array.Length == 0 || text3.Length == 0)
		{
			return null;
		}
		return new ReleaseSummary
		{
			FolderPath = folderPath,
			FolderName = name,
			TrackTitle = text3,
			ArtistsText = text,
			Artists = array,
			SearchKey = TextNormalizer.ForSearch($"{text} {text3} {name}"),
			RootLabel = rootLabel
		};
	}

	public static IReadOnlyList<ReleaseSummary> Search(IReadOnlyList<ReleaseSummary> releases, string query, int maximumResults = 12)
	{
		string normalizedQuery = TextNormalizer.ForSearch(query);
		if (normalizedQuery.Length == 0)
		{
			return Array.Empty<ReleaseSummary>();
		}
		string[] tokens = normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		return (from item in (from release in releases
				where tokens.All((string token) => release.SearchKey.Contains(token, StringComparison.Ordinal))
				select new
				{
					Release = release,
					Score = Score(release, normalizedQuery, tokens)
				} into item
				orderby item.Score
				select item).ThenBy(item => item.Release.ArtistsText, StringComparer.CurrentCultureIgnoreCase).ThenBy(item => item.Release.TrackTitle, StringComparer.CurrentCultureIgnoreCase).Take(maximumResults)
			select item.Release).ToArray();
	}

	public static bool ContainsOldPathSegment(string path)
	{
		return path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any((string segment) => segment.Equals("Old", StringComparison.OrdinalIgnoreCase));
	}

	private static IndexResult Scan(IEnumerable<string> searchFolders, CancellationToken cancellationToken)
	{
		List<ReleaseSummary> list = new List<ReleaseSummary>();
		List<string> list2 = new List<string>();
		HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (string item in searchFolders.Distinct<string>(StringComparer.OrdinalIgnoreCase))
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (string.IsNullOrWhiteSpace(item) || ContainsOldPathSegment(item))
			{
				continue;
			}
			if (!Directory.Exists(item))
			{
				list2.Add(item);
				continue;
			}
			string name = new DirectoryInfo(item).Name;
			AddIfRelease(item, name, list, seen);
			try
			{
				foreach (string item2 in Directory.EnumerateDirectories(item, "*", SearchOption.TopDirectoryOnly))
				{
					cancellationToken.ThrowIfCancellationRequested();
					if (!new DirectoryInfo(item2).Name.Equals("Old", StringComparison.OrdinalIgnoreCase))
					{
						AddIfRelease(item2, name, list, seen);
					}
				}
			}
			catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
			{
				ErrorLog.Write(ex);
				list2.Add(item);
			}
		}
		return new IndexResult
		{
			Releases = list.OrderBy<ReleaseSummary, string>((ReleaseSummary release) => release.ArtistsText, StringComparer.CurrentCultureIgnoreCase).ThenBy<ReleaseSummary, string>((ReleaseSummary release) => release.TrackTitle, StringComparer.CurrentCultureIgnoreCase).ToArray(),
			UnavailableFolders = list2.Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray()
		};
	}

	private static void AddIfRelease(string folder, string rootLabel, ICollection<ReleaseSummary> releases, ISet<string> seen)
	{
		if (!ContainsOldPathSegment(folder) && seen.Add(Path.GetFullPath(folder)))
		{
			ReleaseSummary releaseSummary = ParseReleaseFolder(folder, rootLabel);
			if (releaseSummary != null)
			{
				releases.Add(releaseSummary);
			}
		}
	}

	private static int Score(ReleaseSummary release, string query, IReadOnlyList<string> tokens)
	{
		string text = TextNormalizer.ForSearch(release.TrackTitle);
		string text2 = TextNormalizer.ForSearch(release.ArtistsText);
		if (text == query || text2 == query)
		{
			return 0;
		}
		if (text.StartsWith(query, StringComparison.Ordinal) || text2.StartsWith(query, StringComparison.Ordinal))
		{
			return 10;
		}
		int num = release.SearchKey.IndexOf(query, StringComparison.Ordinal);
		if (num >= 0)
		{
			return 20 + num;
		}
		int num2 = tokens.Sum((string token) => release.SearchKey.IndexOf(token, StringComparison.Ordinal));
		return 100 + num2;
	}
}
