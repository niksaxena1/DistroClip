namespace DistributionHelper.Services;

public sealed record WaveformInfo(double[] Peaks, double[] Rms, double DurationSeconds, double LeadingSilenceSeconds, double TrailingSilenceSeconds, double? IntegratedLufs, double SamplePeakDb, double TruePeakDb, bool[] ClippedBuckets, bool[] TruePeakOverBuckets, double[] ShortTermLufs, double[] BrightnessHz);
