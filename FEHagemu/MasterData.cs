using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using FEHagemu.HSDArchive;
using FEHagemu.Services.GameData;
using FEHagemu.Services.Images;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Path = System.IO.Path;

namespace FEHagemu
{
    public sealed class MasterData
    {
        public const string DATAEXT = "*.lz";
        public const string DefaultVmdkPath = @"D:\App\LDPlayer9\vms\leidian0\data.vmdk";

        public static string MSG_PATH { get; private set; } = ApplicationDataPaths.LocalDataPath("Data");
        public static string SKL_PATH { get; private set; } = ApplicationDataPaths.LocalDataPath("SRPG", "Skill");
        public static string PERSON_PATH { get; private set; } = ApplicationDataPaths.LocalDataPath("SRPG", "Person");
        public static string ENEMY_PATH { get; private set; } = ApplicationDataPaths.LocalDataPath("SRPG", "Enemy");
        public static string MAP_PATH { get; private set; } = ApplicationDataPaths.LocalDataPath("SRPGMap");
        public static string FACE_PATH { get; private set; } = ApplicationDataPaths.LocalDataPath("FACE");
        public static string FIELD_PATH { get; private set; } = ApplicationDataPaths.LocalDataPath("Field");
        public static string UI_PATH { get; private set; } = ApplicationDataPaths.LocalDataPath("UI");

        private static readonly string SettingsPath = ApplicationDataPaths.SettingsPath;
        private static readonly SemaphoreSlim loadGate = new(1, 1);
        private static readonly object sourceContextSync = new();
        private static MasterDataSourceContext? sourceContext;
        private static string[] availableMessageLanguages = ["TWZH"];
        private static string[] supportedMessageLanguages = ["TWZH"];

        public static string SourcePath { get; private set; } = DefaultVmdkPath;
        public static string MessageLanguage { get; private set; } = "TWZH";
        public static bool UseVmdkSource { get; private set; } = true;
        public static string WritebackProviderId { get; private set; } =
            EmulatorWritebackConfiguration.AutomaticProvider;
        public static string? WritebackExecutablePath { get; private set; }
        public static string? WritebackInstanceId { get; private set; }
        public static string ApplicationDataPath => ApplicationDataPaths.Root;
        public static IReadOnlyList<string> AvailableMessageLanguages
        {
            get
            {
                lock (sourceContextSync)
                {
                    return availableMessageLanguages.ToArray();
                }
            }
        }
        public static bool IsMessageLanguageSupported(string language)
        {
            lock (sourceContextSync)
            {
                return supportedMessageLanguages.Contains(language, StringComparer.OrdinalIgnoreCase);
            }
        }
        public static string SourceDescription
        {
            get
            {
                lock (sourceContextSync)
                {
                    return sourceContext?.Description
                        ?? (UseVmdkSource ? SourcePath : "Local Data directory");
                }
            }
        }
        public static string WritebackDescription
        {
            get
            {
                lock (sourceContextSync)
                {
                    return sourceContext?.WritebackDescription
                        ?? (UseVmdkSource ? "尚未检测模拟器写回方式" : "本地 Data 目录");
                }
            }
        }
        public static string? LastLoadError { get; private set; }

        public static List<uint> Versions = [];
        static Bitmap?[] ICON_ATLAS = [];
        static string[] SKILL_ATLAS_PATHS = [];
        static Bitmap STATUS = null!;
        static ITextureAtlas? STATUS_ATLAS;
        static ITextureAtlas? COMMON_ATLAS;
        static ITextureAtlas? RESONATE_ATLAS;
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

        static MasterData()
        {
            ApplicationDataPaths.EnsureInitialized();
            LoadSettings();
        }

        public static void ConfigureVmdkSource(string vmdkPath, string language)
        {
            if (string.IsNullOrWhiteSpace(vmdkPath))
                throw new ArgumentException("A VMDK path is required.", nameof(vmdkPath));

            SourcePath = Path.GetFullPath(vmdkPath);
            MessageLanguage = NormalizeLanguage(language);
            UseVmdkSource = true;
            SaveSettings();
        }

        public static void ConfigureLocalSource(string language)
        {
            MessageLanguage = NormalizeLanguage(language);
            UseVmdkSource = false;
            SaveSettings();
        }

        public static void ConfigureLdPlayerWriteback(string? consolePath, int? instanceIndex)
        {
            WritebackProviderId = "ldplayer";
            WritebackExecutablePath = string.IsNullOrWhiteSpace(consolePath)
                ? null
                : Path.GetFullPath(consolePath.Trim());
            WritebackInstanceId = instanceIndex?.ToString();
            SaveSettings();
        }

        public static void ConfigureAutomaticWriteback()
        {
            WritebackProviderId = EmulatorWritebackConfiguration.AutomaticProvider;
            WritebackExecutablePath = null;
            WritebackInstanceId = null;
            SaveSettings();
        }

        public static void SetMessageLanguage(string language)
        {
            MessageLanguage = NormalizeLanguage(language);
            SaveSettings();
        }

        internal static IReadOnlyList<MasterDataModificationEntry> GetModifiedAssets()
        {
            lock (sourceContextSync)
                return sourceContext?.Modifications ?? [];
        }

