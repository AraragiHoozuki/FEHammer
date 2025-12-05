using Avalonia;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using FEHagemu.HSDArchive;
using FEHagemu.HSDArcIO;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Path = System.IO.Path;

namespace FEHagemu
{
    public sealed class MasterData
    {
        public const string DATAEXT = "*.lz";
        public const string MSG_PATH = @"Data\Data\";
        public const string SKL_PATH = @"Data\SRPG\Skill\";
        public const string PERSON_PATH = @"Data\SRPG\Person\";
        public const string ENEMY_PATH = @"Data\SRPG\Enemy\";
        public const string FACE_PATH = @"Data\FACE\";
        public const string FIELD_PATH = @"Data\Field\";
        public const string UI_PATH = @"Data\UI\";

        public static List<uint> Versions = [];
        static Bitmap[] ICON_ATLAS = null!;
        static Bitmap STATUS = null!;
        static Bitmap ABCSX_ATLAS = null!;

        public static HSDArc<SkillList>[] SkillArcs = null!;
        public static HSDArc<PersonList>[] PersonArcs = null!;
        public static HSDArc<EnemyList>[] EnemyArcs = null!;
        public static HSDArc<MessageList>[] MsgArcs = null!;
        public static ConcurrentDictionary<string, string> MsgDict = [];
        public static ConcurrentDictionary<string, Person> PersonDict = [];
        public static ConcurrentDictionary<string, Enemy> EnemyDict = [];
        public static ConcurrentDictionary<string, Skill> SkillDict = [];
        private static ConcurrentDictionary<string, Bitmap> faceCache = [];
        public static Bitmap FallBackFace { get; } = new Bitmap(AssetLoader.Open(new Uri($"avares://FEHagemu/Assets/Face/None.png")));
        public static Bitmap EmptyBitmap { get; } = new Bitmap(AssetLoader.Open(new Uri("avares://FEHagemu/Assets/empty.png")));

        public static HSDArc<SkillList> ModSkillArc => SkillArcs.FirstOrDefault(arc => arc.path.EndsWith("Tutorial.bin.lz"))!;
        public static HSDArc<PersonList> ModPersonArc => PersonArcs.FirstOrDefault(arc => arc.path.EndsWith("Tutorial.bin.lz"))!;
        public static HSDArc<EnemyList> ModEnemyArc => EnemyArcs.FirstOrDefault(arc => arc.path.EndsWith("Tutorial.bin.lz"))!;
        public static HSDArc<MessageList> ModMsgArc => MsgArcs.FirstOrDefault(arc => arc.path.EndsWith("Tutorial.bin.lz"))!;
        

        public static async Task<bool> LoadAsync()
        {
            try
            {
                var t1 = Task.Run(LoadPersons);
                var t2 = Task.Run(LoadEnemies);
                var t3 = Task.Run(LoadSkills);
                var t4 = Task.Run(LoadMessages);
                await Task.WhenAll(t1, t2, t3, t4);
                await Task.Run(InitImage);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Load Failed: {ex}");
                return false;
            }
        }

        private static void LoadPersons()
        {
            var files = Directory.GetFiles(PERSON_PATH, DATAEXT);
            PersonArcs = new HSDArc<PersonList>[files.Length];
            Parallel.For(0, files.Length, i =>
            {
                PersonArcs[i] = new HSDArc<PersonList>(files[i]);
                foreach (var p in PersonArcs[i].data.list)
                {
                    PersonDict[p.id] = p;
                }
            });
        }

        private static void LoadEnemies()
        {
            var files = Directory.GetFiles(ENEMY_PATH, DATAEXT);
            EnemyArcs = new HSDArc<EnemyList>[files.Length];
            Parallel.For(0, files.Length, i =>
            {
                EnemyArcs[i] = new HSDArc<EnemyList>(files[i]);
                foreach (var e in EnemyArcs[i].data.list)
                {
                    EnemyDict[e.id] = e;
                }
            });
        }

        private static void LoadSkills()
        {
            var files = Directory.GetFiles(SKL_PATH, DATAEXT);
            SkillArcs = new HSDArc<SkillList>[files.Length];
            Parallel.For(0, files.Length, i =>
            {
                SkillArcs[i] = new HSDArc<SkillList>(files[i]);
                foreach (var s in SkillArcs[i].data.list)
                {
                    SkillDict[s.id] = s;
                }
            });
        }

        private static void LoadMessages()
        {
            var files = Directory.GetFiles(MSG_PATH, DATAEXT);
            MsgArcs = new HSDArc<MessageList>[files.Length];

            Parallel.For(0, files.Length, i =>
            {
                MsgArcs[i] = new HSDArc<MessageList>(files[i]);
                var list = MsgArcs[i].data.list;
                for (int j = 0; j < list.Length - 1; j += 2)
                {
                    MsgDict[list[j]] = list[j + 1];
                }
            });
        }

