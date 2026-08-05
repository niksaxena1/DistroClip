using System;

namespace DistributionHelper.Services;

public sealed record ArtworkHashEntry(ulong Hash, long FileLength, DateTime LastWriteUtc);
