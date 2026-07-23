using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FEHagemu.HSDArchive;
using FEHagemu.ViewModels.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using System.Threading.Tasks;
using Ursa.Controls;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace FEHagemu.ViewModels.Tools
{
    public partial class PersonEditorViewModel : ViewModelBase
    {
        private readonly bool loadPortraitPreviews;
        private string? sourcePersonId;
        private bool editingAddedPerson;
        public Action<string>? OnSaved { get; set; }
        [ObservableProperty] private bool _isSaving;
        [ObservableProperty] private string _id = "PID_NewPerson";
        [ObservableProperty] private string _roman = "";
        [ObservableProperty] private string _face = "";
        partial void OnFaceChanged(string value)
        {
            if (loadPortraitPreviews) RefreshPortraits();
        }
        [ObservableProperty] private string _face2 = "";

        [ObservableProperty] private uint _idNum;
        [ObservableProperty] private uint _versionNum = 65535;
        [ObservableProperty] private uint _sortValue;
        [ObservableProperty] private uint _origin;

        [ObservableProperty] private WeaponType _weaponType;
        [ObservableProperty] private Element _tomeClass;
        [ObservableProperty] private MoveType _moveType;

        [ObservableProperty] private byte _series;
        [ObservableProperty] private bool _regularQ;
        [ObservableProperty] private bool _permanentQ;
        [ObservableProperty] private byte _baseVector;
        [ObservableProperty] private byte _refresherQ;

        [ObservableProperty] private ulong _timestamp;

        // Stats
        [ObservableProperty] private int _hp;
        [ObservableProperty] private int _atk;
        [ObservableProperty] private int _spd;
        [ObservableProperty] private int _def;
        [ObservableProperty] private int _res;

        // Grow
        [ObservableProperty] private int _growHp;
        [ObservableProperty] private int _growAtk;
        [ObservableProperty] private int _growSpd;
        [ObservableProperty] private int _growDef;
        [ObservableProperty] private int _growRes;

        public System.Collections.ObjectModel.ObservableCollection<StringWrapper> SkillsList { get; } = new();

        // Dragonflowers
        [ObservableProperty] private uint _dragonflowerNum;


        // Legendary
        [ObservableProperty] private bool _isLegendary;
        [ObservableProperty] private string? _legendaryBtnSkillId;
        [ObservableProperty] private LegendaryKind _legendaryKind;
        [ObservableProperty] private LegendaryElement _legendaryElement;
        [ObservableProperty] private byte _legendaryBst;
        [ObservableProperty] private bool _legendaryDuelQ;
        [ObservableProperty] private bool _legendaryAeExtraSlotQ;
        // Legendary Bonus Stats
        [ObservableProperty] private int _lHp;
        [ObservableProperty] private int _lAtk;
        [ObservableProperty] private int _lSpd;
        [ObservableProperty] private int _lDef;
        [ObservableProperty] private int _lRes;


        [ObservableProperty] private string _nameText = "New Person";
        [ObservableProperty] private string _titleText = "Title";

        // Portrait previews
        [ObservableProperty] private Avalonia.Media.Imaging.Bitmap? _faceFcImage;
        [ObservableProperty] private Avalonia.Media.Imaging.Bitmap? _faceImage;
        [ObservableProperty] private Avalonia.Media.Imaging.Bitmap? _btlFaceImage;
        [ObservableProperty] private Avalonia.Media.Imaging.Bitmap? _btlFaceCImage;
        [ObservableProperty] private Avalonia.Media.Imaging.Bitmap? _btlFaceDImage;
        [ObservableProperty] private Avalonia.Media.Imaging.Bitmap? _btlFaceBuImage;
        [ObservableProperty] private Avalonia.Media.Imaging.Bitmap? _btlFaceBuDImage;

        // public IEnumerable<Origins> OriginsList => Enum.GetValues<Origins>();
        public IEnumerable<WeaponType> WeaponTypes => Enum.GetValues<WeaponType>();
        public IEnumerable<MoveType> MoveTypes => Enum.GetValues<MoveType>();
        public IEnumerable<Element> Elements => Enum.GetValues<Element>();
        public IEnumerable<LegendaryKind> LegendaryKinds => Enum.GetValues<LegendaryKind>();
        public IEnumerable<LegendaryElement> LegendaryElements => Enum.GetValues<LegendaryElement>();

        public PersonEditorViewModel(bool loadPortraitPreviews = true)
        {
            this.loadPortraitPreviews = loadPortraitPreviews;
        }

        [RelayCommand]
        public async Task SelectPersonFromGame()
        {
            var vm = new PersonSelectorViewModel();
            var result = await OverlayDialog.ShowModal(
                FEHagemu.Views.PersonSelectorView.CreateSelectionView(),
                vm,
                null,
                new OverlayDialogOptions()
            {
                Title = "Select Person",
                CanResize = true,
                Buttons = DialogButton.OKCancel
            });

            if (result == DialogResult.OK && vm.SelectedPerson is not null && vm.SelectedPerson.person is Person p)
            {
                LoadPerson(p);
            }
        }

        private void RefreshPortraits()
        {
            FaceFcImage = LoadPortrait("Face_FC");
            FaceImage = LoadPortrait("Face");
            BtlFaceImage = LoadPortrait("BtlFace");
            BtlFaceCImage = LoadPortrait("BtlFace_C");
            BtlFaceDImage = LoadPortrait("BtlFace_D");
            BtlFaceBuImage = LoadPortrait("BtlFace_BU");
            BtlFaceBuDImage = LoadPortrait("BtlFace_BU_D");
        }

        private Avalonia.Media.Imaging.Bitmap? LoadPortrait(string type)
        {
            if (string.IsNullOrEmpty(Face)) return null;
            string path = MasterData.GetPortraitLocalPath(Face, type);
            if (File.Exists(path))
            {
                try
                {
                    using var fs = File.OpenRead(path);
                    return new Avalonia.Media.Imaging.Bitmap(fs);
                }
                catch { return null; }
            }
            return null;
        }

        [RelayCommand]
        public async Task ReplacePortrait(string portraitType)
        {
            if (string.IsNullOrEmpty(Face)) return;
            var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;
            if (mainWindow is null) return;

            var files = await mainWindow.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = $"Select {portraitType} Image",
                AllowMultiple = false,
                FileTypeFilter = new[] { Avalonia.Platform.Storage.FilePickerFileTypes.ImageAll }
            });

            if (files.Count > 0)
            {
                string targetPath = MasterData.GetPortraitLocalPath(Face, portraitType);
                string backupPath = targetPath + ".bak";

                try
                {
                    if (!File.Exists(targetPath))
                        throw new FileNotFoundException("The original portrait was not found.", targetPath);
                    if (!File.Exists(backupPath))
                        File.Copy(targetPath, backupPath);

                    int targetW = 158, targetH = 158;
                    if (portraitType == "Face_FC") { targetW = 158; targetH = 158; }
                    else if (portraitType == "Face" || portraitType.StartsWith("BtlFace") && !portraitType.Contains("BU")) { targetW = 1684; targetH = 1920; }
                    else if (portraitType.StartsWith("BtlFace_BU")) { targetW = 476; targetH = 300; }

                    using (var sourceImage = await SixLabors.ImageSharp.Image.LoadAsync(files[0].Path.LocalPath))
                    {
                        SixLabors.ImageSharp.Processing.ProcessingExtensions.Mutate(sourceImage, x => x.Resize(targetW, targetH));
                        await sourceImage.SaveAsWebpAsync(targetPath);
                    }
                    var writeback = await MasterData.WriteBackFilesAsync([targetPath]);
                    RefreshPortraits();
                    await MessageBox.ShowOverlayAsync(
                        $"{portraitType} 已保存到 {writeback.DestinationText}。",
                        "保存成功");
                }
                catch (Exception e)
                {
                    await MessageBox.ShowOverlayAsync($"Error processing image: {e.Message}", "Error");
                }
            }
        }

        [RelayCommand]
        public async Task RestorePortrait(string portraitType)
        {
            if (string.IsNullOrEmpty(Face)) return;
            string targetPath = MasterData.GetPortraitLocalPath(Face, portraitType);
            try
            {
                await MasterData.RestoreFilesByLocalPathAsync([targetPath]);
                RefreshPortraits();
                await MessageBox.ShowOverlayAsync($"{portraitType} restored successfully.", "Success");
            }
            catch (Exception e)
            {
                await MessageBox.ShowOverlayAsync($"Error restoring image: {e.Message}", "Error");
            }
        }





        public void LoadPerson(Person p)
        {
            sourcePersonId = p.id;
            editingAddedPerson = MasterData.IsAddedPerson(p);
            Id = p.id;
            Roman = p.roman;
            Face = p.face;
            Face2 = p.face2;

            // Name/Title
            NameText = MasterData.GetMessage("M" + p.id) ?? p.id;
            string body = MasterData.StripIdPrefix(p.id, out string prefix);
            TitleText = MasterData.GetMessage("M" + prefix + "HONOR_" + body) ?? "Title";

            IdNum = p.id_num;
            VersionNum = p.version_num;
            SortValue = p.sort_value;
            Origin = p.origins;

            WeaponType = p.weapon_type;
            TomeClass = p.tome_class;
            MoveType = p.move_type;

            Series = p.series;
            RegularQ = p.regularQ != 0;
            PermanentQ = p.permanentQ != 0;
            BaseVector = p.base_vector;
            RefresherQ = p.refresherQ;
            Timestamp = p.timestamp;

            if (p.stats != null)
            {
                Hp = p.stats.hp; Atk = p.stats.atk; Spd = p.stats.spd; Def = p.stats.def; Res = p.stats.res;
            }
            else { Hp = Atk = Spd = Def = Res = 0; }

            if (p.grow != null)
            {
                GrowHp = p.grow.hp; GrowAtk = p.grow.atk; GrowSpd = p.grow.spd; GrowDef = p.grow.def; GrowRes = p.grow.res;
            }
            else { GrowHp = GrowAtk = GrowSpd = GrowDef = GrowRes = 0; }

            // Skills
            SkillsList.Clear();
            var pSkills = p.skills ?? Array.Empty<string>();
            for (int i = 0; i < pSkills.Length; i++)
            {
                SkillsList.Add(new StringWrapper 
                { 
                    Label = $"Skill {i}",
                    Value = pSkills[i]
                });
            }

            // Dragonflowers
            if (p.dragonflower != null)
            {
                DragonflowerNum = p.dragonflower.num;
            }

            // Legendary
            IsLegendary = p.legendary != null;
            if (p.legendary != null)
            {
                LegendaryBtnSkillId = p.legendary.btn_skill_id;
                LegendaryKind = p.legendary.kind;
                LegendaryElement = p.legendary.element;
                LegendaryBst = p.legendary.bst;
                LegendaryDuelQ = p.legendary.duelQ != 0;
                LegendaryAeExtraSlotQ = p.legendary.ae_extra_slotQ != 0;

                if (p.legendary.bonus_stats != null)
                {
                    LHp = p.legendary.bonus_stats.hp; LAtk = p.legendary.bonus_stats.atk; LSpd = p.legendary.bonus_stats.spd; LDef = p.legendary.bonus_stats.def; LRes = p.legendary.bonus_stats.res;
                }
                else { LHp = LAtk = LSpd = LDef = LRes = 0; }
            }
        }

        public void LoadPerson(IPerson person)
        {
            if (person is Person regularPerson)
            {
                LoadPerson(regularPerson);
                return;
            }

            if (person is not Enemy enemy) return;

            sourcePersonId = enemy.id;
            editingAddedPerson = false;
            Id = enemy.id;
            Roman = enemy.roman;
            Face = enemy.face;
            Face2 = enemy.face2;
            NameText = MasterData.GetMessage("M" + enemy.id);
            TitleText = string.Empty;
            IdNum = enemy.id_num;
            VersionNum = enemy.Version;
            SortValue = 0;
            Origin = 0;
            WeaponType = enemy.weapon_type;
            TomeClass = enemy.tome_class;
            MoveType = enemy.move_type;
            Series = 0;
            RegularQ = false;
            PermanentQ = false;
            BaseVector = 0;
            RefresherQ = enemy.refresherQ;
            Timestamp = enemy.timestamp;

            Hp = enemy.stats.hp;
            Atk = enemy.stats.atk;
            Spd = enemy.stats.spd;
            Def = enemy.stats.def;
            Res = enemy.stats.res;
            GrowHp = enemy.grow.hp;
            GrowAtk = enemy.grow.atk;
            GrowSpd = enemy.grow.spd;
            GrowDef = enemy.grow.def;
            GrowRes = enemy.grow.res;

            SkillsList.Clear();
            string?[] enemySkills = [enemy.top_weapon, enemy.assist1, enemy.assist2, enemy.special];
            string[] labels = ["Top Weapon", "Assist 1", "Assist 2", "Special"];
            for (int i = 0; i < enemySkills.Length; i++)
                SkillsList.Add(new StringWrapper { Label = labels[i], Value = enemySkills[i] });

            DragonflowerNum = 0;
            IsLegendary = false;
        }



        private Stats CreateStats(int hp, int atk, int spd, int def, int res)
        {
            return new Stats
            {
                hp = checked((short)hp),
                atk = checked((short)atk),
                spd = checked((short)spd),
                def = checked((short)def),
                res = checked((short)res)
            };
        }

        public Person CreatePersonObj()
        {
            var p = new Person();
            p.id = Id;
            p.roman = Roman;
            p.face = Face;
            p.face2 = Face2;

            p.id_num = IdNum;
            p.version_num = VersionNum;
            p.sort_value = SortValue;
            p.origins = Origin;

            p.weapon_type = WeaponType;
            p.tome_class = TomeClass;
            p.move_type = MoveType;

            p.series = Series;
            p.regularQ = (byte)(RegularQ ? 1 : 0);
            p.permanentQ = (byte)(PermanentQ ? 1 : 0);
            p.base_vector = BaseVector;
            p.refresherQ = RefresherQ;
            p.timestamp = Timestamp;

            p.stats = CreateStats(Hp, Atk, Spd, Def, Res);
            p.grow = CreateStats(GrowHp, GrowAtk, GrowSpd, GrowDef, GrowRes);

            p.skills = SkillsList.Select(s => string.IsNullOrEmpty(s.Value) ? null : s.Value).ToArray()!;

            // Dragonflower
            p.dragonflower = new DragonFlowerInfo
            {
                num = DragonflowerNum,
                costs = new uint[] { 40, 80, 120, 160 } // Default costs, hard to edit dynamically without complex UI
            };

            // Legendary
            if (IsLegendary)
            {
                p.legendary = new LegendaryInfo
                {
                    btn_skill_id = LegendaryBtnSkillId ?? string.Empty,
                    kind = LegendaryKind,
                    element = LegendaryElement,
                    bst = LegendaryBst,
                    duelQ = (byte)(LegendaryDuelQ ? 1 : 0),
                    ae_extra_slotQ = (byte)(LegendaryAeExtraSlotQ ? 1 : 0),
                    bonus_stats = CreateStats(LHp, LAtk, LSpd, LDef, LRes)
                };
            }
            else
            {
                p.legendary = null;
            }

            return p;
        }



        [RelayCommand]
        public async Task SaveToGame()
        {
            if (IsSaving) return;
            IsSaving = true;
            try
            {
                var p = CreatePersonObj();
                if (!editingAddedPerson)
                {
                    p.id = MasterData.CreateUniqueModId(
                        p.id,
                        "PID_",
                        candidate => MasterData.PersonDict.ContainsKey(candidate));
                }
                else if (!p.id.StartsWith("PID_", StringComparison.OrdinalIgnoreCase)
                    || !p.id.Contains("MOD", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("新增角色的 ID 必须以 PID_ 开头并保留 MOD 标记。");
                }

                var personArc = MasterData.ModPersonArc
                    ?? throw new InvalidOperationException("Person Tutorial.bin.lz was not found.");
                var messageArc = MasterData.ModMsgArc
                    ?? throw new InvalidOperationException("Message Tutorial.bin.lz was not found.");

                string? previousPersonId = editingAddedPerson
                    ? sourcePersonId ?? throw new InvalidOperationException("找不到新增角色的原始 ID。")
                    : null;
                MasterData.UpsertModPerson(p, previousPersonId);
                if (previousPersonId is not null
                    && !string.Equals(previousPersonId, p.id, StringComparison.Ordinal))
                {
                    MasterData.DeleteMessage(messageArc, "M" + previousPersonId);
                    string oldBody = MasterData.StripIdPrefix(previousPersonId, out string oldPrefix);
                    MasterData.DeleteMessage(messageArc, "M" + oldPrefix + "HONOR_" + oldBody);
                }
                string mpid = "M" + p.id;
                MasterData.AddMessage(messageArc, mpid, NameText);

                string body = MasterData.StripIdPrefix(p.id, out string prefix);
                string honorKey = "M" + prefix + "HONOR_" + body;
                MasterData.AddMessage(messageArc, honorKey, TitleText);

                await personArc.Save();
                await messageArc.Save();
                var writeback = await MasterData.WriteBackFilesAsync(
                    [personArc.FilePath, messageArc.FilePath]);

                Id = p.id;
                sourcePersonId = p.id;
                editingAddedPerson = true;
                await MessageBox.ShowOverlayAsync(
                    $"角色 {p.id} 已保存到 {writeback.DestinationText}。",
                    "保存成功");
                OnSaved?.Invoke(p.id);
            }
            catch (Exception ex)
            {
                await MessageBox.ShowOverlayAsync(ex.Message, "保存失败");
            }
            finally
            {
                IsSaving = false;
            }
        }

        [RelayCommand]
        public async Task PickSkill(StringWrapper wrapper)
        {
            if (wrapper == null) return;
            var vm = new SkillSelectorViewModel();
            var result = await OverlayDialog.ShowModal(FEHagemu.Views.SkillSelectorView.CreateSelectionView(), vm, null, new OverlayDialogOptions()
            {
                Title = "Select Skill",
                CanResize = true,
                Buttons = DialogButton.OKCancel
            });

            if (result == DialogResult.OK && vm.SelectedSkill?.skill is not null)
            {
                wrapper.Value = vm.SelectedSkill.skill.id;
            }
        }

        [RelayCommand]
        public async Task PickLegendaryBtnSkill()
        {
            var vm = new SkillSelectorViewModel();
            var result = await OverlayDialog.ShowModal(FEHagemu.Views.SkillSelectorView.CreateSelectionView(), vm, null, new OverlayDialogOptions()
            {
                Title = "Select Skill",
                CanResize = true,
                Buttons = DialogButton.OKCancel
            });

            if (result == DialogResult.OK && vm.SelectedSkill?.skill is not null)
            {
                LegendaryBtnSkillId = vm.SelectedSkill.skill.id;
            }
        }
    }

    public partial class StringWrapper : ObservableObject
    {
        [ObservableProperty] private string? _label;
        [ObservableProperty] private string? _value;
    }
}
