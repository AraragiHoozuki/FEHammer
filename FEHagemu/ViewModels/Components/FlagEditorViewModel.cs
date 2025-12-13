using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using FEHagemu.ViewModels.Tools;

using Avalonia.Media; // Added

namespace FEHagemu.ViewModels.Components
{
    public partial class FlagEditorViewModel : ViewModelBase
    {
        public string Title { get; }
        public Type FlagType { get; }
        private readonly Func<Enum, IImage?>? _iconProvider;

        [ObservableProperty]
        private ulong _currentValue;

        [ObservableProperty]
        private bool _isExpanded;

        public Action<FlagEditorViewModel>? OnExpansionRequested;

        partial void OnIsExpandedChanged(bool value)
        {
            if (value)
            {
                OnExpansionRequested?.Invoke(this);
            }
        }

        public ObservableCollection<FlagItemViewModel> Flags { get; } = new();

        public FlagEditorViewModel(string title, Type flagType, ulong initialValue, Func<Enum, IImage?>? iconProvider = null)
        {
            Title = title;
            FlagType = flagType;
            _currentValue = initialValue; // Don't trigger callback yet
            _iconProvider = iconProvider;
            InitializeFlags();
            UpdateFlagsFromValue();
        }

        private void InitializeFlags()
        {
            var values = Enum.GetValues(FlagType);
            foreach (Enum v in values)
            {
                var uVal = Convert.ToUInt64(v);
                if (uVal != 0 && (uVal & (uVal - 1)) == 0) // Powers of 2 only
                {
                    IImage? icon = _iconProvider?.Invoke(v);
                    Flags.Add(new FlagItemViewModel(v.ToString(), uVal, UpdateValueFromFlags, icon));
                }
            }
        }

        private void UpdateFlagsFromValue()
        {
            foreach (var flag in Flags)
            {
                flag.SetIsCheckedSilent((CurrentValue & flag.Value) == flag.Value);
            }
        }

        partial void OnCurrentValueChanged(ulong value)
        {
            UpdateFlagsFromValue();
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
