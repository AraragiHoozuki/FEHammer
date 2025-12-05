using Avalonia.Data.Converters;
using Avalonia.Data;
using Avalonia.Media;
using System;
using System.Globalization;
using FEHagemu.HSDArchive;
using Avalonia.Platform;
using Avalonia.Media.Imaging;

namespace FEHagemu.Converters
{
    public class SkillIconConverter : IValueConverter
    {
        public static readonly SkillIconConverter Instance = new();
        public object? Convert(object? value, Type targetType,
                                        object? parameter, CultureInfo culture)
        {
            if (targetType.IsAssignableTo(typeof(IImage))) {
                if (value is null)
                {
                    return MasterData.GetSkillIcon(0);
                } else if (value is Skill sk)
                {
                    return MasterData.GetSkillIcon((int)sk.icon);
                }
            }
            return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
        }
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class WeaponTypeIconConverter : IValueConverter
    {
        public static readonly WeaponTypeIconConverter Instance = new();
        public object? Convert(object? value, Type targetType,
                                        object? parameter, CultureInfo culture)
        {
            if (targetType.IsAssignableTo(typeof(IImage)))
            {
                if (value is int i)
                {
                    return MasterData.WeaponTypeIcons[i];
                }
            }
            return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
        }
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class FaceConverter : IValueConverter
    {
        public static readonly FaceConverter Instance = new();
        public object? Convert(object? value, Type targetType,
                                        object? parameter, CultureInfo culture)
        {
            if (targetType.IsAssignableTo(typeof(IImage)))
            {
                if (value is string name)
                {
                    if (string.IsNullOrEmpty(name))
                    {
                        return new Bitmap(AssetLoader.Open(new Uri("avares://FEHagemu/Assets/empty.png")));
                    } else
                    {
                        var uri = new Uri($"avares://FEHagemu/Assets/Face/{name}/Face_FC.png");
                        if (AssetLoader.Exists(uri))
                        {
                            return new Bitmap(AssetLoader.Open(uri));
                        }
                        else
                        {
                            uri = new Uri($"avares://FEHagemu/Assets/Face/ch00_00_Eclat_F_Avatar01/Face_FC.png");
                            return new Bitmap(AssetLoader.Open(uri));
                        }
                    }
                }
            }
            return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
        }
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class WeaponIconConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int index)
            {
                return MasterData.GetWeaponIcon(index);
            }
            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }


}
