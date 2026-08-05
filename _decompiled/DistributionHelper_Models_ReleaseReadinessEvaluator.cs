using System;
using System.Collections.Generic;
using System.Linq;

namespace DistributionHelper.Models;

public static class ReleaseReadinessEvaluator
{
	public static ReleaseReadinessResult Evaluate(ReleaseDetails details)
	{
		List<string> list = new List<string>();
		if (details.AudioPath == null)
		{
			list.Add("WAV master missing");
		}
		if (details.ArtworkPath == null)
		{
			list.Add("artwork missing");
		}
		if (details.ContractPath == null)
		{
			list.Add("contract missing");
		}
		if (details.Payees.Count != details.Summary.Artists.Count && !details.PayeeCountMismatchExpected)
		{
			list.Add("legal names need review");
		}
		if (details.IsCover && details.Songwriters.Count == 0)
		{
			list.Add("songwriters missing");
		}
		foreach (string warning in details.Warnings)
		{
			if (!list.Contains<string>(warning, StringComparer.OrdinalIgnoreCase))
			{
				list.Add(warning);
			}
		}
		if (list.Count == 0)
		{
			return new ReleaseReadinessResult(SearchReadinessStatus.Ready, "Ready — master, artwork, contract and legal names verified");
		}
		string text = string.Join("; ", list.Take(2));
		if (list.Count > 2)
		{
			text += $"; +{list.Count - 2} more";
		}
		return new ReleaseReadinessResult(SearchReadinessStatus.NeedsAttention, "Needs attention — " + text);
	}
}
