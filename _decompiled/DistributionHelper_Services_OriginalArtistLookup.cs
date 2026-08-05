using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DistributionHelper.Services;

public static class OriginalArtistLookup
{
	private static readonly HttpClient Http = CreateClient();

	private static HttpClient CreateClient()
	{
		HttpClient httpClient = new HttpClient();
		httpClient.Timeout = TimeSpan.FromSeconds(12.0);
		httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DistroClip/1.0");
		httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("(local music distribution helper)");
		httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
		return httpClient;
	}

	public static async Task<OriginalArtistResult> FindAsync(string songTitle, IReadOnlyList<string> songwriters, CancellationToken cancellationToken)
	{
		_ = 4;
		try
		{
			(string Id, string Title)? work = await FindWorkAsync(songTitle, songwriters, cancellationToken);
			if (!work.HasValue)
			{
				return new OriginalArtistResult(null, null, null, "No matching song found on MusicBrainz.");
			}
			await Task.Delay(1100, cancellationToken);
			OriginalArtistResult originalArtistResult = await TryWikidataAsync(work.Value.Id, work.Value.Title, cancellationToken);
			if ((object)originalArtistResult != null)
			{
				return originalArtistResult;
			}
			await Task.Delay(1100, cancellationToken);
			(string, string)? tuple = await SelectEarliestRecordingAsync(work.Value.Id, work.Value.Title, cancellationToken);
			return (!tuple.HasValue) ? new OriginalArtistResult(null, work.Value.Title, null, "No dated recordings found for this song.") : new OriginalArtistResult(tuple.Value.Item1, work.Value.Title, tuple.Value.Item2, null);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception exception)
		{
			ErrorLog.Write(exception);
			return new OriginalArtistResult(null, null, null, "Lookup failed — check the internet connection.");
		}
	}

	private static async Task<(string Id, string Title)?> FindWorkAsync(string songTitle, IReadOnlyList<string> songwriters, CancellationToken cancellationToken)
	{
		if (songwriters.Count > 0)
		{
			(string, string)? result = await SearchWorkAsync($"work:\"{EscapeQuery(songTitle)}\" AND artist:\"{EscapeQuery(songwriters[0])}\"", songTitle, cancellationToken);
			if (result.HasValue)
			{
				return result;
			}
			await Task.Delay(1100, cancellationToken);
		}
		return await SearchWorkAsync("work:\"" + EscapeQuery(songTitle) + "\"", songTitle, cancellationToken);
	}

	private static async Task<(string Id, string Title)?> SearchWorkAsync(string query, string songTitle, CancellationToken cancellationToken)
	{
		using JsonDocument document = await GetJsonAsync("https://musicbrainz.org/ws/2/work?query=" + Uri.EscapeDataString(query) + "&fmt=json&limit=5", cancellationToken);
		return SelectWork(document, songTitle);
	}

	public static (string Id, string Title)? SelectWork(JsonDocument document, string songTitle)
	{
		if (!document.RootElement.TryGetProperty("works", out var value))
		{
			return null;
		}
		string text = TextNormalizer.ForSearch(songTitle);
		foreach (JsonElement item in value.EnumerateArray())
		{
			JsonElement value2;
			int num = (item.TryGetProperty("score", out value2) ? value2.GetInt32() : 0);
			JsonElement value3;
			string text2 = (item.TryGetProperty("title", out value3) ? value3.GetString() : null);
			JsonElement value4;
			string text3 = (item.TryGetProperty("id", out value4) ? value4.GetString() : null);
			if (text3 != null && text2 != null && num >= 85 && TextNormalizer.ForSearch(text2) == text)
			{
				return (text3, text2);
			}
		}
		return null;
	}

