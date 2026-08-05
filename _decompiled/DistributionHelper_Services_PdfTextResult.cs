namespace DistributionHelper.Services;

public sealed class PdfTextResult
{
	public required string Text { get; init; }

	public string? Error { get; init; }
}