        public static IImage[] WeaponTypeIcons { get; private set; } = null!;
        public static IImage[] MoveTypeIcons { get; private set; } = null!;
        public static IImage[] OriginTypeIcons { get; private set; } = null!;


        private const int SkillAtlasCapacity = 169; // 13行 * 13列
        private const int SkillGridCols = 13;
        private const int SkillIconSize = 76;

        private const int WeaponIconSize = 56;
        private const int WeaponStartY = 261;
        private const int WeaponStartX = 1;

        private const int MoveIconSize = 56;
        private const int MoveStartY = 469;
        private const int MoveStartX = 353;

        private const int OriginWidth = 90;
        private const int OriginHeight = 88;
        private const int OriginStartY = 171;
        private const int OriginStartX = -3;
        
        private static readonly Dictionary<int, IImage> skillIconCache = new();
        private static readonly Dictionary<string, IImage> abcsxCache = new();

        public static void InitImage()
        {
            STATUS = new Bitmap(Path.Combine(UI_PATH, "Status.png"));
            ABCSX_ATLAS = new Bitmap(Path.Combine(UI_PATH, "ABCSX.webp"));
            var directory = new DirectoryInfo(UI_PATH);
            var files = directory.GetFiles("Skill_Passive*.png")
                                 .OrderBy(f => f.Name.Length) 
                                 .ThenBy(f => f.Name)
                                 .ToArray();
            ICON_ATLAS = new Bitmap[files.Length];
            for (int i = 0; i < files.Length; i++)
            {
                ICON_ATLAS[i] = new Bitmap(files[i].FullName);
            }
            WeaponTypeIcons = new IImage[(int)WeaponType.ColorlessBeast + 1];
            MoveTypeIcons = new IImage[(int)MoveType.Flying + 1];
            OriginTypeIcons = new IImage[(int)Origins.Engage + 1];
            
        }

        public static IImage GetSkillIcon(int id)
        {
            if (skillIconCache.TryGetValue(id, out var cachedImage))
            {
                return cachedImage;
            }
            if (ICON_ATLAS == null || ICON_ATLAS.Length == 0)
                throw new InvalidOperationException("Skill Atlases not initialized.");

            int atlasIndex = id / SkillAtlasCapacity;
            int localIndex = id % SkillAtlasCapacity;
            if (atlasIndex >= ICON_ATLAS.Length)
            {
                atlasIndex = 0;
                localIndex = 1;
            }
            int row = localIndex / SkillGridCols;
            int col = localIndex % SkillGridCols;
            var sourceBitmap = ICON_ATLAS[atlasIndex];
            var rect = new PixelRect(col * SkillIconSize, row * SkillIconSize, SkillIconSize, SkillIconSize);
            var cropped = new CroppedBitmap(sourceBitmap, rect);
            skillIconCache[id] = cropped;
            return cropped;
        }

        public static IImage GetWeaponIcon(int id)
        {
            if (id < 0 || id >= WeaponTypeIcons.Length) return null!;
            if (WeaponTypeIcons[id] is null)
            {
                if (STATUS == null) throw new InvalidOperationException("Status Atlas not initialized.");
                WeaponTypeIcons[id] = new CroppedBitmap(STATUS,
                    new PixelRect(WeaponStartX + WeaponIconSize * id, WeaponStartY, WeaponIconSize, WeaponIconSize));
            }
            return WeaponTypeIcons[id];
        }

        public static IImage GetMoveIcon(int id)
        {
            if (id < 0 || id >= MoveTypeIcons.Length) return null!;
            if (MoveTypeIcons[id] is null)
            {
                if (STATUS == null) throw new InvalidOperationException("Status Atlas not initialized.");
                MoveTypeIcons[id] = new CroppedBitmap(STATUS,
                    new PixelRect(MoveStartX + MoveIconSize * id, MoveStartY, MoveIconSize, MoveIconSize));
            }
            return MoveTypeIcons[id];
        }

        public static IImage GetOriginIcon(int id)
        {
            if (id < 0 || id >= OriginTypeIcons.Length) return null!;
            if (OriginTypeIcons[id] is null)
            {
                if (STATUS == null) throw new InvalidOperationException("Status Atlas not initialized.");
                OriginTypeIcons[id] = new CroppedBitmap(STATUS,
                    new PixelRect(OriginStartX + OriginWidth * id, OriginStartY, OriginWidth, OriginHeight));
            }
            return OriginTypeIcons[id];
        }

