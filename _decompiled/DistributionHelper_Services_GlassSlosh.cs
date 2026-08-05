using System;
using System.Windows;
using System.Windows.Media;

namespace DistributionHelper.Services;

public sealed class GlassSlosh
{
	private const double MaxOffset = 16.0;

	private const double Stiffness = 170.0;

	private const double Damping = 13.0;

	private readonly Window _window;

	private Point _lastLocation;

	private bool _running;

	private bool _frameHooked;

	private double _x;

	private double _y;

	private double _velocityX;

	private double _velocityY;

	private DateTime _lastFrame;

	private Size _inflatedSize = new Size(360.0, 140.0);

	public LiquidLensEffect? Target { get; set; }

	public GlassSlosh(Window window)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		_window = window;
	}

	public void Configure(Size inflatedIslandSize)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		_inflatedSize = inflatedIslandSize;
	}

	public void Start()
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		if (!_running)
		{
			_running = true;
			_lastLocation = new Point(_window.Left, _window.Top);
			_window.LocationChanged += Window_LocationChanged;
		}
	}

	public void Stop()
	{
		if (_running)
		{
			_running = false;
			_window.LocationChanged -= Window_LocationChanged;
			_x = (_y = (_velocityX = (_velocityY = 0.0)));
			Apply();
			Unhook();
		}
	}

	public void NudgeScroll(double verticalChange)
	{
		Impulse(0.0, (0.0 - verticalChange) * 0.35);
	}

	private void Window_LocationChanged(object? sender, EventArgs e)
	{
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		Point lastLocation = default(Point);
		((Point)(ref lastLocation))..ctor(_window.Left, _window.Top);
		double num = ((Point)(ref lastLocation)).X - ((Point)(ref _lastLocation)).X;
		double num2 = ((Point)(ref lastLocation)).Y - ((Point)(ref _lastLocation)).Y;
		_lastLocation = lastLocation;
		if (!(Math.Abs(num) > 200.0) && !(Math.Abs(num2) > 200.0))
		{
			Impulse(num * 0.5, num2 * 0.5);
		}
	}

	private void Impulse(double deltaX, double deltaY)
	{
		if (_running && Target != null)
		{
			_x = Math.Clamp(_x + deltaX, -16.0, 16.0);
			_y = Math.Clamp(_y + deltaY, -16.0, 16.0);
			Hook();
		}
	}

	private void Hook()
	{
		if (!_frameHooked)
		{
			_frameHooked = true;
			_lastFrame = DateTime.UtcNow;
			CompositionTarget.Rendering += OnFrame;
		}
	}

	private void Unhook()
	{
		if (_frameHooked)
		{
			_frameHooked = false;
			CompositionTarget.Rendering -= OnFrame;
		}
	}

	private void OnFrame(object? sender, EventArgs e)
	{
		DateTime utcNow = DateTime.UtcNow;
		double num = Math.Min(0.033, (utcNow - _lastFrame).TotalSeconds);
		_lastFrame = utcNow;
		_velocityX += (-170.0 * _x - 13.0 * _velocityX) * num;
		_velocityY += (-170.0 * _y - 13.0 * _velocityY) * num;
		_x += _velocityX * num;
		_y += _velocityY * num;
		if (Math.Abs(_x) < 0.05 && Math.Abs(_y) < 0.05 && Math.Abs(_velocityX) < 0.5 && Math.Abs(_velocityY) < 0.5)
		{
			_x = (_y = (_velocityX = (_velocityY = 0.0)));
			Apply();
			Unhook();
		}
		else
		{
			Apply();
		}
	}

	private void Apply()
	{
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		LiquidLensEffect target = Target;
		if (target != null)
		{
			((DependencyObject)target).SetCurrentValue(LiquidLensEffect.SloshProperty, (object)new Point(_x / Math.Max(60.0, ((Size)(ref _inflatedSize)).Width), _y / Math.Max(60.0, ((Size)(ref _inflatedSize)).Height)));
		}
	}
}
