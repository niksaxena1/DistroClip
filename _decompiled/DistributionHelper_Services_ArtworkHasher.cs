using System;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DistributionHelper.Services;

public static class ArtworkHasher
{
	private const int DecodeSize = 32;

	private const int HashBlock = 8;

	private static readonly double[,] Cosines = BuildCosineTable();

	public static ulong? ComputeHash(string path, CancellationToken cancellationToken = default(CancellationToken))
	{
		try
		{
			using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.SequentialScan);
			return ComputeHash(stream, cancellationToken);
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

	public static ulong? ComputeHash(Stream stream, CancellationToken cancellationToken = default(CancellationToken))
	{
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			BitmapImage bitmapImage = new BitmapImage();
			bitmapImage.BeginInit();
			bitmapImage.StreamSource = stream;
			bitmapImage.DecodePixelWidth = 32;
			bitmapImage.DecodePixelHeight = 32;
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
			bitmapImage.EndInit();
			FormatConvertedBitmap formatConvertedBitmap = new FormatConvertedBitmap(bitmapImage, PixelFormats.Gray8, null, 0.0);
			byte[] array = new byte[1024];
			formatConvertedBitmap.CopyPixels(array, 32, 0);
			cancellationToken.ThrowIfCancellationRequested();
			double[,] array2 = new double[32, 32];
			for (int i = 0; i < 32; i++)
			{
				for (int j = 0; j < 32; j++)
				{
					array2[i, j] = (int)array[i * 32 + j];
				}
			}
			double[,] array3 = Dct2D(array2);
			Span<double> span = stackalloc double[63];
			int num = 0;
			for (int k = 0; k < 8; k++)
			{
				for (int l = 0; l < 8; l++)
				{
					if (l != 0 || k != 0)
					{
						span[num++] = array3[k, l];
					}
				}
			}
			Span<double> span2 = stackalloc double[span.Length];
			span.CopyTo(span2);
			span2.Sort();
			double num2 = span2[span2.Length / 2];
			ulong num3 = 0uL;
			for (int m = 0; m < span.Length; m++)
			{
				if (span[m] > num2)
				{
					num3 |= (ulong)(1L << m);
				}
			}
			return num3;
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

	public static int HammingDistance(ulong a, ulong b)
	{
		return BitOperations.PopCount(a ^ b);
	}

	private static double[,] Dct2D(double[,] input)
	{
		double[,] array = new double[32, 32];
		for (int i = 0; i < 32; i++)
		{
			for (int j = 0; j < 32; j++)
			{
				double num = 0.0;
				for (int k = 0; k < 32; k++)
				{
					num += input[i, k] * Cosines[k, j];
				}
				array[i, j] = num;
			}
		}
		double[,] array2 = new double[32, 32];
		for (int l = 0; l < 32; l++)
		{
			for (int m = 0; m < 32; m++)
			{
				double num2 = 0.0;
				for (int n = 0; n < 32; n++)
				{
					num2 += array[n, l] * Cosines[n, m];
				}
				array2[m, l] = num2;
			}
		}
		return array2;
	}

	private static double[,] BuildCosineTable()
	{
		double[,] array = new double[32, 32];
		for (int i = 0; i < 32; i++)
		{
			for (int j = 0; j < 32; j++)
			{
				array[i, j] = Math.Cos((double)((2 * i + 1) * j) * Math.PI / 64.0);
			}
		}
		return array;
	}
}
