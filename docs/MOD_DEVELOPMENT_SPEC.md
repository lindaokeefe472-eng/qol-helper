# 《Probably Stolen》Mod 架构设计与开发落地完整指南

> **文档目标**：为后续开发者/模型提供一份拿来即用、具备底层反编译证据支持的工程级开发指南，涵盖“秒出字（瞬时文本）”与“物品分类标注、自动归类、自动上下选择与一键选食物”两大核心 Mod 模块的完整实现。

---

## 目录
1. [游戏引擎与底层反编译架构发现](#1-游戏引擎与底层反编译架构发现)
2. [官方 Mod 加载系统与规范 (ModLoader & ModManifest)](#2-官方-mod-加载系统与规范-modloader--modmanifest)
3. [模块一：秒出字 Mod (InstantDialogueMod) 研发规范](#3-模块一秒出字-mod-instantdialoguemod-研发规范)
4. [模块二：物品分类与一键匹配 Mod (ItemHelperMod) 研发规范](#4-模块二物品分类与一键匹配-mod-itemhelpermod-研发规范)
5. [完整代码实现参考 (Ready-to-Compile C#)](#5-完整代码实现参考-ready-to-compile-c)
6. [manifest.xml 配置与编译部署流程](#6-manifestxml-配置与编译部署流程)
7. [TDD 自动化测试与实机验收方案](#7-tdd-自动化测试与实机验收方案)

---

## 1. 游戏引擎与底层反编译架构发现

- **引擎架构**：Unity 2022+ (MonoBleedingEdge 64 位运行时)。
- **核心程序集路径**：`./Probably Stolen_Data/Managed/`
  - `Assembly-CSharp.dll`：包含所有核心业务逻辑（对话管理、商店交易、货架背包、物品系统、Mod 加载器）。
  - `0Harmony.dll`：**游戏官方已内置 Harmony 库**，无需自带或外挂 BepInEx，游戏原生支持 Harmony 补丁。
  - `Febucci.TextAnimator.Runtime.dll`：负责打字机动画的核心插件。
  - `DseCecil.dll`：基于 Mono.Cecil 改造的内置元数据分析器，供游戏的 `ModScanner` 静态安全检查使用。
  - `UnityEngine.dll` / `UnityEngine.CoreModule.dll` / `UnityEngine.UI.dll`：Unity 核心 UI 模块。

---

## 2. 官方 Mod 加载系统与规范 (ModLoader & ModManifest)

### 2.1 目录结构标准
游戏会在根目录扫描 `Mods/` 文件夹。每个 Mod 必须有独立子目录：
```text
Probably Stolen/
└── Mods/
    └── ProbablyStolen.QoLHelper/
        ├── manifest.xml                <-- 必须的清单配置文件
        └── ProbablyStolen.QoLHelper.dll <-- 编译后的 Mod 程序集
```

### 2.2 清单格式 (manifest.xml)
由 `ModLoader.ParseManifestCore` 解析，XML 格式规范如下：
```xml
<?xml version="1.0" encoding="utf-8"?>
<ModManifest>
  <ID>ProbablyStolen.QoLHelper</ID>
  <Name>QoL Helper (Instant Dialogue &amp; Item Helper)</Name>
  <Author>Developer</Author>
  <ModVersion>1.0.0</ModVersion>
  <Description>秒出字、物品分类标注、自动归置、快捷上下选择与一键匹配食物需求助手</Description>
  <GameVersions>
    <Version>1.0</Version>
  </GameVersions>
  <Assemblies>
    <Assembly>ProbablyStolen.QoLHelper.dll</Assembly>
  </Assemblies>
  <Capabilities>
    <!-- 声明所需能力，避免被 ModScanner 拒绝 -->
    <Capability>All</Capability>
  </Capabilities>
</ModManifest>
```

### 2.3 Mod 生命周期接口 (IMod)
Mod 程序集必须包含且仅包含一个实现 `IMod` 接口的公开类：
```csharp
public interface IMod
{
    void Init();       // Mod 首次被载入时触发
    void OnEnable();   // Mod 被启用时触发（在此注册事件/注入逻辑）
    void OnDisable();  // Mod 被禁用时触发（在此清理事件/注销逻辑）
}
```
> **极其重要**：`ModLoader.LoadModDll` 内部逻辑会在实例化 `IMod` 后，**自动对当前程序集执行 `harmonyInstance.PatchAll(assembly)`**！开发者无需手动创建 Harmony 实例，只需使用标准的 `[HarmonyPatch]` 属性标注补丁类。

---

## 3. 模块一：秒出字 Mod (InstantDialogueMod) 研发规范

### 3.1 原版打字机与对话卡顿根因剖析
反编译 `DialogUIManager` 提取的关键流程：
1. **字符打字机**：`TextBubble` 包含 `Febucci.UI.Core.TypewriterByCharacter typewriter`。当展示文本时，逐字延时播放，极度磨叽。
2. **硬编码延时**：
   - `DialogUIManager.GetTypewriterDelay()` 默认返回 `2.0f / dialogSpeed`。
   - `DialogUIManager.OnCurrentTextDisplayed()` 在文字显示完毕后，依然强行注入 `0.85f * ScaleByDialogSpeed()` 的延时，然后才调用 `ScheduleAutoAdvance`。
3. **跳过机制**：
   - 原版自带 `DialogUIManager.SkipTypewriters()`，本质是遍历各个气泡调用 `typewriter.SkipTypewriter()`。

### 3.2 补丁设计方案
1. **即时跳过打字动画**：
   - 对 `DialogUIManager.DisplayClientText`（顾客说话）、`DialogUIManager.DisplaySecText`（安保说话）、`DialogUIManager.DisplayerPlayerText`（玩家说话）打 Postfix 补丁：
     在文本设定完成后，立刻调用 `clientBubble.typewriter.SkipTypewriter()`，并将文本状态推进到已完成。
2. **打字机延迟归零**：
   - Prefix 拦截 `DialogUIManager.GetTypewriterDelay()`，直接返回 `0f`（阻断原版 2 秒延时）。
3. **消除对话停留死区**：
   - Prefix 拦截 `DialogUIManager.ScaleByDialogSpeed(float delay)`，若当前处于快进模式，直接返回 `0f` 或极小值。
4. **空格/按键即刻跳至下一句**：
   - 在 Update 中监听 `KeyCode.Space` 或鼠标操作，触发 `DialogUIManager.OnAdvancePressed()`，消除文本停留，实现真正的一秒连过。

---

## 4. 模块二：物品分类与一键匹配 Mod (ItemHelperMod) 研发规范

### 4.1 核心数据结构与类拓扑

| 类名 | 命名空间/位置 | 核心作用与字段 |
| :--- | :--- | :--- |
| `GameItem` | 全局 | 物品核心类。属性：`identifier`、`name`、`unitValue`、`itemTypes`。方法：`IsTag(string tag)`、`IsOneOfTags(...)`、`GetDisplayName()`。 |
| `FoodItemHelper` | 全局 | 食物专属辅助类。常量：`CANNED_FOOD_TAG`、`MEAT_FOOD_TAG`、`FRESHNESS_TAG`、`CALORIE_VALUE_TAG`。方法：`GetFreshnessString()`。 |
| `StoreClient` | 全局 | 当前顾客。字段：`clientBuyingTagList`（需求标签列表，如包含 `"food"`）、`clientBuyingIdList`（特定需求物品 ID）。方法：`IsClientBuyingThisItem(GameItem item)`。 |
| `PlayerStore` | 全局 | 商店单例 `PlayerStore.instance`。字段：`currentClientInstance.storeClient`（当前进店顾客）、`gridInv`（商店背包/货架）。方法：`FindAllItem()`、`AddDirectSellingItemToTable(...)`。 |
| `ItemSelectHandler`| 全局 | 单例 `ItemSelectHandler.current`。管理当前鼠标选中/拿起物品。方法：`SelectItem(GameItem item)`、`TrySelectItem(GameItem item)`。 |
| `ModHook` | 全局 | 官方钩子单例 `ModHook.Instance`。事件：`OnCreateTooltipLate(RichTextBuilder, GameItem)`。 |
| `InventorySortHelper`| 全局 | 整理背包算法。方法：`Sort(GameGridInventory inv)`、`PackShelves(...)`。 |

### 4.2 子功能 1：物品分类专属标注 (Item Category Badging)
- **挂载点**：监听官方事件 `ModHook.Instance.OnCreateTooltipLate`。
- **实现逻辑**：
  ```csharp
  ModHook.Instance.OnCreateTooltipLate += (RichTextBuilder builder, GameItem item) => {
      string tagText = GetCategoryTag(item);
      if (!string.IsNullOrEmpty(tagText)) {
          builder.Append(tagText);
      }
  };
  ```
- **标签判定规则**：
  - **【食物 / FOOD】**：`item.IsTag("food")`，展示保质期与卡路里。
  - **【药品 / MEDS】**：`item.IsTag("medical") || item.IsTag("med") || item.IsTag("injector")`。
  - **【武器 / WEAPONS】**：`item.IsTag("gun") || item.IsTag("melee") || item.IsTag("ammo")`。
  - **【违禁品 / CONTRABAND】**：`item.IsTag("contraband")`。
  - **【赃物 / STOLEN】**：`item.IsTag("stolen")`。

### 4.3 子功能 2：自动上下顺序选择 (Item Cycle Navigation)
- **操作逻辑**：在游戏主循环（`MonoBehaviour.Update`）中监听按键：
  - `KeyCode.UpArrow`（或 `Keypad 8`）：选择前一个物品。
  - `KeyCode.DownArrow`（或 `Keypad 2`）：选择下一个物品。
- **实现算法**：
  1. 获取当前场景中的所有有效物品：`var items = PlayerStore.instance.FindAllItem();`
  2. 过滤掉已售出或在交易台上的物品，维护当前聚焦索引 `currentIndex`。
  3. 计算 `nextIndex = (currentIndex ± 1 + count) % count`。
  4. 调用 `ItemSelectHandler.current.SelectItem(items[nextIndex])`，完成物理光标拿起/选中。

### 4.4 子功能 3：一键按顾客需求选取/送达食物 (One-Key Food / Match)
- **操作逻辑**：按下快捷键 `F`（专门找食物）或 `Q`（自动匹配当前顾客任意需求）：
- **实现算法**：
  1. 获取当前进店的顾客：
     ```csharp
     var client = PlayerStore.instance?.currentClientInstance?.storeClient;
     ```
  2. 获取商店中的所有存货：
     ```csharp
     var allItems = PlayerStore.instance.FindAllItem();
     ```
  3. 过滤出满足条件的物品：
     - 若按 `F`：过滤 `item.IsTag("food")`。
     - 若按 `Q`：调用 `client.IsClientBuyingThisItem(item)`。
  4. 自动选取：
     - 单击按键：循环调用 `ItemSelectHandler.current.SelectItem(targetItem)`，直接吸附到玩家鼠标光标；
     - 双击或配合 Shift 按键：直接调用 `PlayerStore.instance.AddDirectSellingItemToTable(targetItem)`，将食物直接送至柜台准备收钱！

---

## 5. 完整代码实现参考 (Ready-to-Compile C#)

以下为开箱即用的单一源文件 Mod 实现代码，支持直接使用 `.NET Framework 4.8 / csc.exe` 或 Visual Studio 编译。

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace ProbablyStolen.QoLHelper
{
    // ==========================================
    // 1. Mod 核心入口 (实现官方 IMod 接口)
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
            
            // 注册物品悬浮提示分类标注钩子
            if (ModHook.Instance != null)
            {
                ModHook.Instance.OnCreateTooltipLate += OnCreateTooltipLateHandler;
            }

            // 挂载运行时按键监听脚本
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
    // 2. 秒出字补丁 (Harmony Patches)
    // ==========================================
    [HarmonyPatch(typeof(DialogUIManager))]
    public static class InstantDialoguePatches
    {
        // 瞬间跳过客户端气泡打字机
        [HarmonyPatch("DisplayClientText")]
        [HarmonyPostfix]
        public static void DisplayClientText_Postfix(DialogUIManager __instance)
        {
            if (__instance != null && __instance.clientBubble != null && __instance.clientBubble.typewriter != null)
            {
                __instance.clientBubble.typewriter.SkipTypewriter();
            }
        }

        // 瞬间跳过安保气泡打字机
        [HarmonyPatch("DisplaySecText")]
        [HarmonyPostfix]
        public static void DisplaySecText_Postfix(DialogUIManager __instance)
        {
            if (__instance != null && __instance.secBubble != null && __instance.secBubble.typewriter != null)
            {
                __instance.secBubble.typewriter.SkipTypewriter();
            }
        }

        // 瞬间跳过玩家气泡打字机
        [HarmonyPatch("DisplayerPlayerText")]
        [HarmonyPostfix]
        public static void DisplayerPlayerText_Postfix(DialogUIManager __instance)
        {
            if (__instance != null && __instance.playerBubble != null && __instance.playerBubble.typewriter != null)
            {
                __instance.playerBubble.typewriter.SkipTypewriter();
            }
        }

        // 将原版 2.0s 延迟直接归零
        [HarmonyPatch("GetTypewriterDelay")]
        [HarmonyPrefix]
        public static bool GetTypewriterDelay_Prefix(ref float __result)
        {
            __result = 0.0f;
            return false; // 跳过原方法逻辑
        }
    }

    // ==========================================
    // 3. 键盘导航、上下选择与一键匹配食物脚本
    // ==========================================
    public class QoLInputRunner : MonoBehaviour
    {
        private int _currentItemIndex = 0;

        private void Update()
        {
            // F 键：一键选取食物
            if (Input.GetKeyDown(KeyCode.F))
            {
                SelectMatchingItem(isFoodOnly: true);
            }

            // Q 键：一键匹配当前顾客意向物品
            if (Input.GetKeyDown(KeyCode.Q))
            {
                SelectMatchingItem(isFoodOnly: false);
            }

            // 方向键 上：选择上一个物品
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                CycleSelectItem(isForward: false);
            }

            // 方向键 下：选择下一个物品
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                CycleSelectItem(isForward: true);
            }

            // 空格键：在对话激活时快速跳进
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
                Debug.Log("[QoLHelper] 未找到匹配的物品！");
                return;
            }

            _currentItemIndex = (_currentItemIndex + 1) % candidateItems.Count;
            var chosenItem = candidateItems[_currentItemIndex];

            if (ItemSelectHandler.current != null)
            {
                ItemSelectHandler.current.SelectItem(chosenItem);
                Debug.Log($"[QoLHelper] 成功选中匹配物品: {chosenItem.GetDisplayName()}");
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

## 6. manifest.xml 配置与编译部署流程

### 6.1 编译命令 (使用内置 csc.exe)
可使用 Windows 自带的 `.NET Framework 4.8` 编译器执行一键编译：

```powershell
# 设定路径
$managed = "G:\game\Probably Stolen\Probably Stolen_Data\Managed"
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$outDir = "G:\game\Probably Stolen\Mods\ProbablyStolen.QoLHelper"

# 确保输出目录存在
New-Item -ItemType Directory -Force -Path $outDir

# 编译
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
  "源码路径\QoLHelperMod.cs"
```

### 6.2 配置文件布署
在 `$outDir\manifest.xml` 写入前述清单文件，游戏启动即自动装载。

---

## 7. TDD 自动化测试与实机验收方案

为遵守严苛的工业级落地标准，建议执行以下三道防线验收：

1. **第一防线：清单合规静态核验 (Schema Test)**
   - 编写测试脚本调用 `ModLoader.ParseManifestCore`，断言 `ID`、`Assemblies`、`Capabilities` 解析无报错。
2. **第二防线：安全扫描免拦截核验 (ModScanner Test)**
   - 调用游戏内置的 `ModScanner.Scan`，核验生成的 DLL，断言 `result.Refused == false` 且 `result.HardFlags.Count == 0`。
3. **第三防线：实机端到端验收 (E2E Test Checklist)**
   - [ ] 打开游戏，进入主菜单点击“Mods”，查看 `QoL Helper` 是否已自动识别且状态为激活。
   - [ ] 开启任意新游戏或存档，与进店顾客对话，断言打字机零延迟全显，无等待卡顿。
   - [ ] 鼠标悬浮在面包、拉面、枪械、违禁品上，断言卡片最下方正确展示醒目的彩色分类标注。
   - [ ] 顾客进店提出要食物时，按下键盘 `F`，断言货架上的食物立即被光标精准吸附并拾起。
   - [ ] 按下方向键 `↑` / `↓`，断言光标在货架各件存货间流畅平移切换。
