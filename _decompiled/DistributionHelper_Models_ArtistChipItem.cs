using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace DistributionHelper.Models;

public sealed class ArtistChipItem(string value) : INotifyPropertyChanged
{
	private ImageSource? _thumbnail;

	private string? _profileUrl;

	public string Value { get; } = value;

	public string DisplayText => Value;

	public ImageSource? Thumbnail
	{
		get
		{
			return _thumbnail;
		}
		set
		{
			_thumbnail = value;
			Raise("Thumbnail");
			Raise("ThumbnailVisibility");
		}
	}

	public string? ProfileUrl
	{
		get
		{
			return _profileUrl;
		}
		set
		{
			_profileUrl = value;
			Raise("ProfileUrl");
		}
	}

	public Visibility ThumbnailVisibility
	{
		get
		{
			if (Thumbnail != null)
			{
				return Visibility.Visible;
			}
			return Visibility.Collapsed;
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	private void Raise(string name)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
