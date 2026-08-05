using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace DistributionHelper.Models;

public sealed class ReleaseSummary : INotifyPropertyChanged
{
	private BitmapSource? _searchArtworkThumbnail;

	private bool _showSearchArtworkThumbnail;

	private bool _showSearchReadinessStatus;

	private SearchReadinessStatus _searchReadinessStatus;

	private string? _searchReadinessToolTip;

	public required string FolderPath { get; init; }

	public required string FolderName { get; init; }

	public required string TrackTitle { get; init; }

	public required string ArtistsText { get; init; }

	public required IReadOnlyList<string> Artists { get; init; }

	public required string SearchKey { get; init; }

	public required string RootLabel { get; init; }

	public BitmapSource? SearchArtworkThumbnail
	{
		get
		{
			return _searchArtworkThumbnail;
		}
		set
		{
			if (_searchArtworkThumbnail != value)
			{
				_searchArtworkThumbnail = value;
				OnPropertyChanged("SearchArtworkThumbnail");
			}
		}
	}

	public bool ShowSearchArtworkThumbnail
	{
		get
		{
			return _showSearchArtworkThumbnail;
		}
		set
		{
			if (_showSearchArtworkThumbnail != value)
			{
				_showSearchArtworkThumbnail = value;
				OnPropertyChanged("ShowSearchArtworkThumbnail");
			}
		}
	}

	public bool ShowSearchReadinessStatus
	{
		get
		{
			return _showSearchReadinessStatus;
		}
		set
		{
			if (_showSearchReadinessStatus != value)
			{
				_showSearchReadinessStatus = value;
				OnPropertyChanged("ShowSearchReadinessStatus");
			}
		}
	}

	public SearchReadinessStatus SearchReadinessStatus
	{
		get
		{
			return _searchReadinessStatus;
		}
		set
		{
			if (_searchReadinessStatus != value)
			{
				_searchReadinessStatus = value;
				OnPropertyChanged("SearchReadinessStatus");
			}
		}
	}

	public string? SearchReadinessToolTip
	{
		get
		{
			return _searchReadinessToolTip;
		}
		set
		{
			if (!(_searchReadinessToolTip == value))
			{
				_searchReadinessToolTip = value;
				OnPropertyChanged("SearchReadinessToolTip");
			}
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
