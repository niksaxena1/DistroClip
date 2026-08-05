using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DistributionHelper.Services;

public sealed class OriginalArtistCacheStore
{
	private readonly string _cachePath;

	private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	public OriginalArtistCacheStore()
	{
		_cachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DistroClip", "original-artists.json");
	}

	public Dictionary<string, OriginalArtistResult> Load()
	{
		try
		{
			if (File.Exists(_cachePath))
			{
				return JsonSerializer.Deserialize<Dictionary<string, OriginalArtistResult>>(File.ReadAllText(_cachePath), _jsonOptions) ?? new Dictionary<string, OriginalArtistResult>(StringComparer.Ordinal);
			}
		}
		catch (Exception exception)
		{
			ErrorLog.Write(exception);
		}
		return new Dictionary<string, OriginalArtistResult>(StringComparer.Ordinal);
	}

	public void Save(Dictionary<string, OriginalArtistResult> cache)
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(_cachePath));
			string text = _cachePath + ".tmp";
			File.WriteAllText(text, JsonSerializer.Serialize(cache, _jsonOptions));
			File.Move(text, _cachePath, overwrite: true);
		}
		catch (Exception exception)
		{
			ErrorLog.Write(exception);
		}
	}
}