	private static async Task<OriginalArtistResult?> TryWikidataAsync(string workId, string workTitle, CancellationToken cancellationToken)
	{
		_ = 2;
		try
		{
			using JsonDocument workDocument = await GetJsonAsync("https://musicbrainz.org/ws/2/work/" + workId + "?inc=url-rels&fmt=json", cancellationToken);
			string qid = FindWikidataId(workDocument);
			if (qid == null)
			{
				return null;
			}
			using JsonDocument entityDocument = await GetJsonAsync("https://www.wikidata.org/wiki/Special:EntityData/" + qid + ".json", cancellationToken);
			JsonElement entity = entityDocument.RootElement.GetProperty("entities").GetProperty(qid);
			List<(string, bool)> list = ReadPerformers(entity);
			if (list.Count == 0)
			{
				return null;
			}
			List<(string, string, bool)> list2 = await GetWikidataLabelsAsync(list, cancellationToken);
			if (list2.Count == 0)
			{
				return null;
			}
			string text = ReadDescription(entity);
			if (list2.Count > 1 && !string.IsNullOrWhiteSpace(text))
			{
				string lower = text.ToLowerInvariant();
				List<(string, string, bool)> list3 = list2.Where<(string, string, bool)>(((string Id, string Name, bool HasRoleQualifier) item) => item.HasRoleQualifier || lower.Contains(item.Name.ToLowerInvariant())).ToList();
				if (list3.Any<(string, string, bool)>(((string Id, string Name, bool HasRoleQualifier) item) => !item.HasRoleQualifier))
				{
					list2 = list3;
				}
			}
			string firstReleaseDate = ReadPublicationDate(entity);
			return new OriginalArtistResult(string.Join(", ", list2.Select<(string, string, bool), string>(((string Id, string Name, bool HasRoleQualifier) item) => item.Name)), workTitle, firstReleaseDate, null);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception exception)
		{
			ErrorLog.Write(exception);
			return null;
		}
	}

	private static string? FindWikidataId(JsonDocument workDocument)
	{
		if (!workDocument.RootElement.TryGetProperty("relations", out var value))
		{
			return null;
		}
		foreach (JsonElement item in value.EnumerateArray())
		{
			if (!((item.TryGetProperty("type", out var value2) ? value2.GetString() : null) != "wikidata") && item.TryGetProperty("url", out var value3) && value3.TryGetProperty("resource", out var value4))
			{
				Match match = Regex.Match(value4.GetString() ?? string.Empty, "Q\\d+");
				if (match.Success)
				{
					return match.Value;
				}
			}
		}
		return null;
	}

	private static List<(string Id, bool HasRoleQualifier)> ReadPerformers(JsonElement entity)
	{
		List<(string, bool)> list = new List<(string, bool)>();
		if (!entity.TryGetProperty("claims", out var value) || !value.TryGetProperty("P175", out var value2))
		{
			return list;
		}
		foreach (JsonElement item2 in value2.EnumerateArray())
		{
			if (item2.TryGetProperty("mainsnak", out var value3) && value3.TryGetProperty("datavalue", out var value4) && value4.TryGetProperty("value", out var value5) && value5.TryGetProperty("id", out var value6))
			{
				string id = value6.GetString();
				if (id != null && list.All<(string, bool)>(((string Id, bool HasRoleQualifier) existing) => existing.Id != id))
				{
					JsonElement value7;
					JsonElement value8;
					bool item = item2.TryGetProperty("qualifiers", out value7) && value7.TryGetProperty("P2868", out value8);
					list.Add((id, item));
				}
			}
		}
		return list;
	}

	private static string? ReadDescription(JsonElement entity)
	{
		if (!entity.TryGetProperty("descriptions", out var value) || !value.TryGetProperty("en", out var value2) || !value2.TryGetProperty("value", out var value3))
		{
			return null;
		}
		return value3.GetString();
	}

	private static string? ReadPublicationDate(JsonElement entity)
	{
		if (!entity.TryGetProperty("claims", out var value) || !value.TryGetProperty("P577", out var value2))
		{
			return null;
		}
		foreach (JsonElement item in value2.EnumerateArray())
		{
			if (item.TryGetProperty("mainsnak", out var value3) && value3.TryGetProperty("datavalue", out var value4) && value4.TryGetProperty("value", out var value5) && value5.TryGetProperty("time", out var value6))
			{
				string text = value6.GetString();
				if (text != null && text.Length > 10)
				{
					return text.TrimStart('+').Substring(0, 10).Replace("-00", string.Empty);
				}
			}
		}
		return null;
	}

	private static async Task<List<(string Id, string Name, bool HasRoleQualifier)>> GetWikidataLabelsAsync(IReadOnlyList<(string Id, bool HasRoleQualifier)> performers, CancellationToken cancellationToken)
	{
		using JsonDocument jsonDocument = await GetJsonAsync("https://www.wikidata.org/w/api.php?action=wbgetentities&ids=" + string.Join("|", performers.Select(((string Id, bool HasRoleQualifier) tuple2) => tuple2.Id)) + "&props=labels&languages=en&format=json", cancellationToken);
		List<(string, string, bool)> list = new List<(string, string, bool)>();
		if (!jsonDocument.RootElement.TryGetProperty("entities", out var value))
		{
			return list;
		}
		foreach (var (text, item) in performers)
		{
			if (value.TryGetProperty(text, out var value2) && value2.TryGetProperty("labels", out var value3) && value3.TryGetProperty("en", out var value4) && value4.TryGetProperty("value", out var value5))
			{
				string text2 = value5.GetString();
				if (text2 != null && text2.Length > 0)
				{
					list.Add((text, text2, item));
				}
			}
		}
		return list;
	}

