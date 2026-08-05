using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace DistributionHelper.Services;

public sealed class LiquidLensEffect : ShaderEffect
{
	[ComImport]
	[Guid("8BA5FB08-5195-40e2-AC58-0D989C3A0102")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface ID3DBlob
	{
		[PreserveSig]
		nint GetBufferPointer();

		[PreserveSig]
		nint GetBufferSize();
	}

	private const string ShaderSource = "sampler2D input : register(s0);\nfloat2 size : register(c0);\nfloat2 geom : register(c1);\nfloat2 inset : register(c2);\nfloat2 slosh : register(c3);\n\nfloat4 main(float2 uv : TEXCOORD) : COLOR\n{\n    float2 halfSize = size * 0.5;\n    float2 p = uv * size - halfSize;\n    float2 q = abs(p) - (halfSize - geom.x);\n    float dist = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - geom.x;\n    float edge = saturate(1.0 + dist / max(geom.y, 1.0));\n    float rim = edge * edge * (3.0 - 2.0 * edge);\n\n    float2 dir = p / max(halfSize, 1.0);\n    float len = max(length(dir), 0.001);\n    dir /= len;\n\n    float2 base = inset + uv * (1.0 - 2.0 * inset) + slosh;\n\n    float2 warped = base - (base - 0.5) * 0.014 * (1.0 - rim);\n    warped += dir * inset * (rim * rim);\n\n    float2 ca = dir * inset * rim * 0.24;\n    float4 core = tex2D(input, clamp(warped, 0.002, 0.998));\n    float red = tex2D(input, clamp(warped + ca, 0.002, 0.998)).r;\n    float blue = tex2D(input, clamp(warped - ca, 0.002, 0.998)).b;\n    float3 colour = float3(red, core.g, blue);\n\n    float luma = dot(colour, float3(0.299, 0.587, 0.114));\n    colour = luma + (colour - luma) * (1.22 + 0.16 * rim);\n\n    float sheen = (1.0 - smoothstep(0.0, 0.5, uv.y)) * 0.03;\n    colour += (rim * rim * 0.085 + sheen) * core.a;\n    return float4(colour, core.a);\n}";

	public static readonly DependencyProperty InputProperty = ShaderEffect.RegisterPixelShaderSamplerProperty("Input", typeof(LiquidLensEffect), 0);

	public static readonly DependencyProperty SizePxProperty = DependencyProperty.Register("SizePx", typeof(Point), typeof(LiquidLensEffect), (PropertyMetadata)new UIPropertyMetadata((object)new Point(300.0, 90.0), ShaderEffect.PixelShaderConstantCallback(0)));

	public static readonly DependencyProperty GeometryProperty = DependencyProperty.Register("Geometry", typeof(Point), typeof(LiquidLensEffect), (PropertyMetadata)new UIPropertyMetadata((object)new Point(24.0, 15.0), ShaderEffect.PixelShaderConstantCallback(1)));

	public static readonly DependencyProperty InsetProperty = DependencyProperty.Register("Inset", typeof(Point), typeof(LiquidLensEffect), (PropertyMetadata)new UIPropertyMetadata((object)new Point(0.07, 0.18), ShaderEffect.PixelShaderConstantCallback(2)));

	public static readonly DependencyProperty SloshProperty = DependencyProperty.Register("Slosh", typeof(Point), typeof(LiquidLensEffect), (PropertyMetadata)new UIPropertyMetadata((object)new Point(0.0, 0.0), ShaderEffect.PixelShaderConstantCallback(3)));

	public Brush Input
	{
		get
		{
			return (Brush)((DependencyObject)this).GetValue(InputProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(InputProperty, (object)value);
		}
	}

	public Point SizePx
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (Point)((DependencyObject)this).GetValue(SizePxProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((DependencyObject)this).SetValue(SizePxProperty, (object)value);
		}
	}

	public Point Geometry
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (Point)((DependencyObject)this).GetValue(GeometryProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((DependencyObject)this).SetValue(GeometryProperty, (object)value);
		}
	}

