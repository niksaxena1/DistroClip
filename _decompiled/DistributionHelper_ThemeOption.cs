using System.Windows.Media;
using DistributionHelper.Models;

namespace DistributionHelper;

public sealed class ThemeOption
{
	public required AppTheme Theme { get; init; }

	public required string Name { get; init; }

	public required string Description { get; init; }

	public required Brush PreviewBackground { get; init; }

	public required Brush PreviewPanel { get; init; }

	public required Brush PreviewAccent { get; init; }
}