        public static Task<MasterDataWritebackResult> WriteBackFilesAsync(IEnumerable<string> localPaths)
        {
            if (!UseVmdkSource)
                return Task.FromResult(new MasterDataWritebackResult(
                    false,
                    0,
                    0,
                    "本地 Data 目录"));
            MasterDataSourceContext context;
            lock (sourceContextSync)
            {
                context = sourceContext
                    ?? throw new InvalidOperationException("The VMDK MasterData source has not been loaded.");
            }
            return context.WriteBackFilesAsync(localPaths);
        }

        internal static Task RestoreModifiedFilesAsync(IEnumerable<string> remotePaths)
        {
            MasterDataSourceContext context;
            lock (sourceContextSync)
            {
                context = sourceContext
                    ?? throw new InvalidOperationException("The VMDK MasterData source has not been loaded.");
            }
            return context.RestoreFilesAsync(remotePaths);
        }

        public static async Task RestoreFilesByLocalPathAsync(IEnumerable<string> localPaths)
        {
            string[] paths = localPaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (UseVmdkSource)
            {
                MasterDataSourceContext context;
                lock (sourceContextSync)
                {
                    context = sourceContext
                        ?? throw new InvalidOperationException("The VMDK MasterData source has not been loaded.");
                }
                await context.RestoreLocalFilesAsync(paths);
                return;
            }

            foreach (string path in paths)
            {
                string backupPath = path + ".bak";
                if (!File.Exists(backupPath))
                    throw new FileNotFoundException("No backup is available for this file.", backupPath);
                File.Copy(backupPath, path, overwrite: true);
                File.Delete(backupPath);
            }
        }

