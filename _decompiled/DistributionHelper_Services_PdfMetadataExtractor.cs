using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.RegularExpressions.Generated;
using System.Threading;
using DistributionHelper.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace DistributionHelper.Services;

public static class PdfMetadataExtractor
{
	private const string SuppressedPrincipal = "abhinavjm";

	public static PdfTextResult ExtractText(string path, CancellationToken cancellationToken = default(CancellationToken))
	{
		try
		{
			using PdfDocument pdfDocument = PdfDocument.Open(path);
			List<string> list = new List<string>();
			foreach (Page page in pdfDocument.GetPages())
			{
				cancellationToken.ThrowIfCancellationRequested();
				string text = ContentOrderTextExtractor.GetText(page);
				if (!string.IsNullOrWhiteSpace(text))
				{
					list.Add(text);
				}
			}
			cancellationToken.ThrowIfCancellationRequested();
			string text2 = SanitizePdfText(string.Join(Environment.NewLine, list));
			return new PdfTextResult
			{
				Text = text2,
				Error = (string.IsNullOrWhiteSpace(text2) ? "The PDF has no readable text layer." : null)
			};
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception exception)
		{
			ErrorLog.Write(exception);
			return new PdfTextResult
			{
				Text = string.Empty,
				Error = "The PDF could not be read automatically."
			};
		}
	}

	public static bool IsSuppressedPrincipal(string name)
	{
		return TextNormalizer.ForSearch(name).Replace(" ", string.Empty) == "abhinavjm";
	}

	public static bool NamesSuppressedPrincipal(string text)
	{
		if (!string.IsNullOrWhiteSpace(text))
		{
			return TextNormalizer.ForSearch(text).Replace(" ", string.Empty).Contains("abhinavjm", StringComparison.Ordinal);
		}
		return false;
	}

	public static IReadOnlyList<string> ExtractPayees(string text, IReadOnlyList<string> artists)
	{
		return (from mapping in ExtractPayeeMappings(text, artists)
			select mapping.LegalName).ToArray();
	}

	public static IReadOnlyList<PayeeMapping> ExtractPayeeMappings(string text, IReadOnlyList<string> artists)
	{
		IReadOnlyList<PayeeMapping> mapped = ExtractArtistMappedPayees(text, artists);
		IEnumerable<PayeeMapping> second = from name in ExtractPlainParties(text)
			where mapped.All((PayeeMapping existing) => TextNormalizer.ForSearch(existing.LegalName) != TextNormalizer.ForSearch(name))
			where artists.All((string artist) => TextNormalizer.ForSearch(artist) != TextNormalizer.ForSearch(name))
			select new PayeeMapping(name, null);
		return (from mapping in mapped.Concat(second)
			where !IsSuppressedPrincipal(mapping.LegalName)
			select mapping).ToArray();
	}

