using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DistributionHelper.Services;

public static class SpotifyArtistService
{
	private sealed class CachedArtist
	{
		public string? Id { get; set; }

		public string? Name { get; set; }

		public string? ImageUrl { get; set; }

		public DateTime FetchedUtc { get; set; }

		public bool TriedCoArtists { get; set; }
	}

	private const string EnvFilePath = "C:\\SpotiBase\\web\\.env.local";

	private static readonly HttpClient Http = new HttpClient
	{
		Timeout = TimeSpan.FromSeconds(20.0)
	};

	private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);

	private static Dictionary<string, CachedArtist>? _cache;

	private static string? _accessToken;

	private static DateTime _accessTokenExpiresUtc;

	private static (string Id, string Secret)? _credentials;

	private static bool _credentialsResolved;

	private static string CacheDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DistroClip");

	private static string CachePath => Path.Combine(CacheDirectory, "spotify-artists.json");

	private static string ThumbDirectory => Path.Combine(CacheDirectory, "artist-thumbs");

	public static async Task<SpotifyArtistProfile?> LookupAsync(string artistName, IReadOnlyList<string> coArtists, CancellationToken cancellationToken)
	{
		try
		{
			return await LookupCoreAsync(artistName, coArtists, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
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

	private static async Task<SpotifyArtistProfile?> LookupCoreAsync(string artistName, IReadOnlyList<string> coArtists, CancellationToken cancellationToken)
	{
		string key = TextNormalizer.ForSearch(artistName);
		if (key.Length == 0)
		{
			return null;
		}
		await Gate.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			LoadCache();
			bool flag = false;
			if (_cache.TryGetValue(key, out var value))
			{
				if (value.Id != null)
				{
					return await MaterializeAsync(value, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				}
				flag = (DateTime.UtcNow - value.FetchedUtc).TotalDays < 7.0;
				if (flag && (value.TriedCoArtists || coArtists.Count == 0))
				{
					return null;
				}
			}
			if (!ResolveCredentials().HasValue)
			{
				return null;
			}
			CachedArtist cachedArtist = (flag ? null : (await SearchAsync(artistName, key, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)));
			CachedArtist cachedArtist2 = cachedArtist;
			bool triedCoArtists = false;
			if (cachedArtist2 == null && coArtists.Count > 0)
			{
				triedCoArtists = true;
				cachedArtist2 = await SearchViaCoArtistsAsync(artistName, key, coArtists, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			if (cachedArtist2 == null)
			{
				cachedArtist2 = new CachedArtist
				{
					TriedCoArtists = triedCoArtists
				};
			}
			cachedArtist2.TriedCoArtists = cachedArtist2.Id != null || triedCoArtists;
			cachedArtist2.FetchedUtc = DateTime.UtcNow;
			_cache[key] = cachedArtist2;
			SaveCache();
			return (cachedArtist2.Id == null) ? null : (await MaterializeAsync(cachedArtist2, cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
		}
		finally
		{
			Gate.Release();
		}
	}

	private static async Task<CachedArtist?> SearchViaCoArtistsAsync(string artistName, string normalizedTarget, IReadOnlyList<string> coArtists, CancellationToken cancellationToken)
	{
		string token = await EnsureTokenAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (token == null)
		{
			return null;
		}
		foreach (string item in coArtists.Take(4))
		{
			string text = Uri.EscapeDataString(artistName + " " + item);
			using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "https://api.spotify.com/v1/search?q=" + text + "&type=track&limit=20");
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
			using HttpResponseMessage response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (!response.IsSuccessStatusCode)
			{
				goto end_IL_01ba;
			}
			using (JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false)))
			{
				if (!json.RootElement.TryGetProperty("tracks", out var value) || !value.TryGetProperty("items", out var value2))
				{
					continue;
				}
				foreach (JsonElement item2 in value2.EnumerateArray())
				{
					if (!item2.TryGetProperty("artists", out var value3))
					{
						continue;
					}
					foreach (JsonElement item3 in value3.EnumerateArray())
					{
						JsonElement value4;
						string id = (item3.TryGetProperty("id", out value4) ? value4.GetString() : null);
						JsonElement value5;
						string name = (item3.TryGetProperty("name", out value5) ? value5.GetString() : null);
						if (id != null && name != null && !(TextNormalizer.ForSearch(name) != normalizedTarget))
						{
							return (await FetchArtistAsync(id, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)) ?? new CachedArtist
							{
								Id = id,
								Name = name
							};
						}
					}
				}
				continue;
			}
			end_IL_01ba:;
		}
		return null;
	}

	private static async Task<CachedArtist?> FetchArtistAsync(string id, CancellationToken cancellationToken)
	{
		string text = await EnsureTokenAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (text == null)
		{
			return null;
		}
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "https://api.spotify.com/v1/artists/" + id);
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", text);
		using HttpResponseMessage response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (!response.IsSuccessStatusCode)
		{
			return null;
		}
		using JsonDocument jsonDocument = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
		JsonElement value;
		string name = (jsonDocument.RootElement.TryGetProperty("name", out value) ? value.GetString() : null);
		string text2 = null;
		if (jsonDocument.RootElement.TryGetProperty("images", out var value2))
		{
			foreach (JsonElement item in value2.EnumerateArray())
			{
				text2 = (item.TryGetProperty("url", out var value3) ? value3.GetString() : text2);
			}
		}
		return new CachedArtist
		{
			Id = id,
			Name = name,
			ImageUrl = text2
		};
	}

	private static async Task<SpotifyArtistProfile?> MaterializeAsync(CachedArtist entry, CancellationToken cancellationToken)
	{
		if (entry.Id == null)
		{
			return null;
		}
		string profileUrl = "https://open.spotify.com/artist/" + entry.Id;
		string thumbPath = null;
		if (entry.ImageUrl != null)
		{
			thumbPath = Path.Combine(ThumbDirectory, entry.Id + ".jpg");
			if (!File.Exists(thumbPath))
			{
				try
				{
					Directory.CreateDirectory(ThumbDirectory);
					string path = thumbPath;
					await File.WriteAllBytesAsync(path, await Http.GetByteArrayAsync(entry.ImageUrl, cancellationToken).ConfigureAwait(continueOnCapturedContext: false), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				}
				catch (OperationCanceledException)
				{
					throw;
				}
				catch (Exception exception)
				{
					ErrorLog.Write(exception);
					thumbPath = null;
				}
			}
		}
		return new SpotifyArtistProfile(entry.Id, entry.Name ?? string.Empty, profileUrl, thumbPath);
	}

	private static async Task<CachedArtist?> SearchAsync(string artistName, string normalizedTarget, CancellationToken cancellationToken)
	{
		string text = await EnsureTokenAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (text == null)
		{
			return null;
		}
		string text2 = Uri.EscapeDataString("artist:\"" + artistName + "\"");
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "https://api.spotify.com/v1/search?q=" + text2 + "&type=artist&limit=50");
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", text);
		using HttpResponseMessage response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (!response.IsSuccessStatusCode)
		{
			return null;
		}
		using JsonDocument jsonDocument = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
		List<(string, string, int, string)> list = new List<(string, string, int, string)>();
		if (jsonDocument.RootElement.TryGetProperty("artists", out var value) && value.TryGetProperty("items", out var value2))
		{
			foreach (JsonElement item2 in value2.EnumerateArray())
			{
				JsonElement value3;
				string text3 = (item2.TryGetProperty("id", out value3) ? value3.GetString() : null);
				JsonElement value4;
				string text4 = (item2.TryGetProperty("name", out value4) ? value4.GetString() : null);
				if (text3 == null || text4 == null)
				{
					continue;
				}
				JsonElement value5;
				JsonElement value6;
				int item = ((item2.TryGetProperty("followers", out value5) && value5.TryGetProperty("total", out value6)) ? value6.GetInt32() : 0);
				string text5 = null;
				if (item2.TryGetProperty("images", out var value7))
				{
					foreach (JsonElement item3 in value7.EnumerateArray())
					{
						text5 = (item3.TryGetProperty("url", out var value8) ? value8.GetString() : text5);
					}
				}
				list.Add((text3, text4, item, text5));
			}
		}
		(string, string, string)? tuple = PickBestMatch(list, normalizedTarget);
		return (!tuple.HasValue) ? null : new CachedArtist
		{
			Id = tuple.Value.Item1,
			Name = tuple.Value.Item2,
			ImageUrl = tuple.Value.Item3
		};
	}

	internal static (string Id, string Name, string? ImageUrl)? PickBestMatch(IEnumerable<(string Id, string Name, int Followers, string? ImageUrl)> candidates, string normalizedTarget)
	{
		using (IEnumerator<(string, string, int, string)> enumerator = (from candidate in candidates
			where TextNormalizer.ForSearch(candidate.Name) == normalizedTarget
			orderby candidate.Followers descending
			select candidate).GetEnumerator())
		{
			if (enumerator.MoveNext())
			{
				(string, string, int, string) current = enumerator.Current;
				return (current.Item1, current.Item2, current.Item4);
			}
		}
		return null;
	}

	private static async Task<string?> EnsureTokenAsync(CancellationToken cancellationToken)
	{
		if (_accessToken != null && DateTime.UtcNow < _accessTokenExpiresUtc - TimeSpan.FromSeconds(30.0))
		{
			return _accessToken;
		}
		(string, string)? tuple = ResolveCredentials();
		if (!tuple.HasValue)
		{
			return null;
		}
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token");
		string parameter = Convert.ToBase64String(Encoding.UTF8.GetBytes(tuple.Value.Item1 + ":" + tuple.Value.Item2));
		request.Headers.Authorization = new AuthenticationHeaderValue("Basic", parameter);
		request.Content = new FormUrlEncodedContent(new _003C_003Ez__ReadOnlySingleElementList<KeyValuePair<string, string>>(new KeyValuePair<string, string>("grant_type", "client_credentials")));
		using HttpResponseMessage response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (!response.IsSuccessStatusCode)
		{
			return null;
		}
		using JsonDocument jsonDocument = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
		_accessToken = jsonDocument.RootElement.GetProperty("access_token").GetString();
		JsonElement value;
		int num = (jsonDocument.RootElement.TryGetProperty("expires_in", out value) ? value.GetInt32() : 3600);
		_accessTokenExpiresUtc = DateTime.UtcNow.AddSeconds(num);
		return _accessToken;
	}

	private static (string Id, string Secret)? ResolveCredentials()
	{
		if (_credentialsResolved)
		{
			return _credentials;
		}
		_credentialsResolved = true;
		string text = Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_ID");
		string text2 = Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_SECRET");
		if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(text2))
		{
			try
			{
				if (File.Exists("C:\\SpotiBase\\web\\.env.local"))
				{
					string envText = File.ReadAllText("C:\\SpotiBase\\web\\.env.local");
					text = ParseEnvValue(envText, "SPOTIFY_CLIENT_ID");
					text2 = ParseEnvValue(envText, "SPOTIFY_CLIENT_SECRET");
				}
			}
			catch (Exception exception)
			{
				ErrorLog.Write(exception);
			}
		}
		if (!string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(text2))
		{
			_credentials = (text, text2);
		}
		return _credentials;
	}

	internal static string? ParseEnvValue(string envText, string key)
	{
		string[] array = envText.Split('\n');
		for (int i = 0; i < array.Length; i++)
		{
			string text = array[i].Trim();
			if (text.StartsWith('#'))
			{
				continue;
			}
			int num = text.IndexOf('=');
			if (num > 0 && text.Substring(0, num).Trim().Equals(key, StringComparison.Ordinal))
			{
				string text2 = text;
				int num2 = num + 1;
				string text3 = text2.Substring(num2, text2.Length - num2).Trim().Trim('"');
				if (text3.Length != 0)
				{
					return text3;
				}
				return null;
			}
		}
		return null;
	}

	private static void LoadCache()
	{
		if (_cache != null)
		{
			return;
		}
		try
		{
			if (File.Exists(CachePath))
			{
				_cache = JsonSerializer.Deserialize<Dictionary<string, CachedArtist>>(File.ReadAllText(CachePath));
			}
		}
		catch (Exception exception)
		{
			ErrorLog.Write(exception);
		}
		if (_cache == null)
		{
			_cache = new Dictionary<string, CachedArtist>();
		}
	}

	private static void SaveCache()
	{
		try
		{
			Directory.CreateDirectory(CacheDirectory);
			File.WriteAllText(CachePath, JsonSerializer.Serialize(_cache, new JsonSerializerOptions
			{
				WriteIndented = true
			}));
		}
		catch (Exception exception)
		{
			ErrorLog.Write(exception);
		}
	}
}
