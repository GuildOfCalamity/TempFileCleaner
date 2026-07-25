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
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TempFileCleaner.Controls
{
    public partial class StatCard : UserControl
    {
        public StatCard()
        {
            InitializeComponent();
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

        // Value
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(string), typeof(StatCard),
                new PropertyMetadata(string.Empty));

        public string Value
        {
            get => (string)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
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
    }
}
