using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Logging;
using GoldenAnvil.Utility.Windows;

namespace Oraculum.UI
{
	public class DiceFlipPanel : Grid
	{
		public DiceFlipPanel()
		{
			m_placeholder = CreateFace(-1);
			var valueBinding = new Binding(nameof(MaximumValue))
			{
				Source = this,
				Converter = new ValueToPlaceholderConverter(),
			};
			m_placeholder.Text.SetBinding(TextBlock.TextProperty, valueBinding);
			m_placeholder.Border.Visibility = Visibility.Hidden;

			var maxSizeBinding = new MultiBinding { Converter = CommonConverters.Max };
			maxSizeBinding.Bindings.Add(new Binding(ActualWidthProperty.Name) { Source = m_placeholder.Border });
			maxSizeBinding.Bindings.Add(new Binding(ActualHeightProperty.Name) { Source = m_placeholder.Border });
			SetBinding(MinWidthProperty, maxSizeBinding);
			SetBinding(MinHeightProperty, maxSizeBinding);

			m_faces = Enumerable.Range(0, 7)
				.Select(i => CreateFace(i))
				.AsReadOnlyList();

			m_activeFace = 0;
		}

		public static readonly DependencyProperty ShouldAnimateProperty = DependencyPropertyUtility<DiceFlipPanel>.Register(x => x.ShouldAnimate);

		public bool ShouldAnimate
		{
			get => (bool) GetValue(ShouldAnimateProperty);
			set => SetValue(ShouldAnimateProperty, value);
		}

		public static readonly DependencyProperty TargetValueProperty = DependencyPropertyUtility<DiceFlipPanel>.Register(x => x.TargetValue, new PropertyChangedCallback(OnTargetValueChanged), 0);

		public int? TargetValue
		{
			get => (int?) GetValue(TargetValueProperty);
			set => SetValue(TargetValueProperty, value);
		}

		public static readonly DependencyProperty MinimumValueProperty = DependencyPropertyUtility<DiceFlipPanel>.Register(x => x.MinimumValue);

		public int? MinimumValue
		{
			get => (int?) GetValue(MinimumValueProperty);
			set => SetValue(MinimumValueProperty, value);
		}

		public static readonly DependencyProperty MaximumValueProperty = DependencyPropertyUtility<DiceFlipPanel>.Register(x => x.MaximumValue);

		public int? MaximumValue
		{
			get => (int?) GetValue(MaximumValueProperty);
			set => SetValue(MaximumValueProperty, value);
		}

		public static readonly DependencyProperty AnimationFinishedCommandProperty = DependencyPropertyUtility<DiceFlipPanel>.Register(x => x.AnimationFinishedCommand);

		public ICommand? AnimationFinishedCommand
		{
			get => (ICommand?) GetValue(AnimationFinishedCommandProperty);
			set => SetValue(AnimationFinishedCommandProperty, value);
		}

		public static readonly DependencyProperty AnimationFinishedCommandParameterProperty = DependencyPropertyUtility<DiceFlipPanel>.Register(x => x.AnimationFinishedCommandParameter);

		public object? AnimationFinishedCommandParameter
		{
			get => GetValue(AnimationFinishedCommandParameterProperty);
			set => SetValue(AnimationFinishedCommandParameterProperty, value);
		}

		private Face ActiveFace => m_faces[m_activeFace];

		private Face InactiveFace => m_faces[m_activeFace - 1];

		private void IncrementActive() => m_activeFace++;

		private void ResetActive() => m_activeFace = 0;

		private static void OnTargetValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var panel = (DiceFlipPanel) d;

			if (panel.m_lastStoryboard is not null)
			{
				panel.m_lastStoryboard.Completed -= panel.OnStoryboardCompleted;
				panel.m_lastStoryboard.Stop();
			}
			panel.ResetActive();
			panel.m_faces[0].Border.RenderTransform = new ScaleTransform(1.0, 1.0);
			for (int i = 1; i < panel.m_faces.Count; i++)
			{
				var face = panel.m_faces[i];
				face.Direction = GetRandomDirection();
				face.Border.RenderTransform = panel.GetInitialScaleTransform(face);
			}

			if (!panel.ShouldAnimate || panel.MinimumValue is null || panel.MaximumValue is null || panel.TargetValue is null)
			{
				panel.m_faces[0].SetValue(panel.TargetValue ?? 0);
				panel.m_faces[^1].SetValue(panel.TargetValue ?? 0);
				panel.AnimationFinishedCommand?.Execute(panel.AnimationFinishedCommandParameter);
				return;
			}

			var targetValue = panel.TargetValue.Value;
			var minimumValue = panel.MinimumValue.Value;
			var maximumValue = panel.MaximumValue.Value;

