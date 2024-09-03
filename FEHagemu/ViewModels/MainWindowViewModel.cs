﻿using Avalonia;
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
using System.Collections.Generic;
using System;

namespace FEHagemu.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {

        HSDArc<SRPGMap> mapArc = null!;
        SRPGMap mapData = null!;
        [ObservableProperty]
        ObservableCollection<ObservableCollection<MapSpaceViewModel>> mapSpaces = [];
        [ObservableProperty]
        GameBoardViewModel? gameBoard;
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
                        if (!id_name.Contains("MOD")) id_name = id_name + "MOD";
                        s.name = $"MSID_{id_name}";
                        s.description = $"MSID_H_{id_name}";

                        var found = MasterData.GetSkill(s.id);
                        if (found is not null)
                        {
                            if (!id_name.Contains("MOD"))
                            {
                                await MessageBox.ShowOverlayAsync("Cannot overwrite built-in skills", "Error", icon: MessageBoxIcon.Error);
                            }
                            else
                            {
                                MasterData.AddSkill(skill_arc, s);
                                MasterData.AddMessage(msg_arc, s.name, name);
                                MasterData.AddMessage(msg_arc, s.description, description);
                                skill_modified = true;
                            }
                        }
                        else
                        {
                            MasterData.AddSkill(skill_arc, s);
                            MasterData.AddMessage(msg_arc, s.name, name);
                            MasterData.AddMessage(msg_arc, s.description, description);
                            skill_modified = true;
                        }

                        if (!skill_modified) return;

                        byte[] buffer;
                        buffer = skill_arc.Binarize();
                        File.WriteAllBytes(skill_arc.FilePath, Cryptor.EncryptAndCompress(buffer));
                        buffer = msg_arc.Binarize();
                        File.WriteAllBytes(msg_arc.FilePath, Cryptor.EncryptAndCompress(buffer));
                    }

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
                    GameBoard = new GameBoardViewModel(mapData);
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
            mapData.map_units = GameBoard.Cells.SelectMany(cell => cell).SelectMany(cell => cell.Units).Select(uvm => uvm.unit).ToArray();
            mapData.unit_count = (uint)mapData.map_units.Length;
            byte[] buffer = mapArc.Binarize();
            File.WriteAllBytes(mapArc.FilePath, Cryptor.EncryptAndCompress(buffer));
        }
        [RelayCommand]
        void ExportPackage()
        {
            if (mapArc is null || GameBoard is null) return;
            var root = Path.GetDirectoryName(mapArc.FilePath);
            if (Directory.Exists(root)) {
                byte[] buffer;
                var assets = Directory.CreateDirectory(root + "\\assets");
                if (assets != null) {
                    DirectoryInfo common = assets.CreateSubdirectory("Common");
                    DirectoryInfo srpg = common.CreateSubdirectory("SRPG");
                    DirectoryInfo skill = srpg.CreateSubdirectory("Skill");
                    buffer = File.ReadAllBytes(MasterData.SkillArcs.FirstOrDefault(arc => arc.path.EndsWith("Tutorial.bin.lz"))!.path);
                    File.WriteAllBytes(skill.FullName + "\\Tutorial.bin.lz", buffer);

                    DirectoryInfo twzh = assets.CreateSubdirectory("TWZH");
                    DirectoryInfo message = twzh.CreateSubdirectory("Message");
                    DirectoryInfo data = message.CreateSubdirectory("Data");
                    buffer = File.ReadAllBytes(MasterData.MsgArcs.FirstOrDefault(arc => arc.path.EndsWith("Data_Tutorial.bin.lz"))!.path);
                    File.WriteAllBytes(data.FullName + "\\Data_Tutorial.bin.lz", buffer);

                    DirectoryInfo srpg_map = common.CreateSubdirectory("SRPGMap");
                    mapData.map_units = GameBoard.Cells.SelectMany(cell => cell).SelectMany(cell => cell.Units).Select(uvm => uvm.unit).ToArray();
                    mapData.unit_count = (uint)mapData.map_units.Length;
                    using (MemoryStream ms = new MemoryStream())
                    using (FEHArcWriter writer = new FEHArcWriter(ms))
                    using (FileStream fs = File.OpenWrite(mapArc.FilePath))
                    {
                        writer.WriteStart();
                        writer.WriteStruct(mapData);
                        writer.WritePointerOffsets();
                        writer.WriteEnd(mapArc.header.unknown1, mapArc.header.unknown2, mapArc.header.magic);
                        //writer.WriteAll(_arc, _map);
                        buffer = mapArc.XStart.Concat(ms.ToArray()).ToArray();
                    }
                    File.WriteAllBytes(srpg_map.FullName + "\\" + Path.GetFileName(mapArc.FilePath), Cryptor.EncryptAndCompress(buffer));
                }
            }
            
        }
    }
}