	public static IReadOnlyList<string> ExtractPlainParties(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return Array.Empty<string>();
		}
		Match match = BetweenPartiesRegex().Match(text);
		if (!match.Success || match.Groups["parties"].Length > 700)
		{
			return Array.Empty<string>();
		}
		string input = Regex.Replace(match.Groups["parties"].Value, "\\([^)]*\\)", " ");
		List<string> list = new List<string>();
		string[] array = Regex.Split(input, "\\s*(?:,|\\band\\b)\\s*", RegexOptions.IgnoreCase);
		foreach (string text2 in array)
		{
			if (!Regex.IsMatch(text2, "\\bof\\s+[‘’“”\"']", RegexOptions.IgnoreCase))
			{
				string text3 = CleanName(text2);
				if (text3.Split(' ').Length >= 2 && IsPlausiblePersonName(text3) && !Regex.IsMatch(text3, "\\b(?:LLC|Ltd|Inc|GmbH|Media|Records|Music|Publishing)\\b", RegexOptions.IgnoreCase))
				{
					AddUnique(list, text3);
				}
			}
		}
		return list;
	}

	private static IReadOnlyList<PayeeMapping> ExtractArtistMappedPayees(string text, IReadOnlyList<string> artists)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return Array.Empty<PayeeMapping>();
		}
		List<(string, string, bool)> list = CollectAliasPairs(text, artists);
		List<PayeeMapping> list2 = new List<PayeeMapping>(MapTitledArtists(text, artists, list));
		foreach (IGrouping<string, (string, IReadOnlyList<string>)> item in from pair in list
			where pair.Strong
			select (Alias: CleanName(pair.Artist), Names: SplitJointLegalNames(pair.LegalName)) into pair
			where pair.Alias.Length > 0 && pair.Names.Count > 0
			where artists.All((string artist) => TextNormalizer.ForSearch(artist) != TextNormalizer.ForSearch(pair.Alias))
			group pair by TextNormalizer.ForSearch(pair.Alias))
		{
			(string, IReadOnlyList<string>) tuple = (from candidate in item
				orderby candidate.Names.Count descending, candidate.Names.Sum((string text2) => text2.Length) descending
				select candidate).First();
			foreach (string name in tuple.Item2)
			{
				if (list2.All((PayeeMapping existing) => TextNormalizer.ForSearch(existing.LegalName) != TextNormalizer.ForSearch(name)))
				{
					list2.Add(new PayeeMapping(name, tuple.Item1));
				}
			}
		}
		return list2;
	}

	private static List<(string Artist, string LegalName, bool Strong)> CollectAliasPairs(string text, IReadOnlyList<string> artists)
	{
		List<(string, string, bool)> list = new List<(string, string, bool)>();
		foreach (Match item in PerformerFullNameRegex().Matches(text))
		{
			list.Add((CleanName(item.Groups["artist"].Value), CleanName(item.Groups["legal"].Value), true));
		}
		foreach (Match item2 in PerformingAsRegex().Matches(text))
		{
			list.Add((CleanName(item2.Groups["artist"].Value), CleanName(item2.Groups["legal"].Value), true));
		}
		foreach (Match item3 in RepresentedQuotedArtistRegex().Matches(text))
		{
			list.Add((CleanName(item3.Groups["artist"].Value), CleanName(item3.Groups["legal"].Value), true));
		}
		foreach (Match item4 in LegalNameOfQuotedArtistRegex().Matches(text))
		{
			list.Add((CleanName(item4.Groups["artist"].Value), CleanName(item4.Groups["legal"].Value), false));
		}
		foreach (string artist in artists)
		{
			string text2 = Regex.Escape(artist);
			string pattern = "(?im)(?:^|,|\\band\\b|\\bbetween\\b)\\s*(?<legal>[\\p{L}\\p{M}][\\p{L}\\p{M}\\p{N} .,'’\\-]{1,100}?)\\s+of\\s+[\\u2018\\u2019\\u201C\\u201D\"']\\s*" + text2 + "\\s*[\\u2018\\u2019\\u201C\\u201D\"']";
			Match match5 = Regex.Match(text, pattern, RegexOptions.CultureInvariant);
			if (match5.Success)
			{
				list.Add((artist, CleanName(match5.Groups["legal"].Value), false));
			}
		}
		return list;
	}

	private static IReadOnlyList<PayeeMapping> MapTitledArtists(string text, IReadOnlyList<string> artists, List<(string Artist, string LegalName, bool Strong)> pairedNames)
	{
		List<PayeeMapping> list = new List<PayeeMapping>();
		int num = 0;
		foreach (string artist in artists)
		{
			string normalizedArtist = TextNormalizer.ForSearch(artist);
			IReadOnlyList<string> readOnlyList = (from pair in pairedNames
				where TextNormalizer.ForSearch(pair.Artist) == normalizedArtist
				select SplitJointLegalNames(pair.LegalName) into parts
				orderby parts.Count descending
				select parts).FirstOrDefault();
			if (readOnlyList != null && readOnlyList.Count > 0)
			{
				list.AddRange(readOnlyList.Select((string name) => new PayeeMapping(name, artist)));
				num++;
			}
		}
		if (num == artists.Count)
		{
			return list;
		}
		Match match = ContractingPartyNameRegex().Match(text);
		Match match2 = ContractingPartyRegex().Match(text);
		if (artists.Count == 1 && match.Success)
		{
			string text2 = CleanName(match.Groups["legal"].Value);
			if (IsPlausiblePersonName(text2))
			{
				return new _003C_003Ez__ReadOnlySingleElementList<PayeeMapping>(new PayeeMapping(text2, artists[0]));
			}
		}
		else if (artists.Count == 2 && match.Success && match2.Success)
		{
			string text3 = CleanName(match.Groups["legal"].Value);
			string text4 = CleanName(match2.Groups["legal"].Value);
			if (IsPlausiblePersonName(text3) && IsPlausiblePersonName(text4))
			{
				return new _003C_003Ez__ReadOnlyArray<PayeeMapping>(new PayeeMapping[2]
				{
					new PayeeMapping(text4, artists[0]),
					new PayeeMapping(text3, artists[1])
				});
			}
		}
		IReadOnlyList<string> readOnlyList2 = ExtractSignatoryNames(text, artists);
		if (readOnlyList2.Count == artists.Count)
		{
			return readOnlyList2.Select((string name, int index) => new PayeeMapping(name, artists[index])).ToArray();
		}
		Match match3 = StageNamesRegex().Match(text);
		if (match3.Success)
		{
			string[] array = (from value in Regex.Split(match3.Groups["artists"].Value, "\\s*,\\s*").Select(CleanName)
				where value.Length > 0
				select value).ToArray();
			if (array.Length == artists.Count && array.Select(TextNormalizer.ForSearch).SequenceEqual(artists.Select(TextNormalizer.ForSearch)))
			{
				Match match4 = PartySectionRegex().Match(text);
				string input = (match4.Success ? match4.Groups["section"].Value : text);
				List<string> list2 = (from match5 in FullNameLabelRegex().Matches(input)
					select CleanName(match5.Groups["legal"].Value)).Where(IsPlausiblePersonName).ToList();
				if (list2.Count >= artists.Count)
				{
					return list2.Take(artists.Count).Select((string name, int index) => new PayeeMapping(name, artists[index])).ToArray();
				}
			}
		}
		return list;
	}

	public static IReadOnlyList<string> ExtractSongwriters(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return Array.Empty<string>();
		}
		Match match = ByCopyrightRegex().Match(text);
		if (!match.Success)
		{
			match = SongwriterLabelRegex().Match(text);
		}
		if (!match.Success)
		{
			return Array.Empty<string>();
		}
		return DistinctNames(Regex.Split(Regex.Replace(Regex.Replace(Regex.Replace(match.Groups["writers"].Value, "\\s+", " ").Trim(), "^(?:by|songwriters?|writers?)\\s*:?[ ]*", string.Empty, RegexOptions.IgnoreCase), "\\s+(?:Copyright|Publisher|Composition|IPI|CAE)\\b.*$", string.Empty, RegexOptions.IgnoreCase), "\\s*(?:,|;|\\band\\b|&)\\s*", RegexOptions.IgnoreCase).Select(CleanName).Where(IsPlausiblePersonName));
	}

	public static LicenseReleaseMetadata ExtractLicenseReleaseMetadata(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return new LicenseReleaseMetadata(null, null);
		}
		Match match = LicenseArtistRegex().Match(text);
		Match match2 = LicenseTitleRegex().Match(text);
		string text2 = (match.Success ? CleanName(match.Groups["artist"].Value) : null);
		string text3 = (match2.Success ? CleanName(match2.Groups["title"].Value) : null);
		return new LicenseReleaseMetadata(string.IsNullOrWhiteSpace(text2) ? null : text2, string.IsNullOrWhiteSpace(text3) ? null : text3);
	}

	public static bool ContainsUnreadableCharacters(IEnumerable<string> values)
	{
		return values.Any((string value) => value.Contains('\ufffd'));
	}

	private static IReadOnlyList<string> ExtractSignatoryNames(string text, IReadOnlyList<string> artists)
	{
		int num = text.IndexOf("Signator", StringComparison.OrdinalIgnoreCase);
		if (num < 0)
		{
			return Array.Empty<string>();
		}
		int num2 = num;
		string[] array = (from line in text.Substring(num2, text.Length - num2).Split(new char[2] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(CleanName)
			where line.Length > 0
			select line).ToArray();
		List<string> list = new List<string>();
		HashSet<string> hashSet = artists.Select(TextNormalizer.ForSearch).ToHashSet<string>(StringComparer.Ordinal);
		foreach (string artist in artists)
		{
			string text2 = TextNormalizer.ForSearch(artist);
			for (int num3 = 0; num3 < array.Length - 1; num3++)
			{
				if (!(TextNormalizer.ForSearch(array[num3]) != text2))
				{
					string value = array[num3 + 1];
					string item = TextNormalizer.ForSearch(value);
					if (IsPlausiblePersonName(value) && !hashSet.Contains(item) && !TextNormalizer.ForSearch(value).Contains(text2, StringComparison.Ordinal))
					{
						list.Add(CleanName(value));
					}
					break;
				}
			}
		}
		return list;
	}

	private static IReadOnlyList<string> DistinctNames(IEnumerable<string> values)
	{
		List<string> list = new List<string>();
		foreach (string value in values)
		{
			AddUnique(list, value);
		}
		return list;
	}

	private static void AddUnique(ICollection<string> values, string value)
	{
		string cleaned = CleanName(value);
		if (IsPlausiblePersonName(cleaned) && !values.Any((string existing) => TextNormalizer.ForSearch(existing) == TextNormalizer.ForSearch(cleaned)))
		{
			values.Add(cleaned);
		}
	}

	private static IReadOnlyList<string> SplitJointLegalNames(string value)
	{
		string text = CleanName(value);
		if (text.Length == 0)
		{
			return Array.Empty<string>();
		}
		if (!Regex.IsMatch(text, ",|\\band\\b|&", RegexOptions.IgnoreCase))
		{
			if (!IsPlausiblePersonName(text))
			{
				return Array.Empty<string>();
			}
			return new _003C_003Ez__ReadOnlySingleElementList<string>(text);
		}
		string[] array = (from part in Regex.Split(text, "\\s*(?:,|\\band\\b|&)\\s*", RegexOptions.IgnoreCase).Select(CleanName)
			where part.Split(' ').Length >= 2 && IsPlausiblePersonName(part)
			select part).ToArray();
		if (array.Length == 0)
		{
			if (!IsPlausiblePersonName(text))
			{
				return Array.Empty<string>();
			}
			return new string[1] { text };
		}
		return array;
	}

	private static bool IsPlausiblePersonName(string value)
	{
		int length = value.Length;
		if (length < 2 || length > 120 || false || !value.Any(char.IsLetter))
		{
			return false;
		}
		string normalized = TextNormalizer.ForSearch(value);
		if (normalized == "n a")
		{
			return false;
		}
		return !new string[11]
		{
			"signature", "address", "licensee", "triple gen media", "date", "email", "performer", "signatory", "copyright", "publisher",
			"agreement"
		}.Any((string term) => normalized.StartsWith(term, StringComparison.Ordinal));
	}

	private static string CleanName(string value)
	{
		return Regex.Replace(Regex.Replace(Regex.Replace(value, "\\s+", " ").Trim(), "^(?:and|between)\\s+", string.Empty, RegexOptions.IgnoreCase), "\\s+(?:Address|Date|Signature|Email|Phone|Performer|Artist|Copyright|Publisher)\\s*:.*$", string.Empty, RegexOptions.IgnoreCase).Trim(' ', '\t', ':', ';', ',', '.', '-', '–', '—', '"', '\'', '“', '”', '‘', '’');
	}

	private static string SanitizePdfText(string text)
	{
		StringBuilder stringBuilder = new StringBuilder(text.Length);
		foreach (char c in text)
		{
			bool flag = !char.IsControl(c);
			if (!flag)
			{
				bool flag2;
				switch (c)
				{
				case '\t':
				case '\n':
				case '\r':
					flag2 = true;
					break;
				default:
					flag2 = false;
					break;
				}
				flag = flag2;
			}
			if (flag)
			{
				stringBuilder.Append(c);
			}
		}
		return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
	}

	[GeneratedCode("System.Text.RegularExpressions.Generator", "8.0.13.2707")]
	private static Regex PerformerFullNameRegex()
	{
		return _003CRegexGenerator_g_003EFCD9766337A91C9CB73FFA01501ABA45A397905D88BAE620D16537D87E0FF0AAA__PerformerFullNameRegex_0.Instance;
	}

	[GeneratedCode("System.Text.RegularExpressions.Generator", "8.0.13.2707")]
	private static Regex PerformingAsRegex()
	{
		return _003CRegexGenerator_g_003EFCD9766337A91C9CB73FFA01501ABA45A397905D88BAE620D16537D87E0FF0AAA__PerformingAsRegex_1.Instance;
	}

	[GeneratedCode("System.Text.RegularExpressions.Generator", "8.0.13.2707")]
	private static Regex LegalNameOfQuotedArtistRegex()
	{
		return _003CRegexGenerator_g_003EFCD9766337A91C9CB73FFA01501ABA45A397905D88BAE620D16537D87E0FF0AAA__LegalNameOfQuotedArtistRegex_2.Instance;
	}

	[GeneratedCode("System.Text.RegularExpressions.Generator", "8.0.13.2707")]
	private static Regex RepresentedQuotedArtistRegex()
	{
		return _003CRegexGenerator_g_003EFCD9766337A91C9CB73FFA01501ABA45A397905D88BAE620D16537D87E0FF0AAA__RepresentedQuotedArtistRegex_3.Instance;
	}

	[GeneratedCode("System.Text.RegularExpressions.Generator", "8.0.13.2707")]
	private static Regex FullNameLabelRegex()
	{
		return _003CRegexGenerator_g_003EFCD9766337A91C9CB73FFA01501ABA45A397905D88BAE620D16537D87E0FF0AAA__FullNameLabelRegex_4.Instance;
	}

	[GeneratedCode("System.Text.RegularExpressions.Generator", "8.0.13.2707")]
	private static Regex StageNamesRegex()
	{
		return _003CRegexGenerator_g_003EFCD9766337A91C9CB73FFA01501ABA45A397905D88BAE620D16537D87E0FF0AAA__StageNamesRegex_5.Instance;
	}

	[GeneratedCode("System.Text.RegularExpressions.Generator", "8.0.13.2707")]
	private static Regex ContractingPartyNameRegex()
	{
		return _003CRegexGenerator_g_003EFCD9766337A91C9CB73FFA01501ABA45A397905D88BAE620D16537D87E0FF0AAA__ContractingPartyNameRegex_6.Instance;
	}

	[GeneratedCode("System.Text.RegularExpressions.Generator", "8.0.13.2707")]
	private static Regex ContractingPartyRegex()
	{
		return _003CRegexGenerator_g_003EFCD9766337A91C9CB73FFA01501ABA45A397905D88BAE620D16537D87E0FF0AAA__ContractingPartyRegex_7.Instance;
	}

	[GeneratedCode("System.Text.RegularExpressions.Generator", "8.0.13.2707")]
	private static Regex PartySectionRegex()
	{
		return _003CRegexGenerator_g_003EFCD9766337A91C9CB73FFA01501ABA45A397905D88BAE620D16537D87E0FF0AAA__PartySectionRegex_8.Instance;
	}

	[GeneratedCode("System.Text.RegularExpressions.Generator", "8.0.13.2707")]
	private static Regex BetweenPartiesRegex()
	{
		return _003CRegexGenerator_g_003EFCD9766337A91C9CB73FFA01501ABA45A397905D88BAE620D16537D87E0FF0AAA__BetweenPartiesRegex_9.Instance;
	}

	[GeneratedCode("System.Text.RegularExpressions.Generator", "8.0.13.2707")]
	private static Regex ByCopyrightRegex()
	{
		return _003CRegexGenerator_g_003EFCD9766337A91C9CB73FFA01501ABA45A397905D88BAE620D16537D87E0FF0AAA__ByCopyrightRegex_10.Instance;
	}

	[GeneratedCode("System.Text.RegularExpressions.Generator", "8.0.13.2707")]
	private static Regex SongwriterLabelRegex()
	{
		return _003CRegexGenerator_g_003EFCD9766337A91C9CB73FFA01501ABA45A397905D88BAE620D16537D87E0FF0AAA__SongwriterLabelRegex_11.Instance;
	}

	[GeneratedCode("System.Text.RegularExpressions.Generator", "8.0.13.2707")]
	private static Regex LicenseArtistRegex()
	{
		return _003CRegexGenerator_g_003EFCD9766337A91C9CB73FFA01501ABA45A397905D88BAE620D16537D87E0FF0AAA__LicenseArtistRegex_12.Instance;
	}

	[GeneratedCode("System.Text.RegularExpressions.Generator", "8.0.13.2707")]
	private static Regex LicenseTitleRegex()
	{
		return _003CRegexGenerator_g_003EFCD9766337A91C9CB73FFA01501ABA45A397905D88BAE620D16537D87E0FF0AAA__LicenseTitleRegex_13.Instance;
	}
}
