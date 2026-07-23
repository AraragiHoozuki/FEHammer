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
using System;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace FEHagemu.ViewModels
{
    using FEHagemu.Views;
    using FEHagemu.Views.Tools;
    using FEHagemu.ViewModels.Tools;

    public sealed class MessageLanguageItem
    {
        public MessageLanguageItem(
            string code,
            bool isSupported,
            bool isSelected,
            Func<MessageLanguageItem, Task> select)
        {
            Code = code;
            IsSupported = isSupported;
            IsSelected = isSelected;
            SelectCommand = new AsyncRelayCommand(() => select(this), () => IsSupported);
        }

        public string Code { get; }
        public bool IsSupported { get; }
        public bool IsSelected { get; }
        public IAsyncRelayCommand SelectCommand { get; }
        public override string ToString() => IsSupported ? Code : $"{Code}（暂不支持）";
    }

    public sealed class MapListItem
    {
        public MapListItem(string filePath)
        {
            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
            DisplayName = Path.GetFileNameWithoutExtension(
                Path.GetFileNameWithoutExtension(FileName));
        }

        public string FilePath { get; }
        public string FileName { get; }
        public string DisplayName { get; }
    }

    public partial class MainWindowViewModel : ViewModelBase
    {
        public MainWindowViewModel()
        {
            LoadMasterData();
        }

        HSDArc<SRPGMap>? mapArc;
        SRPGMap? mapData;
        private readonly System.Collections.Generic.List<MapListItem> allMaps = [];
        private bool mapBrowserInitialized;
        [ObservableProperty]
        ObservableCollection<ObservableCollection<MapSpaceViewModel>> mapSpaces = [];
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasOpenMap))]
        GameBoardViewModel? gameBoard;
        public bool HasOpenMap => GameBoard is not null;
        public ObservableCollection<MapListItem> FilteredMaps { get; } = [];
        [ObservableProperty]
        MapListItem? selectedMap;
        [ObservableProperty]
        string? mapSearchText;
        [ObservableProperty]
        string mapResultCountText = string.Empty;
        [ObservableProperty]
        string mapStatusText = "请选择地图";
        [ObservableProperty]
        bool savingMap;
        partial void OnMapSearchTextChanged(string? value) => ApplyMapFilter();
        partial void OnSelectedMapChanged(MapListItem? value)
        {
            if (value is not null
                && !string.Equals(mapArc?.FilePath, value.FilePath, StringComparison.OrdinalIgnoreCase))
                LoadMap(value.FilePath);
        }
        [ObservableProperty]
        PersonSelectorViewModel? personBrowser;
        [ObservableProperty]
        SkillSelectorViewModel? skillBrowser;
        [ObservableProperty]
        MessageBrowserViewModel? messageBrowser;
        [ObservableProperty]
        RestoreBrowserViewModel? restoreBrowser;
        [ObservableProperty]
        int selectedMainTabIndex;
        private bool masterDataLoaded;
        partial void OnSelectedMainTabIndexChanged(int value)
        {
            if (masterDataLoaded) EnsureSelectedBrowser(value);
        }
        [ObservableProperty]
        bool loadingQ = true;
        public ObservableCollection<MessageLanguageItem> MessageLanguages { get; } = [];
        [ObservableProperty]
        MessageLanguageItem? selectedMessageLanguage;
        [ObservableProperty]
        string masterDataSourceText = MasterData.SourceDescription;
        [ObservableProperty]
        string masterDataWritebackText = MasterData.WritebackDescription;
        [ObservableProperty]
        string masterDataStatus = "正在加载 MasterData...";

        async void LoadMasterData()
        {
            await ReloadMasterDataCore(showError: false);
        }

        [RelayCommand]
        async Task SelectMasterDataVmdk()
        {
            var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
            if (mainWindow is null) return;

            var files = await mainWindow.StorageProvider.OpenFilePickerAsync(
                new Avalonia.Platform.Storage.FilePickerOpenOptions
                {
                    Title = "Select LDPlayer data.vmdk",
                    AllowMultiple = false,
                    FileTypeFilter =
                    [
                        new Avalonia.Platform.Storage.FilePickerFileType("VMDK disk image")
                        {
                            Patterns = ["*.vmdk"]
                        }
                    ]
                });
            if (files.Count == 0) return;

            if (!await TryConfigureMasterData(
                    () => MasterData.ConfigureVmdkSource(
                        files[0].Path.LocalPath,
                        SelectedMessageLanguage?.Code ?? MasterData.MessageLanguage)))
                return;
            MasterDataSourceText = MasterData.SourceDescription;
            await ReloadMasterDataCore(showError: true);
        }

        [RelayCommand]
        async Task ReloadMasterData()
        {
            await ReloadMasterDataCore(showError: true);
        }

        [RelayCommand]
        async Task ConfigureLdPlayerWriteback()
        {
            var owner = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
            if (owner is null) return;

            var window = new LdPlayerWritebackSettingsWindow
            {
                DataContext = new LdPlayerWritebackSettingsViewModel()
            };
            bool saved = await window.ShowDialog<bool>(owner);
            if (saved) await ReloadMasterDataCore(showError: true);
        }

        async Task ChangeMessageLanguage(MessageLanguageItem selected)
        {
            if (!selected.IsSupported)
            {
                MasterDataStatus = $"{selected.Code} 语言文件尚未下载，暂不支持";
                return;
            }
            if (string.Equals(selected.Code, MasterData.MessageLanguage, StringComparison.OrdinalIgnoreCase))
                return;

            LoadingQ = true;
            MasterDataStatus = $"正在加载 {selected.Code} Message...";
            bool loaded = await MasterData.ReloadMessagesAsync(selected.Code);
            if (loaded && MessageBrowser is not null)
                MessageBrowser = new MessageBrowserViewModel();
            RefreshMessageLanguages();
            MasterDataStatus = loaded
                ? $"已切换文本语言：{MasterData.MessageLanguage}，{MasterData.MsgDict.Count} 条文本"
                : $"语言切换失败：{MasterData.LastLoadError}";
            LoadingQ = false;

            if (!loaded)
            {
                await MessageBox.ShowAsync(
                    MasterData.LastLoadError ?? "Unknown Message load error.",
                    "MasterData",
                    MessageBoxIcon.Error,
                    MessageBoxButton.OK);
            }
        }

        [RelayCommand]
        async Task UseLocalMasterData()
        {
            if (!await TryConfigureMasterData(() => MasterData.ConfigureLocalSource(MasterData.MessageLanguage)))
                return;
            await ReloadMasterDataCore(showError: true);
        }

        [RelayCommand]
        async Task ClearMasterDataCache()
        {
            var result = await MessageBox.ShowAsync(
                "删除缓存将清除所有本地修改、备份和待同步数据。\n\n"
                + "如果清除前没有先还原模拟器中的修改，清除后将无法自动还原。\n\n"
                + "是否继续？",
                "删除 MasterData 缓存",
                MessageBoxIcon.Warning,
                MessageBoxButton.YesNo);
            if (result != MessageBoxResult.Yes) return;

            LoadingQ = true;
            MasterDataStatus = "正在删除 MasterData 缓存...";
            try
            {
                await MasterData.ClearCacheAsync();
                await ReloadMasterDataCore(showError: true);
                if (masterDataLoaded)
                    MasterDataStatus = "缓存已完全清除，并已从当前数据源重新加载";
            }
            catch (Exception ex)
            {
                LoadingQ = false;
                MasterDataStatus = $"删除缓存失败：{ex.Message}";
                await MessageBox.ShowAsync(
                    ex.Message,
                    "删除缓存失败",
                    MessageBoxIcon.Error,
                    MessageBoxButton.OK);
            }
        }

        private static async Task<bool> TryConfigureMasterData(Action configure)
        {
            try
            {
                configure();
                return true;
            }
            catch (Exception ex)
            {
                await MessageBox.ShowAsync(ex.Message, "MasterData", MessageBoxIcon.Error, MessageBoxButton.OK);
                return false;
            }
        }

        private async Task ReloadMasterDataCore(bool showError)
        {
            LoadingQ = true;
            MasterDataStatus = "正在加载 MasterData...";
            masterDataLoaded = false;
            ResetMapBrowser();
            PersonBrowser = null;
            SkillBrowser = null;
            MessageBrowser = null;
            RestoreBrowser = null;

            bool loaded = await MasterData.LoadAsync();
            if (loaded)
            {
                masterDataLoaded = true;
                EnsureSelectedBrowser(SelectedMainTabIndex);
            }
            MasterDataSourceText = MasterData.SourceDescription;
            MasterDataWritebackText = MasterData.WritebackDescription;
            RefreshMessageLanguages();
            MasterDataStatus = loaded
                ? $"已加载：{MasterData.PersonDict.Count} 个角色，{MasterData.SkillDict.Count} 个技能"
                : $"加载失败：{MasterData.LastLoadError}";
            LoadingQ = false;

            if (!loaded && showError)
            {
                await MessageBox.ShowAsync(
                    MasterData.LastLoadError ?? "Unknown MasterData load error.",
                    "MasterData",
                    MessageBoxIcon.Error,
                    MessageBoxButton.OK);
            }
        }

        private void RefreshMessageLanguages()
        {
            SelectedMessageLanguage = null;
            MessageLanguages.Clear();
            foreach (string language in MasterData.AvailableMessageLanguages)
            {
                bool isSelected = string.Equals(
                    language,
                    MasterData.MessageLanguage,
                    StringComparison.OrdinalIgnoreCase);
                var item = new MessageLanguageItem(
                    language,
                    MasterData.IsMessageLanguageSupported(language),
                    isSelected,
                    ChangeMessageLanguage);
                MessageLanguages.Add(item);
                if (isSelected) SelectedMessageLanguage = item;
            }
        }

        private void EnsureSelectedBrowser(int tabIndex)
        {
            switch (tabIndex)
            {
                case 0 when !mapBrowserInitialized:
                    RefreshMapBrowser();
                    break;
                case 1 when PersonBrowser is null:
                    RefreshPersonBrowser();
                    break;
                case 2 when SkillBrowser is null:
                    RefreshSkillBrowser();
                    break;
                case 3 when MessageBrowser is null:
                    MessageBrowser = new MessageBrowserViewModel();
                    break;
                case 4:
                    if (RestoreBrowser is null)
                    {
                        RestoreBrowser = new RestoreBrowserViewModel
                        {
                            OnRestoreCompleted = () => ReloadMasterDataCore(showError: true)
                        };
                    }
                    else
                    {
                        RestoreBrowser.Refresh();
                    }
                    break;
            }
        }

        private void ResetMapBrowser()
        {
            mapArc = null;
            mapData = null;
            GameBoard = null;
            SelectedMap = null;
            MapSearchText = null;
            allMaps.Clear();
            FilteredMaps.Clear();
            MapResultCountText = string.Empty;
            MapStatusText = "请选择地图";
            mapBrowserInitialized = false;
        }

        private void RefreshMapBrowser()
        {
            mapBrowserInitialized = true;
            allMaps.Clear();

            if (Directory.Exists(MasterData.MAP_PATH))
            {
                allMaps.AddRange(Directory
                    .EnumerateFiles(MasterData.MAP_PATH, MasterData.DATAEXT, SearchOption.TopDirectoryOnly)
                    .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                    .Select(path => new MapListItem(path)));
            }

            ApplyMapFilter();
            MapStatusText = allMaps.Count > 0
                ? "从左侧选择地图以开始浏览或编辑"
                : "Common/SRPGMap 中没有可用的地图文件";
        }

        private void ApplyMapFilter()
        {
            string search = MapSearchText?.Trim() ?? string.Empty;
            var matches = string.IsNullOrEmpty(search)
                ? allMaps
                : allMaps.Where(item =>
                    item.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || item.FileName.Contains(search, StringComparison.OrdinalIgnoreCase));

            FilteredMaps.Clear();
            foreach (MapListItem item in matches)
                FilteredMaps.Add(item);
            MapResultCountText = $"{FilteredMaps.Count} / {allMaps.Count}";
        }

        [RelayCommand]
        async Task ImportSkill()
        {
            var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;
            if (mainWindow is null) return;

            var files = await mainWindow.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Import skill json",
                AllowMultiple = false,
            });
            if (files.Count == 0) return;

            try
            {
                await using var stream = await files[0].OpenReadAsync();
                using var streamReader = new StreamReader(stream);
                string json = await streamReader.ReadToEndAsync();
                Skill skill = JsonSerializer.Deserialize<Skill>(json, new JsonSerializerOptions
                {
                    IncludeFields = true,
                    Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                    IgnoreReadOnlyProperties = true,
                }) ?? throw new InvalidDataException("技能 JSON 内容无效。");
                if (!skill.id.StartsWith("SID_", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("技能 ID 必须以 SID_ 开头。");

                var skillArc = MasterData.ModSkillArc
                    ?? throw new InvalidOperationException("Skill Tutorial.bin.lz was not found.");
                var messageArc = MasterData.ModMsgArc
                    ?? throw new InvalidOperationException("Message Tutorial.bin.lz was not found.");

                string sourceId = skill.id;
                string nameContent = MasterData.GetMessage(skill.name) ?? skill.name;
                string descriptionContent = MasterData.GetMessage(skill.description) ?? skill.description;
                bool updateExisting = skillArc.data.list.Any(item =>
                    string.Equals(item.id, sourceId, StringComparison.Ordinal)
                    && item.id.Contains("MOD", StringComparison.OrdinalIgnoreCase));
                if (!updateExisting)
                {
                    skill.id = MasterData.CreateUniqueModId(
                        sourceId,
                        "SID_",
                        candidate => MasterData.SkillDict.ContainsKey(candidate));
                }

                string idBody = MasterData.StripIdPrefix(skill.id, out _);
                skill.name = $"MSID_{idBody}";
                skill.description = $"MSID_H_{idBody}";
                MasterData.UpsertModSkill(skill, updateExisting ? sourceId : null);
                MasterData.AddMessage(messageArc, skill.name, nameContent);
                MasterData.AddMessage(messageArc, skill.description, descriptionContent);
                await skillArc.Save();
                await messageArc.Save();
                var writeback = await MasterData.WriteBackFilesAsync(
                    [skillArc.FilePath, messageArc.FilePath]);
                RefreshSkillBrowser();
                await MessageBox.ShowOverlayAsync(
                    $"技能 {skill.id} 已导入并保存到 {writeback.DestinationText}。",
                    "导入成功");
            }
            catch (Exception ex)
            {
                await MessageBox.ShowOverlayAsync(ex.Message, "导入技能失败", icon: MessageBoxIcon.Error);
            }
        }
        [RelayCommand]
        async Task ImportPerson()
        {
            var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;
            if (mainWindow is null) return;

            var files = await mainWindow.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Import person json",
                AllowMultiple = false,
            });
            if (files.Count == 0) return;

            try
            {
                await using var stream = await files[0].OpenReadAsync();
                using var streamReader = new StreamReader(stream);
                string json = await streamReader.ReadToEndAsync();
                var options = new JsonSerializerOptions
                {
                    IncludeFields = true,
                    Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                    IgnoreReadOnlyProperties = true,
                };

                var messageArc = MasterData.ModMsgArc
                    ?? throw new InvalidOperationException("Message Tutorial.bin.lz was not found.");

                if (files[0].Name.StartsWith("PID_", StringComparison.OrdinalIgnoreCase))
                {
                    Person person = JsonSerializer.Deserialize<Person>(json, options)
                        ?? throw new InvalidDataException("角色 JSON 内容无效。");
                    if (!person.id.StartsWith("PID_", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("角色 ID 必须以 PID_ 开头。");

                    var personArc = MasterData.ModPersonArc
                        ?? throw new InvalidOperationException("Person Tutorial.bin.lz was not found.");
                    string sourceId = person.id;
                    string sourceBody = MasterData.StripIdPrefix(sourceId, out string sourcePrefix);
                    string nameContent = MasterData.GetMessage("M" + sourceId) ?? sourceBody;
                    string titleContent = MasterData.GetMessage("M" + sourcePrefix + "HONOR_" + sourceBody) ?? string.Empty;
                    bool updateExisting = personArc.data.list.Any(item =>
                        string.Equals(item.id, sourceId, StringComparison.Ordinal)
                        && item.id.Contains("MOD", StringComparison.OrdinalIgnoreCase));
                    if (!updateExisting)
                    {
                        person.id = MasterData.CreateUniqueModId(
                            sourceId,
                            "PID_",
                            candidate => MasterData.PersonDict.ContainsKey(candidate));
                    }

                    MasterData.UpsertModPerson(person, updateExisting ? sourceId : null);
                    MasterData.AddMessage(messageArc, "M" + person.id, nameContent);
                    string body = MasterData.StripIdPrefix(person.id, out string prefix);
                    MasterData.AddMessage(messageArc, "M" + prefix + "HONOR_" + body, titleContent);
                    await personArc.Save();
                    await messageArc.Save();
                    var writeback = await MasterData.WriteBackFilesAsync(
                        [personArc.FilePath, messageArc.FilePath]);
                    PackageArchive(personArc);
                    RefreshPersonBrowser();
                    await MessageBox.ShowOverlayAsync(
                        $"角色 {person.id} 已导入并保存到 {writeback.DestinationText}。",
                        "导入成功");
                }
                else if (files[0].Name.StartsWith("EID_", StringComparison.OrdinalIgnoreCase))
                {
                    Enemy enemy = JsonSerializer.Deserialize<Enemy>(json, options)
                        ?? throw new InvalidDataException("敌方 JSON 内容无效。");
                    if (!enemy.id.StartsWith("EID_", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("敌方 ID 必须以 EID_ 开头。");

                    var enemyArc = MasterData.ModEnemyArc
                        ?? throw new InvalidOperationException("Enemy Tutorial.bin.lz was not found.");
                    string sourceId = enemy.id;
                    string nameContent = MasterData.GetMessage("M" + sourceId)
                        ?? MasterData.StripIdPrefix(sourceId, out _);
                    bool updateExisting = enemyArc.data.list.Any(item =>
                        string.Equals(item.id, sourceId, StringComparison.Ordinal)
                        && item.id.Contains("MOD", StringComparison.OrdinalIgnoreCase));
                    if (!updateExisting)
                    {
                        enemy.id = MasterData.CreateUniqueModId(
                            sourceId,
                            "EID_",
                            candidate => MasterData.EnemyDict.ContainsKey(candidate));
                    }

                    MasterData.AddEnemy(enemyArc, enemy);
                    MasterData.AddMessage(messageArc, "M" + enemy.id, nameContent);
                    await enemyArc.Save();
                    await messageArc.Save();
                    var writeback = await MasterData.WriteBackFilesAsync(
                        [enemyArc.FilePath, messageArc.FilePath]);
                    PackageArchive(enemyArc);
                    RefreshPersonBrowser();
                    await MessageBox.ShowOverlayAsync(
                        $"敌方 {enemy.id} 已导入并保存到 {writeback.DestinationText}。",
                        "导入成功");
                }
                else
                {
                    throw new InvalidDataException("文件名必须以 PID_ 或 EID_ 开头。");
                }
            }
            catch (Exception ex)
            {
                await MessageBox.ShowOverlayAsync(ex.Message, "导入角色失败", icon: MessageBoxIcon.Error);
            }
        }

        private void PackageArchive<T>(HSDArc<T> arc) where T : new()
        {
            if (mapArc is null || string.IsNullOrWhiteSpace(arc.path) || !File.Exists(arc.path)) return;

            string? root = Path.GetDirectoryName(mapArc.FilePath);
            if (string.IsNullOrEmpty(root)) return;

            var folderName = Path.GetFileName(Path.GetDirectoryName(arc.path));
            if (string.IsNullOrEmpty(folderName)) return;

            var targetDir = Path.Combine(root, "assets", "Common", "SRPG", folderName);
            Directory.CreateDirectory(targetDir);
            var destFile = Path.Combine(targetDir, Path.GetFileName(arc.path));
            File.Copy(arc.path, destFile, true);
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
                    SelectedMainTabIndex = 0;
                    EnsureSelectedBrowser(0);
                    string path = files[0].Path.LocalPath;
                    MapListItem? listedMap = allMaps.FirstOrDefault(item =>
                        string.Equals(item.FilePath, path, StringComparison.OrdinalIgnoreCase));
                    if (listedMap is not null)
                    {
                        if (ReferenceEquals(SelectedMap, listedMap))
                            LoadMap(path);
                        else
                            SelectedMap = listedMap;
                    }
                    else
                    {
                        SelectedMap = null;
                        LoadMap(path);
                    }
                }
            }
        }

        private bool LoadMap(string path)
        {
            try
            {
                var nextArc = new HSDArc<SRPGMap>(path);
                SRPGMap nextMap = nextArc.data;
                mapArc = nextArc;
                mapData = nextMap;

                if (GameBoard is null)
                    GameBoard = new GameBoardViewModel(nextMap);
                else
                    GameBoard.SetMap(nextMap);

                MapStatusText = $"{Path.GetFileName(path)} · {nextMap.field.width} × {nextMap.field.height}";
                return true;
            }
            catch (Exception ex)
            {
                MapStatusText = $"地图加载失败：{ex.Message}";
                return false;
            }
        }



        [ObservableProperty]
        BoardUnitViewModel? selectedUnit;
        [RelayCommand]
        async Task SaveMap()
        {
            if (SavingMap) return;
            if (GameBoard is null)
            {
                await MessageBox.ShowAsync("Cannot save without opening a map!", "Error", MessageBoxIcon.Error, MessageBoxButton.OK);
                return;
            }
            if (mapArc is null || mapData is null)
                return;

            SavingMap = true;
            try
            {
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
                uint w = mapData.field.width;
                uint h = mapData.field.height;
                for (int i = 0; i < h; i++)
                {
                    int view_y = (int)(h - i - 1);
                    for (int j = 0; j < w; j++)
                    {
                        mapData.field.terrains[i * w + j].tid = (byte)GameBoard.Cells[view_y][j].Terrain;
                    }
                }

                await mapArc.Save();
                var writeback = await MasterData.WriteBackFilesAsync([mapArc.FilePath]);
                MapStatusText = $"{Path.GetFileName(mapArc.FilePath)} · 已保存到 {writeback.DestinationText}";
            }
            catch (Exception ex)
            {
                MapStatusText = $"地图保存失败：{ex.Message}";
                await MessageBox.ShowAsync(ex.Message, "地图保存失败", MessageBoxIcon.Error, MessageBoxButton.OK);
            }
            finally
            {
                SavingMap = false;
            }
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
            var msgDataDir = Directory.CreateDirectory(Path.Combine(assetsPath, MasterData.MessageLanguage, "Message", "Data"));
            var srpgMapDir = Directory.CreateDirectory(Path.Combine(assetsPath, "Common", "SRPGMap"));
            var uiDir = Directory.CreateDirectory(Path.Combine(assetsPath, "Common", "UI"));

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
            var uiSourceDir = new DirectoryInfo(MasterData.UI_PATH);
            var uiSources = uiSourceDir.GetFiles("Skill_Passive*.png").Where(f => File.Exists(f.FullName + ".bak")).ToArray();
            foreach (var uiSource in uiSources) {
                File.Copy(uiSource.FullName, Path.Combine(uiDir.FullName, uiSource.Name), true);
            }

            // Export modified Person Archives
            //if (MasterData.PersonArcs != null)
            //{
            //    foreach (var personArc in MasterData.PersonArcs)
            //    {
            //        if (File.Exists(personArc.path + ".bak"))
            //        {
            //            var destFile = Path.Combine(personDir.FullName, Path.GetFileName(personArc.path));
            //            File.Copy(personArc.path, destFile, true);
            //        }
            //    }
            //}

            // Export modified Face Portraits
            if (!string.IsNullOrEmpty(MasterData.FACE_PATH) && Directory.Exists(MasterData.FACE_PATH))
            {
                var faceSourceDir = new DirectoryInfo(MasterData.FACE_PATH);
                var faceExportDir = Directory.CreateDirectory(Path.Combine(assetsPath, "Common", "Face"));
                foreach (var dir in faceSourceDir.GetDirectories())
                {
                    var modifiedPngs = dir.GetFiles("*.png").Where(f => File.Exists(f.FullName + ".bak")).ToArray();
                    if (modifiedPngs.Length > 0)
                    {
                        var targetFaceDir = Directory.CreateDirectory(Path.Combine(faceExportDir.FullName, dir.Name));
                        foreach (var png in modifiedPngs)
                        {
                            File.Copy(png.FullName, Path.Combine(targetFaceDir.FullName, png.Name), true);
                        }
                    }
                }
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
        private void OpenSkillEditor()
        {
            SelectedMainTabIndex = 2;
            EnsureSelectedBrowser(2);
            if (SkillBrowser?.SelectedSkill is not null)
                SkillBrowser.IsEditMode = true;
        }

        private void RefreshSkillBrowser()
        {
            var browser = new SkillSelectorViewModel
            {
                OnPersonNavigationRequested = NavigateToPerson
            };
            SkillBrowser = browser;
        }

        private void NavigateToPerson(string personId)
        {
            SelectedMainTabIndex = 1;
            EnsureSelectedBrowser(1);
            PersonBrowser?.NavigateToPerson(personId);
        }

        private void NavigateToSkill(string skillId)
        {
            SelectedMainTabIndex = 2;
            EnsureSelectedBrowser(2);
            SkillBrowser?.NavigateToSkill(skillId);
        }

        [RelayCommand]
        private void OpenFlagTool()
        {
            var window = new FlagCheckToolWindow();
            window.DataContext = new FlagCheckToolViewModel();
            window.Show();
        }

        [RelayCommand]
        private void OpenPersonEditor()
        {
            SelectedMainTabIndex = 1;
            EnsureSelectedBrowser(1);
            if (PersonBrowser?.SelectedPerson is not null)
                PersonBrowser.IsEditMode = true;
        }

        private void RefreshPersonBrowser()
        {
            PersonBrowser = new PersonSelectorViewModel
            {
                OnSkillNavigationRequested = NavigateToSkill
            };
        }

        [RelayCommand]
        private void OpenEnhanceViewer()
        {
            var window = new EnhanceViewerWindow();
            window.DataContext = new EnhanceViewerViewModel();
            window.Show();
        }

        [RelayCommand]
        private async Task OpenAbout()
        {
            var owner = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
            var window = new AboutWindow
            {
                DataContext = new AboutWindowViewModel()
            };
            if (owner is null)
                window.Show();
            else
                await window.ShowDialog(owner);
        }
    }
}
