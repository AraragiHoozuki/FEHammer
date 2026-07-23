# FEHagemu

FEHagemu 是一个面向《火焰纹章 英雄》（Fire Emblem Heroes）的非官方游戏数据浏览与编辑工具。程序可以直接从 Android 模拟器的 `data.vmdk` 中读取数据和图片资源，并提供地图、角色、技能和 文本 的统一浏览/编辑界面。

> [!WARNING]
> 本工具会修改模拟器中的游戏资源。虽然程序会为修改文件建立备份，但仍建议在首次使用前备份模拟器实例或创建快照。不要在没有还原数据的情况下删除 FEHagemu 缓存，否则程序将无法继续自动还原先前写入模拟器的文件。

> [!NOTE]
> FEHagemu 不附带任何游戏资源。所有数据均从用户本机安装的游戏或用户准备的本地 `Data` 目录中读取。

## 目录

- [主要功能](#主要功能)
- [系统要求](#系统要求)
- [从源码运行](#从源码运行)
- [首次配置](#首次配置)
- [使用方法](#使用方法)
- [保存、备份和离线同步](#保存备份和离线同步)
- [缓存和多实例](#缓存和多实例)
- [资源路径参考](#资源路径参考)
- [模拟器兼容性](#模拟器兼容性)
- [常见问题](#常见问题)
- [已知限制](#已知限制)
- [项目结构](#项目结构)
- [开发注意事项](#开发注意事项)
- [免责声明](#免责声明)
- [许可证](#许可证)

## 主要功能

### 数据读取

- 直接读取稀疏 VMDK 中的 MBR 分区和 ext 文件系统，无需把虚拟磁盘挂载为 Windows 盘符。
- 自动定位 FEH 资源根目录：
  - `data/com.nintendo.zaba/files/assets`
  - `assets`
  - 直接以 `Common` 为根目录的数据副本
- 读取并解析：
  - `Common/SRPG/Person` 角色数据
  - `Common/SRPG/Enemy` 敌方数据
  - `Common/SRPG/Skill` 技能数据
  - `Common/SRPGMap` 地图数据
  - `{语言}/Message/Data` Message 数据
  - `Common/Face` 角色头像
  - `Common/Field` 地图背景
  - `Common/UI` 技能图标和 UI Atlas
- 自动识别扩展名为 `.png`、实际内容为 WebP 的游戏图片。
- 支持由 `.plist` 描述的纹理 Atlas，包括 `Status`、`Common` 和 `Resonate`。
- 可以从包名带随机后缀的 `app/com.nintendo.zaba-*/base.apk` 中读取 APK 内的公共 UI Atlas。
- 按需加载头像、地图背景和技能图标，并限制技能 Atlas 缓存数量，降低主界面内存占用。

### 地图

- 从 `Common/SRPGMap` 自动建立地图列表。
- 按文件名搜索并加载地图。
- 显示 Field 背景、地形、出击点、单位头像和地图网格。
- 可以单独控制地图网格和地形覆盖层是否显示。
- 切换 Field 背景，并编辑地形、我方出击点和地图 Unit。
- 双击格子打开模态 Unit 编辑窗口：
  - 查看坐标、地形和出击点状态。
  - 横向浏览同一格子的多个 Unit。
  - 新增、复制、粘贴、删除或更换 Unit 角色。
  - 编辑等级、突破、属性、阵营、移动行为和生成条件。
  - 编辑武器、辅助、奥义和 A/B/C/S/X 技能槽。
  - 显示被动技能槽位图标、奥义 CD 和 SpawnTurn 数字。
- 保存按钮位于地图编辑区域，保存后进入统一备份和写回流程。

> [!CAUTION]
> 当前版本的“变更地图大小”命令尚无独立尺寸输入界面。执行它会按内部尺寸重新建立地形数组并清空当前地图中的 Unit，不建议在正常编辑流程中使用。

### 角色

- 左侧使用虚拟化头像列表，右侧显示角色详情或编辑界面。
- 支持名称、版本、武器类型、移动类型和特殊角色类型筛选。
- 浏览界面显示：
  - 头像、武器类型、移动类型和一个或多个 Origin 图标。
  - 基础属性、成长值和总值。
  - 40 级 Neutral、Hone（高成长）和 Flaw（低成长）属性。
  - 普通角色的各项标准属性和总属性排名；Enemy 不参与排名。
  - 非空技能，以“图标 + 名称”方式自动换行显示。
- 点击角色技能可以跳转到对应技能页面。
- 使用详情顶部的“编辑/浏览”按钮原地切换模式，不会弹出独立角色编辑窗口。
- 编辑字段按折叠面板分类：基础信息、战斗分类、基础属性与成长、技能数组、传说/神阶数据和内部数据。
- `Stats` 支持 `-32768` 到 `32767` 的有符号数值。
- 右键菜单支持显示同角色、导出 JSON，以及删除本程序新增的角色。

### 技能

- 左侧为技能列表，右侧为详情或 Tab 化编辑界面。
- 支持名称、技能类别、武器/移动限制、专属、锻造和最高级技能等筛选。
- 可以切换“图标 + 名称”和仅图标显示方式。
- 列表和详情中的奥义会显示 CD 数字。
- 浏览界面显示：
  - 名称、ID、类别、SP、说明和专属状态。
  - 技能前置/后置链。
  - 武器锻造前、锻造后和锻造效果。
  - 无法装备的武器与移动类型。
  - 持有技能的角色头像。
- 点击持有者头像可以跳转到对应角色页面。
- 可以把技能详情钉在独立窗口中，便于对照多个技能。
- 编辑界面按基础、装备、战斗、属性、Flags、高级和关联等页面组织字段。
- 右键菜单支持跳转前后置技能、钉住详情、显示相同 Ability 技能、导出 JSON，以及删除本程序新增的技能。

### Message

- 浏览当前语言下所有有效 Message 项。
- 按 Message ID、文本内容或归档文件名搜索。
- 显示 Message 所属归档。
- 编辑当前选中的文本并保存回对应归档。
- 切换语言只会重新加载 Message，不会改变角色、技能、地图、Face、Field 或 UI 资源。

### 还原

- 列出 FEHagemu 记录的全部修改文件。
- 按角色、技能、敌方、地图、文本、头像、UI 等类别显示。
- 区分“待同步”“已写入模拟器”和“仅本地”状态。
- 支持全选、清除选择、还原所选和全部还原。
- 还原完成后会删除对应备份和修改记录，并重新加载 MasterData。

### 工具

- **Flag(Bitmask) 计算器**：选择 Flag 类型，通过复选项组合或解析数值。
- **状态浏览器**：查看 `ENHANCE` 状态图标、内部编号和 Message 说明。
- **About**：显示程序版本、运行环境和程序数据目录。

## 系统要求

### 直接运行

- Windows 10 或更高版本。
- 已安装《火焰纹章 英雄》的 Android 模拟器实例。
- 当前完整支持 LDPlayer 9（雷电模拟器 9）的自动检测和写回。
- 游戏已经下载所需语言与资源包；空语言目录会显示为暂不支持。
- 如果发布包不是 self-contained，需要安装 .NET 8 Desktop Runtime。

### 从源码构建

- .NET 8 SDK 或更高版本。
- Visual Studio 2022、Rider 或 `dotnet` CLI。
- 能够恢复项目使用的 NuGet 包。
- 仓库中的 `HSDArcSourceGenerator` 项目必须与 `FEHagemu` 位于同一级目录。

## 从源码运行

在仓库根目录执行：

```powershell
dotnet restore FEHammer.sln
dotnet build FEHammer.sln -c Debug
dotnet run --project FEHagemu/FEHagemu.csproj
```

构建 Release：

```powershell
dotnet build FEHammer.sln -c Release
```

生成依赖本机 .NET Runtime 的 Windows x64 发布目录：

```powershell
dotnet publish FEHagemu/FEHagemu.csproj -c Release -r win-x64 --self-contained false
```

发布结果通常位于：

```text
FEHagemu/bin/Release/net8.0/win-x64/publish/
```

## 首次配置

### 1. 选择 VMDK

打开：

```text
数据 > MasterData > VMDK 数据源 > 选择 VMDK...
```

选择模拟器实例的 `data.vmdk` 文件。例如：

```text
D:\App\LDPlayer9\vms\leidian0\data.vmdk
```

只需要选择 `data.vmdk` 本身。界面中类似 `data.vmdk\1.img\data\...` 的路径表示虚拟磁盘内的分区和文件，不是 Windows 中可以继续展开选择的普通目录。

程序会优先使用已保存的路径。如果默认路径不存在，还会根据 VMDK 附近目录、正在运行的 LDPlayer 进程、注册表安装信息和常见安装目录尝试发现 LDPlayer。

选择成功后，菜单状态会显示数据源路径，主界面底部状态会显示已加载的角色和技能数量。

### 2. 配置 LDPlayer 写回

读取 VMDK 不要求模拟器启动，但把修改同步回游戏需要运行中的 LDPlayer 实例和 Root ADB 权限。

打开：

```text
数据 > MasterData > VMDK 数据源 > LDPlayer 写回设置...
```

推荐首先使用：

- 自动检测 `ldconsole.exe` 或 `dnconsole.exe`。
- 根据 VMDK 所在的 `leidianN` / `instanceN` 目录自动检测实例编号。

自动检测失败时，可以手动设置：

- LDPlayer 控制台程序路径。
- 非负的实例编号。

写回前需要在 LDPlayer 设置中启用 Root 权限并重启模拟器。程序会检查实例状态、ADB 可用性和 `uid=0`，任何检查失败都会保留本地修改而不会丢弃。

### 3. 选择文本语言

打开：

```text
数据 > MasterData > 文本语言
```

语言列表来自资源根目录下除 `Common` 外的文件夹，例如 `TWZH`、`USEN`、`JPJA`。只有包含可读取 `Message/Data/*.lz` 的语言可以选择；尚未下载或为空的语言会显示但不可用。

默认优先选择：

1. 上次使用的语言。
2. `TWZH`。
3. 第一个已下载的可用语言。

### 4. 本地 Data 目录模式

菜单中的：

```text
数据 > MasterData > VMDK 数据源 > 使用本地 Data 目录
```

会读取程序目录下的 `Data` 文件夹。支持以下任一布局：

```text
Data/data/com.nintendo.zaba/files/assets/...
Data/assets/...
Data/Common/...
```

本地 Data 模式主要用于数据分析、只读浏览和开发调试。当前版本不会把编辑后的 AppData 工作副本同步回源 `Data` 目录；模拟器写回、待同步记录和还原页面也以 VMDK 模式为主要目标。因此不要在此模式下进行需要持久保存的编辑。

## 使用方法

### 浏览和编辑地图

1. 切换到“地图”页面。
2. 在左侧搜索或选择一个 `SRPGMap` 文件。
3. 使用“地图”菜单切换网格和地形显示。
4. 单击格子选择它；双击格子打开“格子 Unit 编辑”模态窗口。
5. 在窗口顶部修改地形或出击点。
6. 在横向 Unit 列表中选择要编辑的单位。
7. 使用 Unit 的右键菜单更换角色、复制或删除。
8. 使用右侧加号新增空 Unit；复制 Unit 后也可以在目标格右键快速粘贴。
9. 编辑 Unit 的属性、技能和生成参数。
10. 关闭格子窗口后，点击地图标题栏右侧的“保存地图”。

地图保存的内容包括地形数组、出击点和 Unit 数组。

### 浏览和编辑角色

1. 切换到“角色”页面。
2. 使用名称、图标筛选器或版本筛选缩小范围。
3. 单击头像，在右侧查看属性表、40 级属性、排名和技能。
4. 点击技能条目可以跳到“技能”页面。
5. 点击详情顶部的“编辑”，在当前页面展开需要修改的分类。
6. 完成修改后点击“保存”。

保存游戏原有角色时，FEHagemu 不会覆盖原始角色，而是：

1. 生成包含 `MOD` 标记的唯一 `PID_`。
2. 把新角色写入 `Common/SRPG/Person/Tutorial.bin.lz`。
3. 把名称和称号写入当前语言的 `Message/Tutorial.bin.lz`。

再次编辑这个新增角色时，会更新同一个 `MOD` 角色，不会继续创建副本。新增角色的 ID 必须保留 `PID_` 前缀和 `MOD` 标记。

### 浏览和编辑技能

1. 切换到“技能”页面。
2. 使用类别、武器、移动类型和状态筛选器查找技能。
3. 单击技能查看说明、技能链、锻造关系和持有角色。
4. 点击技能链条目可在技能之间跳转。
5. 点击持有者头像可跳到对应角色。
6. 使用右键菜单的“钉住详情”保留当前详情用于对照。
7. 点击详情顶部的“编辑”，通过 Tab 页面修改字段。
8. 点击“保存”。

保存游戏原有技能时，FEHagemu 会：

1. 生成包含 `MOD` 标记的唯一 `SID_`。
2. 把新技能写入 `Common/SRPG/Skill/Tutorial.bin.lz`。
3. 在当前语言的 `Message/Tutorial.bin.lz` 中建立名称和说明。

再次编辑这个新增技能时，会更新原有 `MOD` 技能。新增技能的 ID 必须保留 `SID_` 前缀和 `MOD` 标记。

### 编辑 Message

1. 切换到“Message”页面。
2. 搜索 ID、文本内容或归档文件名。
3. 选择一条 Message。
4. 在右侧编辑文本。
5. 点击“保存到归档”。

Message 页面保存的是当前选中条目所属的原始归档。切换语言后，页面会重新加载新语言的 Message 列表。

### 还原修改

1. 切换到“还原”页面。
2. 检查文件类别、虚拟磁盘路径、修改时间和同步状态。
3. 选择一个或多个文件并点击“还原所选”，或者使用“全部还原”。
4. 完成后程序会删除对应本地 `.bak`、模拟器中的 `.fehagemu.bak` 和修改记录。

如果文件状态为“已写入模拟器”，还原时模拟器必须运行且 ADB/Root 可用。仅处于“待同步”或“仅本地”状态的修改可以在模拟器未运行时取消。

## 保存、备份和离线同步

FEHagemu 不直接修改正在使用的 VMDK 扇区。读取通过内置 VMDK/ext 文件系统完成，写回通过 LDPlayer 控制台提供的 ADB 通道完成，这样可以避免在模拟器运行时直接修改虚拟磁盘文件系统。

一次保存的流程如下：

1. 从 VMDK 读取的文件位于 `%LOCALAPPDATA%\FEHagemu\Cache\MasterData\<数据源哈希>`。
2. 第一次保存某个文件时，在缓存中创建一次 `.bak`；已有备份时不会重复覆盖。
3. 修改后的归档先以临时文件写入，再原子替换缓存文件。
4. 程序在修改清单中登记远程路径、哈希、时间和同步状态。
5. 模拟器可用时，先把原始版本上传为 `<文件名>.fehagemu.bak`，再上传修改文件。
6. 上传后通过 SHA-256 校验，校验成功才把记录标记为“已写入模拟器”。

### 模拟器未启动

如果保存时 LDPlayer 未启动、仍在启动或 ADB 不可用：

- 编辑结果和备份保留在 AppData 缓存。
- 修改记录标记为“待同步”。
- 程序不会把保存操作当作数据丢失。
- 以后任意一次保存检测到模拟器可用时，会把全部待同步文件一起推送到模拟器。

### 游戏更新

重新加载数据时，如果 VMDK 中的文件哈希不同于 FEHagemu 上次写入的版本，程序会把它视为外部变化：

- 使用游戏的新文件更新备份基线。
- 对 `Tutorial.bin.lz` 重新叠加包含 `MOD` 的角色、技能、敌方和 Message 修改。
- 保留修改记录，供后续同步或还原使用。

这可以减少游戏更新覆盖自定义数据或让备份退回旧版本的风险，但更新后仍建议先检查新增内容，再重新同步到模拟器。

## 缓存和多实例

程序数据位于：

```text
%LOCALAPPDATA%\FEHagemu
```

目录结构：

```text
FEHagemu/
├─ Settings/
│  └─ masterdata-source.json    # 数据源、语言和写回设置
├─ Cache/
│  └─ MasterData/
│     └─ <数据源哈希>/          # 每个数据源独立的工作副本、备份和修改清单
└─ Locks/                       # 多进程共享锁
```

- 不同 VMDK 或本地 Data 路径使用不同的哈希缓存目录。
- 多个 FEHagemu 实例共享设置和缓存。
- 文件保存、清单更新、写回和缓存删除使用跨进程锁及临时文件，避免多个实例同时替换同一文件。
- 旧版本位于程序目录 `Data/.MasterDataCache` 的数据会在首次运行时按需迁移到 AppData。

### 删除缓存

打开：

```text
数据 > 删除缓存...
```

该操作会删除全部 MasterData 工作副本、`.bak`、修改清单和待同步数据，然后从当前数据源重新加载。数据源和语言设置仍会保留。

> [!CAUTION]
> 如果模拟器中仍有 FEHagemu 写入的修改，必须先在“还原”页面完成还原。清除缓存后，本地修改清单和备份将不存在，程序无法知道应当还原哪些文件。

## 资源路径参考

| 资源 | VMDK 内路径 |
| --- | --- |
| Message | `data/com.nintendo.zaba/files/assets/{语言}/Message/Data/*.lz` |
| 角色 | `data/com.nintendo.zaba/files/assets/Common/SRPG/Person/*.lz` |
| 敌方 | `data/com.nintendo.zaba/files/assets/Common/SRPG/Enemy/*.lz` |
| 技能 | `data/com.nintendo.zaba/files/assets/Common/SRPG/Skill/*.lz` |
| 地图 | `data/com.nintendo.zaba/files/assets/Common/SRPGMap/*.lz` |
| 头像 | `data/com.nintendo.zaba/files/assets/Common/Face/{FaceName}/*` |
| 地图背景 | `data/com.nintendo.zaba/files/assets/Common/Field/*` |
| UI Atlas | `data/com.nintendo.zaba/files/assets/Common/UI/*.plist` |
| 技能图标 | `data/com.nintendo.zaba/files/assets/Common/UI/Skill_Passive*.png` |
| APK 公共 UI | `app/com.nintendo.zaba-*/base.apk!/assets/Common/UI/*` |

文件名中的 `.png` 只是游戏使用的扩展名，实际编码可能是 WebP。不要仅根据扩展名选择图片解码器。

## 模拟器兼容性

### LDPlayer 9

当前具备完整支持：

- 从非默认安装目录发现 `data.vmdk`。
- 识别 `leidianN` 和 `instanceN` 实例目录。
- 查找 `ldconsole.exe` / `dnconsole.exe`。
- 从 VMDK 路径、运行进程、注册表和常见安装目录推断 LDPlayer 安装位置。
- 通过指定实例的 Root ADB 写入、拉取、移动、删除和校验文件。

### 其他基于 VMDK 的模拟器

读取层不依赖 LDPlayer API，因此在满足以下条件时有机会直接读取：

- VMDK 为当前读取器支持的稀疏 VMDK v1。
- VMDK 未使用 `streamOptimized` 压缩。
- 分区表为可识别的 MBR，并包含 ext 文件系统。
- FEH 数据仍位于兼容的 Android 数据路径。

写回层目前只有 LDPlayer Provider。对于其他模拟器，程序仍可读取 VMDK 和在本地缓存修改，但无法把数据推送到模拟器；还原已经写入模拟器的数据同样不可用。

新增模拟器支持时应实现：

- `IEmulatorWritebackProvider`：根据 VMDK 和设置解析模拟器实例。
- `IEmulatorWritebackTransport`：提供可用性检查、Push、Pull、Move、Delete 和 SHA-256 查询。
- 在 `EmulatorWritebackProviderRegistry` 中注册 Provider。

不要为其他模拟器复用硬编码的 LDPlayer 控制台参数。

## 常见问题

### 找不到 MasterData

确认选择的是实例的 `data.vmdk`，并且游戏至少完成一次资源下载。程序需要在资源根目录中找到 `Common/SRPG/Skill`。

### 语言显示但不可选择

语言目录存在，但 `Message/Data` 中没有 `.lz` 文件。进入游戏下载对应语言数据后重新加载 MasterData。

### 可以浏览，但保存后只显示“待同步”

VMDK 读取与模拟器写回是两条独立链路。检查：

1. 选择的实例是否已经启动完成。
2. LDPlayer 写回设置中的控制台路径和实例编号是否正确。
3. 模拟器是否已启用 Root 权限并重启。
4. ADB 是否能以 `uid=0` 访问 `/data/data/com.nintendo.zaba`。

修复后再次执行任意保存，程序会同步全部待处理文件。

### LDPlayer 安装在其他盘符

直接选择实际 `data.vmdk`。通常程序会沿 VMDK 父目录寻找控制台程序；失败时在“LDPlayer 写回设置”中手动选择 `ldconsole.exe` 或 `dnconsole.exe`。

### 保存原角色或技能后 ID 发生变化

这是预期行为。游戏原始条目不会被覆盖，程序会在 `Tutorial.bin.lz` 中建立带 `MOD` 标记的新条目，并为冲突 ID 自动添加编号后缀。

### 还原时提示模拟器不可用

已经写入模拟器的文件必须通过模拟器端 `.fehagemu.bak` 还原，因此需要启动对应实例并确保 Root ADB 可用。尚未写入模拟器的待同步文件可以离线还原。

### 图片扩展名正确但普通图片软件打不开

游戏中的部分 `.png` 实际是 WebP。FEHagemu 会按文件内容解码；使用其他工具时也应启用格式自动检测。

### 地图单位为什么仍然是头像

当前地图编辑器使用 `face_fc` 静态头像表示 Unit，尚未实现 SpriteStudio SSBP 动画播放。

## 已知限制

- 完整写回仅支持 LDPlayer 9。
- 不支持压缩的 `streamOptimized` VMDK。
- 不支持 GPT 分区表或非 ext 文件系统的模拟器磁盘。
- 只显示已经下载且包含 Message 归档的语言。
- 地图单位暂不播放 SSBP 动画。
- 游戏原始角色和技能只能以新 `MOD` 条目的形式编辑，不会原地覆盖。
- “变更地图大小”尚无独立尺寸输入界面，并且会重建地形、清空 Unit。
- 本地 Data 目录模式不会把编辑后的工作缓存同步回源目录，当前只应当用于读取和开发调试。

## 项目结构

| 路径 | 说明 |
| --- | --- |
| `MasterData.cs` | MasterData 生命周期、字典、图片入口和新增数据管理 |
| `Services/GameData/` | VMDK、ext 文件系统、缓存、备份、模拟器发现和写回 |
| `Services/Images/` | `.plist` 纹理 Atlas 解析与图片裁切 |
| `HSDArc/` | FEH 序列化数据类型和归档模型 |
| `HSDArcIO/` | FEH 二进制归档读写 |
| `ViewModels/` | 主界面、浏览器、地图和编辑器逻辑 |
| `Views/` | Avalonia 页面、对话框和工具窗口 |
| `DSDecmp/` | 第三方 Nintendo 压缩格式实现 |
| `../HSDArcSourceGenerator/` | HSDArc 序列化源生成器 |

核心技术：

- .NET 8
- Avalonia UI 11
- CommunityToolkit.Mvvm
- Semi.Avalonia / Ursa
- SixLabors.ImageSharp
- Roslyn Source Generator

## 开发注意事项

- 所有 Android 资源路径必须先通过 `GameAssetPath.Normalize`，禁止 `.`、`..` 和越出资源根目录的路径。
- 不要直接写入正在运行的 VMDK；新增模拟器应通过对应 Provider/Transport 写回。
- 新的缓存写操作应使用 `SharedDataAccess` 和临时文件原子替换。
- 新的可还原资源类型应登记到 `MasterDataModificationStore`，并提供本地与远程路径映射。
- 图片读取应按文件内容识别格式，不要假设 `.png` 一定是 PNG。
- 大型资源必须按需加载并设置缓存上限，避免恢复一次性加载全部头像或 Atlas 的旧行为。
- `DSDecmp` 是第三方代码；项目构建中该目录可能仍产生独立的可空性警告。

## 免责声明

本项目与 Nintendo、Intelligent Systems 或《火焰纹章 英雄》官方没有关联。使用本工具修改游戏数据可能导致游戏异常、数据重下载或违反相关服务条款，使用者应自行承担风险。

## 许可证

项目采用 MIT License，参见仓库根目录的 [`LICENSE.txt`](../LICENSE.txt)。游戏名称、图像和数据的权利归其各自权利人所有。
