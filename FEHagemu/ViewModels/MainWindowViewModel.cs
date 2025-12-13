using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using FEHagemu.HSDArchive;
using FEHagemu.HSDArcIO;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Linq;
using System.Collections.ObjectModel;
using FEHagemu.FEHArchive;
using System.IO;
using Ursa.Controls;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace FEHagemu.ViewModels
{
    using FEHagemu.Views.Tools;
    using FEHagemu.ViewModels.Tools;
    public partial class MainWindowViewModel : ViewModelBase
    {
        public MainWindowViewModel()
        {
            LoadMasterData();
        }

        HSDArc<SRPGMap> mapArc = null!;
        SRPGMap mapData = null!;
        [ObservableProperty]
        ObservableCollection<ObservableCollection<MapSpaceViewModel>> mapSpaces = [];
        [ObservableProperty]
        GameBoardViewModel? gameBoard;
        [ObservableProperty]
        bool loadingQ = true;

        async void LoadMasterData()
        {
            await MasterData.LoadAsync();
            LoadingQ = false;
        }

        [RelayCommand]
        async Task ImportSkill()
        {
            var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;
            if (mainWindow is not null)
            {
                var file = await mainWindow.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions()
                {
                    Title = "Import skill json",
                    AllowMultiple = false,
                });
                if (file.Count > 0)
                {
                    await using var stream = await file[0].OpenReadAsync();
                    using var streamReader = new StreamReader(stream);
                    string json = await streamReader.ReadToEndAsync();
                    Skill? s = JsonSerializer.Deserialize<Skill>(json, new JsonSerializerOptions()
                    {
                        IncludeFields = true,
                        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                        IgnoreReadOnlyProperties = true,
                    });
                    var skill_arc = MasterData.SkillArcs.FirstOrDefault(arc => arc.path.EndsWith("Tutorial.bin.lz"));
                    var msg_arc = MasterData.MsgArcs.FirstOrDefault(arc => arc.path.EndsWith("Tutorial.bin.lz"));
                    bool skill_modified = false;

                    if (s != null && skill_arc != null && msg_arc != null)
                    {
                        string name = s.name;
                        string description = s.description;
                        string id_name = MasterData.StripIdPrefix(s.id, out _);
                        bool useBuiltinDescription = true;
                        if (!id_name.Contains("MOD")) id_name = id_name + "MOD";
                        s.id = "SID_" + id_name;
                        if (!s.name.StartsWith("MSID_"))
                        {
                            s.name = $"MSID_{id_name}";
                        }
                        if (!s.description.StartsWith("MSID_H_"))
                        {
                            s.description = $"MSID_H_{id_name}";
                            useBuiltinDescription = false;
                        }

                        var found = MasterData.GetSkill(s.id);
                        if (found is not null)
                        {
                            if (!id_name.Contains("MOD"))
                            {
                                await MessageBox.ShowOverlayAsync("Cannot overwrite built-in skills", "Error", icon: MessageBoxIcon.Error);
                                return;
                            }
                            else
                            {
                                MasterData.AddSkill(skill_arc, s);
                                MasterData.AddMessage(msg_arc, s.name, name);
                                if (!useBuiltinDescription) MasterData.AddMessage(msg_arc, s.description, description);
                                skill_modified = true;
                            }
                        }
                        else
                        {
                            MasterData.AddSkill(skill_arc, s);
                            MasterData.AddMessage(msg_arc, s.name, name);
                            if (!useBuiltinDescription) MasterData.AddMessage(msg_arc, s.description, description);
                            skill_modified = true;
                        }

                        if (!skill_modified) return;
                    }

                }
            }
        }
        [RelayCommand]
        async Task ImportPerson()
        {
            var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;
            if (mainWindow is not null)
            {
                var file = await mainWindow.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions()
                {
                    Title = "Import person json",
                    AllowMultiple = false,
                });
                if (file.Count > 0)
                {
                    await using var stream = await file[0].OpenReadAsync();
                    using var streamReader = new StreamReader(stream);
                    string json = await streamReader.ReadToEndAsync();

                    var msg_arc = MasterData.MsgArcs.FirstOrDefault(arc => arc.path.EndsWith("Tutorial.bin.lz"));
                    if (file[0].Name.StartsWith("PID_"))
                    {

                        Person? p = JsonSerializer.Deserialize<Person>(json, new JsonSerializerOptions()
                        {
                            IncludeFields = true,
                            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                            IgnoreReadOnlyProperties = true,
                        });

                        if (p is not null)
                        {
                            string pid = p.Id;
                            var person_arc = MasterData.GetPersonArc(pid);
                            if (person_arc is not null)
                            {
                                MasterData.AddPerson(person_arc, p);
                                person_arc.Save();
                                PackageArchive(person_arc);
                            }
                        }
                    }
                    else if (file[0].Name.StartsWith("EID_"))
                    {
                        Enemy? p = JsonSerializer.Deserialize<Enemy>(json, new JsonSerializerOptions()
                        {
                            IncludeFields = true,
                            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                            IgnoreReadOnlyProperties = true,
                        });

                        if (p is not null)
                        {
                            string pid = p.Id;
                            var enemy_arc = MasterData.GetEnemyArc(pid);
                            if (enemy_arc is not null)
                            {
                                MasterData.AddEnemy(enemy_arc, p);
                                enemy_arc.Save();
                                PackageArchive(enemy_arc);
                            }
                        }
                    }
                }
            }
        }

        private void PackageArchive<T>(HSDArc<T> arc) where T : new()
        {
            var root = Path.GetDirectoryName(mapArc.FilePath);
            var assets = Directory.CreateDirectory(root + "\\assets");
            if (assets != null)
            {

                DirectoryInfo common = assets.CreateSubdirectory("Common");
                DirectoryInfo srpg = common.CreateSubdirectory("SRPG");
                string? folder_name = Path.GetFileName(Path.GetDirectoryName(arc.path));
                if (folder_name is not null)
                {
                    DirectoryInfo folder = srpg.CreateSubdirectory(folder_name);
                    byte[] buffer = File.ReadAllBytes(arc.path);
                    File.WriteAllBytes(Path.Combine(folder.FullName, Path.GetFileName(arc.path)), buffer);
                }
            }
        }
        [RelayCommand]
        async Task OpenMap()
        {
            var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;
            if (mainWindow is not null)
            {
                var files = await mainWindow.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions()
                {
                    Title = "Open SRPG Map",
                    AllowMultiple = false
                });
                if (files.Count > 0)
                {
                    mapArc = new HSDArc<SRPGMap>(files[0].Path.AbsolutePath);
                    mapData = mapArc.data;
                    if (GameBoard is not null)
                    {
                        GameBoard.SetMap(mapData);
                    }
                    else
                    {
                        GameBoard = new GameBoardViewModel(mapData);
                    }

                }
            }
        }



        [ObservableProperty]
        BoardUnitViewModel? selectedUnit;
        [RelayCommand]
        async Task SaveMap()
        {
            if (GameBoard is null)
            {
                await MessageBox.ShowAsync("Cannot save without opening a map!", "Error", MessageBoxIcon.Error, MessageBoxButton.OK);
                return;
            }
            mapData.player_positions = GameBoard.Cells.SelectMany(cell => cell).Where(cell => cell.IsPlayerSlot).Select(cell => new Position()
            {
                x = cell.X,
                y = cell.Y,
                x2 = 0,
                y2 = 0
            }).ToArray();
            mapData.player_count = (uint)mapData.player_positions.Length;
            mapData.map_units = GameBoard.Units.Select(uvm => uvm.unit).ToArray();
            mapData.unit_count = (uint)mapData.map_units.Length;
            uint w = mapData.field.width; uint h = mapData.field.height;
            for (int i = 0; i < h; i++)
            {
                int view_y = (int)(h - i - 1);
                for (int j = 0; j < w; j++)
                {
                    mapData.field.terrains[i * w + j].tid = (byte)GameBoard.Cells[view_y][j].Terrain;
                }
            }
            await mapArc.Save();
            await MasterData.ModSkillArc.Save();
            await MasterData.ModMsgArc.Save();
        }
        [RelayCommand]
        async Task ExportPackage()
        {
            if (mapArc is null || GameBoard is null) return;

            var root = Path.GetDirectoryName(mapArc.FilePath);
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return;
            var assetsPath = Path.Combine(root, "assets");
            var skillDir = Directory.CreateDirectory(Path.Combine(assetsPath, "Common", "SRPG", "Skill"));
            var personDir = Directory.CreateDirectory(Path.Combine(assetsPath, "Common", "SRPG", "Person"));
            var enemyDir = Directory.CreateDirectory(Path.Combine(assetsPath, "Common", "SRPG", "Enemy"));
            var msgDataDir = Directory.CreateDirectory(Path.Combine(assetsPath, "TWZH", "Message", "Data"));
            var srpgMapDir = Directory.CreateDirectory(Path.Combine(assetsPath, "Common", "SRPGMap"));

            var skillSource = MasterData.SkillArcs.FirstOrDefault(arc => arc.path.EndsWith("Tutorial.bin.lz"));
            if (skillSource != null)
            {
                var destFile = Path.Combine(skillDir.FullName, "Tutorial.bin.lz");
                File.Copy(skillSource.path, destFile, true);
            }
            var msgSource = MasterData.MsgArcs.FirstOrDefault(arc => arc.path.EndsWith("Data_Tutorial.bin.lz"));
            if (msgSource != null)
            {
                var destFile = Path.Combine(msgDataDir.FullName, "Data_Tutorial.bin.lz");
                File.Copy(msgSource.path, destFile, true);
            }
            await SaveMap();

            string mapSourcePath = mapArc.FilePath;
            string mapFileName = Path.GetFileName(mapSourcePath);
            string mapDestPath = Path.Combine(srpgMapDir.FullName, mapFileName);
            File.Copy(mapSourcePath, mapDestPath, true);

        }
        [RelayCommand]
        async Task ExportSkills()
        {
            var skillList = MasterData.SkillDict.Values.ToList();
            string jsonString = JsonSerializer.Serialize(skillList, new JsonSerializerOptions()
            {
                IncludeFields = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                WriteIndented = true,
            });
            var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;
            if (mainWindow is not null)
            {
                var file = await mainWindow.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions()
                {
                    Title = "Export skills",
                    SuggestedFileName = $"SRPG_Skills.json",
                });
                if (file is not null)
                {
                    await using var stream = await file.OpenWriteAsync();
                    using var streamWriter = new StreamWriter(stream);
                    await streamWriter.WriteAsync(jsonString);
                }
            }
        }

        [RelayCommand]
        void OpenSkillEditor()
        {
            var window = new SkillEditorWindow();
            window.DataContext = new SkillEditorViewModel();
            window.Show();
        }

        [RelayCommand]
        void OpenFlagTool()
        {
            var window = new FlagCheckToolWindow();
            window.DataContext = new FlagCheckToolViewModel();
            window.Show();
        }
    }
}
