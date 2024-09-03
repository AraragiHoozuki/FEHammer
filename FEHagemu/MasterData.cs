using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using FEHagemu.HSDArchive;
using FEHagemu.HSDArcIO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

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
        public static HSDArc<MessageList>[] MsgArcs = null!;
        public static Dictionary<string, string> MsgDict = [];
        public static Dictionary<string, Person> PersonDict = [];
        public static Dictionary<string, Skill> SkillDict = [];
        public static Dictionary<string, Bitmap> FaceDict = [];
        public static Bitmap FallBackFace = new Bitmap(AssetLoader.Open(new Uri($"avares://FEHagemu/Assets/Face/ch00_00_Eclat_F_Avatar01/Face_FC.png")));
        public static Bitmap EmptyBitmap = new Bitmap(AssetLoader.Open(new Uri("avares://FEHagemu/Assets/empty.png")));


        public static void Init()
        {
            string[] files;
            files = Directory.GetFiles(PERSON_PATH, DATAEXT);
            PersonArcs = new HSDArc<PersonList>[files.Length];
            for (int i = 0; i < files.Length; i++)
            {
                PersonArcs[i] = new HSDArc<PersonList>(files[i]);
                for (int j = 0; j < PersonArcs[i].data.list.Length; j += 1)
                {
                    PersonDict.Add(PersonArcs[i].data.list[j].id, PersonArcs[i].data.list[j]);
                }
            }

            files = Directory.GetFiles(SKL_PATH, DATAEXT);
            SkillArcs = new HSDArc<SkillList>[files.Length];
            for (int i = 0; i < files.Length; i++)
            {
                SkillArcs[i] = new HSDArc<SkillList>(files[i]);
                for (int j = 0; j < SkillArcs[i].data.list.Length; j += 1)
                {
                    SkillDict.Add(SkillArcs[i].data.list[j].id, SkillArcs[i].data.list[j]);
                }
            }
            
            files = Directory.GetFiles(MSG_PATH, DATAEXT);
            MsgArcs = new HSDArc<MessageList>[files.Length];
            for (int i = 0; i < files.Length; i++)
            {
                MsgArcs[i] = new HSDArc<MessageList>(files[i]);
                for (int j = 0; j < MsgArcs[i].data.list.Length - 1; j += 2)
                {
                    if (j == MsgArcs[i].data.list.Length - 1) break;
                    MsgDict.Add(MsgArcs[i].data.list[j], MsgArcs[i].data.list[j + 1]);
                }
            }
            
            
            InitImage();
        }

        public static IImage[] WeaponTypeIcons = null!;
        public static IImage[] MoveTypeIcons = null!;
        static void InitImage()
        {
            STATUS = new Bitmap(UI_PATH + "Status.png");
            ABCSX_ATLAS = new Bitmap(UI_PATH + "ABCSX.webp");
            var atlas = (new DirectoryInfo(UI_PATH)).GetFiles("Skill_Passive*.png");
            ICON_ATLAS = new Bitmap[atlas.Length];
            for (int i = 0; i < ICON_ATLAS.Length; i++)
            {
                ICON_ATLAS[i] = new Bitmap(UI_PATH + "Skill_Passive" + (i + 1) + ".png");
            }
            WeaponTypeIcons = new IImage[(int)WeaponType.ColorlessBeast + 1];
            MoveTypeIcons = new IImage[(int)MoveType.Flying + 1];
            for (int i = 0; i <= (int)WeaponType.ColorlessBeast; i++) WeaponTypeIcons[i] = GetWeaponIcon(i);
            for (int i = 0; i <= (int)MoveType.Flying; i++) MoveTypeIcons[i] = GetMoveIcon(i);
            GetABCSXIcon("A");
            GetABCSXIcon("B");
            GetABCSXIcon("C");
            GetABCSXIcon("S");
            GetABCSXIcon("X");
        }

        public static IImage GetSkillIcon(int id)
        {
            int pic = id / 169;
            if (pic > ICON_ATLAS.Length) { pic = 0; id = 1; }
            int pos = id % 169;
            int row = pos / 13;
            int col = pos - row * 13;
            CroppedBitmap cropped = new(ICON_ATLAS[pic], new Avalonia.PixelRect(col * 76, row * 76, 76, 76));
            return cropped;
        }
        public static IImage GetMoveIcon(int id)
        {
            if (MoveTypeIcons?[id] is not null) return MoveTypeIcons[id];
            CroppedBitmap cropped = new(STATUS, new Avalonia.PixelRect(353 + 55 * id, 413, 55, 55));
            return cropped;
        }
        public static IImage GetWeaponIcon(int id)
        {
            if (WeaponTypeIcons?[id] is not null) { return WeaponTypeIcons[id]; }
            CroppedBitmap cropped = new(STATUS, new Avalonia.PixelRect(1 + 56 * id, 205, 56, 56));
            return cropped;
        }
        public static IImage GetABCSXIcon(string name)
        {
            CroppedBitmap cropped = new(ABCSX_ATLAS, new Avalonia.PixelRect(name switch { 
                "A"=> 0, "B"=> 48, "C"=>96, "S"=>144, "X"=> 192, _ => throw new KeyNotFoundException()
            }, 0, 48, 48));
            return cropped;
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

        public static Person? GetPerson(string id)
        {
            if (id is null) return null;
            if (PersonDict.TryGetValue(id, out Person? p)) return p;
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
                arc.data.size += 1;
                MsgDict.Add(key, message);
            }
        }

        public static void AddSkill(HSDArc<SkillList> arc, Skill skill)
        {
            int index = Array.FindIndex(arc.data.list, s => s.id == skill.id);
            if (index > -1 && index < arc.data.list.Length)
            {
                arc.data.list[index] = skill;
                SkillDict[skill.id] = skill;
            }
            else
            {
                Array.Resize(ref arc.data.list, arc.data.list.Length + 1);
                arc.data.list[^1] = skill;
                arc.data.size += 1;
            }
        }

        public async static Task<Bitmap> GetFaceAsync(string face)
        {
            if (face == null) return FallBackFace;
            if (FaceDict.TryGetValue(face, out Bitmap? result)) return result;
            string path = System.IO.Path.Combine(FACE_PATH, face, "Face_FC.png");
            if (File.Exists(path))
            {
                using (var imageStream = File.OpenRead(path))
                {
                    return  await Task.Run(() => {
                        var bm = Bitmap.DecodeToWidth(imageStream, 64);
                        FaceDict.TryAdd(face, bm);
                        return bm;
                    });
                }
            } else
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
