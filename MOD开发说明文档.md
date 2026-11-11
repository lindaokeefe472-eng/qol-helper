# 《Probably Stolen》Mod 研发落地完整规格与开发文档

> **提示**：本文档位于当前游戏根目录 `./MOD开发说明文档.md`。任何后续开发者或 AI 模型可直接根据本文档进行代码生成、编译与实机部署。

---

## 目录
1. [项目环境与底层逆向成果](#1-项目环境与底层逆向成果)
2. [游戏官方 Mod 系统工作原理与目录规范](#2-游戏官方-mod-系统工作原理与目录规范)
3. [Mod 1：秒出字功能 (Instant Dialogue) 详细设计](#3-mod-1秒出字功能-instant-dialogue-详细设计)
4. [Mod 2：物品分类标注、上下切换与一键选食物详细设计](#4-mod-2物品分类标注上下切换与一键选食物详细设计)
5. [开箱即用完整源码实现 (C#)](#5-开箱即用完整源码实现-c)
6. [manifest.xml 清单文件配置](#6-manifestxml-清单文件配置)
7. [本地一键编译命令与自动化部署](#7-本地一键编译命令与自动化部署)
8. [验收测试清单 (Checklist)](#8-验收测试清单-checklist)

---

## 1. 项目环境与底层逆向成果

- **游戏根目录**：`G:\game\Probably Stolen\`
- **游戏运行时**：Unity 2022+ 64-bit Mono 运行时。
- **核心程序集目录**：`G:\game\Probably Stolen\Probably Stolen_Data\Managed\`
  - `Assembly-CSharp.dll`：包含游戏全部核心逻辑（`DialogUIManager`, `GameItem`, `StoreClient`, `PlayerStore`, `ModLoader` 等）。
  - `0Harmony.dll`：**游戏官方已内置 0Harmony 库**，版本为 Harmony 2.x，无需外挂第三方加载器（如 BepInEx）。
  - `Febucci.TextAnimator.Runtime.dll`：用于对话气泡的打字机组件（`TypewriterByCharacter`）。
  - `DseCecil.dll`：游戏用于 `ModScanner` 静态安全扫描的 Mono.Cecil 库。
  - `netstandard.dll` / `UnityEngine.dll` / `UnityEngine.CoreModule.dll` / `UnityEngine.UI.dll`：基础运行库。

---

## 2. 游戏官方 Mod 系统工作原理与目录规范

### 2.1 目录摆放规范
游戏启动时，由 `ModLoader.LoadAllMods()` 扫描根目录下的 `Mods/` 文件夹。每个 Mod 必须是 `Mods/` 下的独立子目录：
```text
G:\game\Probably Stolen\Mods\
└── ProbablyStolen.QoLHelper\
    ├── manifest.xml                <-- 必须：Mod 清单配置
    └── ProbablyStolen.QoLHelper.dll <-- 必须：编译出的 Mod 核心动态库
```

### 2.2 清单格式 (manifest.xml)
由 `ModLoader.ParseManifestCore` 解析：
```xml
<?xml version="1.0" encoding="utf-8"?>
<ModManifest>
  <ID>ProbablyStolen.QoLHelper</ID>
  <Name>QoL Helper (秒出字与物品助手)</Name>
  <Author>Developer</Author>
  <ModVersion>1.0.0</ModVersion>
  <Description>秒出字、物品分类彩色标注、快捷上下选择与一键选食物助手</Description>
  <GameVersions>
    <Version>1.0</Version>
  </GameVersions>
  <Assemblies>
    <Assembly>ProbablyStolen.QoLHelper.dll</Assembly>
  </Assemblies>
  <Capabilities>
    <!-- 必须声明，否则会被游戏内置的 ModScanner 拦截 -->
    <Capability>All</Capability>
  </Capabilities>
</ModManifest>
```

### 2.3 Mod 入口与 Harmony 自动装配
- Mod 程序集内必须有一个公开类实现游戏内置的 `IMod` 接口（位于 `Assembly-CSharp.dll` 根命名空间）：
  ```csharp
  public interface IMod
  {
      void Init();       // Mod 载入时执行
      void OnEnable();   // Mod 启用时执行
      void OnDisable();  // Mod 禁用时执行
  }
  ```
- **关键机制**：反编译 `ModLoader.LoadModDll` 证实，游戏在调用 `IMod.Init()` 之前，**会自动对当前 Mod 程序集执行 `harmony.PatchAll(asm)`**！因此，开发者无需在代码中手动初始化 Harmony，直接使用标准属性标签即可：
  ```csharp
  [HarmonyPatch(typeof(DialogUIManager))]
  public static class MyPatch { ... }
  ```

---

## 3. Mod 1：秒出字功能 (Instant Dialogue) 详细设计

### 3.1 游戏原版文本缓慢机制反编译分析
反编译 `DialogUIManager` 提取出核心痛点：
1. **打字机逐字输出**：
   - `DialogUIManager.DisplayClientText` / `DisplaySecText` / `DisplayerPlayerText` 为气泡赋值后，气泡绑定的 `Febucci.UI.Core.TypewriterByCharacter` 会逐字打字播放动画，耗时长。
2. **硬编码延时累加**：
   - `DialogUIManager.GetTypewriterDelay()` 强制返回 `2.0f / dialogSpeed`。
   - `DialogUIManager.OnCurrentTextDisplayed()` 在文字显示完毕后，依然强行注入 `0.85f * ScaleByDialogSpeed()` 的延时，然后才进入 `ScheduleAutoAdvance`。
3. **原生跳过接口**：
   - 气泡对象 `bubble.typewriter` 自带 `SkipTypewriter()` 原生方法，调用即可立即完成文本全显。

### 3.2 补丁设计方案
- **Patch 1**：在 `DisplayClientText`、`DisplaySecText`、`DisplayerPlayerText` 之后挂载 Postfix 补丁，在文本启动时立即触发 `typewriter.SkipTypewriter()`，实现 0 毫秒文本全显。
- **Patch 2**：Prefix 拦截 `DialogUIManager.GetTypewriterDelay()`，直接返回 `0f`，打断 2 秒漫长等待。
- **Patch 3**：运行时监听 `空格键`（`KeyCode.Space`），当处于对话状态时，直接调用 `DialogUIManager.Instance.OnAdvancePressed()`，消除句间多余的 0.85 秒停顿。

---

## 4. Mod 2：物品分类标注、上下切换与一键选食物详细设计

### 4.1 核心数据结构与游戏 API

| 类名 | 关键属性 / 方法 | 作用 |
| :--- | :--- | :--- |
| `GameItem` | `IsTag(string tag)` / `GetDisplayName()` / `identifier` | 物品类，用于判断标签（如 `"food"`, `"gun"`, `"medical"`, `"contraband"`, `"stolen"`）。 |
| `StoreClient` | `clientBuyingTagList` / `IsClientBuyingThisItem(GameItem item)` | 顾客类，记录当前进店顾客的需求标签和是否愿意购买特定物品。 |
| `PlayerStore` | `PlayerStore.instance.currentClientInstance.storeClient` <br> `PlayerStore.instance.FindAllItem()` | 商店单例，用于获取当前顾客实例及全店所有货架/背包物品。 |
| `ItemSelectHandler` | `ItemSelectHandler.current.SelectItem(GameItem item)` | 鼠标拾取管理器，调用后直接在玩家光标上选中该物品。 |
| `ModHook` | `ModHook.Instance.OnCreateTooltipLate(RichTextBuilder, GameItem)` | 官方提供的悬浮窗信息扩展钩子，用于注入富文本标注。 |

### 4.2 物品分类彩色标注 (Category Badging)
利用游戏官方提供的 `ModHook.Instance.OnCreateTooltipLate` 事件，在物品信息卡片底部追加富文本彩色标签：
- `item.IsTag("food")` -> `<color=#FFA500>【食物 | FOOD】</color>`
- `item.IsTag("medical") || item.IsTag("med")` -> `<color=#00FF7F>【医疗 | MEDS】</color>`
- `item.IsTag("gun") || item.IsTag("ammo")` -> `<color=#1E90FF>【军备 | WEAPON】</color>`
- `item.IsTag("contraband")` -> `<color=#FF1493>【违禁品 | CONTRABAND】</color>`
- `item.IsTag("stolen")` -> `<color=#DC143C>【赃物 | STOLEN】</color>`

### 4.3 物品自动上下循环选择 (Cycle Navigation)
监听键盘 `↑`（`KeyCode.UpArrow`）和 `↓`（`KeyCode.DownArrow`）：
1. 调用 `PlayerStore.instance.FindAllItem()` 获取存货清单。
2. 内部维护 `_index` 游标，按按键方向循环步进 `(_index ± 1 + count) % count`。
3. 调用 `ItemSelectHandler.current.SelectItem(items[_index])` 自动将当前物品拾取到光标上。

### 4.4 顾客需求匹配与一键选取食物 (One-Key Select Food)
监听键盘 `F` 键（一键食物）与 `Q` 键（一键匹配顾客需求）：
1. 获取当前顾客：`var client = PlayerStore.instance.currentClientInstance?.storeClient;`
2. 过滤符合条件的物品：
   - 若按 `F`：筛选 `item.IsTag("food")`；
   - 若按 `Q`：筛选 `client.IsClientBuyingThisItem(item)`。
3. 若存在匹配项，循环聚焦并调用 `ItemSelectHandler.current.SelectItem(chosenItem)`。玩家单手按键即可瞬间拿到顾客要的食物，极速达成交易。

---

## 5. 开箱即用完整源码实现 (C#)

请将以下完整源码保存为 `G:\game\Probably Stolen\Mods\src\QoLHelperMod.cs`：

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace ProbablyStolen.QoLHelper
{
    // ==========================================
    // 1. Mod 官方入口
    // ==========================================
    public class QoLHelperMod : IMod
    {
        private static GameObject _runnerObject;

        public void Init()
        {
            Debug.Log("[QoLHelper] Mod Initialized.");
        }

        public void OnEnable()
        {
            Debug.Log("[QoLHelper] Mod Enabled.");
            
            // 注册物品提示卡片富文本分类标注
            if (ModHook.Instance != null)
            {
                ModHook.Instance.OnCreateTooltipLate += OnCreateTooltipLateHandler;
            }

            // 挂载运行时按键监听宿主
            if (_runnerObject == null)
            {
                _runnerObject = new GameObject("QoLHelper_InputRunner");
                UnityEngine.Object.DontDestroyOnLoad(_runnerObject);
                _runnerObject.AddComponent<QoLInputRunner>();
            }
        }

        public void OnDisable()
        {
            Debug.Log("[QoLHelper] Mod Disabled.");
            
            if (ModHook.Instance != null)
            {
                ModHook.Instance.OnCreateTooltipLate -= OnCreateTooltipLateHandler;
            }

            if (_runnerObject != null)
            {
                UnityEngine.Object.Destroy(_runnerObject);
                _runnerObject = null;
            }
        }

        private void OnCreateTooltipLateHandler(RichTextBuilder builder, GameItem item)
        {
            if (builder == null || item == null) return;

            string categoryLabel = "";
            if (item.IsTag("food"))
            {
                categoryLabel = "<color=#FFA500>【食物 | FOOD】</color>";
            }
            else if (item.IsTag("medical") || item.IsTag("med"))
            {
                categoryLabel = "<color=#00FF7F>【医疗 | MEDS】</color>";
            }
            else if (item.IsTag("gun") || item.IsTag("ammo"))
            {
                categoryLabel = "<color=#1E90FF>【军备 | WEAPON】</color>";
            }
            else if (item.IsTag("contraband"))
            {
                categoryLabel = "<color=#FF1493>【违禁品 | CONTRABAND】</color>";
            }
            else if (item.IsTag("stolen"))
            {
                categoryLabel = "<color=#DC143C>【赃物 | STOLEN】</color>";
            }

            if (!string.IsNullOrEmpty(categoryLabel))
            {
                builder.Append("\n" + categoryLabel);
            }
        }
    }

    // ==========================================
    // 2. 秒出字 Harmony 补丁
    // ==========================================
    [HarmonyPatch(typeof(DialogUIManager))]
    public static class InstantDialoguePatches
    {
        // 顾客说话瞬间全显
        [HarmonyPatch("DisplayClientText")]
        [HarmonyPostfix]
        public static void DisplayClientText_Postfix(DialogUIManager __instance)
        {
            if (__instance != null && __instance.clientBubble != null && __instance.clientBubble.typewriter != null)
            {
                __instance.clientBubble.typewriter.SkipTypewriter();
            }
        }

        // 安保说话瞬间全显
        [HarmonyPatch("DisplaySecText")]
        [HarmonyPostfix]
        public static void DisplaySecText_Postfix(DialogUIManager __instance)
        {
            if (__instance != null && __instance.secBubble != null && __instance.secBubble.typewriter != null)
            {
                __instance.secBubble.typewriter.SkipTypewriter();
            }
        }

        // 玩家说话瞬间全显
        [HarmonyPatch("DisplayerPlayerText")]
        [HarmonyPostfix]
        public static void DisplayerPlayerText_Postfix(DialogUIManager __instance)
        {
            if (__instance != null && __instance.playerBubble != null && __instance.playerBubble.typewriter != null)
            {
                __instance.playerBubble.typewriter.SkipTypewriter();
            }
        }

        // 消除原版 2 秒打字延时
        [HarmonyPatch("GetTypewriterDelay")]
        [HarmonyPrefix]
        public static bool GetTypewriterDelay_Prefix(ref float __result)
        {
            __result = 0.0f;
            return false;
        }
    }

    // ==========================================
    // 3. 键盘操作、自动上下选择与一键匹配食物
    // ==========================================
    public class QoLInputRunner : MonoBehaviour
    {
        private int _currentItemIndex = 0;

        private void Update()
        {
            // F 键：一键选取货架上的食物
            if (Input.GetKeyDown(KeyCode.F))
            {
                SelectMatchingItem(isFoodOnly: true);
            }

            // Q 键：一键匹配当前顾客意向物品
            if (Input.GetKeyDown(KeyCode.Q))
            {
                SelectMatchingItem(isFoodOnly: false);
            }

            // 方向键 ↑：选择上一个物品
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                CycleSelectItem(isForward: false);
            }

            // 方向键 ↓：选择下一个物品
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                CycleSelectItem(isForward: true);
            }

            // 空格键：对话时一键跳过当前句子停顿
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (DialogUIManager.Instance != null && DialogUIManager.Instance.isConversationActiveFlag)
                {
                    DialogUIManager.Instance.OnAdvancePressed();
                }
            }
        }

        private void SelectMatchingItem(bool isFoodOnly)
        {
            if (PlayerStore.instance == null) return;

            var items = PlayerStore.instance.FindAllItem();
            if (items == null || items.Count == 0) return;

            var client = PlayerStore.instance.currentClientInstance?.storeClient;

            var candidateItems = items.Where(item =>
            {
                if (item == null) return false;
                if (isFoodOnly)
                {
                    return item.IsTag("food");
                }
                else
                {
                    if (client == null) return item.IsTag("food");
                    return client.IsClientBuyingThisItem(item);
                }
            }).ToList();

            if (candidateItems.Count == 0)
            {
                Debug.Log("[QoLHelper] 未找到匹配的可用物品！");
                return;
            }

            _currentItemIndex = (_currentItemIndex + 1) % candidateItems.Count;
            var chosenItem = candidateItems[_currentItemIndex];

            if (ItemSelectHandler.current != null)
            {
                ItemSelectHandler.current.SelectItem(chosenItem);
                Debug.Log($"[QoLHelper] 成功选取物品: {chosenItem.GetDisplayName()}");
            }
        }

        private void CycleSelectItem(bool isForward)
        {
            if (PlayerStore.instance == null) return;

            var items = PlayerStore.instance.FindAllItem();
            if (items == null || items.Count == 0) return;

            if (isForward)
            {
                _currentItemIndex = (_currentItemIndex + 1) % items.Count;
            }
            else
            {
                _currentItemIndex = (_currentItemIndex - 1 + items.Count) % items.Count;
            }

            var chosen = items[_currentItemIndex];
            if (ItemSelectHandler.current != null && chosen != null)
            {
                ItemSelectHandler.current.SelectItem(chosen);
            }
        }
    }
}
```

---

## 6. manifest.xml 清单文件配置

请将以下文件保存为 `G:\game\Probably Stolen\Mods\ProbablyStolen.QoLHelper\manifest.xml`：

```xml
<?xml version="1.0" encoding="utf-8"?>
<ModManifest>
  <ID>ProbablyStolen.QoLHelper</ID>
  <Name>QoL Helper (秒出字与物品助手)</Name>
  <Author>Developer</Author>
  <ModVersion>1.0.0</ModVersion>
  <Description>秒出字、物品分类彩色标注、快捷上下选择与一键选食物助手</Description>
  <GameVersions>
    <Version>1.0</Version>
  </GameVersions>
  <Assemblies>
    <Assembly>ProbablyStolen.QoLHelper.dll</Assembly>
  </Assemblies>
  <Capabilities>
    <Capability>All</Capability>
  </Capabilities>
</ModManifest>
```

---

## 7. 本地一键编译命令与自动化部署

在当前电脑的 PowerShell 中执行以下命令，即可完成一键编译并直接输出到 `Mods/` 目录：

```powershell
$gameDir = "G:\game\Probably Stolen"
$managed = "$gameDir\Probably Stolen_Data\Managed"
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$outDir = "$gameDir\Mods\ProbablyStolen.QoLHelper"
$src = "$gameDir\Mods\src\QoLHelperMod.cs"

# 1. 确保输出目录存在
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

# 2. 编译生成 DLL
& $csc /target:library `
  /out:"$outDir\ProbablyStolen.QoLHelper.dll" `
  /r:"$managed\mscorlib.dll" `
  /r:"$managed\System.dll" `
  /r:"$managed\System.Core.dll" `
  /r:"$managed\netstandard.dll" `
  /r:"$managed\UnityEngine.dll" `
  /r:"$managed\UnityEngine.CoreModule.dll" `
  /r:"$managed\UnityEngine.UI.dll" `
  /r:"$managed\0Harmony.dll" `
  /r:"$managed\Assembly-CSharp.dll" `
  /r:"$managed\Febucci.TextAnimator.Runtime.dll" `
  "$src"

Write-Host "编译部署完成：$outDir\ProbablyStolen.QoLHelper.dll"
```

---

## 8. 验收测试清单 (Checklist)

后续模型或开发者实施完成后，可逐项核对验收：
- [ ] **Mod 识别**：进入游戏主界面点击“Mods”菜单，确认列表中出现 `QoL Helper (秒出字与物品助手)` 且为启用状态。
- [ ] **秒出字实测**：进入任意顾客交易，文本瞬间完整显示，按 `空格键` 瞬间推进至下一句，无拖沓打字机卡顿。
- [ ] **分类标注实测**：鼠标指向货架上的食物、枪支、药剂，悬浮卡片底部清晰出现彩色类别标签（如 `【食物 | FOOD】`）。
- [ ] **一键选食物实测**：进店顾客要食物时，按下 `F` 键，货架上的食物被光标精准拾起；多次按下在多个食物间切换。
- [ ] **上下选择实测**：在货架前按键盘 `↑` / `↓` 键，光标在各个可用物品间依次流畅切换。
