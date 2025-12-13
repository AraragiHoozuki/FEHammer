using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using FEHagemu.HSDArchive;
using Avalonia.Media;

namespace FEHagemu.ViewModels.Tools
{
    public partial class FlagItemViewModel : ViewModelBase
    {
        public string Name { get; }
        public ulong Value { get; }
        public IImage? Icon { get; }
        private readonly Action _onCheckChanged;
        private bool _suppressNotification;

        public FlagItemViewModel(string name, ulong value, Action onCheckChanged, IImage? icon = null)
        {
            Name = name;
            Value = value;
            _onCheckChanged = onCheckChanged;
            Icon = icon;
        }

        [ObservableProperty]
        private bool _isChecked;

        partial void OnIsCheckedChanged(bool value)
        {
            if (!_suppressNotification)
            {
                _onCheckChanged?.Invoke();
            }
        }

        public void SetIsCheckedSilent(bool value)
        {
            if (_isChecked != value)
            {
                _suppressNotification = true;
                IsChecked = value;
                _suppressNotification = false;
            }
        }
    }

    public partial class FlagCheckToolViewModel : ViewModelBase
    {
        public ObservableCollection<Type> FlagTypes { get; } = new()
        {
            typeof(WeaponTypeFlags),
            typeof(MoveTypeFlags),
            typeof(StatsFlag),
            typeof(SkillFlags),
            typeof(SkillFlags1),
            typeof(SkillFlags2),
            typeof(SkillFlags3),
            typeof(SkillFlags4),
        };

        [ObservableProperty]
        private Type? _selectedFlagType;

        [ObservableProperty]
        private ulong _currentValue;

        public ObservableCollection<FlagItemViewModel> Flags { get; } = new();

        partial void OnSelectedFlagTypeChanged(Type? value)
        {
            Flags.Clear();
            CurrentValue = 0;
            if (value == null) return;

            var values = Enum.GetValues(value);
            foreach (Enum v in values)
            {
                var uVal = Convert.ToUInt64(v);

                // Only add single bit flags (power of 2) and non-zero
                if (uVal != 0 && (uVal & (uVal - 1)) == 0)
                {
                    Flags.Add(new FlagItemViewModel(v.ToString(), uVal, UpdateValueFromFlags));
                }
            }
        }

        partial void OnCurrentValueChanged(ulong value)
        {
            foreach (var flag in Flags)
            {
                flag.SetIsCheckedSilent((value & flag.Value) == flag.Value);
            }
        }

        private void UpdateValueFromFlags()
        {
            ulong newVal = 0;
            foreach (var flag in Flags)
            {
                if (flag.IsChecked) newVal |= flag.Value;
            }
            if (CurrentValue != newVal)
            {
                CurrentValue = newVal;
            }
        }
    }
}
