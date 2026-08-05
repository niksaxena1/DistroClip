using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.RegularExpressions.Generated;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using DistributionHelper.Models;

namespace DistributionHelper.Services;

public static class ReleaseDetailsService
{
	private sealed record ContractCandidate(FileInfo File, PdfTextResult TextResult, IReadOnlyList<PayeeMapping> PayeeMappings, int Rank)
	{
		public IReadOnlyList<string> Payees { get; } = PayeeMappings.Select((PayeeMapping mapping) => mapping.LegalName).ToArray();
	}

	private sealed record ArtworkCandidate(FileInfo File, bool IsSquare, long PixelCount, int NameScore);

	private static readonly HashSet<string> ArtworkExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".tif", ".tiff", ".bmp" };

	public static Task<ReleaseDetails> LoadAsync(ReleaseSummary summary, CancellationToken cancellationToken = default(CancellationToken))
	{
		return Task.Run(() => Load(summary, cancellationToken), cancellationToken);
	}

	public static string? FindArtworkPath(string folderPath, CancellationToken cancellationToken = default(CancellationToken))
	{
		try
		{
			return (from file in new DirectoryInfo(folderPath).EnumerateFiles("*", SearchOption.TopDirectoryOnly)
				where ArtworkExtensions.Contains(file.Extension)
				where !IsExcludedArtwork(file.Name)
				select InspectArtwork(file, cancellationToken) into candidate
				where candidate.IsSquare
				orderby candidate.PixelCount descending, candidate.NameScore descending, candidate.File.LastWriteTimeUtc descending
				select candidate.File.FullName).FirstOrDefault();
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception exception)
		{
			ErrorLog.Write(exception);
			return null;
		}
	}

	public static BitmapSource? LoadSearchArtworkThumbnail(string folderPath, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (ReleaseIndexer.ContainsOldPathSegment(folderPath))
		{
			return null;
		}
		try
		{
			FileInfo fileInfo = (from file in new DirectoryInfo(folderPath).EnumerateFiles("*", SearchOption.TopDirectoryOnly)
				where ArtworkExtensions.Contains(file.Extension)
				where !IsExcludedArtwork(file.Name)
				select InspectArtwork(file, cancellationToken) into candidate
				where candidate.IsSquare
				orderby candidate.PixelCount descending, candidate.NameScore descending, candidate.File.LastWriteTimeUtc descending
				select candidate.File).FirstOrDefault();
			return (fileInfo == null) ? null : LoadArtworkThumbnail(fileInfo, new List<string>(), cancellationToken, 64);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception exception)
		{
			ErrorLog.Write(exception);
			return null;
		}
	}

	private static ReleaseDetails Load(ReleaseSummary summary, CancellationToken cancellationToken)
	{
		if (ReleaseIndexer.ContainsOldPathSegment(summary.FolderPath))
		{
			throw new InvalidOperationException("DistroClip will not read a folder inside an Old directory.");
		}
		List<string> list = new List<string>();
		FileInfo[] source;
		try
		{
			source = new DirectoryInfo(summary.FolderPath).EnumerateFiles("*", SearchOption.TopDirectoryOnly).ToArray();
		}
		catch (Exception ex)
		{
			ErrorLog.Write(ex);
			throw new IOException("The release folder could not be read.", ex);
		}
		cancellationToken.ThrowIfCancellationRequested();
		FileInfo[] source2 = source.Where((FileInfo file) => file.Extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase)).ToArray();
		FileInfo license = (from file in source2
			select new
			{
				File = file,
				Score = LicenseScore(file.Name)
			} into item
			where item.Score > 0
			orderby item.Score descending, item.File.LastWriteTimeUtc descending
			select item.File).FirstOrDefault();
		ContractCandidate[] source3 = (from file in (from file in source2
				where license == null || !file.FullName.Equals(license.FullName, StringComparison.OrdinalIgnoreCase)
				where ContractScore(file.Name) > 0
				orderby ContractScore(file.Name) descending, file.LastWriteTimeUtc descending
				select file).ToArray()
			select AnalyzeContract(file, summary.Artists, cancellationToken) into candidate
			orderby ContractScore(candidate.File.Name) descending, candidate.Rank descending, candidate.File.LastWriteTimeUtc descending
			select candidate).ToArray();
		cancellationToken.ThrowIfCancellationRequested();
		source3 = source3.Where((ContractCandidate candidate) => candidate.Rank > 0).ToArray();
		ContractCandidate chosenContract = source3.FirstOrDefault();
		FileInfo fileInfo = chosenContract?.File;
		IReadOnlyList<string> readOnlyList = chosenContract?.Payees ?? Array.Empty<string>();
		bool flag = false;
		if ((object)chosenContract != null)
		{
			if (chosenContract.TextResult.Error != null)
			{
				list.Add("Contract: " + chosenContract.TextResult.Error);
			}
			else
			{
				bool flag2 = PdfMetadataExtractor.NamesSuppressedPrincipal(chosenContract.TextResult.Text);
				bool flag3 = readOnlyList.Count > 0 && summary.Artists.All((string artist) => chosenContract.PayeeMappings.Any((PayeeMapping mapping) => mapping.Artist != null && TextNormalizer.ForSearch(mapping.Artist) == TextNormalizer.ForSearch(artist)));
				flag = flag2 || flag3 || (readOnlyList.Count > 0 && TextNormalizer.ForSearch(chosenContract.TextResult.Text).Contains("prelude", StringComparison.Ordinal));
				if (readOnlyList.Count == 0)
				{
					if (!flag2)
					{
						list.Add("No legal names could be read from the contract; open it to verify.");
					}
				}
				else if (readOnlyList.Count != summary.Artists.Count && !flag)
				{
					list.Add($"Found {readOnlyList.Count} legal name(s) for {summary.Artists.Count} artist(s); verify the contract.");
				}
				if (PdfMetadataExtractor.ContainsUnreadableCharacters(readOnlyList))
				{
					list.Add("A legal name contains an unreadable PDF character; verify it before copying.");
				}
			}
			if (source3.Skip(1).Any((ContractCandidate candidate) => candidate.Payees.Count == summary.Artists.Count && candidate.Rank >= chosenContract.Rank - 200 && ContractScore(candidate.File.Name) >= ContractScore(chosenContract.File.Name)))
			{
				list.Add("Multiple contract PDFs matched; using " + fileInfo.Name + ".");
			}
		}
		else
		{
			list.Add("No signed, filled, or completed contract PDF was found at the release root.");
		}
		IReadOnlyList<string> readOnlyList2 = Array.Empty<string>();
		CoverMetadataCheck coverMetadataCheck = null;
		if (license != null)
		{
			PdfTextResult pdfTextResult = PdfMetadataExtractor.ExtractText(license.FullName, cancellationToken);
			if (pdfTextResult.Error != null)
			{
				list.Add("Proof of Licensing: " + pdfTextResult.Error);
				coverMetadataCheck = new CoverMetadataCheck(CoverMetadataStatus.NeedsReview, null, null, "Proof artist and title could not be read; verify the licence");
			}
			else
			{
				string text = TextNormalizer.ForSearch(pdfTextResult.Text);
				if (!text.Contains("proof of mechanical music licensing", StringComparison.Ordinal) && !text.Contains("easy song", StringComparison.Ordinal))
				{
					list.Add("The Proof of Licensing PDF has an unexpected format; verify the cover details.");
				}
				LicenseReleaseMetadata metadata = PdfMetadataExtractor.ExtractLicenseReleaseMetadata(pdfTextResult.Text);
				coverMetadataCheck = CoverMetadataValidator.Compare(summary, metadata);
				if (coverMetadataCheck.Status == CoverMetadataStatus.NeedsReview)
				{
					list.Add(coverMetadataCheck.Message);
				}
				readOnlyList2 = PdfMetadataExtractor.ExtractSongwriters(pdfTextResult.Text);
				if (readOnlyList2.Count == 0)
				{
					list.Add("This is a cover, but songwriter names could not be read automatically.");
				}
				if (PdfMetadataExtractor.ContainsUnreadableCharacters(readOnlyList2))
				{
					list.Add("A songwriter name contains an unreadable PDF character; verify it before copying.");
				}
			}
		}
		cancellationToken.ThrowIfCancellationRequested();
		FileInfo[] array = source.Where((FileInfo file) => file.Extension.Equals(".wav", StringComparison.OrdinalIgnoreCase)).ToArray();
		var source4 = (from file in array
			select new
			{
				File = file,
				Version = GetMasterVersion(file.Name)
			} into item
			where item.Version != null
			orderby item.Version descending, MixPreference(item.File.Name) descending, item.File.LastWriteTimeUtc descending
			select item).ToArray();
		FileInfo fileInfo2 = source4.FirstOrDefault()?.File;
		string[] otherAudioVersions = (from item in source4.Skip(1)
			select item.Version.ToString()).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToArray();
		if (fileInfo2 == null && array.Length == 0)
		{
			list.Add("No WAV master was found at the release root.");
		}
		else if (fileInfo2 == null)
		{
			fileInfo2 = (from file in array
				orderby MixPreference(file.Name) descending, file.LastWriteTimeUtc descending
				select file).First();
			list.Add("No M-numbered WAV was found; selected " + fileInfo2.Name + " as the available WAV.");
		}
		cancellationToken.ThrowIfCancellationRequested();
		FileInfo fileInfo3 = (from file in source
			where ArtworkExtensions.Contains(file.Extension)
			where !IsExcludedArtwork(file.Name)
			select InspectArtwork(file, cancellationToken) into candidate
			where candidate.IsSquare
			orderby candidate.PixelCount descending, candidate.NameScore descending, candidate.File.LastWriteTimeUtc descending
			select candidate.File).FirstOrDefault();
		if (fileInfo3 == null)
		{
			list.Add("No suitable square JPG, PNG, WEBP, TIFF, or BMP artwork was found at the release root.");
		}
		FileInfo fileInfo4 = source.FirstOrDefault((FileInfo file) => file.Name.Equals("CREDITS.txt", StringComparison.OrdinalIgnoreCase));
		string credits = ((fileInfo4 == null) ? null : ReadTextFile(fileInfo4.FullName, list, cancellationToken));
		BitmapSource artworkThumbnail = ((fileInfo3 == null) ? null : LoadArtworkThumbnail(fileInfo3, list, cancellationToken));
		long? audioFileSize = fileInfo2?.Length;
		TimeSpan? audioDuration = ((fileInfo2 == null) ? ((TimeSpan?)null) : ReadWavDuration(fileInfo2.FullName));
		cancellationToken.ThrowIfCancellationRequested();
		return new ReleaseDetails
		{
			Summary = summary,
			Payees = readOnlyList,
			PayeeMappings = (chosenContract?.PayeeMappings ?? Array.Empty<PayeeMapping>()),
			PayeeCountMismatchExpected = flag,
			Songwriters = readOnlyList2,
			Credits = credits,
			ArtworkPath = fileInfo3?.FullName,
			ArtworkThumbnail = artworkThumbnail,
			AudioPath = fileInfo2?.FullName,
			AudioFileSize = audioFileSize,
			AudioDuration = audioDuration,
			OtherAudioVersions = otherAudioVersions,
			ContractPath = fileInfo?.FullName,
			LicensePath = license?.FullName,
			CoverMetadataCheck = coverMetadataCheck,
			Warnings = list
		};
	}

	public static MasterVersion? GetMasterVersion(string filename)
	{
		Match match = MasterVersionRegex().Match(Path.GetFileNameWithoutExtension(filename));
		if (!match.Success)
		{
			return null;
		}
		int result;
		int[] array = (from part in match.Groups["version"].Value.Split('.')
			select int.TryParse(part, out result) ? result : (-1)).ToArray();
		if (!array.Any((int part) => part < 0))
		{
			return new MasterVersion(array);
		}
		return null;
	}

	public static TimeSpan? ReadWavDuration(string path)
	{
		try
		{
			using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			using BinaryReader binaryReader = new BinaryReader(fileStream, Encoding.ASCII, leaveOpen: false);
			if (fileStream.Length < 12 || Encoding.ASCII.GetString(binaryReader.ReadBytes(4)) != "RIFF" || binaryReader.ReadUInt32() < 4 || Encoding.ASCII.GetString(binaryReader.ReadBytes(4)) != "WAVE")
			{
				return null;
			}
			uint? num = null;
			uint? num2 = null;
			while (fileStream.Position + 8 <= fileStream.Length)
			{
				string text = Encoding.ASCII.GetString(binaryReader.ReadBytes(4));
				uint num3 = binaryReader.ReadUInt32();
				long position = fileStream.Position;
				if (text == "fmt " && num3 >= 16)
				{
					binaryReader.ReadUInt16();
					binaryReader.ReadUInt16();
					binaryReader.ReadUInt32();
					num = binaryReader.ReadUInt32();
				}
				else if (text == "data")
				{
					num2 = num3;
				}
				if (num.HasValue && num.GetValueOrDefault() != 0 && num2.HasValue)
				{
					return TimeSpan.FromSeconds((double)num2.Value / (double)num.Value);
				}
				fileStream.Position = Math.Min(fileStream.Length, position + num3 + num3 % 2);
			}
		}
		catch (IOException)
		{
		}
		catch (UnauthorizedAccessException)
		{
		}
		return null;
	}

	public static int GetMasterNumber(string filename)
	{
		return GetMasterVersion(filename)?.Parts.FirstOrDefault() ?? (-1);
	}

	public static int ContractScore(string filename)
	{
		string name = TextNormalizer.ForSearch(filename);
		if (new string[13]
		{
			"uniqode", "certificate", "proof of licensing", "vocal license", "vocal licence", "vocal buyout agreement", "vocal recording rights transfer", "hitvocals", "hit vocals", "split sheet",
			"unsigned", "incomplete", "draft"
		}.Any((string term) => name.Contains(term, StringComparison.Ordinal)))
		{
			return 0;
		}
		if (Regex.IsMatch(name, "^\\s*[\\[\\(]?\\s*signed\\b", RegexOptions.CultureInvariant))
		{
			return 600;
		}
		if (Regex.IsMatch(name, "^\\s*[\\[\\(]?\\s*filled\\b", RegexOptions.CultureInvariant))
		{
			return 550;
		}
		if (name.Contains("fully signed", StringComparison.Ordinal))
		{
			return 525;
		}
		if (Regex.IsMatch(name, "\\bcompleted?\\b", RegexOptions.CultureInvariant))
		{
			return 500;
		}
		if (Regex.IsMatch(name, "\\bsigned\\b", RegexOptions.CultureInvariant))
		{
			return 450;
		}
		if (Regex.IsMatch(name, "\\bfilled\\b", RegexOptions.CultureInvariant))
		{
			return 400;
		}
		return 0;
	}

	public static int LicenseScore(string filename)
	{
		string text = TextNormalizer.ForSearch(Path.GetFileNameWithoutExtension(filename));
		if (text.Contains("proof of licensing", StringComparison.Ordinal))
		{
			return 600;
		}
		if (text.Contains("proof", StringComparison.Ordinal) && text.Contains("licen", StringComparison.Ordinal))
		{
			return 550;
		}
		if (Regex.IsMatch(text, "\\blpl\\s*\\d+\\b", RegexOptions.IgnoreCase))
		{
			return 450;
		}
		return 0;
	}

	private static ContractCandidate AnalyzeContract(FileInfo file, IReadOnlyList<string> artists, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		PdfTextResult pdfTextResult = PdfMetadataExtractor.ExtractText(file.FullName, cancellationToken);
		IReadOnlyList<PayeeMapping> readOnlyList = ((pdfTextResult.Error == null) ? PdfMetadataExtractor.ExtractPayeeMappings(pdfTextResult.Text, artists) : Array.Empty<PayeeMapping>());
		IReadOnlyList<PayeeMapping> readOnlyList2 = readOnlyList;
		string normalizedText = TextNormalizer.ForSearch(pdfTextResult.Text);
		int num = artists.Count((string artist) => normalizedText.Contains(TextNormalizer.ForSearch(artist), StringComparison.Ordinal));
		int num2 = ContractScore(file.Name) + num * 250 + readOnlyList2.Count * 450 + ((readOnlyList2.Count == artists.Count) ? 1800 : 0);
		if (new string[5] { "license agreement", "artist agreement", "work for hire agreement", "distribution split agreement", "share confirmation" }.Any((string heading) => normalizedText.Contains(heading, StringComparison.Ordinal)))
		{
			num2 += 350;
		}
		if (new string[7] { "certificate of completion", "proof of mechanical music licensing", "hitvocals", "hit vocals exclusive licensing agreement", "vocal buyout agreement", "vocal recording rights transfer", "vocal licensing agreement" }.Any((string term) => normalizedText.Contains(term, StringComparison.Ordinal)))
		{
			num2 = 0;
		}
		return new ContractCandidate(file, pdfTextResult, readOnlyList2, num2);
	}

	private static int MixPreference(string filename)
	{
		string name = TextNormalizer.ForSearch(Path.GetFileNameWithoutExtension(filename));
		if (!new string[6] { "instrumental", "acapella", "a cappella", "tv mix", "extended mix", "radio edit" }.Any((string term) => name.Contains(term, StringComparison.Ordinal)))
		{
			return 0;
		}
		return -100;
	}

	private static bool IsExcludedArtwork(string filename)
	{
		string name = TextNormalizer.ForSearch(Path.GetFileNameWithoutExtension(filename));
		return new string[4] { "social banner", "revenue share", "v layers", "mockup" }.Any((string term) => name.Contains(term, StringComparison.Ordinal));
	}

	private static ArtworkCandidate InspectArtwork(FileInfo file, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		string text = TextNormalizer.ForSearch(Path.GetFileNameWithoutExtension(file.Name));
		int num = 0;
		if (text.Contains("artwork", StringComparison.Ordinal) || text.Contains("cover", StringComparison.Ordinal))
		{
			num += 100;
		}
		if (text.Contains("final", StringComparison.Ordinal))
		{
			num += 40;
		}
		if (text.Contains("3000", StringComparison.Ordinal))
		{
			num += 10;
		}
		try
		{
			using FileStream bitmapStream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			BitmapFrame bitmapFrame = BitmapDecoder.Create(bitmapStream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];
			int pixelWidth = bitmapFrame.PixelWidth;
			int pixelHeight = bitmapFrame.PixelHeight;
			cancellationToken.ThrowIfCancellationRequested();
			return new ArtworkCandidate(file, pixelWidth > 0 && pixelWidth == pixelHeight, (long)pixelWidth * (long)pixelHeight, num);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception exception)
		{
			ErrorLog.Write(exception);
			return new ArtworkCandidate(file, IsSquare: false, 0L, num);
		}
	}

	private static BitmapSource? LoadArtworkThumbnail(FileInfo file, ICollection<string> warnings, CancellationToken cancellationToken, int decodePixelWidth = 520)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			using FileStream streamSource = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			BitmapImage bitmapImage = new BitmapImage();
			bitmapImage.BeginInit();
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.DecodePixelWidth = decodePixelWidth;
			bitmapImage.StreamSource = streamSource;
			bitmapImage.EndInit();
			((Freezable)bitmapImage).Freeze();
			cancellationToken.ThrowIfCancellationRequested();
			return bitmapImage;
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception exception)
		{
			ErrorLog.Write(exception);
			warnings.Add("The artwork is ready to drag, but its preview could not be loaded.");
			return null;
		}
	}

	private static string? ReadTextFile(string path, ICollection<string> warnings, CancellationToken cancellationToken)
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			byte[] bytes = File.ReadAllBytes(path);
			cancellationToken.ThrowIfCancellationRequested();
			(string Text, bool UsedWindows1252) tuple = DecodeCreditsBytes(bytes);
			if (tuple.UsedWindows1252)
			{
				warnings.Add("CREDITS.txt is not UTF-8; it was read as Windows-1252.");
			}
			return tuple.Text;
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception exception)
		{
			ErrorLog.Write(exception);
			warnings.Add("CREDITS.txt could not be read.");
			return null;
		}
	}

	internal static (string Text, bool UsedWindows1252) DecodeCreditsBytes(byte[] bytes)
	{
		if (bytes.Length >= 4 && bytes[0] == byte.MaxValue && bytes[1] == 254 && bytes[2] == 0 && bytes[3] == 0)
		{
			return (Text: new UTF32Encoding(bigEndian: false, byteOrderMark: false, throwOnInvalidCharacters: true).GetString(bytes, 4, bytes.Length - 4), UsedWindows1252: false);
		}
		if (bytes.Length >= 4 && bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 254 && bytes[3] == byte.MaxValue)
		{
			return (Text: new UTF32Encoding(bigEndian: true, byteOrderMark: false, throwOnInvalidCharacters: true).GetString(bytes, 4, bytes.Length - 4), UsedWindows1252: false);
		}
		if (bytes.Length >= 3 && bytes[0] == 239 && bytes[1] == 187 && bytes[2] == 191)
		{
			return (Text: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes, 3, bytes.Length - 3), UsedWindows1252: false);
		}
		if (bytes.Length >= 2 && bytes[0] == byte.MaxValue && bytes[1] == 254)
		{
			return (Text: new UnicodeEncoding(bigEndian: false, byteOrderMark: false, throwOnInvalidBytes: true).GetString(bytes, 2, bytes.Length - 2), UsedWindows1252: false);
		}
		if (bytes.Length >= 2 && bytes[0] == 254 && bytes[1] == byte.MaxValue)
		{
			return (Text: new UnicodeEncoding(bigEndian: true, byteOrderMark: false, throwOnInvalidBytes: true).GetString(bytes, 2, bytes.Length - 2), UsedWindows1252: false);
		}
		try
		{
			return (Text: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes), UsedWindows1252: false);
		}
		catch (DecoderFallbackException)
		{
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
			return (Text: Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback).GetString(bytes), UsedWindows1252: true);
		}
	}

	[GeneratedCode("System.Text.RegularExpressions.Generator", "8.0.13.2707")]
	private static Regex MasterVersionRegex()
	{
		return _003CRegexGenerator_g_003EFCD9766337A91C9CB73FFA01501ABA45A397905D88BAE620D16537D87E0FF0AAA__MasterVersionRegex_14.Instance;
	}
}
