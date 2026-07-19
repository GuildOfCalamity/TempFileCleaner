using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace TempFileCleaner.Converters
{
    public class FileInfoTooltipConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null && value is string path && File.Exists(path))
            {
                var info = new FileInfo(path);

                return
                    $"Name: {info.Name}\n" +
                    $"Directory: {info.DirectoryName}\n" +
                    $"Size: {info.Length.ToFileSize()}\n" +
                    $"Created: {info.CreationTime}\n" +
                    $"Modified: {info.LastWriteTime}\n" +
                    $"Attributes: {info.Attributes}";
            }

            return "File not found";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
