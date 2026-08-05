using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using DistributionHelper.Models;

namespace DistributionHelper.Services;

public sealed class SettingsService
{
	private readonly string _settingsPath;

	private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	public SettingsService()
	{
		string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DistroClip");
		_settingsPath = Path.Combine(path, "settings.json");
	}

	public AppSettings Load()
	{
		try
		{
			if (!File.Exists(_settingsPath))
			{
				return new AppSettings();
			}
			AppSettings? obj = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsPath), _jsonOptions) ?? new AppSettings();
			obj.SearchFolders = obj.SearchFolders.Where((string path) => !string.IsNullOrWhiteSpace(path)).Select(NormalizePath).Distinct<string>(StringComparer.OrdinalIgnoreCase)
				.ToList();
			return obj;
		}
		catch (Exception exception)
		{
			ErrorLog.Write(exception);
			return new AppSettings();
		}
	}

	public void Save(AppSettings settings)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath));
		string text = _settingsPath + ".tmp";
		File.WriteAllText(text, JsonSerializer.Serialize(settings, _jsonOptions));
		File.Move(text, _settingsPath, overwrite: true);
	}

	private static string NormalizePath(string path)
	{
		return Path.TrimEndingDirectorySeparator(path.Trim().Trim('"'));
	}
}