			panel.m_faces[0].Text.Text = panel.m_faces[^1].Text.Text;
			for (int i = 1; i < panel.m_faces.Count; i++)
			{
				var face = panel.m_faces[i];
				face.SetValue(AppModel.Instance.Random.Next(minimumValue, maximumValue));
			}
			panel.m_faces[^1].SetValue(targetValue);

			var storyboard = new Storyboard { BeginTime = TimeSpan.Zero };
			var timeOffset = TimeSpan.Zero;
			for (int i = 1; i < panel.m_faces.Count; i++)
			{
				var duration = c_rollTime * (1.0 + (c_slowDownFactor * i) / (panel.m_faces.Count - 1.0));

				panel.IncrementActive();
				var activeFace = panel.ActiveFace;
				var inactiveFace = panel.InactiveFace;

				storyboard.Children.Add(CreateFaceScaleAnimation(activeFace.Border, activeFace.Direction, RollType.In, timeOffset, duration));
				storyboard.Children.Add(panel.CreateFaceTransformCenterXAnimation(activeFace.Border, activeFace.Direction, RollType.In, timeOffset, duration));
				storyboard.Children.Add(panel.CreateFaceTransformCenterYAnimation(activeFace.Border, activeFace.Direction, RollType.In, timeOffset, duration));

				storyboard.Children.Add(CreateFaceScaleAnimation(inactiveFace.Border, activeFace.Direction, RollType.Out, timeOffset, duration));
				storyboard.Children.Add(panel.CreateFaceTransformCenterXAnimation(inactiveFace.Border, activeFace.Direction, RollType.Out, timeOffset, duration));
				storyboard.Children.Add(panel.CreateFaceTransformCenterYAnimation(inactiveFace.Border, activeFace.Direction, RollType.Out, timeOffset, duration));

				timeOffset += duration;
			}

			panel.m_lastStoryboard = storyboard;
			panel.m_lastStoryboard.Completed += panel.OnStoryboardCompleted;
			storyboard.Begin(panel);
		}

		private void OnStoryboardCompleted(object? sender, EventArgs e)
		{
			AnimationFinishedCommand?.Execute(AnimationFinishedCommandParameter);
		}

		private static Timeline CreateFaceScaleAnimation(Border target, RollDirection direction, RollType type, TimeSpan timeOffset, TimeSpan duration)
		{
			var property = direction is RollDirection.Left or RollDirection.Right ? ScaleTransform.ScaleXProperty : ScaleTransform.ScaleYProperty;
			var startValue = type is RollType.In ? 0.0 : 1.0;
			var endValue = type is RollType.In ? 1.0 : 0.0;

			var animation = new DoubleAnimation
			{
				BeginTime = timeOffset,
				Duration = duration,
				From = startValue,
				To = endValue,
				EasingFunction = new CircleEase { EasingMode = EasingMode.EaseIn },
			};

			Storyboard.SetTarget(animation, target);
			Storyboard.SetTargetProperty(animation, new PropertyPath("(0).(1)", UIElement.RenderTransformProperty, property));

			return animation;
		}

		private Timeline CreateFaceTransformCenterXAnimation(Border target, RollDirection direction, RollType type, TimeSpan timeOffset, TimeSpan duration)
		{
			double centerX;
			if (type is RollType.In)
			{
				if (direction is RollDirection.Up)
					centerX = ActualWidth / 2.0;
				else if (direction is RollDirection.Down)
					centerX = ActualWidth / 2.0;
				else if (direction is RollDirection.Left)
					centerX = ActualWidth;
				else if (direction is RollDirection.Right)
					centerX = 0.0;
				else
					throw new NotImplementedException();
			}
			else if (type is RollType.Out)
			{
				if (direction is RollDirection.Up)
					centerX = ActualWidth / 2.0;
				else if (direction is RollDirection.Down)
					centerX = ActualWidth / 2.0;
				else if (direction is RollDirection.Left)
					centerX = 0.0;
				else if (direction is RollDirection.Right)
					centerX = ActualWidth;
				else
					throw new NotImplementedException();
			}
			else
			{
				throw new NotImplementedException();
			}

			var animation = new DoubleAnimation
			{
				BeginTime = timeOffset,
				Duration = duration,
				From = centerX,
				To = centerX,
			};

			Storyboard.SetTarget(animation, target);
			Storyboard.SetTargetProperty(animation, new PropertyPath("(0).(1)", UIElement.RenderTransformProperty, ScaleTransform.CenterXProperty));
			return animation;
		}

