using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DistributionHelper.Models;

public sealed class AppSettings
{
	public List<string> SearchFolders { get; set; }

	public double? WindowLeft { get; set; }

	public double? WindowTop { get; set; }

	public double WindowWidth { get; set; }

	public double WindowHeight { get; set; }

	public double InterfaceScale { get; set; }

	public bool ShowSearchArtworkThumbnails { get; set; }

	public bool ValidateSearchResultsLive { get; set; }

	public bool ShowRealWaveform { get; set; }

	public bool SplitNamesByDefault { get; set; }

	public string WaveformInnerMode { get; set; }

	public string ScratchpadText { get; set; }

	public bool AlwaysOnTop { get; set; }

	public string Theme { get; set; }

	public int LayoutVersion { get; set; }

	public AppSettings()
	{
		int num = 3;
		List<string> list = new List<string>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<string> span = CollectionsMarshal.AsSpan(list);
		int num2 = 0;
		span[num2] = "Y:\\Unreleased Ext Drive";
		num2++;
		span[num2] = "Y:\\Unreleased Ext Drive\\[P Tracks";
		num2++;
		span[num2] = "Y:\\Releases Ext Drive";
		num2++;
		SearchFolders = list;
		WindowWidth = 410.0;
		WindowHeight = 610.0;
		InterfaceScale = 1.0;
		ShowRealWaveform = true;
		WaveformInnerMode = "DynamicRms";
		ScratchpadText = string.Empty;
		AlwaysOnTop = true;
		Theme = "Acrylic";
	}
}
