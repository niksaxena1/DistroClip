using System;
using System.IO;

namespace DistributionHelper.Services;

public static class ErrorLog
{
	public static void Write(Exception exception)
	{
		try
		{
			string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DistroClip");
			Directory.CreateDirectory(text);
			File.AppendAllText(Path.Combine(text, "errors.log"), $"{DateTimeOffset.Now:O}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
		}
		catch
		{
		}
	}
}
