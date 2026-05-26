using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace FEHagemu.ViewModels.Tools
{
    public partial class EnhanceItemViewModel : ObservableObject
    {
        public int Id { get; }
        public IImage? Icon { get; }
        public string Description { get; }
        public string FormalDescription { get; }

        [ObservableProperty]
        private bool isExpanded;

        public EnhanceItemViewModel(int id)
        {
            Id = id;
            Icon = MasterData.GetEnhanceIcon(id);
            Description = MasterData.GetMessage($"MID_ENHANCE_HELP_{id}");
            FormalDescription = MasterData.GetMessage($"MID_ENHANCE_FORMAL_HELP_{id}");
        }
    }

    public partial class EnhanceViewerViewModel : ObservableObject
    {
        public ObservableCollection<EnhanceItemViewModel> Items { get; } = [];

        public EnhanceViewerViewModel()
        {
            int total = MasterData.EnhanceGridCols * MasterData.EnhanceGridRows;
            for (int i = 0; i < total; i++)
            {
                var icon = MasterData.GetEnhanceIcon(i);
                if (icon is null) continue;
                Items.Add(new EnhanceItemViewModel(i));
            }
        }
    }
}
