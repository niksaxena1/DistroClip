using System.Collections.Generic;
using DistributionHelper.Models;

namespace DistributionHelper.Services;

public sealed class IndexResult
{
	public required IReadOnlyList<ReleaseSummary> Releases { get; init; }

	public required IReadOnlyList<string> UnavailableFolders { get; init; }
}
