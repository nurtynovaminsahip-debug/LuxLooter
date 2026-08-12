using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace LootCollector.UI;

public static class SmoothScroll
{
	private static readonly DependencyProperty OffsetProperty = DependencyProperty.RegisterAttached("Offset", typeof(double), typeof(SmoothScroll), new PropertyMetadata(0.0, delegate(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is ScrollViewer scrollViewer)
		{
			scrollViewer.ScrollToVerticalOffset((double)e.NewValue);
		}
	}));

	private static readonly Dictionary<ScrollViewer, double> Target = new Dictionary<ScrollViewer, double>();

	public static void Enable(ScrollViewer sv)
	{
		sv.PreviewMouseWheel += delegate(object _, MouseWheelEventArgs e)
		{
			e.Handled = true;
			double verticalOffset = sv.VerticalOffset;
			double value;
			double num = (Target.TryGetValue(sv, out value) ? value : verticalOffset);
			if (Math.Abs(num - verticalOffset) > sv.ViewportHeight)
			{
				num = verticalOffset;
			}
			double num2 = Math.Max(0.0, Math.Min(sv.ScrollableHeight, num - (double)e.Delta));
			Target[sv] = num2;
			DoubleAnimation animation = new DoubleAnimation(verticalOffset, num2, TimeSpan.FromMilliseconds(260.0))
			{
				EasingFunction = new CubicEase
				{
					EasingMode = EasingMode.EaseOut
				}
			};
			sv.BeginAnimation(OffsetProperty, animation);
		};
	}
}