        public static async Task ClearCacheAsync()
        {
            await loadGate.WaitAsync().ConfigureAwait(false);
            try
            {
                MasterDataSourceContext? previousContext;
                lock (sourceContextSync)
                {
                    previousContext = sourceContext;
                    sourceContext = null;
                }
                previousContext?.Dispose();
                ResetLoadedData();

                string cacheRoot = Path.GetFullPath(ApplicationDataPaths.MasterDataCacheRoot);
                string expectedRoot = Path.GetFullPath(Path.Combine(
                    ApplicationDataPaths.Root,
                    "Cache",
                    "MasterData"));
                if (!string.Equals(cacheRoot, expectedRoot, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The MasterData cache path failed its safety check.");

                using IDisposable dataLock = await SharedDataAccess.AcquireAsync(
                    "cache-clear:" + cacheRoot).ConfigureAwait(false);
                if (Directory.Exists(cacheRoot))
                    Directory.Delete(cacheRoot, recursive: true);
                Directory.CreateDirectory(cacheRoot);
            }
            finally
            {
                loadGate.Release();
            }
        }

        public static async Task<bool> LoadAsync()
        {
            await loadGate.WaitAsync().ConfigureAwait(false);
            try
            {
                LastLoadError = null;
                ResetLoadedData();
                bool useVmdk = UseVmdkSource;
                string sourcePath = SourcePath;
                string language = MessageLanguage;
                await PrepareSourceAsync(useVmdk, sourcePath, language).ConfigureAwait(false);

                var t1 = Task.Run(LoadPersons);
                var t2 = Task.Run(LoadEnemies);
                var t3 = Task.Run(LoadSkills);
                var t4 = Task.Run(LoadMessages);
                await Task.WhenAll(t1, t2, t3, t4).ConfigureAwait(false);
                await Task.Run(InitImage).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                LastLoadError = ex.Message;
                Debug.WriteLine($"Load Failed: {ex}");
                return false;
            }
            finally
            {
                loadGate.Release();
            }
        }

        public static async Task<bool> ReloadMessagesAsync(string language)
        {
            string requestedLanguage;
            try
            {
                requestedLanguage = NormalizeLanguage(language);
            }
            catch (Exception ex)
            {
                LastLoadError = ex.Message;
                return false;
            }

            await loadGate.WaitAsync().ConfigureAwait(false);
            try
            {
                LastLoadError = null;
                string selectedLanguage = requestedLanguage;
                string messagePath;

                if (UseVmdkSource)
                {
                    MasterDataSourceContext context;
                    lock (sourceContextSync)
                    {
                        context = sourceContext
                            ?? throw new InvalidOperationException("The VMDK MasterData source has not been loaded.");
                        selectedLanguage = context.SupportedMessageLanguages.FirstOrDefault(item =>
                            string.Equals(item, requestedLanguage, StringComparison.OrdinalIgnoreCase))
                            ?? throw new NotSupportedException($"Message language '{requestedLanguage}' has not been downloaded.");
                    }
                    messagePath = await context.PrepareMessageAssetsAsync(selectedLanguage).ConfigureAwait(false);
                }
                else
                {
                    messagePath = ApplicationDataPaths.LocalDataPath("Data");
                }

                var result = await Task.Run(() => ReadMessages(messagePath)).ConfigureAwait(false);
                MSG_PATH = messagePath;
                MsgArcs = result.Arcs;
                MsgDict = result.Messages;
                MessageLanguage = selectedLanguage;
                SaveSettings();
                return true;
            }
            catch (Exception ex)
            {
                LastLoadError = ex.Message;
                Debug.WriteLine($"Message load failed: {ex}");
                return false;
            }
            finally
            {
                loadGate.Release();
            }
        }

        private static async Task PrepareSourceAsync(bool useVmdk, string sourcePath, string language)
        {
            MasterDataSourceContext? previousContext;
            lock (sourceContextSync)
            {
                previousContext = sourceContext;
                sourceContext = null;
            }
            previousContext?.Dispose();

            if (!useVmdk)
            {
                SetLocalPaths();
                return;
            }

            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("The configured VMDK file does not exist.", sourcePath);

            var preparedContext = MasterDataSourceContext.OpenVmdk(
                sourcePath,
                language,
                partitionIndex: 1,
                new EmulatorWritebackConfiguration
                {
                    ProviderId = WritebackProviderId,
                    ExecutablePath = WritebackExecutablePath,
                    InstanceId = WritebackInstanceId
                });
            try
            {
                await preparedContext.PrepareCoreAssetsAsync().ConfigureAwait(false);
                lock (sourceContextSync)
                {
                    sourceContext = preparedContext;
                    availableMessageLanguages = preparedContext.AvailableMessageLanguages.ToArray();
                    supportedMessageLanguages = preparedContext.SupportedMessageLanguages.ToArray();
                    MessageLanguage = preparedContext.Language;
                    ApplyRuntimePaths(preparedContext.Paths);
                }
                SaveSettings();
            }
            catch
            {
                preparedContext.Dispose();
                throw;
            }
        }

        private static void ApplyRuntimePaths(MasterDataRuntimePaths paths)
        {
            MSG_PATH = paths.MessagePath;
            SKL_PATH = paths.SkillPath;
            PERSON_PATH = paths.PersonPath;
            ENEMY_PATH = paths.EnemyPath;
            MAP_PATH = paths.MapPath;
            FACE_PATH = paths.FacePath;
            FIELD_PATH = paths.FieldPath;
            UI_PATH = paths.UiPath;
        }

        private static void SetLocalPaths()
        {
            MSG_PATH = ApplicationDataPaths.LocalDataPath("Data");
            SKL_PATH = ApplicationDataPaths.LocalDataPath("SRPG", "Skill");
            PERSON_PATH = ApplicationDataPaths.LocalDataPath("SRPG", "Person");
            ENEMY_PATH = ApplicationDataPaths.LocalDataPath("SRPG", "Enemy");
            MAP_PATH = ApplicationDataPaths.LocalDataPath("SRPGMap");
            FACE_PATH = ApplicationDataPaths.LocalDataPath("FACE");
            FIELD_PATH = ApplicationDataPaths.LocalDataPath("Field");
            UI_PATH = ApplicationDataPaths.LocalDataPath("UI");
            lock (sourceContextSync)
            {
                availableMessageLanguages = [MessageLanguage];
                supportedMessageLanguages = [MessageLanguage];
            }
        }

        private static void ResetLoadedData()
        {
            Dispose();
            MsgDict.Clear();
            PersonDict.Clear();
            EnemyDict.Clear();
            SkillDict.Clear();
            faceCache.Clear();
            otherIconCache.Clear();
            legendaryIconCache.Clear();
            SkillArcs = [];
            PersonArcs = [];
            EnemyArcs = [];
            MsgArcs = [];
        }

        private static string NormalizeLanguage(string language)
        {
            string normalized = (language ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(normalized)) return "TWZH";
            if (normalized.Any(c => !char.IsLetterOrDigit(c) && c is not '_' and not '-'))
                throw new ArgumentException("Language must be a folder name such as TWZH or USEN.", nameof(language));
            return normalized;
        }

        private static void LoadSettings()
        {
            try
            {
                using IDisposable dataLock = SharedDataAccess.Acquire(SharedDataAccess.FileKey(SettingsPath));
                if (!File.Exists(SettingsPath))
                {
                    string? detectedVmdk = LdPlayerLocator.FindDefaultVmdkPath(DefaultVmdkPath);
                    if (detectedVmdk is not null) SourcePath = detectedVmdk;
                    UseVmdkSource = detectedVmdk is not null;
                    return;
                }

                var settings = JsonSerializer.Deserialize<MasterDataSourceSettings>(File.ReadAllText(SettingsPath));
                if (settings is null) return;
                SourcePath = string.IsNullOrWhiteSpace(settings.SourcePath) ? DefaultVmdkPath : settings.SourcePath;
                MessageLanguage = NormalizeLanguage(settings.Language);
                UseVmdkSource = settings.UseVmdk;
                WritebackProviderId = string.IsNullOrWhiteSpace(settings.WritebackProviderId)
                    ? EmulatorWritebackConfiguration.AutomaticProvider
                    : settings.WritebackProviderId;
                WritebackExecutablePath = string.IsNullOrWhiteSpace(settings.WritebackExecutablePath)
                    ? null
                    : settings.WritebackExecutablePath;
                WritebackInstanceId = string.IsNullOrWhiteSpace(settings.WritebackInstanceId)
                    ? null
                    : settings.WritebackInstanceId;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not load MasterData settings: {ex}");
            }
        }

        private static void SaveSettings()
        {
            string? tempPath = null;
            try
            {
                using IDisposable dataLock = SharedDataAccess.Acquire(SharedDataAccess.FileKey(SettingsPath));
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                var settings = new MasterDataSourceSettings
                {
                    SourcePath = SourcePath,
                    Language = MessageLanguage,
                    UseVmdk = UseVmdkSource,
                    WritebackProviderId = WritebackProviderId,
                    WritebackExecutablePath = WritebackExecutablePath,
                    WritebackInstanceId = WritebackInstanceId
                };
                tempPath = SharedDataAccess.CreateTemporaryPath(SettingsPath, "settings");
                File.WriteAllText(tempPath, JsonSerializer.Serialize(
                    settings,
                    new JsonSerializerOptions { WriteIndented = true }));
                File.Move(tempPath, SettingsPath, overwrite: true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not save MasterData settings: {ex}");
            }
            finally
            {
                if (tempPath is not null && File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        private sealed class MasterDataSourceSettings
        {
            public string SourcePath { get; set; } = DefaultVmdkPath;
            public string Language { get; set; } = "TWZH";
            public bool UseVmdk { get; set; } = true;
            public string WritebackProviderId { get; set; } =
                EmulatorWritebackConfiguration.AutomaticProvider;
            public string? WritebackExecutablePath { get; set; }
            public string? WritebackInstanceId { get; set; }
        }

        private static void LoadPersons()
        {
            PersonArcs = ParseArcs<PersonList>(PERSON_PATH);
            foreach (var arc in PersonArcs)
            {
                foreach (var p in arc.data.list)
                    PersonDict[p.id] = p;
            }
        }

        private static void LoadEnemies()
        {
            EnemyArcs = ParseArcs<EnemyList>(ENEMY_PATH);
            foreach (var arc in EnemyArcs)
            {
                foreach (var e in arc.data.list)
                    EnemyDict[e.id] = e;
            }
        }

        private static void LoadSkills()
        {
            SkillArcs = ParseArcs<SkillList>(SKL_PATH);
            foreach (var arc in SkillArcs)
            {
                foreach (var s in arc.data.list)
                    SkillDict[s.id] = s;
            }
        }

        private static void LoadMessages()
        {
            var result = ReadMessages(MSG_PATH);
            MsgArcs = result.Arcs;
            MsgDict = result.Messages;
        }

        private static (HSDArc<MessageList>[] Arcs, ConcurrentDictionary<string, string> Messages) ReadMessages(string path)
        {
            var arcs = ParseArcs<MessageList>(path);
            var messages = new ConcurrentDictionary<string, string>();
            foreach (var arc in arcs)
            {
                var list = arc.data.list;
                for (int j = 0; j < list.Length - 1; j += 2)
                    messages[list[j]] = list[j + 1];
            }
            return (arcs, messages);
        }

        private static HSDArc<T>[] ParseArcs<T>(string directory) where T : new()
        {
            if (!Directory.Exists(directory))
                throw new DirectoryNotFoundException($"MasterData directory was not found: {directory}");

            string[] files = Directory.GetFiles(directory, DATAEXT)
                .OrderBy(path => Path.GetFileName(path).Contains("Tutorial", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                .ThenBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (files.Length == 0)
                throw new FileNotFoundException($"No MasterData archives were found in: {directory}");

            var arcs = new HSDArc<T>[files.Length];
            Parallel.For(0, files.Length, i => arcs[i] = new HSDArc<T>(files[i]));
            return arcs;
        }

        public static IImage[] WeaponTypeIcons { get; private set; } = null!;
        public static IImage[] MoveTypeIcons { get; private set; } = null!;
        public static IImage[] OriginTypeIcons { get; private set; } = null!;


        public const int SkillAtlasCapacity = 169; // 13行 * 13列
        private const int SkillGridCols = 13;
        private const int SkillIconSize = 76;
        public static int SkillIconCount => SKILL_ATLAS_PATHS.Length * SkillAtlasCapacity;

        private const int WeaponIconSize = 56;
        private const int WeaponStartY = 317;
        private const int WeaponStartX = 1;

        private const int MoveIconSize = 56;
        private const int MoveStartY = 526;
        private const int MoveStartX = 352;

        private const int SpecialCooldownIconSize = 58;
        private const int SpecialCooldownIconCount = 10;

        private const int HpGaugeGlyphWidth = 32;
        private const int HpGaugeGlyphHeight = 38;
        private const int HpGaugeGlyphColumns = 11;

        private const int EnhanceIconSize = 56;
        private const int EnhanceStartX = 1;
        private const int EnhanceStartY = 1;
        public const int EnhanceGridCols = 36;
        public const int EnhanceGridRows = 4;  

        private const int OriginWidth = 90;
        private const int OriginHeight = 88;
        private const int OriginAtlasCellSize = 90;
        private const int OriginStartY = 171;
        private const int OriginStartX = -3;

        private static readonly Dictionary<int, IImage> skillIconCache = new();
        private static readonly LinkedList<int> skillAtlasLru = new();
        private static readonly object skillImageSync = new();
        private const int MaxLoadedSkillAtlases = 8;
        private static readonly Dictionary<int, IImage> enhanceIconCache = new();
        private static readonly Dictionary<string, IImage> abcsxCache = new();

        public static void InitImage()
        {
            STATUS = new Bitmap(Path.Combine(UI_PATH, "Status.png"));
            string statusPlistPath = Path.Combine(UI_PATH, "Status.plist");
            STATUS_ATLAS = File.Exists(statusPlistPath) ? OpenUiAtlas("Status") : null;
            string commonPlistPath = Path.Combine(UI_PATH, "Common.plist");
            COMMON_ATLAS = File.Exists(commonPlistPath) ? OpenUiAtlas("Common") : null;
            string resonatePlistPath = Path.Combine(UI_PATH, "Resonate.plist");
            RESONATE_ATLAS = File.Exists(resonatePlistPath) ? OpenUiAtlas("Resonate") : null;
            string abcsxPath = Path.Combine(UI_PATH, "ABCSX.webp");
            ABCSX_ATLAS = File.Exists(abcsxPath) ? new Bitmap(abcsxPath) : EmptyBitmap;
            var directory = new DirectoryInfo(UI_PATH);
            var files = directory.GetFiles("Skill_Passive*.png")
                                 .OrderBy(f => f.Name.Length)
                                 .ThenBy(f => f.Name)
                                 .ToArray();
            lock (skillImageSync)
            {
                SKILL_ATLAS_PATHS = files.Select(file => file.FullName).ToArray();
                ICON_ATLAS = new Bitmap?[files.Length];
                skillAtlasLru.Clear();
                skillIconCache.Clear();
            }
            WeaponTypeIcons = new IImage[(int)WeaponType.ColorlessBeast + 1];
            MoveTypeIcons = new IImage[(int)MoveType.Flying + 1];
            OriginTypeIcons = new IImage[(int)Origins.Engage + 1];
        }

        public static IImage GetSkillIcon(int id)
        {
            lock (skillImageSync)
            {
                if (skillIconCache.TryGetValue(id, out var cachedImage))
                {
                    TouchSkillAtlas(GetSkillAtlasIndex(id));
                    return cachedImage;
                }
                if (SKILL_ATLAS_PATHS.Length == 0)
                    throw new InvalidOperationException("Skill Atlases not initialized.");

                int atlasIndex = GetSkillAtlasIndex(id);
                int localIndex = atlasIndex == 0 && (id < 0 || id / SkillAtlasCapacity >= SKILL_ATLAS_PATHS.Length)
                    ? 1
                    : id % SkillAtlasCapacity;
                var sourceBitmap = GetSkillAtlas(atlasIndex);
                int row = localIndex / SkillGridCols;
                int col = localIndex % SkillGridCols;
                var rect = new PixelRect(col * SkillIconSize, row * SkillIconSize, SkillIconSize, SkillIconSize);
                var cropped = new CroppedBitmap(sourceBitmap, rect);
                skillIconCache[id] = cropped;
                return cropped;
            }
        }

        private static int GetSkillAtlasIndex(int id)
        {
            int index = id < 0 ? 0 : id / SkillAtlasCapacity;
            return index >= SKILL_ATLAS_PATHS.Length ? 0 : index;
        }

        private static Bitmap GetSkillAtlas(int atlasIndex)
        {
            Bitmap? bitmap = ICON_ATLAS[atlasIndex];
            if (bitmap is null)
            {
                bitmap = new Bitmap(SKILL_ATLAS_PATHS[atlasIndex]);
                ICON_ATLAS[atlasIndex] = bitmap;
            }

            TouchSkillAtlas(atlasIndex);
            TrimSkillAtlases(atlasIndex);
            return bitmap;
        }

        private static void TouchSkillAtlas(int atlasIndex)
        {
            var node = skillAtlasLru.Find(atlasIndex);
            if (node is not null) skillAtlasLru.Remove(node);
            skillAtlasLru.AddLast(atlasIndex);
        }

        private static void TrimSkillAtlases(int currentAtlasIndex)
        {
            while (skillAtlasLru.Count > MaxLoadedSkillAtlases)
            {
                var candidateNode = skillAtlasLru.First;
                while (candidateNode is not null
                    && (candidateNode.Value == 0 || candidateNode.Value == currentAtlasIndex))
                    candidateNode = candidateNode.Next;
                if (candidateNode is null) return;

                int candidate = candidateNode.Value;
                skillAtlasLru.Remove(candidateNode);
                foreach (int key in skillIconCache.Keys
                    .Where(key => GetSkillAtlasIndex(key) == candidate)
                    .ToArray())
                    skillIconCache.Remove(key);
                // Visible CroppedBitmap instances can still reference this source.
                // Dropping our strong reference lets Avalonia release it after recycling the controls.
                ICON_ATLAS[candidate] = null;
            }
        }

        private static void InvalidateSkillAtlas(int atlasIndex)
        {
            foreach (int key in skillIconCache.Keys
                .Where(key => GetSkillAtlasIndex(key) == atlasIndex)
                .ToArray())
                skillIconCache.Remove(key);
            ICON_ATLAS[atlasIndex]?.Dispose();
            ICON_ATLAS[atlasIndex] = null;
            var node = skillAtlasLru.Find(atlasIndex);
            if (node is not null) skillAtlasLru.Remove(node);
        }
        public static async Task<MasterDataWritebackResult> ReplaceSkillIcon(int id, string sourceFilePath)
        {
            if (SKILL_ATLAS_PATHS.Length == 0)
                throw new InvalidOperationException("Skill Atlases not initialized.");

            int atlasIndex = id < 0 ? 0 : id / SkillAtlasCapacity;
            int localIndex = id % SkillAtlasCapacity;
            if (atlasIndex >= SKILL_ATLAS_PATHS.Length)
            {
                atlasIndex = 0;
                localIndex = 1;
            }

            // Figure out the file path for the atlas
            string atlasPath = SKILL_ATLAS_PATHS[atlasIndex];
            string backupPath = atlasPath + ".bak";

            // If backup doesn't exist, create it
            if (!File.Exists(backupPath))
            {
                File.Copy(atlasPath, backupPath);
            }

            int row = localIndex / SkillGridCols;
            int col = localIndex % SkillGridCols;

            // Dispose the old avalonia bitmap so we can write to the file
            lock (skillImageSync)
            {
                InvalidateSkillAtlas(atlasIndex);
            }

            // Use SixLabors.ImageSharp to mutate the atlas
            using (var atlasImage = await SixLabors.ImageSharp.Image.LoadAsync(atlasPath))
            using (var sourceImage = await SixLabors.ImageSharp.Image.LoadAsync(sourceFilePath))
            {
                SixLabors.ImageSharp.Processing.ProcessingExtensions.Mutate(sourceImage, x => x.Resize(SkillIconSize, SkillIconSize));
                SixLabors.ImageSharp.Processing.ProcessingExtensions.Mutate(atlasImage, x => x.Opacity(0, new SixLabors.ImageSharp.Rectangle(col * SkillIconSize, row * SkillIconSize, SkillIconSize, SkillIconSize)));
                SixLabors.ImageSharp.Processing.ProcessingExtensions.Mutate(atlasImage, x => x.DrawImage(sourceImage, new SixLabors.ImageSharp.Point(col * SkillIconSize, row * SkillIconSize), 1f));
                await atlasImage.SaveAsWebpAsync(atlasPath);
            }

            return await WriteBackFilesAsync([atlasPath]);
        }

        public static async Task RestoreSkillIcon(int id)
        {
            if (SKILL_ATLAS_PATHS.Length == 0) return;

            int atlasIndex = id < 0 ? 0 : id / SkillAtlasCapacity;
            if (atlasIndex >= SKILL_ATLAS_PATHS.Length) atlasIndex = 0;
            string atlasPath = SKILL_ATLAS_PATHS[atlasIndex];
            lock (skillImageSync)
            {
                InvalidateSkillAtlas(atlasIndex);
            }
            await RestoreFilesByLocalPathAsync([atlasPath]);
        }

        public static IImage GetWeaponIcon(int id)
        {
            if (id < 0 || id >= WeaponTypeIcons.Length) return null!;
            if (WeaponTypeIcons[id] is null)
            {
                WeaponTypeIcons[id] = STATUS_ATLAS is not null
                    ? STATUS_ATLAS.GetGridCell("Icon_Weapon.png", id, new PixelSize(WeaponIconSize, WeaponIconSize))
                    : GetLegacyStatusIcon(WeaponStartX + WeaponIconSize * id, WeaponStartY, WeaponIconSize, WeaponIconSize);
            }
            return WeaponTypeIcons[id];
        }

        public static IImage GetMoveIcon(int id)
        {
            if (id < 0 || id >= MoveTypeIcons.Length) return null!;
            if (MoveTypeIcons[id] is null)
            {
                MoveTypeIcons[id] = STATUS_ATLAS is not null
                    ? STATUS_ATLAS.GetGridCell("Icon_Move.png", id, new PixelSize(MoveIconSize, MoveIconSize))
                    : GetLegacyStatusIcon(MoveStartX + MoveIconSize * id, MoveStartY, MoveIconSize, MoveIconSize);
            }
            return MoveTypeIcons[id];
        }

        public static IImage GetSpecialCooldownIcon(int cooldown)
        {
            if (STATUS_ATLAS is null || cooldown < 0 || cooldown >= SpecialCooldownIconCount)
                return EmptyBitmap;

            return STATUS_ATLAS.GetGridCell(
                "Icon_Crit.png",
                cooldown,
                new PixelSize(SpecialCooldownIconSize, SpecialCooldownIconSize),
                SpecialCooldownIconCount);
        }

        public static IImage GetHpGaugeDigitIcon(int digit, bool enemy)
        {
            if (STATUS_ATLAS is null || digit is < 0 or > 9)
                return EmptyBitmap;

            int index = (enemy ? HpGaugeGlyphColumns : 0) + digit;
            return STATUS_ATLAS.GetGridCell(
                "Font_HPGauge.png",
                index,
                new PixelSize(HpGaugeGlyphWidth, HpGaugeGlyphHeight),
                HpGaugeGlyphColumns);
        }

        public static ITextureAtlas OpenUiAtlas(string atlasName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(atlasName);
            UiAtlasPaths? preparedPaths = null;
            lock (sourceContextSync)
            {
                if (sourceContext is not null)
                    preparedPaths = sourceContext.EnsureUiAtlasLocalPaths(atlasName);
            }

            if (preparedPaths is not null)
                return PlistTextureAtlas.Load(preparedPaths.TexturePath, preparedPaths.PlistPath);

            string normalizedName = atlasName.EndsWith(".plist", StringComparison.OrdinalIgnoreCase)
                ? atlasName[..^6]
                : atlasName;
            if (string.IsNullOrWhiteSpace(normalizedName)
                || !string.Equals(Path.GetFileName(normalizedName), normalizedName, StringComparison.Ordinal))
                throw new ArgumentException("An atlas name must be a file name without a directory path.", nameof(atlasName));
            return PlistTextureAtlas.Load(Path.Combine(UI_PATH, normalizedName + ".plist"));
        }

        private static IImage GetLegacyStatusIcon(int x, int y, int width, int height)
        {
            if (STATUS == null) throw new InvalidOperationException("Status Atlas not initialized.");
            return new CroppedBitmap(STATUS, new PixelRect(x, y, width, height));
        }

        public static IImage GetOriginIcon(int id)
        {
            if (id < 0 || id >= OriginTypeIcons.Length) return null!;
            if (OriginTypeIcons[id] is null)
            {
                OriginTypeIcons[id] = STATUS_ATLAS is not null
                    ? STATUS_ATLAS.GetGridCell(
                        "Icon_MiniUnit_Head.png",
                        id,
                        new PixelSize(OriginAtlasCellSize, OriginAtlasCellSize),
                        OriginTypeIcons.Length)
                    : GetLegacyStatusIcon(
                        OriginStartX + OriginWidth * id,
                        OriginStartY,
                        OriginWidth,
                        OriginHeight);
            }
            return OriginTypeIcons[id];
        }

        public static IImage GetEnhanceIcon(int id)
        {
            id = id switch
            {
                <= 6 => id + 2,
                <= 9 => id + 3,
                <= 15 => id + 4,
                <= 73 => id + 5,
                _ => id + 6,
            };
            if (enhanceIconCache.TryGetValue(id, out var cached)) return cached;
            if (STATUS == null) throw new InvalidOperationException("Status Atlas not initialized.");
            int row = id / EnhanceGridCols;
            int col = id % EnhanceGridCols;
            if (row >= EnhanceGridRows) return null!;
            var cropped = new CroppedBitmap(STATUS,
                new PixelRect(EnhanceStartX + EnhanceIconSize * col, EnhanceStartY + EnhanceIconSize * row, EnhanceIconSize, EnhanceIconSize));
            enhanceIconCache[id] = cropped;
            return cropped;
        }

        public static IImage GetABCSXIcon(string name)
        {
            if (abcsxCache.TryGetValue(name, out var cached)) return cached;

            (ITextureAtlas? atlas, string? frame) = name switch
            {
                "A" => (COMMON_ATLAS, "Icon_PassiveA_L"),
                "B" => (COMMON_ATLAS, "Icon_PassiveB_L"),
                "C" => (COMMON_ATLAS, "Icon_PassiveC_L"),
                "S" => (COMMON_ATLAS, "Icon_PassiveS_L"),
                "X" => (RESONATE_ATLAS, "Icon_PassiveX_L"),
                _ => (null, null)
            };
            if (frame is not null
                && atlas is not null
                && atlas.TryGetFrame(frame, out _))
            {
                IImage atlasImage = atlas.GetFrameImage(frame);
                abcsxCache[name] = atlasImage;
                return atlasImage;
            }

            if (ABCSX_ATLAS == null || ReferenceEquals(ABCSX_ATLAS, EmptyBitmap))
                return EmptyBitmap;
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
        private static readonly Dictionary<string, Bitmap> otherIconCache = new();
        public static Bitmap GetOtherIcon(string iconName)
        {
            string targetName = !string.IsNullOrEmpty(iconName) ? iconName : "Icon_Chance";
            if (otherIconCache.TryGetValue(targetName, out var cachedBitmap))
            {
                return cachedBitmap;
            }
            try
            {
                var uri = new Uri($"avares://FEHagemu/Assets/UI/Icon/{targetName}.png");
                using var stream = AssetLoader.Open(uri);
                var newBitmap = new Bitmap(stream);

                otherIconCache[targetName] = newBitmap;
                return newBitmap;
            }
            catch
            {
                return otherIconCache.GetValueOrDefault("Icon_Chance")!;
            }
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
            STATUS_ATLAS?.Dispose();
            STATUS_ATLAS = null;
            COMMON_ATLAS?.Dispose();
            COMMON_ATLAS = null;
            RESONATE_ATLAS?.Dispose();
            RESONATE_ATLAS = null;
            STATUS?.Dispose();
            if (ABCSX_ATLAS != null && !ReferenceEquals(ABCSX_ATLAS, EmptyBitmap))
                ABCSX_ATLAS.Dispose();
            lock (skillImageSync)
            {
                foreach (var bmp in ICON_ATLAS) bmp?.Dispose();
                ICON_ATLAS = [];
                SKILL_ATLAS_PATHS = [];
                skillAtlasLru.Clear();
                skillIconCache.Clear();
            }
            foreach (var bitmap in faceCache.Values.Distinct())
                bitmap.Dispose();
            faceCache.Clear();
            enhanceIconCache.Clear();
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
            foreach (var arc in PersonArcs)
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
            if (index > -1 && index < arc.data.list.Length - 1 && ((index & 1) == 0))
            {
                arc.data.list[index + 1] = message;
                MsgDict[key] = message;
            }
            else
            {
                index = arc.data.list.Length;
                Array.Resize(ref arc.data.list, arc.data.list.Length + 2);
                arc.data.list[index] = key;
                arc.data.list[index + 1] = message;
                arc.data.size = (ulong)arc.data.list.Length / 2;
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
                skill.id_num = SkillDict.Values.Where(sk => sk.id_num < 10000).MaxBy(sk => sk.id_num)!.id_num + 1;
                skill.sort_value = SkillDict.Values.MaxBy(sk => sk.sort_value)!.sort_value + 1;
                Array.Resize(ref arc.data.list, arc.data.list.Length + 1);
                arc.data.list[^1] = skill;
                arc.data.size = (ulong)arc.data.list.Length;
                SkillDict.TryAdd(skill.id, skill);
            }
        }

        public static bool IsAddedSkill(Skill skill)
        {
            return skill.id.Contains("MOD", StringComparison.OrdinalIgnoreCase)
                && ModSkillArc?.data.list.Any(item => item.id == skill.id) == true;
        }

        public static bool IsAddedPerson(Person person)
        {
            return person.id.Contains("MOD", StringComparison.OrdinalIgnoreCase)
                && ModPersonArc?.data.list.Any(item => item.id == person.id) == true;
        }

        public static string CreateUniqueModId(string sourceId, string prefix, Func<string, bool> exists)
        {
            string body = sourceId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? sourceId[prefix.Length..]
                : sourceId;
            if (string.IsNullOrWhiteSpace(body)) body = "New";
            if (!body.Contains("MOD", StringComparison.OrdinalIgnoreCase)) body += "MOD";

            string baseId = prefix + body;
            string candidate = baseId;
            for (int suffix = 2; exists(candidate); suffix++)
                candidate = $"{baseId}_{suffix}";
            return candidate;
        }

        public static void UpsertModSkill(Skill skill, string? sourceId)
        {
            var arc = ModSkillArc ?? throw new InvalidOperationException("Skill Tutorial.bin.lz was not found.");
            int sourceIndex = string.IsNullOrEmpty(sourceId)
                ? -1
                : Array.FindIndex(arc.data.list, item => item.id == sourceId);
            if (sourceIndex < 0)
            {
                if (SkillDict.ContainsKey(skill.id))
                    throw new InvalidOperationException($"Skill ID '{skill.id}' already exists.");
                AddSkill(arc, skill);
                return;
            }

            if (!string.Equals(sourceId, skill.id, StringComparison.Ordinal)
                && SkillDict.ContainsKey(skill.id))
                throw new InvalidOperationException($"Skill ID '{skill.id}' already exists.");
            Skill previous = arc.data.list[sourceIndex];
            skill.id_num = previous.id_num;
            skill.sort_value = previous.sort_value;
            arc.data.list[sourceIndex] = skill;
            SkillDict.TryRemove(previous.id, out _);
            SkillDict[skill.id] = skill;
        }

        public static void UpsertModPerson(Person person, string? sourceId)
        {
            var arc = ModPersonArc ?? throw new InvalidOperationException("Person Tutorial.bin.lz was not found.");
            int sourceIndex = string.IsNullOrEmpty(sourceId)
                ? -1
                : Array.FindIndex(arc.data.list, item => item.id == sourceId);
            if (sourceIndex < 0)
            {
                if (PersonDict.ContainsKey(person.id))
                    throw new InvalidOperationException($"Person ID '{person.id}' already exists.");
                AddPerson(arc, person);
                return;
            }

            if (!string.Equals(sourceId, person.id, StringComparison.Ordinal)
                && PersonDict.ContainsKey(person.id))
                throw new InvalidOperationException($"Person ID '{person.id}' already exists.");
            Person previous = arc.data.list[sourceIndex];
            person.id_num = previous.id_num;
            person.sort_value = previous.sort_value;
            arc.data.list[sourceIndex] = person;
            PersonDict.TryRemove(previous.id, out _);
            PersonDict[person.id] = person;
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
            if (!skill.id.Contains("MOD", StringComparison.OrdinalIgnoreCase)) return;
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
            if (!p.id.Contains("MOD", StringComparison.OrdinalIgnoreCase)) return;
            int index = Array.FindIndex(arc.data.list, s => s.id == p.id);
            if (index > -1)
            {
                arc.data.list[index] = arc.data.list[^1];
                arc.data.list = arc.data.list[..^1];
                arc.data.size = (ulong)arc.data.list.Length;
                PersonDict.TryRemove(p.id, out _);
                DeleteMessage(ModMsgArc, $"M{p.id}");
                string body = StripIdPrefix(p.id, out string prefix);
                DeleteMessage(ModMsgArc, $"M{prefix}HONOR_{body}");
            }
        }
        public static void DeleteEnemy(HSDArc<EnemyList> arc, Enemy e)
        {
            if (!e.id.Contains("MOD", StringComparison.OrdinalIgnoreCase)) return;
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
            if (!key.Contains("MOD", StringComparison.OrdinalIgnoreCase)) return;
            int index = Array.FindIndex(arc.data.list, m => m == key);
            if (index > -1)
            {
                arc.data.list[index] = arc.data.list[^2];
                arc.data.list[index + 1] = arc.data.list[^1];
                arc.data.list = arc.data.list[..^2];
                arc.data.size = (ulong)arc.data.list.Length / 2;
                MsgDict.TryRemove(key, out _);
            }
        }

        public async static Task<Bitmap> GetFaceAsync(string face)
        {
            if (string.IsNullOrWhiteSpace(face)) return FallBackFace;
            if (faceCache.TryGetValue(face, out Bitmap? result)) return result;
            string path = GetPortraitLocalPath(face, "Face_FC");
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

        public static string GetPortraitLocalPath(string face, string portraitName)
        {
            if (string.IsNullOrWhiteSpace(face))
                return Path.Combine(FACE_PATH, "_missing_", portraitName + ".png");

            lock (sourceContextSync)
            {
                if (sourceContext is not null)
                    return sourceContext.EnsurePortraitLocalPath(face, portraitName);
            }

            string fileName = portraitName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                ? portraitName
                : portraitName + ".png";
            return Path.Combine(FACE_PATH, face, fileName);
        }

        public static Bitmap GetFieldBackground(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return EmptyBitmap;

            string? path = null;
            lock (sourceContextSync)
            {
                if (sourceContext is not null)
                    path = sourceContext.EnsureFieldLocalPath(id);
            }

            foreach (string extension in new[] { ".jpg", ".png", ".webp" })
            {
                if (path is not null) break;
                string candidate = System.IO.Path.Combine(
                    FIELD_PATH,
                    $"{System.IO.Path.GetFileNameWithoutExtension(id)}{extension}");
                if (File.Exists(candidate)) path = candidate;
            }

            if (File.Exists(path))
            {
                using var imageStream = File.OpenRead(path);
                return new Bitmap(imageStream);
            }
            return EmptyBitmap;
        }
    }

}
