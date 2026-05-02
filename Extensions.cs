using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TempFileCleaner
{
    public static class Extensions
    {
        public enum LogLevel { DEBUG = 0, INFO = 1, WARNING = 2, ERROR = 3, SUCCESS = 4 }

        #region [For .NET 4.8 and lower]
        static readonly WeakReference s_random = new WeakReference(null);
        public static Random Rnd
        {
            get
            {
                var r = (Random)s_random.Target;
                if (r == null)
                {
                    s_random.Target = r = new Random();
                }
                return r;
            }
        }
        #endregion

        #region [Logger with automatic duplicate checking]
        static HashSet<string> _logCache = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        static DateTime _logCacheUpdated = DateTime.Now;
        static int _repeatAllowedSeconds = 15;
        public static void WriteToLog(this string message, LogLevel level = LogLevel.INFO, string fileName = "AppLog.txt", bool debugOnly = false)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            if (_logCache.Add(message))
            {
                _logCacheUpdated = DateTime.Now;
                if (debugOnly)
                {
                    Debug.WriteLine(message);
                }
                else
                {
                    try { System.IO.File.AppendAllText(fileName, $"[{DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.fff tt")}] [{level}] {message}{Environment.NewLine}"); }
                    catch (Exception) { }
                }
            }
            else
            {
                var diff = DateTime.Now - _logCacheUpdated;
                if (diff.Seconds > _repeatAllowedSeconds)
                    _logCache.Clear();
                else
                {
                    if (!debugOnly)
                        Debug.WriteLine($"[WARNING] Duplicate not allowed: {diff.Seconds}secs < {_repeatAllowedSeconds}secs");
                }
            }
        }
        #endregion

        public const double Epsilon = 0.000000000001;
        public static bool IsZeroOrLess(this double value) => value < Epsilon;
        public static bool IsZeroOrLess(this float value) => value < (float)Epsilon;
        public static bool IsZero(this double value) => Math.Abs(value) < Epsilon;
        public static bool IsZero(this float value) => Math.Abs(value) < (float)Epsilon;
        public static bool IsInvalid(this double value)
        {
            if (value == double.NaN || value == double.NegativeInfinity || value == double.PositiveInfinity)
                return true;

            return false;
        }
        public static bool IsInvalidOrZero(this double value)
        {
            if (value == double.NaN || value == double.NegativeInfinity || value == double.PositiveInfinity || value <= 0)
                return true;

            return false;
        }
        public static bool IsInvalidOrZero(this System.Windows.Size value)
        {
            if (value.Width == double.NaN || value.Width == double.NegativeInfinity || value.Width == double.PositiveInfinity || value.Width <= 0)
                return true;
            if (value.Height == double.NaN || value.Height == double.NegativeInfinity || value.Height == double.PositiveInfinity || value.Height <= 0)
                return true;

            return false;
        }
        public static bool IsOne(this double value)
        {
            return Math.Abs(value) >= 1d - Epsilon && Math.Abs(value) <= 1d + Epsilon;
        }
        public static bool AreClose(this double left, double right)
        {
            if (left == right)
                return true;

            double a = (Math.Abs(left) + Math.Abs(right) + 10.0d) * Epsilon;
            double b = left - right;
            return (-a < b) && (a > b);
        }
        public static bool AreClose(this float left, float right)
        {
            if (left == right)
                return true;

            float a = (Math.Abs(left) + Math.Abs(right) + 10.0f) * (float)Epsilon;
            float b = left - right;
            return (-a < b) && (a > b);
        }

        /// <summary>
        /// Clamping function for any value of type <see cref="IComparable{T}"/>.
        /// </summary>
        /// <param name="val">initial value</param>
        /// <param name="min">lowest range</param>
        /// <param name="max">highest range</param>
        /// <returns>clamped value</returns>
        public static T Clamp<T>(this T val, T min, T max) where T : IComparable<T>
        {
            return val.CompareTo(min) < 0 ? min : (val.CompareTo(max) > 0 ? max : val);
        }

        /// <summary>
        /// If the <paramref name="thing"/> is null then log the stack trace from whence it came.
        /// <code>
        ///   object thing = null;
        ///   thing.CheckIsNull();
        ///   thing.CheckIsNull("thing");
        /// </code>
        /// </summary>
        /// <param name="thing">the object to check</param>
        /// <remarks>
        /// The line numbers and file names will only appear correctly if the .pdb files are 
        /// available alongside your .dll or .exe files when the code runs. These files contain 
        /// the mapping between the compiled IL (Intermediate Language) code and your original 
        /// source code lines. Without the corresponding PDB file, the GetFileLineNumber() 
        /// method will return 0, and GetFileName() might return null.
        /// </remarks>
        public static void CheckIsNull(this object thing, string nameOfObject = "")
        {
            if (thing == null)
            {
                StringBuilder message = new StringBuilder();
                // Pass 'true' to the constructor to capture source file info
                StackTrace stackTrace = new StackTrace(true);
                if (!string.IsNullOrEmpty(nameOfObject))
                    message.Append($"[WARNING] CheckIsNull determined the incoming object \"{nameOfObject}\" was null.\n");
                else
                    message.Append($"[WARNING] CheckIsNull determined the incoming object was null.\n");
                message.Append("Call Stack:\n");
                // Iterate through frames to get detailed info
                foreach (StackFrame frame in stackTrace.GetFrames())
                {
                    // Use GetFileLineNumber() and GetFileName()
                    message.Append($"  at {frame.GetMethod().Name} in {frame.GetFileName()}:line {frame.GetFileLineNumber()}\n");
                }
                Debug.Write($"{message}");
            }
            //else
            //{
            //    Debug.WriteLine($"[INFO] Is of type {thing.GetType().FullName}");
            //}
        }

        /// <summary>
        /// This attempts to solve the issue of having to pass the name of the object along with the object itself
        /// just the obtain the name of the object during debug logging.
        /// <code>
        ///   object myObject = null;
        ///   Extensions.CheckIsNull(() => myObject);
        /// </code>
        /// </summary>
        /// <param name="expression">the object to test</param>
        /// <remarks>
        /// The line numbers and file names will only appear correctly if the .pdb files are 
        /// available alongside your .dll or .exe files when the code runs. These files contain 
        /// the mapping between the compiled IL (Intermediate Language) code and your original 
        /// source code lines. Without the corresponding PDB file, the GetFileLineNumber() 
        /// method will return 0, and GetFileName() might return null.
        /// </remarks>
        public static void CheckIsNull(System.Linq.Expressions.Expression<Func<object>> expression)
        {
            // Compile time safety (ensure the expression is valid)
            if (expression.Body is System.Linq.Expressions.MemberExpression memberExpression)
            {
                string objectName = memberExpression.Member.Name;
                object thing = expression.Compile().Invoke(); // Get the actual value
                if (thing == null)
                {
                    StringBuilder message = new StringBuilder();
                    // Pass 'true' to the constructor to capture source file info
                    StackTrace stackTrace = new StackTrace(true);
                    message.Append($"[WARNING] CheckIsNull determined the incoming object \"{objectName}\" was null.\n");
                    message.Append("Call Stack:\n");
                    // Iterate through frames to get detailed info
                    foreach (StackFrame frame in stackTrace.GetFrames())
                    {
                        // Use GetFileLineNumber() and GetFileName()
                        message.Append($"  at {frame?.GetMethod()?.Name} in {frame?.GetFileName()}:line {frame?.GetFileLineNumber()}\n");
                    }
                    Debug.WriteLine($"{message}");
                }
            }
        }

        #region [Color Brush Methods]
        public static (byte A, byte R, byte G, byte B) ParseHexColor(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
                WriteToLog("Hex color string cannot be null or empty.", LogLevel.WARNING);

            // Normalize: remove leading '#'
            if (hex.StartsWith("#"))
                hex = hex.Substring(1);

            if (hex.Length != 6 && hex.Length != 8)
                WriteToLog("Hex color string must be 6 (RRGGBB) or 8 (AARRGGBB) characters.", LogLevel.WARNING);

            int index = 0;

            byte a = 255; // default opaque

            if (hex.Length == 8)
            {
                a = Convert.ToByte(hex.Substring(index, 2), 16);
                index += 2;
            }

            byte r = Convert.ToByte(hex.Substring(index, 2), 16);
            byte g = Convert.ToByte(hex.Substring(index + 2, 2), 16);
            byte b = Convert.ToByte(hex.Substring(index + 4, 2), 16);

            return (a, r, g, b);
        }

        public static RadialGradientBrush CreateRadialBrush(string hex, double opacity = 0.6)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                WriteToLog("Hex color string cannot be null or empty.", LogLevel.WARNING);
                return null;
            }

            // Normalize input (strip leading # if present)
            if (hex.StartsWith("#"))
                hex = hex.Substring(1);

            if (hex.Length != 6)
            {
                WriteToLog("Hex color string must be 6 characters (RRGGBB).", LogLevel.WARNING);
                return null;
            }

            // Parse hex into Color
            byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
            var baseColor = Color.FromRgb(r, g, b);

            // Create lighter/darker variants
            Color lighter = Colors.White;
            //Color lighter = BrightenGamma(baseColor, 2.0); // 100% lighter
            Color darker = DarkenGamma(baseColor, 0.1); // 90% darker

            var brush = new RadialGradientBrush
            {
                Opacity = opacity,
                GradientOrigin = new System.Windows.Point(0.75, 0.25),
                Center = new System.Windows.Point(0.5, 0.5),
                RadiusX = 0.5,
                RadiusY = 0.5
            };

            brush.GradientStops.Add(new GradientStop(lighter, 0.0));
            brush.GradientStops.Add(new GradientStop(baseColor, 0.6));
            brush.GradientStops.Add(new GradientStop(darker, 1.0));

            return brush;
        }

        /// <summary>
        /// Gamma‑corrected brighten (perceptually smoother)
        /// <code>
        ///   var brighter = BrightenGamma(baseColor, 1.5); // 50% brighter
        /// </code>
        /// </summary>
        public static Color BrightenGamma(Color color, double factor = 1.5, double gamma = 2.2)
        {
            // Convert sRGB ⇨ linear
            double r = Math.Pow(color.R / 255.0, gamma);
            double g = Math.Pow(color.G / 255.0, gamma);
            double b = Math.Pow(color.B / 255.0, gamma);

            // Apply brighten factor in linear space
            r = Math.Min(1.0, r * factor);
            g = Math.Min(1.0, g * factor);
            b = Math.Min(1.0, b * factor);

            // Convert back linear ⇨ sRGB
            byte R = (byte)(Math.Pow(r, 1.0 / gamma) * 255);
            byte G = (byte)(Math.Pow(g, 1.0 / gamma) * 255);
            byte B = (byte)(Math.Pow(b, 1.0 / gamma) * 255);

            return Color.FromArgb(color.A, R, G, B);
        }

        /// <summary>
        /// Gamma‑corrected darken (perceptually smoother)
        /// <code>
        ///   var darker = DarkenGamma(baseColor, 0.7); // Darken to 70% brightness
        /// </code>
        /// </summary>
        public static Color DarkenGamma(Color color, double factor = 0.7, double gamma = 2.2)
        {
            // factor < 1.0 will darken, factor = 1.0 no change
            if (factor > 1.0) factor = 1.0;
            if (factor < 0.0) factor = 0.0;

            // Convert sRGB ⇨ linear
            double r = Math.Pow(color.R / 255.0, gamma);
            double g = Math.Pow(color.G / 255.0, gamma);
            double b = Math.Pow(color.B / 255.0, gamma);

            // Apply darken factor in linear space
            r *= factor;
            g *= factor;
            b *= factor;

            // Convert back linear ⇨ sRGB
            byte R = (byte)(Math.Pow(r, 1.0 / gamma) * 255);
            byte G = (byte)(Math.Pow(g, 1.0 / gamma) * 255);
            byte B = (byte)(Math.Pow(b, 1.0 / gamma) * 255);

            return Color.FromArgb(color.A, R, G, B);
        }

        /// <summary>
        /// Generates a random <see cref="System.Windows.Media.Color"/>.
        /// </summary>
        /// <returns><see cref="System.Windows.Media.Color"/> with 255 alpha</returns>
        public static System.Windows.Media.Color GenerateRandomColor()
        {
            return System.Windows.Media.Color.FromRgb((byte)new Random().Next(0, 256), (byte)new Random().Next(0, 256), (byte)new Random().Next(0, 256));
        }

        /// <summary>
        /// Generates a random <see cref="LinearGradientBrush"/> using two <see cref="System.Windows.Media.Color"/>s.
        /// </summary>
        /// <returns><see cref="LinearGradientBrush"/></returns>
        public static LinearGradientBrush CreateGradientBrush(Color c1, Color c2)
        {
            var gs1 = new GradientStop(c1, 0);
            var gs3 = new GradientStop(c2, 1);
            var gsc = new GradientStopCollection { gs1, gs3 };
            var lgb = new LinearGradientBrush
            {
                ColorInterpolationMode = ColorInterpolationMode.ScRgbLinearInterpolation,
                StartPoint = new System.Windows.Point(0, 0),
                EndPoint = new System.Windows.Point(0, 1),
                GradientStops = gsc
            };
            return lgb;
        }

        /// <summary>
        /// Generates a random <see cref="LinearGradientBrush"/> using three <see cref="System.Windows.Media.Color"/>s.
        /// </summary>
        /// <returns><see cref="LinearGradientBrush"/></returns>
        public static LinearGradientBrush CreateGradientBrush(Color c1, Color c2, Color c3)
        {
            var gs1 = new GradientStop(c1, 0);
            var gs2 = new GradientStop(c2, 0.6);
            var gs3 = new GradientStop(c3, 1);
            var gsc = new GradientStopCollection { gs1, gs2, gs3 };
            var lgb = new LinearGradientBrush
            {
                ColorInterpolationMode = ColorInterpolationMode.ScRgbLinearInterpolation,
                StartPoint = new System.Windows.Point(0, 0),
                EndPoint = new System.Windows.Point(0, 1),
                GradientStops = gsc
            };
            return lgb;
        }

        /// <summary>
        /// Generates a random <see cref="SolidColorBrush"/>.
        /// </summary>
        /// <returns><see cref="SolidColorBrush"/> with 255 alpha</returns>
        public static SolidColorBrush CreateRandomBrush()
        {
            byte r = (byte)new Random().Next(0, 256);
            byte g = (byte)new Random().Next(0, 256);
            byte b = (byte)new Random().Next(0, 256);
            return new SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
        }

        /// <summary>
        /// Avoids near-white values by using high saturation ranges prevent desaturation.
        /// </summary>
        public static SolidColorBrush CreateRandomLightBrush(byte alpha = 255)
        {
            return CreateRandomHsvBrush(
                hue: new Random().NextDouble() * 360.0,
                saturation: Lerp(0.65, 1.0, new Random().NextDouble()), // high saturation to avoid gray
                value: Lerp(0.85, 1.0, new Random().NextDouble()),      // bright
                alpha: alpha);
        }

        /// <summary>
        /// Avoids near-black values by using high saturation ranges prevent desaturation.
        /// </summary>
        public static SolidColorBrush CreateRandomDarkBrush(byte alpha = 255)
        {
            return CreateRandomHsvBrush(
                hue: new Random().NextDouble() * 360.0,
                saturation: Lerp(0.65, 1.0, new Random().NextDouble()), // high saturation to avoid gray
                value: Lerp(0.2, 0.45, new Random().NextDouble()),      // dark
                alpha: alpha);
        }

        public static SolidColorBrush CreateRandomHsvBrush(double hue, double saturation, double value, byte alpha)
        {
            var (r, g, b) = HsvToRgb(hue, saturation, value);
            var brush = new SolidColorBrush(Color.FromArgb(alpha, r, g, b));
            if (brush.CanFreeze)
                brush.Freeze(); // freeze for performance (if animation is not needed)
            return brush;
        }

        static (byte r, byte g, byte b) HsvToRgb(double h, double s, double v)
        {
            // h: [0,360), s,v: [0,1]
            if (s <= 0.00001)
            {
                // If saturation is approx zero then return achromatic (grey)
                byte grey = (byte)Math.Round(v * 255.0);
                return (grey, grey, grey);
            }

            h = (h % 360 + 360) % 360; // normalize
            double c = v * s;
            double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
            double m = v - c;

            //double (r1, g1, b1) = h switch
            //{
            //    < 60 => (c, x, 0),
            //    < 120 => (x, c, 0),
            //    < 180 => (0, c, x),
            //    < 240 => (0, x, c),
            //    < 300 => (x, 0, c),
            //    _ => (c, 0, x)
            //};
            double r1, g1, b1;
            if (h < 60) { r1 = c; g1 = x; b1 = 0; }
            else if (h < 120) { r1 = x; g1 = c; b1 = 0; }
            else if (h < 180) { r1 = 0; g1 = c; b1 = x; }
            else if (h < 240) { r1 = 0; g1 = x; b1 = c; }
            else if (h < 300) { r1 = x; g1 = 0; b1 = c; }
            else { r1 = c; g1 = 0; b1 = x; }
            byte r = (byte)Math.Round((r1 + m) * 255.0);
            byte g = (byte)Math.Round((g1 + m) * 255.0);
            byte b = (byte)Math.Round((b1 + m) * 255.0);
            return (r, g, b);
        }

        static void RgbToHsv(byte r, byte g, byte b, out double h, out double s, out double v)
        {
            double rd = r / 255.0;
            double gd = g / 255.0;
            double bd = b / 255.0;

            double max = Math.Max(rd, Math.Max(gd, bd));
            double min = Math.Min(rd, Math.Min(gd, bd));
            double delta = max - min;

            // Hue
            if (delta < 0.00001) { h = 0; }
            else if (max == rd) { h = 60 * (((gd - bd) / delta) % 6); }
            else if (max == gd) { h = 60 * (((bd - rd) / delta) + 2); }
            else { h = 60 * (((rd - gd) / delta) + 4); }
            if (h < 0) { h += 360; }

            // Saturation
            s = (max <= 0) ? 0 : delta / max;

            // Value
            v = max;
        }

        static double Lerp(double a, double b, double t) => a + (b - a) * t;

        public enum ColorTilt
        {
            Red,
            Orange,
            Yellow,
            Green,
            Blue,
            Purple
        }

        /// <summary>
        /// Generates a random <see cref="SolidColorBrush"/> based on a given <see cref="ColorTilt"/>.
        /// </summary>
        public static SolidColorBrush CreateRandomLightBrush(ColorTilt tilt, double tiltStrength = 30, byte alpha = 255)
        {
            double hue = GetTiltedHue(tilt, tiltStrength);
            double saturation = Lerp(0.65, 1.0, new Random().NextDouble()); // high saturation to avoid gray
            double value = Lerp(0.85, 1.0, new Random().NextDouble());      // bright
            return CreateBrushFromHsv(hue, saturation, value, alpha);
        }

        /// <summary>
        /// Generates a random <see cref="SolidColorBrush"/> based on a given <see cref="ColorTilt"/>.
        /// </summary>
        public static SolidColorBrush CreateRandomDarkBrush(ColorTilt tilt, double tiltStrength = 30, byte alpha = 255)
        {
            double hue = GetTiltedHue(tilt, tiltStrength);
            double saturation = Lerp(0.65, 1.0, new Random().NextDouble()); // high saturation to avoid gray
            double value = Lerp(0.2, 0.45, new Random().NextDouble());      // dark
            return CreateBrushFromHsv(hue, saturation, value, alpha);
        }

        /// <summary>
        /// Generates a random <see cref="SolidColorBrush"/> based on a given dictionary of <see cref="ColorTilt"/>s.
        /// </summary>
        public static SolidColorBrush CreateRandomLightBrush(Dictionary<ColorTilt, double> tiltWeights, double tiltStrength = 30, byte alpha = 255)
        {
            double hue = GetBlendedTiltedHue(tiltWeights, tiltStrength);
            double saturation = Lerp(0.65, 1.0, new Random().NextDouble()); // high saturation to avoid gray
            double value = Lerp(0.85, 1.0, new Random().NextDouble());      // bright
            return CreateBrushFromHsv(hue, saturation, value, alpha);
        }

        /// <summary>
        /// Generates a random <see cref="SolidColorBrush"/> based on a given dictionary of <see cref="ColorTilt"/>s.
        /// </summary>
        public static SolidColorBrush CreateRandomDarkBrush(Dictionary<ColorTilt, double> tiltWeights, double tiltStrength = 30, byte alpha = 255)
        {
            double hue = GetBlendedTiltedHue(tiltWeights, tiltStrength);
            double saturation = Lerp(0.65, 1.0, new Random().NextDouble()); // high saturation to avoid gray
            double value = Lerp(0.2, 0.45, new Random().NextDouble());      // dark
            return CreateBrushFromHsv(hue, saturation, value, alpha);
        }

        static SolidColorBrush CreateBrushFromHsv(double hue, double saturation, double value, byte alpha)
        {
            var (r, g, b) = HsvToRgb(hue, saturation, value);
            var brush = new SolidColorBrush(Color.FromArgb(alpha, r, g, b));
            if (brush.CanFreeze) { brush.Freeze(); }
            return brush;
        }

        static double GetTiltedHue(ColorTilt tilt, double variance = 30)
        {
            // Hue centers in degrees for basic colors
            double centerHue;
            switch (tilt)
            {
                case ColorTilt.Red:
                    centerHue = 0.0;      // also wraps near 360
                    break;
                case ColorTilt.Orange:
                    centerHue = 30.0;
                    break;
                case ColorTilt.Yellow:
                    centerHue = 60.0;
                    break;
                case ColorTilt.Green:
                    centerHue = 120.0;
                    break;
                case ColorTilt.Blue:
                    centerHue = 240.0;
                    break;
                case ColorTilt.Purple:
                    centerHue = 280.0; // between magenta (300) and blue
                    break;
                default:
                    centerHue = 0.0;
                    break;
            }

            // Clamp variance to [0,180]
            variance = Math.Max(0, Math.Min(variance, 180));

            // Allow ±30° variation for variety
            double minHue = centerHue - variance;
            double maxHue = centerHue + variance;

            double hue = minHue + new Random().NextDouble() * (maxHue - minHue);
            // Wrap around 0–360
            if (hue < 0) { hue += 360; }
            if (hue >= 360) { hue -= 360; }

            return hue;
        }

        static double GetBlendedTiltedHue(Dictionary<ColorTilt, double> tiltWeights, double tiltStrength)
        {
            if (tiltWeights == null || tiltWeights.Count == 0)
                return new Random().NextDouble() * 360.0;

            // Normalize weights
            double total = tiltWeights.Values.Sum();
            if (total <= 0) return new Random().NextDouble() * 360.0;

            // Pick a tilt based on weighted random
            double roll = new Random().NextDouble() * total;
            double cumulative = 0;
            ColorTilt chosenTilt = tiltWeights.First().Key;

            foreach (var kvp in tiltWeights)
            {
                cumulative += kvp.Value;
                if (roll <= cumulative)
                {
                    chosenTilt = kvp.Key;
                    break;
                }
            }

            // Get center hue for chosen tilt
            double centerHue = GetCenterHue(chosenTilt);

            // Clamp tiltStrength
            tiltStrength = Math.Max(0, Math.Min(tiltStrength, 180));

            // ± tiltStrength variation
            double minHue = centerHue - tiltStrength;
            double maxHue = centerHue + tiltStrength;

            double hue = minHue + new Random().NextDouble() * (maxHue - minHue);
            if (hue < 0) hue += 360;
            if (hue >= 360) hue -= 360;

            return hue;
        }

        static double GetCenterHue(ColorTilt tilt)
        {
            switch (tilt)
            {
                case ColorTilt.Red: return 0.0;
                case ColorTilt.Orange: return 30.0;
                case ColorTilt.Yellow: return 60.0;
                case ColorTilt.Green: return 120.0;
                case ColorTilt.Blue: return 240.0;
                case ColorTilt.Purple: return 280.0;
                default: return 0.0;
            }
        }

        public static SolidColorBrush BrightenBrush(SolidColorBrush brush, double amount)
        {
            if (brush == null)
                throw new ArgumentNullException(nameof(brush));

            // Clamp amount to [0, 1]
            amount = Math.Max(0, Math.Min(amount, 1));

            Color color = brush.Color;

            // Convert to HSV
            double h, s, v;
            RgbToHsv(color.R, color.G, color.B, out h, out s, out v);

            // Increase brightness
            v = Math.Min(1.0, v + amount);

            // Convert back to RGB
            var (r, g, b) = HsvToRgb(h, s, v);

            var newBrush = new SolidColorBrush(Color.FromArgb(color.A, r, g, b));
            if (newBrush.CanFreeze) { newBrush.Freeze(); }
            return newBrush;
        }

        public static SolidColorBrush DarkenBrush(SolidColorBrush brush, double amount)
        {
            if (brush == null)
                throw new ArgumentNullException(nameof(brush));

            // Clamp amount to [0, 1]
            amount = Math.Max(0, Math.Min(amount, 1));

            Color color = brush.Color;

            // Convert to HSV
            double h, s, v;
            RgbToHsv(color.R, color.G, color.B, out h, out s, out v);

            // Decrease brightness
            v = Math.Max(0.0, v - amount);

            // Convert back to RGB
            var (r, g, b) = HsvToRgb(h, s, v);

            var newBrush = new SolidColorBrush(Color.FromArgb(color.A, r, g, b));
            if (newBrush.CanFreeze) newBrush.Freeze();
            return newBrush;
        }

        /// <summary><code>
        ///  /* Brighten by 20%, no saturation change */
        ///  var brighter = Extensions.AdjustBrush(baseBrush, brightnessDelta: 0.2);
        ///  /* Darken by 30%, mute by 20% */
        ///  var darkerMuted = Extensions.AdjustBrush(baseBrush, brightnessDelta: -0.3, saturationDelta: -0.2);
        ///  /* Keep brightness, boost saturation */
        ///  var vivid = Extensions.AdjustBrush(baseBrush, saturationDelta: 0.3);
        /// </code></summary>
        public static SolidColorBrush AdjustBrush(SolidColorBrush brush, double brightnessDelta = 0.0, double saturationDelta = 0.0)
        {
            if (brush == null)
                throw new ArgumentNullException(nameof(brush));

            Color color = brush.Color;

            // Convert to HSV
            double h, s, v;
            RgbToHsv(color.R, color.G, color.B, out h, out s, out v);

            // Apply deltas
            v = Math.Max(0.0, Math.Min(1.0, v + brightnessDelta));
            s = Math.Max(0.0, Math.Min(1.0, s + saturationDelta));

            // Convert back to RGB
            var (r, g, b) = HsvToRgb(h, s, v);

            var adjusted = new SolidColorBrush(Color.FromArgb(color.A, r, g, b));
            if (adjusted.CanFreeze) { adjusted.Freeze(); }
            return adjusted;
        }

        public static SolidColorBrush ShiftSaturation(SolidColorBrush brush, double amount)
        {
            if (brush == null)
                throw new ArgumentNullException(nameof(brush));

            // amount can be positive (more vivid) or negative (more muted)
            // Clamp to [-1, 1] so we don't overshoot
            amount = Math.Max(-1, Math.Min(amount, 1));

            Color color = brush.Color;

            // Convert to HSV
            double h, s, v;
            RgbToHsv(color.R, color.G, color.B, out h, out s, out v);

            // Adjust saturation
            s = Math.Max(0.0, Math.Min(1.0, s + amount));

            // Convert back to RGB
            var (r, g, b) = HsvToRgb(h, s, v);

            var newBrush = new SolidColorBrush(Color.FromArgb(color.A, r, g, b));
            if (newBrush.CanFreeze)
                newBrush.Freeze();

            return newBrush;
        }

        /// <summary>
        /// Returns the Euclidean distance between two <see cref="System.Windows.Media.Color"/>s.
        /// </summary>
        /// <param name="color1">1st <see cref="System.Windows.Media.Color"/></param>
        /// <param name="color2">2nd <see cref="System.Windows.Media.Color"/></param>
        public static double ColorDistance(System.Windows.Media.Color color1, System.Windows.Media.Color color2)
        {
            return Math.Sqrt(Math.Pow(color1.R - color2.R, 2) + Math.Pow(color1.G - color2.G, 2) + Math.Pow(color1.B - color2.B, 2));
        }
        #endregion

        /// <summary>
        /// Fetch all <see cref="System.Windows.Media.Brushes"/>.
        /// </summary>
        /// <returns><see cref="List{T}"/></returns>
        public static List<Brush> GetAllMediaBrushes()
        {
            List<Brush> brushes = new List<Brush>();
            Type brushesType = typeof(Brushes);

            //TypeAttributes ta = typeof(Brushes).Attributes;
            //Debug.WriteLine($"[INFO] TypeAttributes: {ta}");

            // Iterate through the static properties of the Brushes class type.
            foreach (PropertyInfo pi in brushesType.GetProperties(BindingFlags.Static | BindingFlags.Public))
            {
                // Check if the property type is Brush/SolidColorBrush
                if (pi != null && (pi.PropertyType == typeof(Brush) || pi.PropertyType == typeof(SolidColorBrush)))
                {
                    if (pi.Name.Contains("Transparent"))
                        continue;

                    Debug.WriteLine($"[INFO] Adding brush '{pi.Name}'");

                    // Get the brush value from the static property
                    var br = (Brush)pi?.GetValue(null, null);
                    if (br != null)
                        brushes.Add(br);
                }
            }
            return brushes;
        }

        /// <summary>
        /// 'BitmapCacheBrush','DrawingBrush','GradientBrush','ImageBrush',
        /// 'LinearGradientBrush','RadialGradientBrush','SolidColorBrush',
        /// 'TileBrush','VisualBrush','ImplicitInputBrush'
        /// </summary>
        /// <returns><see cref="List{T}"/></returns>
        public static List<Type> GetAllDerivedBrushClasses()
        {
            List<Type> derivedBrushes = new List<Type>();
            // Get the assembly containing the Brush class
            Assembly assembly = typeof(Brush).Assembly;
            try
            {   // Iterate through all types in the assembly
                foreach (Type type in assembly.GetTypes())
                {
                    // Check if the type is a subclass of Brush
                    if (type.IsSubclassOf(typeof(Brush)))
                    {
                        //Debug.WriteLine($"[INFO] Adding type '{type.Name}'");
                        derivedBrushes.Add(type);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] GetAllDerivedBrushClasses: {ex.Message}");
            }
            return derivedBrushes;
        }

        /// <summary>
        /// Fetch all derived types from a super class.
        /// </summary>
        /// <returns><see cref="List{T}"/></returns>
        public static List<Type> GetDerivedSubClasses<T>(T objectClass) where T : class
        {
            List<Type> derivedClasses = new List<Type>();
            // Get the assembly containing the base class
            Assembly assembly = typeof(T).Assembly;
            try
            {   // Iterate through all types in the assembly
                foreach (Type type in assembly.GetTypes())
                {
                    // Check if the type is a subclass of T
                    if (type.IsSubclassOf(typeof(T)))
                    {
                        //Debug.WriteLine($"[INFO] Adding subclass type '{type.Name}'");
                        derivedClasses.Add(type);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] GetDerivedClasses: {ex.Message}");
            }
            return derivedClasses;
        }

        /// <summary>
        /// Example of <see cref="UIElement"/> traversal.
        /// </summary>
        public static void IterateAllUIElements(DockPanel dock)
        {
            UIElementCollection uic = dock.Children;

            foreach (Grid uie in uic)
                uie.Background = new SolidColorBrush(Colors.Green);

            foreach (Border uie in uic)
                uie.Background = new SolidColorBrush(Colors.Orange);

            foreach (StackPanel uie in uic)
                uie.Background = new SolidColorBrush(Colors.Blue);

            foreach (Button uie in uic)
            {
                uie.Background = new SolidColorBrush(Colors.Yellow);

                // Example of restoring default properties
                var locallySetProperties = uie.GetLocalValueEnumerator();
                while (locallySetProperties.MoveNext())
                {
                    DependencyProperty propertyToClear = locallySetProperties.Current.Property;
                    if (!propertyToClear.ReadOnly)
                        uie.ClearValue(propertyToClear);
                }
            }
        }

        /// <summary>
        /// FindVisualChild element in a control group.
        /// <code>
        ///   /* Getting the ContentPresenter of myListBoxItem */
        ///   var myContentPresenter = FindVisualChild<ContentPresenter>(myListBoxItem);
        ///   
        ///   /* Getting the currently selected ListBoxItem. Note that the ListBox must have IsSynchronizedWithCurrentItem set to True for this to work */
        ///   var myListBoxItem = (ListBoxItem)(myListBox.ItemContainerGenerator.ContainerFromItem(myListBox.Items.CurrentItem));
        ///   
        ///   /* Finding textBlock from the DataTemplate that is set on that ContentPresenter */
        ///   var myDataTemplate = myContentPresenter.ContentTemplate;
        ///   var myTextBlock = (TextBlock)myDataTemplate.FindName("textBlock", myContentPresenter);
        ///
        ///   /* Do something to the DataTemplate-generated TextBlock */
        ///   MessageBox.Show($"The text of the TextBlock of the selected list item: {myTextBlock.Text}");
        /// </code>
        /// </summary>
        public static TChildItem FindVisualChild<TChildItem>(DependencyObject obj) where TChildItem : DependencyObject
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                if (child is TChildItem)
                    return (TChildItem)child;
                var childOfChild = FindVisualChild<TChildItem>(child);
                if (childOfChild != null)
                    return childOfChild;
            }
            return null;
        }

        /// <summary>
        /// Find & return a WPF control based on its resource key name.
        /// </summary>
        public static T FindControl<T>(this FrameworkElement control, string resourceKey) where T : FrameworkElement
        {
            return (T)control.FindResource(resourceKey);
        }

        /// <summary>
        /// <code>
        ///   IEnumerable<DependencyObject> cntrls = this.FindUIElements();
        /// </code>
        /// If you're struggling to get this working and finding that your Window (for instance)
        /// has zero visual children, try running this method in the "_Loaded" event handler. 
        /// If you call this from a constructor (even after InitializeComponent), the visual 
        /// children won't be added to the VisualTree yet and it won't work properly.
        /// </summary>
        /// <param name="parent">some parent control like <see cref="System.Windows.Window"/></param>
        /// <returns>list of <see cref="IEnumerable{DependencyObject}"/></returns>
        public static IEnumerable<DependencyObject> FindUIElements(this DependencyObject parent)
        {
            if (parent == null)
                yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject o = VisualTreeHelper.GetChild(parent, i);
                foreach (DependencyObject obj in FindUIElements(o))
                {
                    if (obj == null)
                        continue;
                    if (obj is UIElement ret)
                        yield return ret;
                }
            }
            yield return parent;
        }

        /// <summary>
        /// Should be called on UI thread only.
        /// </summary>
        public static void HideAllVisualChildren<T>(this UIElementCollection coll) where T : UIElementCollection
        {
            // Casting the UIElementCollection into List
            List<FrameworkElement> lstElement = coll.Cast<FrameworkElement>().ToList();
            var lstControl = lstElement.OfType<Control>();
            foreach (Control control in lstControl)
            {
                if (control == null)
                    continue;
                control.Visibility = System.Windows.Visibility.Hidden;
            }
        }

        public static IEnumerable<Control> GetAllControls<T>(this UIElementCollection coll) where T : UIElementCollection
        {
            // Casting the UIElementCollection into List
            List<FrameworkElement> lstElement = coll.Cast<FrameworkElement>().ToList();
            var lstControl = lstElement.OfType<Control>();
            foreach (Control control in lstControl)
            {
                if (control == null)
                    continue;
                yield return control;
            }
        }

        /// <summary>
        /// Display a readable sentence as to when the time will happen.
        /// e.g. "in one second" or "in 2 days"
        /// </summary>
        public static string ToReadableTime(this TimeSpan value)
        {
            double delta = value.TotalSeconds;
            if (delta < 60) { return value.Seconds == 1 ? "one second" : value.Seconds + " seconds"; }
            if (delta < 120) { return "a minute"; }
            if (delta < 3000) { return value.Minutes + " minutes"; } // 50 * 60
            if (delta < 5400) { return "an hour"; } // 90 * 60
            if (delta < 86400) { return value.Hours + " hours"; } // 24 * 60 * 60
            if (delta < 172800) { return "one day"; } // 48 * 60 * 60
            if (delta < 2592000) { return value.Days + " days"; } // 30 * 24 * 60 * 60
            if (delta < 31104000) // 12 * 30 * 24 * 60 * 60
            {
                int months = Convert.ToInt32(Math.Floor((double)value.Days / 30));
                return months <= 1 ? "one month" : months + " months";
            }
            int years = Convert.ToInt32(Math.Floor((double)value.Days / 365));
            return years <= 1 ? "one year" : years + " years";
        }

        /// <summary>
        /// Returns a browser-style file size for the user.
        /// </summary>
        public static string ToFileSize(this long size)
        {
            if (size < 1024) { return (size).ToString("F0") + " Bytes"; }
            if (size < Math.Pow(1024, 2)) { return (size / 1024).ToString("F0") + " KB"; }
            if (size < Math.Pow(1024, 3)) { return (size / Math.Pow(1024, 2)).ToString("F0") + " MB"; }
            if (size < Math.Pow(1024, 4)) { return (size / Math.Pow(1024, 3)).ToString("F0") + " GB"; }
            if (size < Math.Pow(1024, 5)) { return (size / Math.Pow(1024, 4)).ToString("F0") + " TB"; }
            if (size < Math.Pow(1024, 6)) { return (size / Math.Pow(1024, 5)).ToString("F0") + " PB"; }
            return (size / Math.Pow(1024, 6)).ToString("F0") + " EB";
        }
    }
}
