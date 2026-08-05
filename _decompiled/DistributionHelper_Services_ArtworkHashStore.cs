using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DistributionHelper.Services;

public sealed class ArtworkHashStore
{
	private readonly string _cachePath;

	private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	public ArtworkHashStore()
	{
		_cachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DistroClip", "artwork-hashes.json");
	}

	public Dictionary<string, ArtworkHashEntry> Load()
	{
		try
		{
			if (File.Exists(_cachePath))
			{
				return JsonSerializer.Deserialize<Dictionary<string, ArtworkHashEntry>>(File.ReadAllText(_cachePath), _jsonOptions) ?? new Dictionary<string, ArtworkHashEntry>(StringComparer.OrdinalIgnoreCase);
			}
		}
		catch (Exception exception)
		{
			ErrorLog.Write(exception);
		}
		return new Dictionary<string, ArtworkHashEntry>(StringComparer.OrdinalIgnoreCase);
	}

	public void Save(Dictionary<string, ArtworkHashEntry> cache)
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