		private Timeline CreateFaceTransformCenterYAnimation(Border target, RollDirection direction, RollType type, TimeSpan timeOffset, TimeSpan duration)
		{
			double centerY;
			if (type is RollType.In)
			{
				if (direction is RollDirection.Up)
					centerY = ActualHeight;
				else if (direction is RollDirection.Down)
					centerY = 0.0;
				else if (direction is RollDirection.Left)
					centerY = ActualHeight / 2.0;
				else if (direction is RollDirection.Right)
					centerY = ActualHeight / 2.0;
				else
					throw new NotImplementedException();
			}
			else if (type is RollType.Out)
			{
				if (direction is RollDirection.Up)
					centerY = 0.0;
				else if (direction is RollDirection.Down)
					centerY = ActualHeight;
				else if (direction is RollDirection.Left)
					centerY = ActualHeight / 2.0;
				else if (direction is RollDirection.Right)
					centerY = ActualHeight / 2.0;
				else
					throw new NotImplementedException();
			}
			else
			{
				throw new NotImplementedException();
			}

			var animation = new DoubleAnimation
			{
				BeginTime = timeOffset,
				Duration = duration,
				From = centerY,
				To = centerY,
			};

			Storyboard.SetTarget(animation, target);
			Storyboard.SetTargetProperty(animation, new PropertyPath("(0).(1)", UIElement.RenderTransformProperty, ScaleTransform.CenterYProperty));
			return animation;
		}

		private static RollDirection GetRandomDirection()
		{
			return AppModel.Instance.Random.NextRoll(1, 4) switch
			{
				1 => RollDirection.Up,
				2 => RollDirection.Down,
				3 => RollDirection.Left,
				4 => RollDirection.Right,
				_ => throw new NotImplementedException(),
			};
		}

		private ScaleTransform GetInitialScaleTransform(Face face)
		{
			if (face.Direction is RollDirection.Up or RollDirection.Down)
			{
				var centerY = face.Direction is RollDirection.Up ? ActualHeight : 0.0;
				return new ScaleTransform(1.0, 0.0, ActualWidth / 2.0, centerY);
			}
			var centerX = face.Direction is RollDirection.Left ? ActualWidth : 0.0;
			return new ScaleTransform(0.0, 1.0, centerX, ActualHeight / 2.0);
		}

		private Face CreateFace(int index)
		{
			var border = new Border();
			if (index < 0)
			{
				border.HorizontalAlignment = HorizontalAlignment.Center;
				border.VerticalAlignment = VerticalAlignment.Center;
			}
			else
			{
				border.HorizontalAlignment = HorizontalAlignment.Stretch;
				border.VerticalAlignment = VerticalAlignment.Stretch;
			}
			border.SetBinding(
				Border.BorderThicknessProperty,
				new Binding { Source = this, Path = new PropertyPath(TemplateHelper.BorderThicknessProperty) });
			border.SetBinding(
				Border.BorderBrushProperty,
				new Binding { Source = this, Path = new PropertyPath(TemplateHelper.BorderBrushProperty) });
			border.Background = new SolidColorBrush(Color.FromRgb(48, 3, 0));

			var content = new TextBlock();
			content.HorizontalAlignment = HorizontalAlignment.Center;
			content.VerticalAlignment = VerticalAlignment.Center;
			content.Foreground = new SolidColorBrush(Color.FromRgb(255, 233, 232));
			content.FontWeight = FontWeights.Bold;
			border.Child = content;

			var face = new Face(border, content, index);
			Children.Add(face.Border);

			return face;
		}

		private sealed class ValueToPlaceholderConverter : IValueConverter
		{
			public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
			{
				var places = (int?) value > 0 ? (int) Math.Ceiling(Math.Log10(((int?) value).Value + 1)) : 1;
				return new string('0', places);
			}

			public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			{
				throw new NotImplementedException();
			}
		}

		private sealed class Face
		{
			public Face(Border border, TextBlock text, int index)
			{
				Border = border;
				Text = text;
				Index = index;
			}

			public Border Border { get; }
			public TextBlock Text { get; }

			public int Index { get; }

			public RollDirection Direction { get; set; }

			public void SetValue(int value) =>
				Text.Text = value.ToString(CultureInfo.CurrentUICulture);
		}

		private enum RollDirection
		{
			Up,
			Down,
			Left,
			Right,
		}

		private enum RollType
		{
			In,
			Out,
		}

		private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(DiceFlipPanel));

		private static readonly TimeSpan c_rollTime = TimeSpan.FromMilliseconds(100);
		private const double c_slowDownFactor = 2.5;

		private readonly Face m_placeholder;
		private readonly IReadOnlyList<Face> m_faces;

		private int m_activeFace;
		private Storyboard? m_lastStoryboard;
	}
}
