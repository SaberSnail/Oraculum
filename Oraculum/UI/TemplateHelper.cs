using System.Windows;
using System.Windows.Media;

namespace Oraculum.UI
{
	public static class TemplateHelper
	{
		public static readonly DependencyProperty CornerRadiusProperty =
			DependencyProperty.RegisterAttached("CornerRadius", typeof(CornerRadius), typeof(TemplateHelper));

		public static CornerRadius GetCornerRadius(DependencyObject d) =>
			(CornerRadius) d.GetValue(CornerRadiusProperty);

		public static void SetCornerRadius(DependencyObject d, CornerRadius value) =>
			d.SetValue(CornerRadiusProperty, value);

		public static readonly DependencyProperty BorderThicknessProperty =
			DependencyProperty.RegisterAttached("BorderThickness", typeof(Thickness), typeof(TemplateHelper));

		public static Thickness GetBorderThickness(DependencyObject d) =>
			(Thickness) d.GetValue(BorderThicknessProperty);

		public static void SetBorderThickness(DependencyObject d, Thickness value) =>
			d.SetValue(BorderThicknessProperty, value);

		public static readonly DependencyProperty BorderBrushProperty =
			DependencyProperty.RegisterAttached("BorderBrush", typeof(Brush), typeof(TemplateHelper));

		public static Brush GetBorderBrush(DependencyObject d) =>
			(Brush) d.GetValue(BorderBrushProperty);

		public static void SetBorderBrush(DependencyObject d, Brush value) =>
			d.SetValue(BorderBrushProperty, value);
	}
}
