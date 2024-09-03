using CommunityToolkit.Mvvm.ComponentModel;
using FEHagemu.HSDArchive;
using System.Linq;

namespace FEHagemu.ViewModels
{
    public partial class ExporterViewModel : ViewModelBase
    {
        [ObservableProperty]
        string[] personArcs = MasterData.PersonArcs.Select(arc => arc.path).ToArray();
        [ObservableProperty]
        string[] skillArcs = MasterData.SkillArcs.Select(arc => arc.path).ToArray();
    }
}
