namespace DistributionHelper.Models;

public sealed record CoverMetadataCheck(CoverMetadataStatus Status, string? LicensedArtist, string? LicensedTitle, string Message);
