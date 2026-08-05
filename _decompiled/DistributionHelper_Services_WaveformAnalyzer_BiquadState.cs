using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace DistributionHelper.Services;

public static class WaveformAnalyzer
{
	private readonly record struct BiquadCoefficients(double B0, double B1, double B2, double A1, double A2)
	{
		public static BiquadCoefficients KWeightingShelf(double sampleRate)
		{
			double num = Math.Tan(5284.078578647628 / sampleRate);
			double num2 = Math.Pow(10.0, 0.19999219269866736);
			double num3 = Math.Pow(num2, 0.4996667741545416);
			double num4 = 1.0 + num / 0.7071752369554196 + num * num;
			return new BiquadCoefficients((num2 + num3 * num / 0.7071752369554196 + num * num) / num4, 2.0 * (num * num - num2) / num4, (num2 - num3 * num / 0.7071752369554196 + num * num) / num4, 2.0 * (num * num - 1.0) / num4, (1.0 - num / 0.7071752369554196 + num * num) / num4);
		}

		public static BiquadCoefficients KWeightingHighPass(double sampleRate)
		{
			double num = Math.Tan(119.8061151453059 / sampleRate);
			double num2 = 1.0 + num / 0.5003270373238773 + num * num;
			return new BiquadCoefficients(1.0, -2.0, 1.0, 2.0 * (num * num - 1.0) / num2, (1.0 - num / 0.5003270373238773 + num * num) / num2);
		}
	}

	private struct BiquadState
	{
		private double _x1;

		private double _x2;

		private double _y1;

		private double _y2;

		public double Process(in BiquadCoefficients c, double input)
		{
			double num = c.B0 * input + c.B1 * _x1 + c.B2 * _x2 - c.A1 * _y1 - c.A2 * _y2;
			_x2 = _x1;
			_x1 = input;
			_y2 = _y1;
			_y1 = num;
			return num;
		}
	}

	private const double SilenceThreshold = 0.001;

	private const double ClipSampleThreshold = 0.99996;

	private const int TruePeakPhaseCount = 4;

	private const int TruePeakTaps = 12;

	private static readonly double[][] TruePeakPhases = CreateTruePeakPhases();

