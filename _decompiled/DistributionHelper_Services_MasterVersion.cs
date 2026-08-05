using System;
using System.Collections.Generic;
using System.Linq;

namespace DistributionHelper.Services;

public sealed class MasterVersion : IComparable<MasterVersion>
{
	public IReadOnlyList<int> Parts { get; }

	public MasterVersion(IEnumerable<int> parts)
	{
		Parts = parts.ToArray();
	}

	public int CompareTo(MasterVersion? other)
	{
		if (other == null)
		{
			return 1;
		}
		int num = Math.Max(Parts.Count, other.Parts.Count);
		for (int i = 0; i < num; i++)
		{
			int num2 = ((i < Parts.Count) ? Parts[i] : 0);
			int value = ((i < other.Parts.Count) ? other.Parts[i] : 0);
			int num3 = num2.CompareTo(value);
			if (num3 != 0)
			{
				return num3;
			}
		}
		return 0;
	}

	public override string ToString()
	{
		return "M" + string.Join('.', Parts);
	}
}
