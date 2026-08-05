using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DistributionHelper.Services;

public static class TextNormalizer
{
	private static readonly Dictionary<char, string> ExplicitMappings = new Dictionary<char, string>
	{
		['ø'] = "o",
		['Ø'] = "o",
		['æ'] = "ae",
		['Æ'] = "ae",
		['œ'] = "oe",
		['Œ'] = "oe",
		['ß'] = "ss",
		['ẞ'] = "ss",
		['ł'] = "l",
		['Ł'] = "l",
		['ð'] = "d",
		['Ð'] = "d",
		['þ'] = "th",
		['Þ'] = "th",
		['đ'] = "d",
		['Đ'] = "d",
		['ħ'] = "h",
		['Ħ'] = "h",
		['ı'] = "i",
		['ŋ'] = "n",
		['Ŋ'] = "n",
		['Ʌ'] = "a",
		['ʌ'] = "a"
	};

	public static string ForSearch(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}
		StringBuilder stringBuilder = new StringBuilder(value.Length);
		string text = value;
		foreach (char c in text)
		{
			if (ExplicitMappings.TryGetValue(c, out var value2))
			{
				stringBuilder.Append(value2);
			}
			else
			{
				stringBuilder.Append(c);
			}
		}
		string text2 = stringBuilder.ToString().Normalize(NormalizationForm.FormKD);
		StringBuilder stringBuilder2 = new StringBuilder(text2.Length);
		bool flag = true;
		text = text2;
		foreach (char c2 in text)
		{
			if ((uint)(CharUnicodeInfo.GetUnicodeCategory(c2) - 5) > 2u)
			{
				if (char.IsLetterOrDigit(c2))
				{
					stringBuilder2.Append(char.ToLowerInvariant(c2));
					flag = false;
				}
				else if (!flag)
				{
					stringBuilder2.Append(' ');
					flag = true;
				}
			}
		}
		return stringBuilder2.ToString().Trim();
	}
}