	public static WaveformInfo? Analyze(string path, int buckets, CancellationToken cancellationToken)
	{
		try
		{
			using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.SequentialScan);
			return Analyze(stream, buckets, cancellationToken);
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

	public static WaveformInfo? Analyze(Stream stream, int buckets, CancellationToken cancellationToken)
	{
		Span<byte> buffer = stackalloc byte[12];
		if (stream.Read(buffer) != 12 || !buffer.Slice(0, 4).SequenceEqual("RIFF"u8) || !buffer.Slice(8, 4).SequenceEqual("WAVE"u8))
		{
			return null;
		}
		ushort num = 0;
		ushort num2 = 0;
		ushort num3 = 0;
		int num4 = 0;
		long num5 = -1L;
		long val = 0L;
		Span<byte> buffer2 = stackalloc byte[8];
		Span<byte> span = stackalloc byte[40];
		while (stream.Read(buffer2) == 8)
		{
			Span<byte> span2 = buffer2.Slice(0, 4);
			uint num6 = BinaryPrimitives.ReadUInt32LittleEndian(buffer2.Slice(4, 4));
			if (span2.SequenceEqual("fmt "u8))
			{
				int num7 = (int)Math.Min(num6, 40u);
				if (stream.Read(span.Slice(0, num7)) != num7)
				{
					return null;
				}
				num = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(0, 2));
				num2 = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(2, 2));
				num4 = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(4, 4));
				num3 = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(14, 2));
				if (num == 65534 && num6 >= 40)
				{
					num = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(24, 2));
				}
				SkipPadding(stream, num6, num7);
			}
			else
			{
				if (span2.SequenceEqual("data"u8))
				{
					num5 = stream.Position;
					val = num6;
					break;
				}
				SkipPadding(stream, num6, 0);
			}
		}
		bool flag = num5 < 0 || num2 == 0 || num3 == 0 || num4 <= 0 || (num != 1 && num != 3) || (num == 3 && num3 != 32);
		if (!flag)
		{
			bool flag2 = num == 1;
			if (flag2)
			{
				flag2 = (num3 != 16 && num3 != 24 && num3 != 32) || 1 == 0;
			}
			flag = flag2;
		}
		if (flag)
		{
			return null;
		}
		int num8 = num3 / 8;
		int num9 = num8 * num2;
		val = Math.Min(val, stream.Length - num5);
		long num10 = val / num9;
		if (num10 < buckets)
		{
			return null;
		}
		double[] array = new double[buckets];
		double[] array2 = new double[buckets];
		double[] array3 = new double[buckets];
		double[] array4 = new double[num2];
		long[] array5 = new long[buckets];
		bool[] array6 = new bool[buckets];
		bool[] array7 = new bool[buckets];
		double num11 = (double)num10 / (double)buckets;
		double num12 = 0.0;
		long num13 = -1L;
		long num14 = -1L;
		double num15 = 0.0;
		double[][] array8 = new double[num2][];
		int[] array9 = new int[num2];
		int[] array10 = new int[num2];
		for (int i = 0; i < num2; i++)
		{
			array8[i] = new double[12];
		}
		BiquadCoefficients c = BiquadCoefficients.KWeightingShelf(num4);
		BiquadCoefficients c2 = BiquadCoefficients.KWeightingHighPass(num4);
		BiquadState[] array11 = new BiquadState[num2];
		BiquadState[] array12 = new BiquadState[num2];
		int num16 = Math.Max(1, num4 / 10);
		List<double> list = new List<double>();
		double num17 = 0.0;
		long num18 = 0L;
		byte[] array13 = new byte[1048576];
		int num19 = 0;
		long num20 = 0L;
		while (num20 < num10)
		{
			cancellationToken.ThrowIfCancellationRequested();
			int num21 = stream.Read(array13, num19, array13.Length - num19);
			if (num21 <= 0)
			{
				break;
			}
			int num22 = num19 + num21;
			long num23 = Math.Min(num22 / num9, num10 - num20);
			for (long num24 = 0L; num24 < num23; num24++)
			{
				long num25 = num20 + num24;
				int num26 = (int)Math.Min(buckets - 1, (long)((double)num25 / num11));
				int num27 = (int)(num24 * num9);
				bool flag3 = false;
				for (int j = 0; j < num2; j++)
				{
					double num28 = ReadSample(array13, num27 + j * num8, num, num3);
					double num29 = Math.Abs(num28);
					if (num29 > array[num26])
					{
						array[num26] = num29;
					}
					if (num29 > num12)
					{
						num12 = num29;
					}
					if (num29 >= 0.99996)
					{
						array6[num26] = true;
					}
					if (num29 > 0.001)
					{
						flag3 = true;
					}
					array2[num26] += num28 * num28;
					double num30 = num28 - array4[j];
					array3[num26] += num30 * num30;
					array4[j] = num28;
					double[] array14 = array8[j];
					int num31 = array9[j];
					array14[num31] = num28;
					array9[j] = (num31 + 1) % 12;
					if (num29 > 0.7)
					{
						array10[j] = 12;
					}
					if (array10[j] > 0)
					{
						array10[j]--;
						for (int k = 0; k < 4; k++)
						{
							double[] array15 = TruePeakPhases[k];
							double num32 = 0.0;
							int num33 = num31;
							for (int l = 0; l < 12; l++)
							{
								num32 += array15[l] * array14[num33];
								num33 = ((num33 == 0) ? 11 : (num33 - 1));
							}
							double num34 = Math.Abs(num32);
							if (num34 > num15)
							{
								num15 = num34;
							}
							if (num34 > 1.0)
							{
								array7[num26] = true;
							}
						}
					}
					double num35 = array12[j].Process(in c2, array11[j].Process(in c, num28));
					num17 += num35 * num35;
				}
				array5[num26] += num2;
				if (flag3)
				{
					if (num13 < 0)
					{
						num13 = num25;
					}
					num14 = num25;
				}
				if (++num18 == num16)
				{
					list.Add(num17 / (double)num16);
					num17 = 0.0;
					num18 = 0L;
				}
			}
			num20 += num23;
			int num36 = (int)(num23 * num9);
			num19 = num22 - num36;
			if (num19 > 0)
			{
				Array.Copy(array13, num36, array13, 0, num19);
			}
		}
		double[] array16 = new double[buckets];
		for (int m = 0; m < buckets; m++)
		{
			array16[m] = ((array5[m] > 0) ? Math.Sqrt(array2[m] / (double)array5[m]) : 0.0);
		}
		double num37 = array.Max();
		if (num37 > 0.0)
		{
			for (int n = 0; n < buckets; n++)
			{
				array[n] = Math.Min(1.0, array[n] / num37);
				array16[n] = Math.Min(1.0, array16[n] / num37);
			}
		}
		double num38 = (double)num10 / (double)num4;
		double leadingSilenceSeconds = ((num13 < 0) ? num38 : ((double)num13 / (double)num4));
		double trailingSilenceSeconds = ((num13 < 0) ? 0.0 : ((double)(num10 - 1 - num14) / (double)num4));
		double samplePeakDb = ((num12 > 0.0) ? (20.0 * Math.Log10(num12)) : (-120.0));
		num15 = Math.Max(num15, num12);
		double truePeakDb = ((num15 > 0.0) ? (20.0 * Math.Log10(num15)) : (-120.0));
		double[] array17 = new double[buckets];
		for (int num39 = 0; num39 < buckets; num39++)
		{
			if (array2[num39] > 1E-12)
			{
				double num40 = Math.Sqrt(array3[num39] / array2[num39]);
				array17[num39] = (double)num4 / Math.PI * Math.Asin(Math.Min(1.0, num40 / 2.0));
			}
		}
		return new WaveformInfo(array, array16, num38, leadingSilenceSeconds, trailingSilenceSeconds, ComputeIntegratedLoudness(list), samplePeakDb, truePeakDb, array6, array7, ComputeShortTermLoudness(list, buckets), array17);
	}

	private static double[] ComputeShortTermLoudness(IReadOnlyList<double> subBlockEnergies, int buckets)
	{
		double[] array = new double[buckets];
		Array.Fill(array, -70.0);
		if (subBlockEnergies.Count == 0)
		{
			return array;
		}
		double[] array2 = new double[subBlockEnergies.Count];
		for (int i = 0; i < subBlockEnergies.Count; i++)
		{
			int num = Math.Max(0, i - 15);
			int num2 = Math.Min(subBlockEnergies.Count - 1, i + 15);
			double num3 = 0.0;
			for (int j = num; j <= num2; j++)
			{
				num3 += subBlockEnergies[j];
			}
			array2[i] = num3 / (double)(num2 - num + 1);
		}
		double[] array3 = new double[buckets];
		int[] array4 = new int[buckets];
		for (int k = 0; k < array2.Length; k++)
		{
			int num4 = (int)Math.Min((long)buckets - 1L, (long)k * (long)buckets / array2.Length);
			array3[num4] += array2[k];
			array4[num4]++;
		}
		for (int l = 0; l < buckets; l++)
		{
			if (array4[l] > 0)
			{
				array[l] = Math.Max(-70.0, ToLoudness(array3[l] / (double)array4[l]));
			}
		}
		return array;
	}

	private static double[][] CreateTruePeakPhases()
	{
		double num = 23.5;
		double[][] array = new double[4][];
		for (int i = 0; i < 4; i++)
		{
			array[i] = new double[12];
		}
		for (int j = 0; j < 48; j++)
		{
			double num2 = ((double)j - num) / 4.0;
			double num3 = ((num2 == 0.0) ? 1.0 : (Math.Sin(Math.PI * num2) / (Math.PI * num2)));
			double num4 = 0.5 * (1.0 - Math.Cos(Math.PI * 2.0 * (double)j / 47.0));
			array[j % 4][j / 4] = num3 * num4;
		}
		double[][] array2 = array;
		foreach (double[] array3 in array2)
		{
			double num5 = array3.Sum();
			for (int l = 0; l < 12; l++)
			{
				array3[l] /= num5;
			}
		}
		return array;
	}

	private static double? ComputeIntegratedLoudness(IReadOnlyList<double> subBlockEnergies)
	{
		if (subBlockEnergies.Count < 4)
		{
			return null;
		}
		List<double> list = new List<double>(subBlockEnergies.Count - 3);
		for (int i = 0; i + 4 <= subBlockEnergies.Count; i++)
		{
			list.Add((subBlockEnergies[i] + subBlockEnergies[i + 1] + subBlockEnergies[i + 2] + subBlockEnergies[i + 3]) / 4.0);
		}
		List<double> list2 = list.Where((double energy) => ToLoudness(energy) > -70.0).ToList();
		if (list2.Count == 0)
		{
			return null;
		}
		double relativeThreshold = ToLoudness(list2.Average()) - 10.0;
		List<double> list3 = list2.Where((double energy) => ToLoudness(energy) > relativeThreshold).ToList();
		if (list3.Count != 0)
		{
			return ToLoudness(list3.Average());
		}
		return null;
	}

	private static double ToLoudness(double meanSquare)
	{
		if (!(meanSquare <= 0.0))
		{
			return -0.691 + 10.0 * Math.Log10(meanSquare);
		}
		return double.NegativeInfinity;
	}

	private static double ReadSample(byte[] buffer, int offset, ushort formatTag, ushort bitsPerSample)
	{
		if (formatTag == 3)
		{
			float num = BitConverter.ToSingle(buffer, offset);
			return double.IsFinite(num) ? Math.Clamp(num, -1f, 1f) : 0f;
		}
		return bitsPerSample switch
		{
			16 => (double)BitConverter.ToInt16(buffer, offset) / 32768.0, 
			24 => (double)(buffer[offset] | (buffer[offset + 1] << 8) | ((sbyte)buffer[offset + 2] << 16)) / 8388608.0, 
			32 => (double)BitConverter.ToInt32(buffer, offset) / 2147483648.0, 
			_ => 0.0, 
		};
	}

	private static void SkipPadding(Stream stream, uint chunkSize, int alreadyRead)
	{
		long num = chunkSize - alreadyRead + chunkSize % 2;
		if (num > 0)
		{
			stream.Seek(num, SeekOrigin.Current);
		}
	}
}
