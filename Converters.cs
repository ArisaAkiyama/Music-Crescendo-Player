using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
namespace DesktopMusicPlayer
{

    /// <summary>

    /// Converts boolean to Spotify green color for toggle buttons (shuffle, repeat)

    /// </summary>

    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isEnabled && isEnabled)
            {
                return new SolidColorBrush(Color.FromRgb(135, 206, 235)); // #87CEEB
            }
            return new SolidColorBrush(Color.FromRgb(179, 179, 179)); // #B3B3B3
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>

    /// Converts IsPlaying boolean to Play/Pause icon glyph

    /// </summary>

    public class BoolToPlayPauseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isPlaying && isPlaying)
            {
                return "\uE769"; // Pause icon
            }
            return "\uE768"; // Play icon
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>

    /// Converts bool to Visibility (true = Visible, false = Collapsed)

    /// </summary>

    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && boolValue)
            {
                return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>

    /// Converts bool to Visibility inverse (true = Collapsed, false = Visible)

    /// </summary>

    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && boolValue)
            {
                return Visibility.Collapsed;
            }
            return Visibility.Visible;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>

    /// Converts empty string to Visible (for placeholder text)

    /// </summary>

    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str && string.IsNullOrEmpty(str))
            {
                return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>

    /// Converts null to Visibility (not null = Visible, null = Collapsed)

    /// </summary>

    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>

    /// Multi-value converter to calculate progress bar fill width

    /// </summary>

    public class ProgressWidthConverter : IMultiValueConverter
    {
        public static readonly ProgressWidthConverter Instance = new ProgressWidthConverter();
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 3 &&
            values[0] is double value &&
            values[1] is double maximum &&
            values[2] is double actualWidth &&
            maximum > 0)
            {
                return (value / maximum) * actualWidth;
            }
            return 0.0;
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>

    /// Converts count to Visibility (0 = Visible, >0 = Collapsed)

    /// Used for "empty" messages

    /// </summary>

    public class ZeroToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count && count == 0)
            {
                return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>

    /// Inverts a boolean value (true -> false, false -> true)

    /// Used for disabling menu items on Liked Songs playlist

    /// </summary>

    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return true;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return false;
        }
    }

    /// <summary>

    /// Combines Song and Playlist into a Tuple for Add to Playlist command

    /// </summary>

    public class SongPlaylistCommandParameterConverter : IMultiValueConverter
    {
        public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 2)
            {
                return Tuple.Create(values[0], values[1]);
            }
            return null;
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>

    /// Returns green color if song is currently playing, white otherwise

    /// </summary>

    public class IsCurrentSongConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is Models.Song song && values[1] is Models.Song currentSong)
            {
                if (song.FilePath.Equals(currentSong.FilePath, StringComparison.OrdinalIgnoreCase))
                {
                    return new SolidColorBrush(Color.FromRgb(135, 206, 235)); // Spotify green #87CEEB
                }
            }
            // Default color based on parameter (Title=white, Artist=gray)
            if (parameter?.ToString() == "Artist")
            {
                return new SolidColorBrush(Color.FromRgb(179, 179, 179)); // #B3B3B3
            }
            return new SolidColorBrush(Colors.White);
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>

    /// Adds 1 to an integer value (used for 1-based indexing)

    /// </summary>

    public class AddOneConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue)
            {
                return intValue + 1;
            }
            return value;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>

    /// Returns true if value is less than parameter

    /// </summary>

    public class LessThanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double doubleVal && double.TryParse(parameter?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double threshold))
            {
                return doubleVal < threshold;
            }
            return false;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>

    /// Returns true if value is greater than parameter

    /// </summary>

    public class GreaterThanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double doubleVal && double.TryParse(parameter?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double threshold))
            {
                return doubleVal > threshold;
            }
            return false;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>

    /// Returns true if the current row's FilePath matches CurrentSong.FilePath AND IsPlaying is true

    /// </summary>

    public class IsCurrentAndPlayingConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 3)
            {
                var rowFilePath = values[0] as string;
                var currentFilePath = values[1] as string;
                var isPlaying = values[2] is bool playing && playing;
                if (!string.IsNullOrEmpty(rowFilePath) && !string.IsNullOrEmpty(currentFilePath))
                {
                    return rowFilePath.Equals(currentFilePath, StringComparison.OrdinalIgnoreCase) && isPlaying;
                }
            }
            return false;
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>

    /// Returns true if the two string values are equal

    /// </summary>

    public class IsSameSongConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2)
            {
                var path1 = values[0] as string;
                var path2 = values[1] as string;
                if (!string.IsNullOrEmpty(path1) && !string.IsNullOrEmpty(path2))
                {
                    return path1 == path2;
                }
            }
            return false;
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>

    /// Gets the row number (1-based index) from a CollectionView for a given item

    /// </summary>

    public class RowNumberConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is Models.Song song && values[1] is System.ComponentModel.ICollectionView collectionView)
            {
                int index = 0;
                foreach (var item in collectionView)
                {
                    index++;
                    if (item is Models.Song s && s.FilePath.Equals(song.FilePath, StringComparison.OrdinalIgnoreCase))
                    {
                        return index.ToString();
                    }
                }
            }
            return "1";
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>

    /// Checks if an item exists in a collection. Returns Visibility.Visible if true, Collapsed if false.

    /// Values[0]: Item (Song)

    /// Values[1]: Collection (IEnumerable)

    /// </summary>

    public class IsItemInCollectionConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] != null && values[1] is System.Collections.IEnumerable collection)
            {
                foreach (var item in collection)
                {
                    if (item == values[0])
                    {
                        return Visibility.Visible;
                    }
                }
            }
            return Visibility.Collapsed;
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}