        public static IImage GetABCSXIcon(string name)
        {
            if (abcsxCache.TryGetValue(name, out var cached)) return cached;
            if (abcsxCache == null) throw new InvalidOperationException("ABCSX Atlas not initialized.");
            int xOffset = name switch
            {
                "A" => 0,
                "B" => 48,
                "C" => 96,
                "S" => 144,
                "X" => 192,
                _ => throw new KeyNotFoundException($"Unknown icon type: {name}")
            };
            var cropped = new CroppedBitmap(ABCSX_ATLAS, new PixelRect(xOffset, 0, 48, 48));
            abcsxCache[name] = cropped; 
            return cropped;
        }

        private static readonly Dictionary<string, Bitmap> legendaryIconCache = new();

        public static Bitmap GetLegendaryIcon(string? iconName)
        {
            string targetName = !string.IsNullOrEmpty(iconName) ? iconName : "SeasonNone";
            if (legendaryIconCache.TryGetValue(targetName, out var cachedBitmap))
            {
                return cachedBitmap;
            }
            try
            {
                var uri = new Uri($"avares://FEHagemu/Assets/UI/LegendaryIcons/Icon_{targetName}.png");
                using var stream = AssetLoader.Open(uri);
                var newBitmap = new Bitmap(stream);

                legendaryIconCache[targetName] = newBitmap;
                return newBitmap;
            }
            catch
            {
                return legendaryIconCache.GetValueOrDefault("SeasonNone")!;
            }
        }

        public static void Dispose()
        {
            STATUS?.Dispose();
            ABCSX_ATLAS?.Dispose();
            if (ICON_ATLAS != null)
            {
                foreach (var bmp in ICON_ATLAS) bmp.Dispose();
            }
            skillIconCache.Clear();
            abcsxCache.Clear();
        }
        public static string StripIdPrefix(string id, out string prefix)
        {
            string[] split = id.Split('_', 2);
            prefix = split[0] + "_";
            return split[1];
        }
        public static string GetMessage(string id)
        {

            if (MsgDict.TryGetValue(id, out string? value)) return value;
            return id;
        }

        public static Skill? GetSkill(string? id)
        {
            if (id is null) return null;
            if (SkillDict.TryGetValue(id, out Skill? skill)) return skill;
            return null;
        }

        public static IPerson? GetPerson(string id)
        {
            if (id is null) return null;
            if (EnemyDict.TryGetValue(id, out Enemy? p)) return p;
            if (PersonDict.TryGetValue(id, out Person? e)) return e;
            return null;
        }

        public static HSDArc<PersonList>? GetPersonArc(string pid)
        {
            foreach(var arc in PersonArcs)
            {
                foreach (var person in arc.data.list)
                {
                    if (person.id == pid) return arc;
                }
            }
            return null;
        }
        public static HSDArc<EnemyList>? GetEnemyArc(string eid)
        {
            foreach (var arc in EnemyArcs)
            {
                foreach (var enemy in arc.data.list)
                {
                    if (enemy.id == eid) return arc;
                }
            }
            return null;
        }

        public static bool CheckSkillCategory(string id, SkillCategory cat)
        {
            if (id is null) return false;
            Skill? sk = GetSkill(id);
            if (sk is not null)
            {
                return sk.category == cat;
            }
            return false;
        }

        public static void AddMessage(HSDArc<MessageList> arc, string key, string message)
        {
            int index = Array.FindIndex(arc.data.list, msg => msg == key);
            if (index > -1 && index < arc.data.list.Length - 1 && ((index&1)==0))
            {
                arc.data.list[index + 1] = message;
                MsgDict[key] = message;
            } else
            {
                index = arc.data.list.Length;
                Array.Resize(ref arc.data.list, arc.data.list.Length + 2);
                arc.data.list[index] = key;
                arc.data.list[index + 1] = message;
                arc.data.size = (ulong)arc.data.list.Length/2;
                MsgDict.TryAdd(key, message);
            }
        }

        public static void AddSkill(HSDArc<SkillList> arc, Skill skill)
        {
            int index = Array.FindIndex(arc.data.list, s => s.id == skill.id);
            if (index > -1)
            {
                skill.id_num = arc.data.list[index].id_num;
                skill.sort_value = arc.data.list[index].sort_value;
                arc.data.list[index] = skill;
                SkillDict[skill.id] = skill;
            }
            else
            {
                skill.id_num = SkillDict.Values.MaxBy(sk => sk.id_num)!.id_num + 1;
                skill.sort_value = SkillDict.Values.MaxBy(sk => sk.sort_value)!.sort_value + 1;
                Array.Resize(ref arc.data.list, arc.data.list.Length + 1);
                arc.data.list[^1] = skill;
                arc.data.size = (ulong)arc.data.list.Length;
                SkillDict.TryAdd(skill.id, skill);
            }
        }