	public Point Inset
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (Point)((DependencyObject)this).GetValue(InsetProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((DependencyObject)this).SetValue(InsetProperty, (object)value);
		}
	}

	public Point Slosh
	{
		get
		{
			//IL_000b: Unknown result type (might be due to invalid IL or missing references)
			return (Point)((DependencyObject)this).GetValue(SloshProperty);
		}
		set
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			((DependencyObject)this).SetValue(SloshProperty, (object)value);
		}
	}

	private LiquidLensEffect(PixelShader shader)
	{
		base.PixelShader = shader;
		UpdateShaderValue(InputProperty);
		UpdateShaderValue(SizePxProperty);
		UpdateShaderValue(GeometryProperty);
		UpdateShaderValue(InsetProperty);
		UpdateShaderValue(SloshProperty);
	}

	public static LiquidLensEffect? TryCreate()
	{
		try
		{
			byte[] array = CompileShader("sampler2D input : register(s0);\nfloat2 size : register(c0);\nfloat2 geom : register(c1);\nfloat2 inset : register(c2);\nfloat2 slosh : register(c3);\n\nfloat4 main(float2 uv : TEXCOORD) : COLOR\n{\n    float2 halfSize = size * 0.5;\n    float2 p = uv * size - halfSize;\n    float2 q = abs(p) - (halfSize - geom.x);\n    float dist = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - geom.x;\n    float edge = saturate(1.0 + dist / max(geom.y, 1.0));\n    float rim = edge * edge * (3.0 - 2.0 * edge);\n\n    float2 dir = p / max(halfSize, 1.0);\n    float len = max(length(dir), 0.001);\n    dir /= len;\n\n    float2 base = inset + uv * (1.0 - 2.0 * inset) + slosh;\n\n    float2 warped = base - (base - 0.5) * 0.014 * (1.0 - rim);\n    warped += dir * inset * (rim * rim);\n\n    float2 ca = dir * inset * rim * 0.24;\n    float4 core = tex2D(input, clamp(warped, 0.002, 0.998));\n    float red = tex2D(input, clamp(warped + ca, 0.002, 0.998)).r;\n    float blue = tex2D(input, clamp(warped - ca, 0.002, 0.998)).b;\n    float3 colour = float3(red, core.g, blue);\n\n    float luma = dot(colour, float3(0.299, 0.587, 0.114));\n    colour = luma + (colour - luma) * (1.22 + 0.16 * rim);\n\n    float sheen = (1.0 - smoothstep(0.0, 0.5, uv.y)) * 0.03;\n    colour += (rim * rim * 0.085 + sheen) * core.a;\n    return float4(colour, core.a);\n}", "main", "ps_3_0");
			if (array == null)
			{
				return null;
			}
			PixelShader pixelShader = new PixelShader();
			pixelShader.SetStreamSource(new MemoryStream(array));
			return new LiquidLensEffect(pixelShader);
		}
		catch (Exception exception)
		{
			ErrorLog.Write(exception);
			return null;
		}
	}

	[DllImport("d3dcompiler_47.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
	private static extern int D3DCompile([MarshalAs(UnmanagedType.LPStr)] string srcData, nint srcDataSize, [MarshalAs(UnmanagedType.LPStr)] string? sourceName, nint defines, nint include, [MarshalAs(UnmanagedType.LPStr)] string entryPoint, [MarshalAs(UnmanagedType.LPStr)] string target, uint flags1, uint flags2, out ID3DBlob? code, out ID3DBlob? errors);

	internal static byte[]? CompileForDiagnostics()
	{
		return CompileShader("sampler2D input : register(s0);\nfloat2 size : register(c0);\nfloat2 geom : register(c1);\nfloat2 inset : register(c2);\nfloat2 slosh : register(c3);\n\nfloat4 main(float2 uv : TEXCOORD) : COLOR\n{\n    float2 halfSize = size * 0.5;\n    float2 p = uv * size - halfSize;\n    float2 q = abs(p) - (halfSize - geom.x);\n    float dist = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - geom.x;\n    float edge = saturate(1.0 + dist / max(geom.y, 1.0));\n    float rim = edge * edge * (3.0 - 2.0 * edge);\n\n    float2 dir = p / max(halfSize, 1.0);\n    float len = max(length(dir), 0.001);\n    dir /= len;\n\n    float2 base = inset + uv * (1.0 - 2.0 * inset) + slosh;\n\n    float2 warped = base - (base - 0.5) * 0.014 * (1.0 - rim);\n    warped += dir * inset * (rim * rim);\n\n    float2 ca = dir * inset * rim * 0.24;\n    float4 core = tex2D(input, clamp(warped, 0.002, 0.998));\n    float red = tex2D(input, clamp(warped + ca, 0.002, 0.998)).r;\n    float blue = tex2D(input, clamp(warped - ca, 0.002, 0.998)).b;\n    float3 colour = float3(red, core.g, blue);\n\n    float luma = dot(colour, float3(0.299, 0.587, 0.114));\n    colour = luma + (colour - luma) * (1.22 + 0.16 * rim);\n\n    float sheen = (1.0 - smoothstep(0.0, 0.5, uv.y)) * 0.03;\n    colour += (rim * rim * 0.085 + sheen) * core.a;\n    return float4(colour, core.a);\n}", "main", "ps_3_0");
	}

	private static byte[]? CompileShader(string source, string entryPoint, string target)
	{
		if (D3DCompile(source, source.Length, null, IntPtr.Zero, IntPtr.Zero, entryPoint, target, 0u, 0u, out ID3DBlob code, out ID3DBlob errors) != 0 || code == null)
		{
			string text = "Shader compilation failed.";
			if (errors != null)
			{
				text = Marshal.PtrToStringAnsi(errors.GetBufferPointer(), (int)errors.GetBufferSize());
			}
			ErrorLog.Write(new InvalidOperationException("LiquidLens: " + text));
			return null;
		}
		byte[] array = new byte[code.GetBufferSize()];
		Marshal.Copy(code.GetBufferPointer(), array, 0, array.Length);
		return array;
	}
}
