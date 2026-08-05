using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DistributionHelper.Models;

namespace DistributionHelper.Services;

public static class GelInteraction
{
	private static readonly DependencyProperty GelTransformProperty = DependencyProperty.RegisterAttached("GelTransform", typeof(ScaleTransform), typeof(GelInteraction));

	public static void Attach()
	{
		EventManager.RegisterClassHandler(typeof(ButtonBase), UIElement.PreviewMouseLeftButtonDownEvent, (MouseButtonEventHandler)delegate(object sender, MouseButtonEventArgs _)
		{
			Press(sender as FrameworkElement);
		}, handledEventsToo: true);
		EventManager.RegisterClassHandler(typeof(ButtonBase), UIElement.PreviewMouseLeftButtonUpEvent, (MouseButtonEventHandler)delegate(object sender, MouseButtonEventArgs _)
		{
			Release(sender as FrameworkElement);
		}, handledEventsToo: true);
		EventManager.RegisterClassHandler(typeof(ButtonBase), UIElement.MouseEnterEvent, (MouseEventHandler)delegate(object sender, MouseEventArgs _)
		{
			Hover(sender as FrameworkElement);
		}, handledEventsToo: true);
		EventManager.RegisterClassHandler(typeof(ButtonBase), UIElement.MouseLeaveEvent, (MouseEventHandler)delegate(object sender, MouseEventArgs _)
		{
			Release(sender as FrameworkElement);
		}, handledEventsToo: true);
	}

	public static void AttachTo(FrameworkElement element)
	{
		element.PreviewMouseLeftButtonDown += delegate
		{
			Press(element);
		};
		element.PreviewMouseLeftButtonUp += delegate
		{
			Release(element);
		};
		element.MouseEnter += delegate
		{
			Hover(element);
		};
		element.MouseLeave += delegate
		{
			Release(element);
		};
		element.LostMouseCapture += delegate
		{
			Release(element);
		};
	}

	public static void Hover(FrameworkElement? element)
	{
		if (element != null && ThemeManager.Current == AppTheme.LiquidGlass && Mouse.LeftButton != MouseButtonState.Pressed)
		{
			ScaleTransform scaleTransform = EnsureTransform(element);
			if (scaleTransform != null)
			{
				DoubleAnimation animation = new DoubleAnimation(1.015, TimeSpan.FromMilliseconds(150.0))
				{
					EasingFunction = new SineEase
					{
						EasingMode = EasingMode.EaseOut
					}
				};
				scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
				scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
			}
		}
	}

	public static void Press(FrameworkElement? element)
	{
		if (element != null && ThemeManager.Current == AppTheme.LiquidGlass)
		{
			ScaleTransform scaleTransform = EnsureTransform(element);
			if (scaleTransform != null)
			{
				DoubleAnimation animation = new DoubleAnimation(0.962, TimeSpan.FromMilliseconds(90.0))
				{
					EasingFunction = new CubicEase
					{
						EasingMode = EasingMode.EaseOut
					}
				};
				DoubleAnimation animation2 = new DoubleAnimation(0.94, TimeSpan.FromMilliseconds(90.0))
				{
					EasingFunction = new CubicEase
					{
						EasingMode = EasingMode.EaseOut
					}
				};
				scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
				scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation2);
			}
		}
	}

	public static void Release(FrameworkElement? element)
	{
		if (((element != null) ? ((DependencyObject)element).GetValue(GelTransformProperty) : null) is ScaleTransform scaleTransform)
		{
			scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, CreateSpringBack());
			scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, CreateSpringBack());
		}
	}

	private static ScaleTransform? EnsureTransform(FrameworkElement element)
	{
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		if (((DependencyObject)element).GetValue(GelTransformProperty) is ScaleTransform result)
		{
			return result;
		}
		if (element.RenderTransform != null && element.RenderTransform != Transform.Identity && (!(element.RenderTransform is MatrixTransform { Matrix: var matrix }) || !((Matrix)(ref matrix)).IsIdentity))
		{
			return null;
		}
		Transform transform = (element.RenderTransform = new ScaleTransform(1.0, 1.0));
		ScaleTransform scaleTransform = (ScaleTransform)transform;
		element.RenderTransformOrigin = new Point(0.5, 0.5);
		((DependencyObject)element).SetValue(GelTransformProperty, (object)scaleTransform);
		return scaleTransform;
	}

	private static DoubleAnimationUsingKeyFrames CreateSpringBack()
	{
		return new DoubleAnimationUsingKeyFrames
		{
			KeyFrames = 
			{
				(DoubleKeyFrame)new EasingDoubleKeyFrame(1.028, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(130.0)))
				{
					EasingFunction = new SineEase
					{
						EasingMode = EasingMode.EaseOut
					}
				},
				(DoubleKeyFrame)new EasingDoubleKeyFrame(0.993, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(240.0))),
				(DoubleKeyFrame)new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(330.0)))
				{
					EasingFunction = new SineEase
					{
						EasingMode = EasingMode.EaseOut
					}
				}
			}
		};
	}
}