        public static void AddPerson(HSDArc<PersonList> arc, Person p)
        {
            int index = Array.FindIndex(arc.data.list, s => s.id == p.id);
            if (index > -1)
            {
                p.id_num = arc.data.list[index].id_num;
                p.sort_value = arc.data.list[index].sort_value;
                arc.data.list[index] = p;
                PersonDict[p.id] = p;
            }
            else
            {
                p.id_num = PersonDict.Values.MaxBy(sk => sk.id_num)!.id_num + 1;
                p.sort_value = PersonDict.Values.MaxBy(sk => sk.sort_value)!.sort_value + 1;
                Array.Resize(ref arc.data.list, arc.data.list.Length + 1);
                arc.data.list[^1] = p;
                arc.data.size = (ulong)arc.data.list.Length;
                PersonDict.TryAdd(p.id, p);
            }
        }

        public static void AddEnemy(HSDArc<EnemyList> arc, Enemy e)
        {
            int index = Array.FindIndex(arc.data.list, s => s.id == e.id);
            if (index > -1)
            {
                e.id_num = arc.data.list[index].id_num;
                arc.data.list[index] = e;
                EnemyDict[e.id] = e;
            }
            else
            {
                e.id_num = PersonDict.Values.MaxBy(sk => sk.id_num)!.id_num + 1;
                Array.Resize(ref arc.data.list, arc.data.list.Length + 1);
                arc.data.list[^1] = e;
                arc.data.size = (ulong)arc.data.list.Length;
                EnemyDict.TryAdd(e.id, e);
            }
        }

        public static void DeleteSkill(HSDArc<SkillList> arc, Skill skill)
        {
            if (!skill.id.Contains("MOD")) return;
            int index = Array.FindIndex(arc.data.list, s => s.id == skill.id);
            if (index > -1)
            {
                arc.data.list[index] = arc.data.list[^1];
                arc.data.list = arc.data.list[..^1];
                arc.data.size = (ulong)arc.data.list.Length;
                SkillDict.TryRemove(skill.id, out _);
                DeleteMessage(ModMsgArc, skill.name);
                DeleteMessage(ModMsgArc, skill.description);
            }
        }

        public static void DeletePerson(HSDArc<PersonList> arc, Person p)
        {
            if (!p.id.Contains("MOD")) return;
            int index = Array.FindIndex(arc.data.list, s => s.id == p.id);
            if (index > -1)
            {
                arc.data.list[index] = arc.data.list[^1];
                arc.data.list = arc.data.list[..^1];
                arc.data.size = (ulong)arc.data.list.Length;
                PersonDict.TryRemove(p.id, out _);
                DeleteMessage(ModMsgArc, $"M{p.id}");
            }
        }
        public static void DeleteEnemy(HSDArc<EnemyList> arc, Enemy e)
        {
            if (!e.id.Contains("MOD")) return;
            int index = Array.FindIndex(arc.data.list, s => s.id == e.id);
            if (index > -1)
            {
                arc.data.list[index] = arc.data.list[^1];
                arc.data.list = arc.data.list[..^1];
                arc.data.size = (ulong)arc.data.list.Length;
                EnemyDict.TryRemove(e.id, out _);
                DeleteMessage(ModMsgArc, $"M{e.id}");
            }
        }

        public static void DeleteMessage(HSDArc<MessageList> arc, string key)
        {
            if (!key.Contains("MOD")) return;
            int index = Array.FindIndex(arc.data.list, m => m == key);
            if (index > -1)
            {
                arc.data.list[index] = arc.data.list[^2];
                arc.data.list[index+1] = arc.data.list[^1];
                arc.data.list = arc.data.list[..^2];
                arc.data.size = (ulong)arc.data.list.Length/2;
                MsgDict.TryRemove(key, out _);
            }
        }

        public async static Task<Bitmap> GetFaceAsync(string face)
        {
            if (face == null) return FallBackFace;
            if (faceCache.TryGetValue(face, out Bitmap? result)) return result;
            string path = System.IO.Path.Combine(FACE_PATH, face, "Face_FC.png");
            if (File.Exists(path))
            {
                return await Task.Run(() =>
                {
                    using var stream = File.OpenRead(path);
                    var bm = Bitmap.DecodeToWidth(stream, 64);
                    faceCache.TryAdd(face, bm);
                    return bm;
                });
            }
            else
            {
                return FallBackFace;
            }

        }

        public static Bitmap GetFieldBackground(string id)
        {
            if (id == null) return EmptyBitmap;
            string path = System.IO.Path.Combine(FIELD_PATH, $"{id}.jpg");
            if (!File.Exists(path)) path = System.IO.Path.Combine(FIELD_PATH, $"{id}.png");
            if (File.Exists(path))
            {
                using (var imageStream = File.OpenRead(path))
                {
                    return new Bitmap(imageStream);
                }
            }
            else
            {
                return EmptyBitmap;
            }
        }
    }

}