	private static async Task<(string Credit, string Date)?> SelectEarliestRecordingAsync(string workId, string workTitle, CancellationToken cancellationToken)
	{
		(string Credit, string Date)? best = null;
		for (int offset = 0; offset < 300; offset += 100)
		{
			if (offset > 0)
			{
				await Task.Delay(1100, cancellationToken);
			}
			using JsonDocument jsonDocument = await GetJsonAsync($"https://musicbrainz.org/ws/2/recording?work={workId}&inc=artist-credits&fmt=json&limit=100&offset={offset}", cancellationToken);
			(string, string)? tuple = SelectOriginalRecording(jsonDocument, workTitle);
			if (tuple.HasValue && (!best.HasValue || string.CompareOrdinal(PadReleaseDate(tuple.Value.Item2), PadReleaseDate(best.Value.Date)) < 0))
			{
				best = tuple;
			}
			JsonElement value;
			int num = (jsonDocument.RootElement.TryGetProperty("recording-count", out value) ? value.GetInt32() : 0);
			if (offset + 100 >= num)
			{
				break;
			}
		}
		return best;
	}

	public static (string Credit, string Date)? SelectOriginalRecording(JsonDocument document, string workTitle)
	{
		if (!document.RootElement.TryGetProperty("recordings", out var value))
		{
			return null;
		}
		string wantedTitle = TextNormalizer.ForSearch(workTitle);
		List<(string, string, string)> list = new List<(string, string, string)>();
		foreach (JsonElement item2 in value.EnumerateArray())
		{
			JsonElement value2;
			string text = (item2.TryGetProperty("first-release-date", out value2) ? value2.GetString() : null);
			if (!string.IsNullOrWhiteSpace(text) && text.Length >= 4)
			{
				JsonElement value3;
				string item = (item2.TryGetProperty("title", out value3) ? (value3.GetString() ?? string.Empty) : string.Empty);
				string text2 = JoinArtistCredit(item2);
				if (text2.Length > 0)
				{
					list.Add((text2, item, text));
				}
			}
		}
		if (list.Count == 0)
		{
			return null;
		}
		List<(string, string, string)> list2 = list.Where<(string, string, string)>(((string Credit, string Title, string Date) tuple2) => TextNormalizer.ForSearch(tuple2.Title) == wantedTitle).ToList();
		(string, string, string) tuple = ((list2.Count > 0) ? list2 : list).OrderBy<(string, string, string), string>(((string Credit, string Title, string Date) tuple2) => PadReleaseDate(tuple2.Date), StringComparer.Ordinal).First();
		return (tuple.Item1, tuple.Item3);
	}

	public static string PadReleaseDate(string date)
	{
		return date.Length switch
		{
			4 => date + "-12-31", 
			7 => date + "-31", 
			_ => date, 
		};
	}

	private static string JoinArtistCredit(JsonElement recording)
	{
		if (!recording.TryGetProperty("artist-credit", out var value))
		{
			return string.Empty;
		}
		List<string> list = new List<string>();
		foreach (JsonElement item in value.EnumerateArray())
		{
			JsonElement value2;
			string text = (item.TryGetProperty("name", out value2) ? value2.GetString() : null);
			JsonElement value3;
			string text2 = (item.TryGetProperty("joinphrase", out value3) ? value3.GetString() : null);
			if (!string.IsNullOrEmpty(text))
			{
				list.Add(text + (text2 ?? string.Empty));
			}
		}
		return string.Concat(list).Trim();
	}

	private static async Task<JsonDocument> GetJsonAsync(string url, CancellationToken cancellationToken)
	{
		using HttpResponseMessage response = await Http.GetAsync(url, cancellationToken);
		response.EnsureSuccessStatusCode();
		JsonDocument result;
		await using (Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken))
		{
			result = await JsonDocument.ParseAsync(stream, default(JsonDocumentOptions), cancellationToken);
		}
		return result;
	}

	private static string EscapeQuery(string value)
	{
		return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
	}
}
