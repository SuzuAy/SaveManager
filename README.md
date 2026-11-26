# SM (Save Manager)

给 **Casualties Unknown** 加**无上限存档槽**的 BepInEx 5 插件。
Unlimited save slots for **Casualties Unknown** — a BepInEx 5 plugin.

[中文](#中文) · [English](#english) · [License](#license)

---

## 中文

### 简介

原版只有一个续关点 `save.sv`，而且**读档成功后就把文件删掉**。SM 把每次存档拍成独立快照：游戏里随时按 `F5` 存，设置菜单里多出一个「存档」分页可以挑任意一档读，档数没有上限，游戏外也能直接拖文件换档。界面全部用游戏原生 prefab 拼，和设置菜单其它页一个样。

### 功能

- **无上限存档槽**：每次存档一个独立文件夹，自动命名（`2026-11-22_1534_D1`）
- **游戏内随时存档**：`F5`，不用回主菜单
- **存档页是独立窗口**（不在设置菜单里）：ESC 圆形按钮、主菜单右上角的圆形图标、`F8` 都能打开它
- **主菜单存档图标**：右上角圆形软盘按钮，没进游戏也能先管理存档
- **删除有二次确认**：点「删除此档」先变红提示，再点一次才真删；支持**批量管理**（多选后「删除所选」）
- **保存并退出到主菜单**：游戏内一键存档后直接回主菜单
- **ESC 暂停菜单的圆形存档按钮**：白线圆环 + 软盘图标，点一下打开独立存档窗口，弹出动画与原生按钮一致
- **独立存档窗口**：借用游戏设置窗口的外壳（边框/返回按钮原生），隐藏分页栏与「重置为默认」，内容就是存档页
- **设置页可折叠 + 原版控件**：行为 / 存档标记 / 其它 / 界面 分区可折叠，开关是右侧复选框、间隔是数字框、语言是下拉框
- **中英双语**：`Language / 语言` 可选「跟随游戏（默认）/ 中文 / English」
- **游戏内直接读档**：解除暂停 → 回主菜单 → 铺档 → 继续，回到该层开头
- **沙盒档 / 实机档双向隔离**：沙盒里只看得到沙盒档，反之亦然，跨模式读取会被拒绝
- **联机兼容**：把 [CasualtiesTogether](https://github.com/creaturefeaturelarry/casualties-together) 的 `mp_save\`（每个玩家的 `STEAM_<id>\save.sv`、`mp_rules.json`）整目录收进槽里
- **覆盖前自动留上一档**，可一键回滚
- **所有开关都能在游戏里改**（存档页 →「存档选项与设置 ▸」独立子页），改完立即写盘，下次启动保持
- **控制台命令**，方便脚本化

### 安装

需要 **BepInEx 5.4.23.x**（`netstandard2.1`）。

- **Thunderstore**：[**SuzuAy-SaveManager**](https://thunderstore.io/c/casualties-unknown/p/SuzuAy/SaveManager/)（社区 Casualties: Unknown）
- **Gale**：Gale 是 Thunderstore 客户端，在 Mods 页搜 `SaveManager` 即可（或直接装上面的包）
- **手动**：

```
<游戏目录>\BepInEx\plugins\SaveManager\SaveManager.dll
```

启动后 `BepInEx\LogOutput.log` 里应能看到 `设置页已注册「存档」分页`。

### 使用

| 键 | 作用 |
|---|---|
| `F5` | 存档到新槽（自动命名） |
| `F8` | 打开设置 →「存档」分页（游戏内会先自动暂停） |
| `F9` | 读最近一档（按当前模式取） |

也可以走原生路径：`Esc` 暂停 → 设置 → 点「存档」。

存档页布局：

```
┌──────────────┬──────────────────────────────────────────┐
│ ＋ 新建存档   │ 已选：2026-11-22_1534_D1                 │
│ 读取最近一档  │ ┌ 新名字 ──────────────────────────────┐ │
│ 打开文件夹    │ └──────────────────────────────────────┘ │
│ 读取此档      │ 共 4 档（无上限，按时间倒序）             │
│ 回滚上一档    │ ▶ 2026-11-22_1621_D1  D1 · 0m28s · 4件 · 联机 │
│ 删除此档      │   2026-11-22_1610_D1  D1 · 1m22s · 0件 · 沙盒 │
│ 存档设置 ▸    │   …（右侧列表可滚轮/拖拽滚动）            │
└──────────────┴──────────────────────────────────────────┘
```

控制台命令：

```
cssave [名字]      存一个新槽（不写名字=自动命名）
cslots             列出所有槽
csload <槽名>      读这个槽（回主菜单流程）
csslots            打开设置里的「存档」分页
csdir              打开存档目录
csdel <槽名>       删除
csrename <旧> <新>  改名
csrollback <槽名>   回滚到该槽的 backup/
csexport <槽名>     把槽铺成游戏当前的 save.sv
```

### 存档放在哪

```
%USERPROFILE%\AppData\LocalLow\Orsoniks\CasualtiesUnknown\SaveSlots\
  <槽名>\
    meta.json     元数据（时间/层数/存活/角色/件数/模式/沙盒/作弊/玩家）
    save.sv       单人存档（原版格式，gzip + JSON）
    mp\           联机存档（存在时才有）
    backup\       覆盖同名槽之前的上一档
```

不进游戏换档：

1. 关掉游戏
2. `SaveSlots\<槽名>\save.sv` 覆盖到 `...\Orsoniks\CasualtiesUnknown\save.sv`
3. 联机档：`SaveSlots\<槽名>\mp\` 整个覆盖到 `...\CasualtiesUnknown\mp_save\`
4. 开游戏 → 主菜单点「继续」

### 配置

`BepInEx\config\suzuay.savemanager.cfg`（也能在游戏「存档设置」子页改，重启后保持）

| 键 | 默认 | 说明 |
|---|---|---|
| `ExperimentalInGameLoad` | `true` | 游戏内直接读档 |
| `AutoSave.Enabled` / `Minutes` | `false` / `5` | 定时自动存档 |
| `MarkCheatedSave` | `false` | 存档标记 cheated（成就关闭到重启） |
| `SandboxNoCheatMark` | `true` | 沙盒里不标作弊 |
| `AllowInMultiplayer` | `true` | 联机时允许存读档 |
| `KeepLastBackup` | `true` | 覆盖同名槽前保留上一档 |
| `BypassTutorialGate` | `true` | 没做完基础教程也能读档 |
| `SlotRootOverride` | 空 | 自定义存档根目录 |
| `QuickSave` / `QuickLoad` / `Panel` | `F5` / `F9` / `F8` | 键位 |

### 已知限制

1. **读档回到当前层的开头**：原版 `save.sv` 只存身体/物品/世界数值，不含地图布局与玩家坐标，地图每层随机重新生成（原版 `saveandquit` 同样如此）
2. **深度 ≥ 9（WHERE WE MUST REACH 及之后）**：原版不许存档，本插件绕过控制台检查直接调 `SaveGame()`，但读回来是否正常未验证，别在终点层存
3. **联机**：只负责整目录搬运 `mp_save\`，多人局细节取决于 CasualtiesTogether 本身
4. **成就**：`MarkCheatedSave` 默认关；打开后按原版语义「成就关闭到重启」

### 兼容性

| 项 | 版本 |
|---|---|
| 游戏 | Casualties Unknown（Unity **2022.3.62f3**，Mono） |
| BepInEx | 5.4.23.x（`netstandard2.1`） |
| HarmonyX | 随 BepInEx 一起（`0Harmony.dll`） |
| 联机模组 | CasualtiesTogether（`mp_save\` 格式） |

### 构建

```powershell
git clone https://github.com/SuzuAy/SaveManager
cd SaveManager
Copy-Item local.props.example local.props   # 填自己的游戏路径
dotnet build SaveManager.csproj -c Release
```

`local.props` 填两个路径：`GameRoot`（游戏根目录，含 `CasualtiesUnknown_Data\Managed`）、`GaleProfileBepInEx`（BepInEx 目录，含 `core\` 与 `plugins\`）。编完自动把 DLL 拷到 `plugins\SaveManager\`。

也可以直接命令行覆盖：

```powershell
dotnet build -c Release -p:GameRoot="D:\Games\Casualties Unknown" -p:GaleProfileBepInEx="D:\Games\Casualties Unknown\BepInEx"
```

---

## English

### What it is

Vanilla *Casualties Unknown* keeps a single resume point (`save.sv`) and **deletes it once you load it**. SM snapshots every save into its own folder: press `F5` anytime, open the new **Save** tab in the settings menu to load any slot — unlimited slots — and you can swap files on disk while the game is closed. The UI is built from the game's own prefabs, so it matches the rest of the settings menu.

### Features

- **Unlimited slots**, auto-named (`2026-11-22_1534_D1`)
- **Save anytime in-game** with `F5`
- **Native "Save" tab** (6th settings page): left action column + scrollable slot list
- **In-game load**: unpause → main menu → restore files → continue (back to the start of the current layer)
- **Sandbox / normal saves are isolated** from each other; cross-loading is refused
- **Multiplayer aware**: captures the whole `mp_save\` folder used by [CasualtiesTogether](https://github.com/creaturefeaturelarry/casualties-together) (per-player `STEAM_<id>\save.sv`, `mp_rules.json`)
- **Keeps the previous version** of a slot before overwriting; one-click rollback
- **All options editable in-game** (Save → "Save options ▸" sub-page) and persisted to the config
- **Console commands** for scripting

### Install

Requires **BepInEx 5.4.23.x** (`netstandard2.1`).

- **Thunderstore**: [**SuzuAy-SaveManager**](https://thunderstore.io/c/casualties-unknown/p/SuzuAy/SaveManager/) (community Casualties: Unknown)
- **Gale**: Gale *is* a Thunderstore client — search `SaveManager` on the Mods page
- **Manual**: drop the DLL at `<GameDir>\BepInEx\plugins\SaveManager\SaveManager.dll`

### Keys

`F5` save · `F8` open the Save tab · `F9` load the most recent slot of the current mode

```
cssave [name]   cslots   csload <slot>   csslots   csdir
csdel <slot>    csrename <old> <new>    csrollback <slot>    csexport <slot>
```

### Save location

```
%USERPROFILE%\AppData\LocalLow\Orsoniks\CasualtiesUnknown\SaveSlots\<slot>\
    meta.json   save.sv   mp\   backup\
```

To swap a slot outside the game: copy `<slot>\save.sv` over `...\CasualtiesUnknown\save.sv` (and `<slot>\mp\` over `...\CasualtiesUnknown\mp_save\` for multiplayer saves), then launch the game and press Continue.

### Known limitations

1. Loading returns you to the **beginning of the current layer** — vanilla saves contain no map layout or player position
2. Depth ≥ 9 layers cannot be saved in vanilla; this mod bypasses that check but loading such a save is untested
3. Multiplayer handling is limited to copying `mp_save\`; the rest is up to CasualtiesTogether
4. `MarkCheatedSave` is off by default; enabling it disables achievements until restart

---

## License

**MIT License, Copyright (c) 2026 SuzuAy.** The full, legally binding text is in [LICENSE](LICENSE); the summary below is only a convenience and the LICENSE file wins.

**You may** use, copy, modify, merge, publish, distribute, sublicense and/or sell copies of the software; use it in videos, modpacks and on servers, commercial or not.

**You must** keep the copyright notice and this permission notice in all copies or substantial portions of the software.

**No warranty** — the software is provided *as is*, without warranty of any kind, express or implied, including but not limited to the warranties of merchantability, fitness for a particular purpose and non-infringement. In no event shall the authors be liable for any claim, damages or other liability arising from the software or its use.

**Scope & third parties** — this repository contains only original plugin source code. It does **not** ship, unpack or redistribute the game, BepInEx, HarmonyX, Unity, TextMeshPro or any other third-party asset; those are referenced at compile time only.

**No affiliation** — SM is an unofficial fan-made mod. It is not affiliated with, endorsed by, or supported by the developers or publishers of *Casualties Unknown*, Thunderstore, Gale, or the CasualtiesTogether mod.

**Your risk** — save files and mods can fail. Back up your saves (`%LocalLow%\Orsoniks\CasualtiesUnknown\`) before using save-manipulating tools.
