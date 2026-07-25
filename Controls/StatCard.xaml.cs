using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TempFileCleaner.Controls
{
    [TemplatePart(Name = "PART_ValueTextBlock", Type = typeof(TextBlock))]
    public partial class StatCard : UserControl
    {
        public StatCard()
        {
            InitializeComponent();
        }

        TextBlock _valueTextBlock;
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _valueTextBlock = GetTemplateChild("PART_ValueTextBlock") as TextBlock;
        }

        public enum StatFormat
        {
            Number,
            Currency,
            Percent,
            Custom
        }

        public static readonly DependencyProperty CustomFormatStringProperty =
            DependencyProperty.Register(nameof(CustomFormatString), typeof(string), typeof(StatCard),
            new PropertyMetadata("N0"));

        public string CustomFormatString
        {
            get => (string)GetValue(CustomFormatStringProperty);
            set => SetValue(CustomFormatStringProperty, value);
        }

        // Format
        public static readonly DependencyProperty ValueFormatProperty =
            DependencyProperty.Register(nameof(ValueFormat), typeof(StatFormat), typeof(StatCard),
                new PropertyMetadata(StatFormat.Number));

        public StatFormat ValueFormat
        {
            get => (StatFormat)GetValue(ValueFormatProperty);
            set => SetValue(ValueFormatProperty, value);
        }

        // Title
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(StatCard),
                new PropertyMetadata(string.Empty));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        // Value (with animation)
        public static readonly DependencyProperty AnimatedValueProperty =
        DependencyProperty.Register(nameof(AnimatedValue), typeof(double), typeof(StatCard),
            new PropertyMetadata(0.0, OnAnimatedValueChanged));

        public double AnimatedValue
        {
            get => (double)GetValue(AnimatedValueProperty);
            set => SetValue(AnimatedValueProperty, value);
        }

        static void OnAnimatedValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (StatCard)d;
            double newVal = (double)e.NewValue;

            // Change precision formatting here if needed
            //control.ValueTextBlock.Text = newVal.ToString("N0");
            if (control._valueTextBlock == null)
                return;
            control._valueTextBlock.Text = control.FormatValue(newVal);
        }

        // Value (no animation)
        //public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(StatCard), new PropertyMetadata(string.Empty));
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(string), typeof(StatCard),
                new PropertyMetadata(string.Empty, OnValueChanged));

        public string Value
        {
            get => (string)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (StatCard)d;

            if (double.TryParse(e.OldValue?.ToString(), out double oldVal) &&
                double.TryParse(e.NewValue?.ToString(), out double newVal))
            {
                control.AnimateValue(oldVal, newVal);
            }
            else
            {
                if (control._valueTextBlock == null)
                    return;
                // If non-numeric just set text directly (because we're using DoubleAnimation)
                control._valueTextBlock.Text = $"{e.NewValue}";
            }
        }


        // TitleColor
        public static readonly DependencyProperty TitleColorProperty =
            DependencyProperty.Register(nameof(TitleColor), typeof(Brush), typeof(StatCard),
                new PropertyMetadata(Brushes.DodgerBlue));

        public Brush TitleColor
        {
            get => (Brush)GetValue(TitleColorProperty);
            set => SetValue(TitleColorProperty, value);
        }

        // ValueColor
        public static readonly DependencyProperty ValueColorProperty =
            DependencyProperty.Register(nameof(ValueColor), typeof(Brush), typeof(StatCard),
                new PropertyMetadata(Brushes.White));

        public Brush ValueColor
        {
            get => (Brush)GetValue(ValueColorProperty);
            set => SetValue(ValueColorProperty, value);
        }

        // BackgroundColor
        public static readonly DependencyProperty BackgroundColorProperty =
            DependencyProperty.Register(nameof(BackgroundColor), typeof(Brush), typeof(StatCard),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(21, 21, 21))));

        public Brush BackgroundColor
        {
            get => (Brush)GetValue(BackgroundColorProperty);
            set => SetValue(BackgroundColorProperty, value);
        }

        // BorderColor
        public static readonly DependencyProperty BorderColorProperty =
            DependencyProperty.Register(nameof(BorderColor), typeof(Brush), typeof(StatCard),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(42, 127, 255))));

        public Brush BorderColor
        {
            get => (Brush)GetValue(BorderColorProperty);
            set => SetValue(BorderColorProperty, value);
        }

        /// <summary>
        /// Animates the value change from 'from' to 'to' over a specified duration with easing.
        /// This causes the control to "count up" or "count down" to the new value, instead of abruptly setting the new value.
        /// </summary>
        /// <param name="from">The starting value.</param>
        /// <param name="to">The target value.</param>
        void AnimateValue(double from, double to)
        {
            var animation = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = TimeSpan.FromMilliseconds(500),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            // UserControl inherits from Animatable → BeginAnimation is valid here
            BeginAnimation(AnimatedValueProperty, animation);
        }

        string FormatValue(double value)
        {
            switch (ValueFormat)
            {
                case StatFormat.Currency:
                    return value.ToString("C2"); // $12,345.00

                case StatFormat.Percent:
                    return value.ToString("P1"); // 12,345.0%

                case StatFormat.Custom:
                    return value.ToString(CustomFormatString);

                default:
                    return value.ToString("N0"); // 12,345
            }
        }

        public static readonly DependencyProperty GlowColorProperty =
            DependencyProperty.Register(nameof(GlowColor), typeof(Color), typeof(StatCard),
            new PropertyMetadata(Color.FromRgb(0, 200, 255))); // default blue

        public Color GlowColor
        {
            get => (Color)GetValue(GlowColorProperty);
            set => SetValue(GlowColorProperty, value);
        }

        public static readonly DependencyProperty GlowRadiusProperty =
            DependencyProperty.Register(nameof(GlowRadius), typeof(double), typeof(StatCard),
                new PropertyMetadata(10.0)); // default glow size

        public double GlowRadius
        {
            get => (double)GetValue(GlowRadiusProperty);
            set => SetValue(GlowRadiusProperty, value);
        }

    }

    public class DoubleAnimationHelper : DependencyObject
    {
        public event EventHandler<double> ValueChanged;

        public static readonly DependencyProperty AnimatedValueProperty =
            DependencyProperty.Register(nameof(AnimatedValue), typeof(double),
                typeof(DoubleAnimationHelper),
                new PropertyMetadata(0.0, OnAnimatedValueChanged));

        public double AnimatedValue
        {
            get => (double)GetValue(AnimatedValueProperty);
            set => SetValue(AnimatedValueProperty, value);
        }

        static void OnAnimatedValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var helper = (DoubleAnimationHelper)d;
            helper.ValueChanged?.Invoke(helper, (double)e.NewValue);
        }
    }

}
