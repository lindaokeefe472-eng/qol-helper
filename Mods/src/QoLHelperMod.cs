using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace ProbablyStolen.QoLHelper
{
    // ==========================================
    // 1. Mod 入口
    // ==========================================
    public class QoLHelperMod : IMod
    {
        private static GameObject _runnerObject;

        public void Init(ModManifest manifest)
        {
            Debug.Log("[QoLHelper] Initialized.");
        }

        public void OnEnable()
        {
            Debug.Log("[QoLHelper] Enabled. build=hotkeys-1");

            if (_runnerObject == null)
            {
                _runnerObject = new GameObject("QoLHelper_InputRunner");
                UnityEngine.Object.DontDestroyOnLoad(_runnerObject);
                _runnerObject.AddComponent<QoLInputRunner>();
            }
        }

        public void OnDisable()
        {
            Debug.Log("[QoLHelper] Disabled.");

            if (_runnerObject != null)
            {
                UnityEngine.Object.Destroy(_runnerObject);
                _runnerObject = null;
            }
        }
    }

    // ==========================================
    // 2. 秒出字 Harmony 补丁
    // ==========================================
    [HarmonyPatch(typeof(DialogUIManager))]
    public static class InstantDialoguePatches
    {
        [HarmonyPatch("DisplayClientText")]
        [HarmonyPostfix]
        public static void DisplayClientText_Postfix(DialogUIManager __instance)
        {
            if (__instance != null && __instance.clientBubble != null && __instance.clientBubble.typewriter != null)
            {
                __instance.clientBubble.typewriter.SkipTypewriter();
            }
        }

        [HarmonyPatch("DisplaySecText")]
        [HarmonyPostfix]
        public static void DisplaySecText_Postfix(DialogUIManager __instance)
        {
            if (__instance != null && __instance.secBubble != null && __instance.secBubble.typewriter != null)
            {
                __instance.secBubble.typewriter.SkipTypewriter();
            }
        }

        [HarmonyPatch("DisplayerPlayerText")]
        [HarmonyPostfix]
        public static void DisplayerPlayerText_Postfix(DialogUIManager __instance)
        {
            if (__instance != null && __instance.playerBubble != null && __instance.playerBubble.typewriter != null)
            {
                __instance.playerBubble.typewriter.SkipTypewriter();
            }
        }

        // 原版 GetTypewriterDelay 返回 2.0f / dialogSpeed，等待过长，直接归零
        [HarmonyPatch("GetTypewriterDelay")]
        [HarmonyPrefix]
        public static bool GetTypewriterDelay_Prefix(ref float __result)
        {
            __result = 0f;
            return false;
        }

        // 原版句间停顿 = ScaleByDialogSpeed(0.85/0.99/1.0)，整体归零
        [HarmonyPatch("ScaleByDialogSpeed")]
        [HarmonyPrefix]
        public static bool ScaleByDialogSpeed_Prefix(ref float __result)
        {
            __result = 0f;
            return false;
        }

        // 若游戏设置为“手动推进”，每句读完立刻自动按一次推进
        [HarmonyPatch("OnCurrentTextDisplayed")]
        [HarmonyPostfix]
        public static void OnCurrentTextDisplayed_Postfix(DialogUIManager __instance)
        {
            if (__instance == null) return;
            if (!__instance.isWaitingForManualAdvance) return;
            if (__instance.currentDialog == null) return;
            if (__instance.IsChoice()) return;
            if (QoLInputRunner.Instance != null) QoLInputRunner.Instance.QueueAutoAdvance(__instance);
        }
    }

    // ==========================================
    // 3. 鉴定/估价：直接给出答案
    // ==========================================
    [HarmonyPatch(typeof(InspectionUIManager))]
    public static class InspectionAnswerPatches
    {
        [HarmonyPatch("OnInspectItem")]
        [HarmonyPostfix]
        public static void OnInspectItem_Postfix(InspectionUIManager __instance)
        {
            if (__instance == null) return;
            QoLInputRunner.Answer = InspectionAnswer.Build(__instance.currentInspectingItem);
        }

        [HarmonyPatch("CloseUI")]
        [HarmonyPostfix]
        public static void CloseUI_Postfix()
        {
            QoLInputRunner.Answer = null;
        }
    }

    // ==========================================
    // 3.5 鉴定服务：收费后生成的鉴定单直接丢进垃圾箱
    // ==========================================
    [HarmonyPatch(typeof(AppraisalServiceHelper))]
    public static class AppraisalSlipCleanupPatch
    {
        [HarmonyPatch("GiveAppraisalFee")]
        [HarmonyPostfix]
        public static void GiveAppraisalFee_Postfix()
        {
            QoLInputRunner.QueueAppraisalSlipCleanup();
        }
    }

    // ==========================================
    // 3.7 快速读档后把当前存档槽还原，避免自动存档写进快速槽
    // ==========================================
    [HarmonyPatch(typeof(PlayerStore))]
    public static class QuickLoadSlotRestorePatch
    {
        [HarmonyPatch("LoadGame")]
        [HarmonyPostfix]
        public static void LoadGame_Postfix()
        {
            QoLInputRunner.RestoreSlotAfterLoad();
        }
    }

    // ==========================================
    // 3.6 治安部检查：先把违禁/盗品塞进走私者暗格，塞不下再丢垃圾桶
    // ==========================================
    [HarmonyPatch(typeof(ContrabandHelper))]
    public static class InspectionContrabandPatch
    {
        // StartInspection 会从前台/称重台/背包/展示柜/垃圾桶逐个搜，
        // 在此之前把会被没收的东西挪走
        [HarmonyPatch("StartInspection")]
        [HarmonyPrefix]
        public static void StartInspection_Prefix()
        {
            QoLInputRunner.HideContrabandForInspection();
        }
    }

    public static class InspectionAnswer
    {
        // 顺序与游戏 toggle1~toggle5 一一对应
        private static readonly string[] CigaretteTags =
            { "CIGARETTE_LETTERING_ISSUE", "CIGARETTE_COLOR_ISSUE", "CIGARETTE_SEAL_ISSUE", "CIGARETTE_LOGO_ISSUE", "CIGARETTE_BOX_ISSUE" };
        private static readonly string[] CigaretteNames =
            { "Lettering", "Color", "Seal", "Logo", "Box" };

        private static readonly string[] StampTags =
            { "STAMP_LETTERING_ISSUE", "STAMP_LOGO_ISSUE", "STAMP_SEAL_ISSUE", "STAMP_AMOUNT_ISSUE", "STAMP_CHIP_ISSUE" };
        private static readonly string[] StampNames =
            { "Lettering", "Logo", "Seal", "Amount", "Chip" };

        private static readonly string[] InjectorTags =
            { "ISSUE_INDICATOR", "ISSUE_LABEL", "ISSUE_SERIAL", "ISSUE_BODY", "ISSUE_COLOR" };
        private static readonly string[] InjectorNames =
            { "Indicator", "Label", "Serial", "Body", "Color" };

        public static string Build(GameItem item)
        {
            if (item == null) return null;

            StringBuilder sb = new StringBuilder();
            sb.Append("[QoL] ");

            bool wrote = false;
            if (item.IsTag("CIGARETTE_TAG"))
            {
                AppendVisual(sb, item, "CATEGORY_GENUINE_CIGARETTE", "genuineCigarette", CigaretteTags, CigaretteNames);
                wrote = true;
            }
            else if (item.IsTag("FOOD_STAMP_TAG"))
            {
                AppendVisual(sb, item, "CATEGORY_GENUINE_STAMP", "genuineStamp", StampTags, StampNames);
                wrote = true;
            }
            else if (item.IsTag("INS_INJECTOR_TAG"))
            {
                AppendVisual(sb, item, "CATEGORY_GENUINE_INJECTOR", "genuineInjector", InjectorTags, InjectorNames);
                wrote = true;
            }
            else if (item.IsTag("RIBWICH_TAG"))
            {
                bool fake = RibwichHelper.IsCounterfeit(item);
                sb.Append(fake
                    ? "<color=#FF5555>RIBWICH: COUNTERFEIT</color>"
                    : "<color=#55FF55>RIBWICH: OK</color>");
                wrote = true;
            }

            // 估价服务：直接给出正确估值
            if (item.IsTag("APPRAISAL_ITEM"))
            {
                if (wrote) sb.Append("\n");
                int value = AppraisalHelper.GetCorrectBaseValue(item);
                sb.Append("<color=#FFD24A>Correct value: ").Append(value).Append("</color>");
                wrote = true;
            }

            if (!wrote) return null;
            return sb.ToString();
        }

        private static void AppendVisual(StringBuilder sb, GameItem item, string category,
            string genuineId, string[] tags, string[] names)
        {
            ItemFeature feature = item.FindItemFeatureByCategory(category);
            if (feature == null || feature.realCondition == null)
            {
                sb.Append("<color=#FFD24A>UNKNOWN</color>");
            }
            else if (string.Equals(feature.realCondition.identifier, genuineId))
            {
                // 真相存于 realCondition，fakeCondition 只是对外伪装
                sb.Append("<color=#55FF55>GENUINE</color>");
            }
            else
            {
                sb.Append("<color=#FF5555>COUNTERFEIT</color>");
            }

            StringBuilder found = new StringBuilder();
            for (int i = 0; i < tags.Length; i++)
            {
                if (!item.IsTag(tags[i])) continue;
                if (found.Length > 0) found.Append(", ");
                found.Append(names[i]).Append(" [").Append(i + 1).Append("]");
            }

            sb.Append("\nIssues: ").Append(found.Length > 0 ? found.ToString() : "none");
        }
    }

    // ==========================================
    // 4. 顾客身份：伪人（aug）与罪犯（通缉）
    // ==========================================
    public static class ClientInfo
    {
        public static string Build(StoreClient client)
        {
            if (client == null) return null;

            StringBuilder sb = new StringBuilder();

            if (client.isAug)
            {
                string tell = client.augIdleTell.ToString();
                if (tell == "Freeze") tell = "stands still";
                else if (tell == "Seizure") tell = "twitching";
                else if (tell == "Normal") tell = "normal";
                else if (tell == "Default") tell = "-";

                Append(sb, "[QoL] <color=#FF9E3D>AUG (fake person)</color>"
                    + "  Tier " + client.augTier + "  Tell: " + tell);
            }

            if (client.isWanted)
            {
                StringBuilder line = new StringBuilder("<color=#FF5555>WANTED (criminal)</color>");
                if (client.isStealthWanted) line.Append("  stealth");
                if (client.reportReward > 0) line.Append("  Reward: ").Append(client.reportReward);
                if (!string.IsNullOrEmpty(client.realName) && !string.Equals(client.realName, client.displayName))
                {
                    line.Append("  Real: ").Append(client.realName);
                }
                Append(sb, "[QoL] " + line.ToString());
            }

            if (client.IsSellingContraband()) Append(sb, "[QoL] <color=#FFD24A>Selling contraband</color>");
            if (client.IsSellingStolen()) Append(sb, "[QoL] <color=#FFD24A>Selling stolen goods</color>");

            if (sb.Length == 0) return null;
            return sb.ToString();
        }

        private static void Append(StringBuilder sb, string line)
        {
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(line);
        }
    }

    // ==========================================
    // 5. 键盘操作、自动上下选择与一键匹配食物
    // ==========================================
    public class QoLInputRunner : MonoBehaviour
    {
        public static QoLInputRunner Instance;

        // 鉴定答案文本，由 InspectionAnswerPatches 写入，OnGUI 负责画出
        public static string Answer;
        // 当前顾客身份提示（伪人/罪犯）
        public static string ClientAnswer;

        private static StoreClient _lastClient;

        private bool _advanceQueued = false;
        private GUIStyle _answerStyle;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Answer = null;
            ClientAnswer = null;
            _lastClient = null;
        }

        private void OnGUI()
        {
            string text = Answer;
            if (!string.IsNullOrEmpty(ClientAnswer))
            {
                text = string.IsNullOrEmpty(text) ? ClientAnswer : text + "\n\n" + ClientAnswer;
            }
            if (string.IsNullOrEmpty(text)) return;

            if (_answerStyle == null)
            {
                _answerStyle = new GUIStyle(GUI.skin.label);
                _answerStyle.fontSize = 18;
                _answerStyle.richText = true;
                _answerStyle.normal.textColor = Color.white;
            }

            int lines = 1;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n') lines++;
            }

            Rect box = new Rect(12f, 12f, 640f, 26f * lines + 16f);
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.78f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = old;
            GUI.Label(new Rect(box.x + 12f, box.y + 8f, box.width - 24f, box.height - 16f), text, _answerStyle);
        }

        private void UpdateClientAnswer()
        {
            StoreClient client = null;
            if (PlayerStore.Instance != null && PlayerStore.Instance.currentClientInstance != null)
            {
                client = PlayerStore.Instance.currentClientInstance.GetClientBlueprint();
            }

            if (client == _lastClient) return;
            _lastClient = client;
            ClientAnswer = ClientInfo.Build(client);
        }

        // 延迟一帧推进，避免在打字机回调里同步递归
        public void QueueAutoAdvance(DialogUIManager ui)
        {
            if (_advanceQueued) return;
            _advanceQueued = true;
            StartCoroutine(AutoAdvanceRoutine(ui));
        }

        private IEnumerator AutoAdvanceRoutine(DialogUIManager ui)
        {
            yield return null;
            _advanceQueued = false;
            if (ui == null) yield break;
            if (!ui.isWaitingForManualAdvance) yield break;
            if (ui.currentDialog == null) yield break;
            if (ui.IsChoice()) yield break;
            ui.OnAdvancePressed();
        }

        // SelectItem 是私有方法，通过 Harmony 的 AccessTools 取到后调用
        private static readonly MethodInfo _selectItemMethod =
            AccessTools.Method(typeof(ItemSelectHandler), "SelectItem");

        // 游戏内部物品类型标签均为大写
        private static readonly string[] FoodTags =
            { "FOOD", "FRESH_FOOD", "PROCESSED_FOOD", "TREAT", "BREVAGE" };

        private int _cycleIndex = -1;
        private int _queryIndex = -1;
        private bool _updateErrorLogged;
        private static bool _slipCleanupQueued;

        // 按住 Q 的连发节奏：先等一小会再开始连发，之后每 0.1s 卖一件
        private const float HoldStartDelay = 0.35f;
        private const float HoldRepeatInterval = 0.1f;
        private float _qRepeatAt;

        // 快速存档用的独立槽位，不碰玩家自己的存档
        private const int QuickSlotId = 999;

        // 撤回/下一步：记录每次 Q/F 放到称重台的物品和它原来的容器
        private class SellRecord
        {
            public GameItem item;
            public GameInventory origin;
        }

        private const int SellHistoryLimit = 50;
        private readonly List<SellRecord> _sellHistory = new List<SellRecord>();
        private readonly List<SellRecord> _redoHistory = new List<SellRecord>();

        // 快速读档后要把“当前存档槽”还原成读档前那个，否则之后的存档会写进快速槽
        public static bool SlotRestorePending;
        public static int SlotBeforeLoad;

        private static void PushSellRecord(GameItem item, GameInventory origin)
        {
            if (item == null || origin == null || Instance == null) return;
            Instance._sellHistory.Add(new SellRecord { item = item, origin = origin });
            if (Instance._sellHistory.Count > SellHistoryLimit)
            {
                Instance._sellHistory.RemoveAt(0);
            }
            // 新的操作让“下一步”的栈失效
            Instance._redoHistory.Clear();
        }

        // 撤回：把最近一次自动放上称重台的物品放回原容器
        private void UndoSell()
        {
            while (_sellHistory.Count > 0)
            {
                SellRecord rec = _sellHistory[_sellHistory.Count - 1];
                _sellHistory.RemoveAt(_sellHistory.Count - 1);
                if (rec.item == null)
                {
                    continue;
                }

                EmporiumEntry entry = EmporiumEntry.Instance;
                var counter = entry != null ? entry.backInvinvElement : null;
                if (counter == null || rec.item.parentInventory != counter || rec.origin == null)
                {
                    Debug.Log("[QoLHelper] Undo skipped: " + rec.item.GetDisplayName() + " is no longer on the counter.");
                    continue;
                }

                GraphUtils.TryAcceptAll(rec.origin, rec.item, -1);
                if (rec.item.parentInventory != rec.origin)
                {
                    GameItem originParent = rec.origin.GetParentItem();
                    Debug.Log("[QoLHelper] Undo failed: no room in "
                        + (originParent != null ? originParent.identifier : "origin"));
                    _sellHistory.Add(rec);
                    return;
                }

                _redoHistory.Add(rec);
                Debug.Log("[QoLHelper] Undid sale: " + rec.item.GetDisplayName());
                return;
            }

            Debug.Log("[QoLHelper] Undo: nothing to undo.");
        }

        // 下一步：把刚撤回的物品重新放回称重台
        private void RedoSell()
        {
            while (_redoHistory.Count > 0)
            {
                SellRecord rec = _redoHistory[_redoHistory.Count - 1];
                _redoHistory.RemoveAt(_redoHistory.Count - 1);
                if (rec.item == null) continue;

                GameInventory origin;
                if (!TryPlaceOnSellingCounter(rec.item, out origin))
                {
                    Debug.Log("[QoLHelper] Redo failed: counter has no room for " + rec.item.GetDisplayName());
                    _redoHistory.Add(rec);
                    return;
                }

                rec.origin = origin;
                _sellHistory.Add(rec);
                Debug.Log("[QoLHelper] Redid sale: " + rec.item.GetDisplayName());
                return;
            }

            Debug.Log("[QoLHelper] Redo: nothing to redo.");
        }

        // F5：存到独立快速槽位，结束后切回原来的槽位
        private void QuickSave()
        {
            PlayerStore store = PlayerStore.Instance;
            if (store == null)
            {
                Debug.Log("[QoLHelper] Quick save: no game running.");
                return;
            }

            int originalSlot = store.saveSlotId;
            try
            {
                store.saveSlotId = QuickSlotId;
                store.SaveGame();
                Debug.Log("[QoLHelper] Quick saved to slot " + QuickSlotId + ".");
            }
            catch (Exception e)
            {
                Debug.Log("[QoLHelper] Quick save failed: " + e.Message);
            }
            finally
            {
                store.saveSlotId = originalSlot;
            }
        }

        // F9：从快速槽位重开场景读档，读完后把当前槽位还原
        private void QuickLoad()
        {
            PlayerStore store = PlayerStore.Instance;
            if (store == null)
            {
                Debug.Log("[QoLHelper] Quick load: no game running.");
                return;
            }

            string quickFile = SaveFiles.AbsolutePath(SaveFiles.Slot(QuickSlotId));
            if (!System.IO.File.Exists(quickFile))
            {
                Debug.Log("[QoLHelper] Quick load: no quick save yet (slot " + QuickSlotId + ").");
                return;
            }

            NewGameData data = NewGameData.Instance;
            if (data == null)
            {
                Debug.Log("[QoLHelper] Quick load: NewGameData missing.");
                return;
            }

            SlotBeforeLoad = store.saveSlotId;
            SlotRestorePending = true;
            data.saveSlot = QuickSlotId;
            data.saveFile = null;
            Debug.Log("[QoLHelper] Quick loading slot " + QuickSlotId + "...");
            UnityEngine.SceneManagement.SceneManager.LoadScene("InventoryScene");
        }

        // 读档结束后把 NewGameData.saveSlot 和 PlayerStore.saveSlotId 还原，
        // 否则游戏之后自动存档会一直写进快速槽
        public static void RestoreSlotAfterLoad()
        {
            if (!SlotRestorePending) return;
            SlotRestorePending = false;

            NewGameData data = NewGameData.Instance;
            if (data != null)
            {
                data.saveSlot = SlotBeforeLoad;
                data.saveFile = null;
            }
            if (PlayerStore.Instance != null)
            {
                PlayerStore.Instance.saveSlotId = SlotBeforeLoad;
            }
            Debug.Log("[QoLHelper] Slot restored to " + SlotBeforeLoad + " after quick load.");
        }

        // 在估价输入框里打字时不要触发 Q/F/Z/X 热键
        private static bool IsTypingInField()
        {
            InspectionUIManager ui = InspectionUIManager.Instance;
            return ui != null && ui.isEditingAppraisalField;
        }

        // 鉴定面板还开着、且正在看的确实是估价物品时返回它，否则 null
        private static GameItem GetAppraisalTarget()
        {
            InspectionUIManager ui = InspectionUIManager.Instance;
            if (ui == null) return null;
            if (ui.appraisalPanel == null || !ui.appraisalPanel.activeInHierarchy) return null;
            GameItem item = ui.currentInspectingItem;
            if (item == null || !item.IsTag("APPRAISAL_ITEM")) return null;
            return item;
        }

        // 估价只差输入数字再点按钮，这里直接用正确估值提交
        public static bool TryQuickAppraise()
        {
            GameItem item = GetAppraisalTarget();
            if (item == null) return false;
            try
            {
                int value = AppraisalHelper.GetCorrectBaseValue(item);
                AppraisalServiceHelper.OnEstimate(item, value);
                InspectionUIManager.Instance.CloseUI();
                Debug.Log("[QoLHelper] Quick appraisal: " + item.GetDisplayName() + " = " + value);
                return true;
            }
            catch (Exception e)
            {
                Debug.Log("[QoLHelper] Quick appraisal failed: " + e.Message);
                return false;
            }
        }

        // 鉴定单是 GiveAppraisalFee 里同步塞到前台的，延后一帧再收走，
        // 免得在它自己的放置流程里挪物品
        public static void QueueAppraisalSlipCleanup()
        {
            _slipCleanupQueued = true;
        }

        // 鉴定收费生成的单子（物品 id = slip）落到前台后直接丢进垃圾箱
        private static void TrashAppraisalSlips()
        {
            EmporiumEntry entry = EmporiumEntry.Instance;
            if (entry == null || entry.frontInvinvElement == null) return;

            var items = GetAllStoreItems();
            int moved = 0;
            for (int i = 0; i < items.Count; i++)
            {
                GameItem item = items[i];
                if (item == null) continue;
                if (item.parentInventory != entry.frontInvinvElement) continue;
                if (!string.Equals(item.identifier, "slip", StringComparison.Ordinal)) continue;
                ModHelper.MoveItemToTrash(item);
                moved++;
                Debug.Log("[QoLHelper] Trashed appraisal slip: " + item.GetDisplayName());
            }
            if (moved == 0) Debug.Log("[QoLHelper] No appraisal slip found on front counter.");
        }

        // 检查开始前藏东西：走私者暗格里的物品会被判定为“藏匿”而不是搜到，
        // 所以优先塞暗格；没暗格或塞不下才丢垃圾桶
        public static void HideContrabandForInspection()
        {
            try
            {
                var items = GetAllStoreItems();
                if (items.Count == 0) return;

                var bays = new List<GameItem>();
                var risky = new List<GameItem>();
                for (int i = 0; i < items.Count; i++)
                {
                    GameItem item = items[i];
                    if (item == null) continue;

                    if (ContrabandHelper.IsSmugglerBay(item))
                    {
                        // 暗格本身要放在某个容器里才能往里塞东西
                        if (item.parentInventory != null) bays.Add(item);
                        continue;
                    }

                    // 与游戏检查同一套判定：CONTRABAND_ITEM_TAG 或 STOLEN_TAG
                    if (!ContrabandHelper.IsFlaggableItem(item)) continue;
                    if (!GeneralHelper.IsItemEffectivelyOwned(item)) continue;
                    if (IsInsideSmugglerBay(item)) continue;
                    if (HasAnyTag(item, ProtectedTags)) continue;
                    // 加工中的原料不动，免得搅乱机器里的生产流程
                    if (item.IsTag("PROGRESS_ITEM_TAG")) continue;
                    risky.Add(item);
                }

                if (risky.Count == 0)
                {
                    Debug.Log("[QoLHelper] Inspection: nothing to hide. bays=" + bays.Count);
                    return;
                }

                int hidden = 0;
                int trashed = 0;
                for (int i = 0; i < risky.Count; i++)
                {
                    GameItem item = risky[i];
                    if (item == null || item.parentInventory == null) continue;
                    // 上一层容器可能已经被整个塞进暗格了
                    if (IsInsideSmugglerBay(item)) continue;

                    if (TryHideInSmugglerBay(item, bays))
                    {
                        hidden++;
                        continue;
                    }

                    try
                    {
                        ModHelper.MoveItemToTrash(item);
                        trashed++;
                        Debug.Log("[QoLHelper] Trashed contraband: " + DescribeItem(item));
                    }
                    catch (Exception e)
                    {
                        Debug.Log("[QoLHelper] Trash contraband failed: " + DescribeItem(item) + " " + e.Message);
                    }
                }

                Debug.Log("[QoLHelper] Inspection contraband: hidden=" + hidden + " trashed=" + trashed + " bays=" + bays.Count);
            }
            catch (Exception e)
            {
                Debug.Log("[QoLHelper] HideContrabandForInspection failed: " + e.Message);
            }
        }

        private static bool TryHideInSmugglerBay(GameItem item, List<GameItem> bays)
        {
            for (int i = 0; i < bays.Count; i++)
            {
                GameItem bay = bays[i];
                if (bay == null || bay == item) continue;
                GraphUtils.TryAcceptAll(bay, item, -1);
                if (IsInsideSmugglerBay(item))
                {
                    Debug.Log("[QoLHelper] Hid contraband: " + DescribeItem(item) + " -> " + bay.identifier);
                    return true;
                }
            }
            return false;
        }

        private static bool IsInsideSmugglerBay(GameItem item)
        {
            if (item == null) return false;
            GameInventory inv = item.parentInventory;
            for (int depth = 0; inv != null && depth < 32; depth++)
            {
                GameItem parent = inv.GetParentItem();
                if (parent == null) break;
                if (ContrabandHelper.IsSmugglerBay(parent)) return true;
                inv = parent.parentInventory;
            }
            return false;
        }

        private static bool IsFood(GameItem item)
        {
            for (int i = 0; i < FoodTags.Length; i++)
            {
                if (item.IsTag(FoodTags[i])) return true;
            }
            return false;
        }

        // 只有游戏本身允许“拾起”的物品才参与选择，避免选中隐藏或不可用物品
        private static bool IsSelectable(GameItem item)
        {
            return item != null && item.MaySelectSlotItem() && item.CanSelectSlotItem();
        }

        private static void Select(GameItem item)
        {
            var handler = ItemSelectHandler.current;
            if (handler == null || !IsSelectable(item) || _selectItemMethod == null) return;
            _selectItemMethod.Invoke(handler, new object[] { item });
            Debug.Log("[QoLHelper] Selected: " + item.GetDisplayName());
        }

        private static List<GameItem> GetSelectableItems()
        {
            if (PlayerStore.Instance == null) return new List<GameItem>();
            var all = PlayerStore.Instance.FindAllItem();
            if (all == null) return new List<GameItem>();
            return all.Where(IsSelectable).ToList();
        }

        private void Update()
        {
            try
            {
                UpdateCore();
            }
            catch (Exception e)
            {
                // 场景加载早期 PlayerStore.Instance 的构造函数会抛 NRE，
                // 不拦下来会把同一帧后面的按键处理全部跳过
                if (!_updateErrorLogged)
                {
                    _updateErrorLogged = true;
                    Debug.Log("[QoLHelper] Update skipped: " + e.Message);
                }
            }
        }

        private void UpdateCore()
        {
            // 检查界面关闭后自动清除答案
            if (!string.IsNullOrEmpty(Answer))
            {
                InspectionUIManager ui = InspectionUIManager.Instance;
                if (ui == null || ui.panel == null || !ui.panel.activeInHierarchy)
                {
                    Answer = null;
                }
            }

            UpdateClientAnswer();

            if (_slipCleanupQueued)
            {
                _slipCleanupQueued = false;
                try
                {
                    TrashAppraisalSlips();
                }
                catch (Exception e)
                {
                    Debug.Log("[QoLHelper] TrashAppraisalSlips failed: " + e.Message);
                }
            }

            // 在估价输入框里打字时不响应热键，免得吞掉正常输入
            bool typing = IsTypingInField();

            // Z：撤回上一次 Q/F 放上秤的东西；X：把它再放回去
            if (!typing && Input.GetKeyDown(KeyCode.Z)) UndoSell();
            if (!typing && Input.GetKeyDown(KeyCode.X)) RedoSell();
            // F5 快速存档 / F9 快速读档
            if (!typing && Input.GetKeyDown(KeyCode.F5)) QuickSave();
            if (!typing && Input.GetKeyDown(KeyCode.F9)) QuickLoad();

            if (typing) return;

            // F 键：把顾客要的食物直接放上称重台
            if (Input.GetKeyDown(KeyCode.F))
            {
                QuickSell(isFoodOnly: true, quiet: false);
            }

            // Q 键：鉴定面板打开时直接按正确估值提交，否则把顾客想要的物品放上称重台。
            // 按住不放会连续执行，不用一下一下点
            float now = Time.unscaledTime;
            if (Input.GetKeyDown(KeyCode.Q))
            {
                _qRepeatAt = now + HoldStartDelay;
                if (!TryQuickAppraise()) QuickSell(isFoodOnly: false, quiet: false);
            }
            else if (Input.GetKey(KeyCode.Q) && now >= _qRepeatAt)
            {
                _qRepeatAt = now + HoldRepeatInterval;
                // 鉴定只有一次，别在面板还开着的时候开始连续卖货
                if (GetAppraisalTarget() == null) QuickSell(isFoodOnly: false, quiet: true);
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
        }

        // 把物品直接移进后柜台（称重区），这是原版拖拽售卖走的同一条链路：
        // UncheckedAccept -> AddedItemToWeightedArea -> OnWeightedAreaChanged -> PlacedItemForSelling
        private static bool TryPlaceOnSellingCounter(GameItem item, out GameInventory origin)
        {
            origin = null;
            if (item == null) return false;
            EmporiumEntry entry = EmporiumEntry.Instance;
            if (entry == null) return false;

            var counter = entry.backInvinvElement;
            if (counter == null) return false;

            // 记下原来在哪个容器里，撤回时按原路放回去
            GameInventory previous = item.parentInventory;

            // -1 表示整组；返回剩余未放置数量。全程不使用 TryAcceptAllOrDestroy，
            // 柜台放不下时物品会留在原背包，不会被销毁。
            int remaining = GraphUtils.TryAcceptAll(counter, item, -1);
            bool placed = remaining <= 0 || item.parentInventory == counter;
            if (placed) origin = previous;
            return placed;
        }

        // 商店里所有物品，包含箱子/背包里装着的物品
        private static List<GameItem> GetAllStoreItems()
        {
            var result = new List<GameItem>();
            if (PlayerStore.Instance == null) return result;

            var all = PlayerStore.Instance.FindAllItem();
            if (all == null) return result;

            for (int i = 0; i < all.Count; i++)
            {
                GameItem item = all[i];
                if (item == null || result.Contains(item)) continue;
                result.Add(item);

                // FindAllItem 自己会递归，这里逐层补一遍，避免有的容器窗口不在它的列表里
                foreach (object child in item.FindAllChildItems())
                {
                    GameItem childItem = child as GameItem;
                    if (childItem != null && !result.Contains(childItem)) result.Add(childItem);
                }
            }
            return result;
        }

        // 箱子里的物品通常不满足“可选中”条件，售卖只判断是否归玩家所有
        private static bool IsQuickSellable(GameItem item)
        {
            if (item == null) return false;
            if (GeneralHelper.IsItemEffectivelyOwned(item)) return true;
            // 机器刚产出的东西往往没有 IS_OWNED_TAG（例如刚打印出来的枪），
            // 机器归玩家所有，这里按“自家产出”放行
            return IsInsideMachine(item);
        }

        // 治安官在场时会检查称重台：违禁品触发执法对话，盗品直接加热度，这类物品不能自动搬上去
        private static bool IsSafeForClient(GameItem item, StoreClient client)
        {
            if (item == null || client == null) return false;
            if (!client.isSecurity && !client.isSentenceOfficer) return true;
            if (StolenHelper.IsStolenItem(item)) return false;
            if (ContrabandHelper.IsContraband(item)) return false;
            return true;
        }

        // 生产机器（打印机、熔炉、水培机、净水机…）以及机器里正在加工的原料/产物，都不能自动搬上称重台
        private static readonly string[] MachineTags =
        {
            "STANDARD_MACHINE_TAG", "PRINTER_TAG", "BROKEN_MACHINE_TAG", "HYDROPONIC_MACHINE_TAG", "MACHINERY_TAG",
            "RAS_TAG", "RAS_WATER_TANK_TAG", "RAS_TANK_GRID_TAG", "MACHINE_STATE_TAG", "PROGRESS_TYPE_MACHINE_TAG",
            "IS_BATTERY_POWERED_TAG", "MACHINE_RECHARGABLE_TAG", "STORAGE_COORDINATOR_TAG"
        };

        // 设备内部装着的东西（电池、滤芯）不能卖，但设备本身不算
        private static readonly string[] DeviceTags =
            { "FILTER_CONTAINER_TAG", "FILTER_SIZE_TAG" };

        // 机器内部只有“成品”能卖；这些是机器自己的原料/零件/耗材，必须留在机器里
        private static readonly string[] MachinePartTags =
        {
            "PROGRESS_ITEM_TAG",
            "MODULE_TAG", "SHIP_MODULE_TAG", "EQUIPMENT_MODULE_TAG", "MODULE_STUCK_TAG",
            "HYPERTECH_BATTERY_TAG", "Battery",
            "FILTER_SIZE_TAG", "FILTER_CONTAINER_TAG", "RAS_FILTER_SLOT_TAG",
            "LIQUID_CONTAINER_TAG", "GRADUATED_CONTAINER_TAG",
            "INDUSTRIAL_FISH_FEED_TAG", "EXOTIC_FUEL_TAG"
        };

        // 可以自动取物的储物容器：箱子、背包、包裹、冰箱、保险箱、弹药盒、工具箱等
        private static readonly string[] StorageTags =
        {
            "CONTAINER_TAG", "BACKPACK_TAG", "STORABLE_PACKAGE_TAG", "PRE_CONTAINER_TAG",
            "GRADUATED_CONTAINER_TAG", "POUCH_TAG", "AMMO_BOX_TAG", "FRIDGE_TAG", "SAFE_TAG",
            "TOOLBOX_ITEM_TAG", "LIQUID_CONTAINER_TAG", "ORGAN_TRANSPORT_CONTAINER_TAG"
        };

        // 剧情/任务箱子及其内容（卢给的玩具箱、已布置的炸弹）不能动
        private static readonly string[] ProtectedTags =
            { "LOU_TOY_BOX_TAG", "LOU_TOY_TAG", "LOU_BOMB_ARMED_TAG" };

        private static bool HasAnyTag(GameItem item, string[] tags)
        {
            for (int i = 0; i < tags.Length; i++)
            {
                if (item.IsTag(tags[i])) return true;
            }
            return false;
        }

        // 顺着容器往上找：机器、任务箱子、设备内部的物品都跳过，
        // 其余祖先必须是储物容器，装在工具/设备里的电池、滤芯就属于后者
        private static bool IsNotBlockedContainer(GameItem item)
        {
            if (item == null) return false;
            if (HasAnyTag(item, MachineTags) || HasAnyTag(item, ProtectedTags)) return false;

            GameInventory inv = item.parentInventory;
            for (int depth = 0; inv != null && depth < 32; depth++)
            {
                GameItem parent = inv.GetParentItem();
                if (parent == null) break;
                if (HasAnyTag(parent, ProtectedTags) || HasAnyTag(parent, DeviceTags)) return false;
                if (HasAnyTag(parent, MachineTags))
                {
                    // 机器/设备内部：只放行已经产出、能直接拿走的成品；
                    // 加工中的原料和机器零件（电池、滤芯、模块、液体、饲料）留在机器里
                    return !HasAnyTag(item, MachinePartTags);
                }
                if (!HasAnyTag(parent, StorageTags)) return false;
                inv = parent.parentInventory;
            }
            return true;
        }

        // 物品是否装在玩家自己的生产机器里（机器产出常常没有 IS_OWNED_TAG）
        private static bool IsInsideMachine(GameItem item)
        {
            if (item == null) return false;
            GameInventory inv = item.parentInventory;
            for (int depth = 0; inv != null && depth < 32; depth++)
            {
                GameItem parent = inv.GetParentItem();
                if (parent == null) break;
                if (HasAnyTag(parent, MachineTags)) return true;
                inv = parent.parentInventory;
            }
            return false;
        }

        // 预包装商品（净水药片瓶、营养片瓶、水质检测包、啤酒箱）装的是“整包卖”的东西，
        // 里面的单件不能被抓出来单独上秤；普通箱子/背包没有这个标签，不受影响
        private static bool IsInsidePreContainer(GameItem item)
        {
            if (item == null) return false;
            GameInventory inv = item.parentInventory;
            for (int depth = 0; inv != null && depth < 32; depth++)
            {
                GameItem parent = inv.GetParentItem();
                if (parent == null) break;
                if (parent.IsTag("PRE_CONTAINER_TAG")) return true;
                inv = parent.parentInventory;
            }
            return false;
        }

        // ---- 诊断用：Q 按下去没反应时，日志里能看出卡在哪一层 ----
        private static string DescribeItem(GameItem item)
        {
            if (item == null) return "null";
            return item.GetDisplayName() + "[" + item.identifier + "]";
        }

        // 物品本身 <- 所在容器的名字 <- 再上一层
        private static string DescribeChain(GameItem item)
        {
            StringBuilder sb = new StringBuilder();
            GameItem cur = item;
            for (int depth = 0; cur != null && depth < 6; depth++)
            {
                if (depth > 0) sb.Append(" <- ");
                sb.Append(cur.identifier);
                GameInventory inv = cur.parentInventory;
                cur = inv != null ? inv.GetParentItem() : null;
            }
            return sb.ToString();
        }

        private static string DescribeTags(GameItem item)
        {
            if (item == null || item.state == null) return "";
            IDictionary dict = item.state.dict as IDictionary;
            if (dict == null) return "";
            StringBuilder sb = new StringBuilder();
            int n = 0;
            foreach (object key in dict.Keys)
            {
                string tag = key as string;
                if (tag == null || !item.IsTag(tag)) continue;
                if (n > 0) sb.Append('|');
                sb.Append(tag);
                n++;
                if (n >= 14) break;
            }
            return sb.ToString();
        }

        // 列出物品都装在哪些容器里，用来确认某类容器（背包/储物舱/机器）有没有被扫描到
        private static string DescribeContainerStats(List<GameItem> items)
        {
            var counts = new Dictionary<string, int>();
            var tags = new Dictionary<string, string>();
            for (int i = 0; i < items.Count; i++)
            {
                GameItem item = items[i];
                if (item == null) continue;
                GameInventory inv = item.parentInventory;
                GameItem parent = inv != null ? inv.GetParentItem() : null;
                string key = parent != null ? parent.identifier : "(top)";
                int count;
                counts.TryGetValue(key, out count);
                counts[key] = count + 1;
                if (parent != null && !tags.ContainsKey(key)) tags[key] = DescribeTags(parent);
            }

            var ordered = counts.OrderByDescending(pair => pair.Value).Take(8).ToList();
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < ordered.Count; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(ordered[i].Key).Append(" x").Append(ordered[i].Value);
                string tag;
                if (tags.TryGetValue(ordered[i].Key, out tag)) sb.Append(" [").Append(tag).Append(']');
            }
            return sb.ToString();
        }

        private static string DescribeBuyTags(StoreClient client)        {
            try
            {
                FieldInfo f = typeof(StoreClient).GetField("clientBuyingTagList", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f == null) return "?";
                IEnumerable list = f.GetValue(client) as IEnumerable;
                if (list == null) return "?";
                StringBuilder sb = new StringBuilder();
                int n = 0;
                foreach (object o in list)
                {
                    if (n > 0) sb.Append('|');
                    sb.Append(o);
                    n++;
                    if (n >= 12) break;
                }
                return sb.ToString();
            }
            catch (Exception)
            {
                return "?";
            }
        }

        private static bool LooksLikeGun(GameItem item)
        {
            if (item == null) return false;
            return item.IsTag("ADV_GUN_TAG") || item.IsTag("GUN_FRAME_TAG")
                || item.IsTag("WEAPON_MOD_TAG") || item.IsTag("AMMO_TYPE_TAG");
        }

        // 订单只认完整枪械（ADV_GUN_TAG），并且要满足所有必选要求
        private static bool MeetsGunOrder(GameItem item, GunOrderData order)
        {
            if (item == null || order == null) return false;
            if (!item.IsTag("ADV_GUN_TAG")) return false;
            List<GunOrderRequirement> reqs = order.mandatoryRequirements;
            if (reqs == null) return true;
            for (int i = 0; i < reqs.Count; i++)
            {
                GunOrderRequirement req = reqs[i];
                if (req != null && !req.IsMet(item)) return false;
            }
            return true;
        }

        private static string RejectReason(GameItem item, StoreClient client)
        {
            if (!IsSafeForClient(item, client)) return "dirty";
            if (!IsNotBlockedContainer(item)) return "container";
            if (!NotOnSellingCounter(item)) return "on-counter";
            if (!IsQuickSellable(item)) return "not-owned";
            return "ok";
        }

        // ---- 改装服务单：顾客把枪放到前台，要求把 GUN_CURRENT_* 属性抬上去 ----
        private static GameItem FindServiceGun(GunOrderData order)
        {
            if (order == null) return null;
            GameItem gun = null;
            try
            {
                gun = order.GetPlacedServiceGun();
            }
            catch (Exception)
            {
            }
            if (gun != null) return gun;
            // 玩家把枪拿在手上/放进背包时 GetPlacedServiceGun 返回 null，这里从私有字段兜底
            try
            {
                FieldInfo f = typeof(GunOrderData).GetField("serviceGun", BindingFlags.Instance | BindingFlags.NonPublic);
                if (f != null) gun = f.GetValue(order) as GameItem;
            }
            catch (Exception)
            {
            }
            return gun;
        }

        // "GUN_CURRENT_FIREPOWER_TAG" -> "FIREPOWER"
        private static string StatNameFromRequirement(string reqId)
        {
            if (string.IsNullOrEmpty(reqId)) return null;
            const string prefix = "GUN_CURRENT_";
            if (!reqId.StartsWith(prefix, StringComparison.Ordinal)) return null;
            string stat = reqId.Substring(prefix.Length);
            if (stat.EndsWith("_TAG", StringComparison.Ordinal))
            {
                stat = stat.Substring(0, stat.Length - 4);
            }
            return stat;
        }

        // 与 GunHelper.UpdateCurrentGunStats 同一套算法：枪身基础值 × 配件百分比 + 配件固定值
        private static int PredictStatGain(GameItem gun, GameItem mod, string stat)
        {
            if (gun == null || mod == null || stat == null) return 0;
            int baseValue = gun.GetTagReadonly("GUN_" + stat + "_TAG").GetInt();
            int percent = mod.GetTagReadonly("GUN_" + stat + "_TAG").GetInt();
            int flat = mod.GetTagReadonly("MOD_BASE_" + stat + "_TAG").GetInt();
            return baseValue * percent / 100 + flat;
        }

        private static bool IsRequirementMet(GunOrderRequirement req, GameItem gun)
        {
            return req == null || gun == null || req.IsMet(gun);
        }

        private static bool[] CaptureRequirementStates(List<GunOrderRequirement> reqs, GameItem gun)
        {
            if (reqs == null) return new bool[0];
            bool[] states = new bool[reqs.Count];
            for (int i = 0; i < reqs.Count; i++)
            {
                states[i] = IsRequirementMet(reqs[i], gun);
            }
            return states;
        }

        // 新配件不能把原本已经满足的必选要求压下去
        private static bool BreaksMetRequirement(List<GunOrderRequirement> reqs, bool[] before, GameItem gun)
        {
            if (reqs == null || before == null) return false;
            for (int i = 0; i < reqs.Count && i < before.Length; i++)
            {
                if (before[i] && !IsRequirementMet(reqs[i], gun)) return true;
            }
            return false;
        }

        private static bool IsInstalledOn(GameItem gun, GameItem mod)
        {
            if (gun == null || mod == null) return false;
            GameInventory inv = mod.parentInventory;
            if (inv != null && inv.GetParentItem() == gun) return true;
            foreach (object child in gun.FindAllChildItems())
            {
                if (child == mod) return true;
            }
            return false;
        }

        private static string DescribeRequirements(GameItem gun, GunOrderData order)
        {
            StringBuilder sb = new StringBuilder();
            List<GunOrderRequirement> mand = order.mandatoryRequirements;
            int met = 0, total = 0;
            if (mand != null)
            {
                total = mand.Count;
                for (int i = 0; i < mand.Count; i++)
                {
                    if (IsRequirementMet(mand[i], gun)) met++;
                }
            }
            sb.Append("mandatory=").Append(met).Append('/').Append(total);
            List<GunOrderRequirement> bonus = order.bonusRequirements;
            met = 0;
            total = 0;
            if (bonus != null)
            {
                total = bonus.Count;
                for (int i = 0; i < bonus.Count; i++)
                {
                    if (IsRequirementMet(bonus[i], gun)) met++;
                }
            }
            sb.Append(" bonus=").Append(met).Append('/').Append(total);
            return sb.ToString();
        }

        private void HandleServiceOrder(StoreClient client, GunOrderData order)
        {
            GameItem gun = FindServiceGun(order);
            if (gun == null)
            {
                Debug.Log("[QoLHelper] Service order: gun not available yet.");
                return;
            }
            if (!GunModHelper.MayModGun(gun))
            {
                Debug.Log("[QoLHelper] Service order: gun cannot be modified.");
                return;
            }

            // 先按当前配件重算一遍，避免读到旧属性
            GunHelper.UpdateCurrentGunStats(gun);

            // 优先补必选要求，全部满足后再补额外要求（多装一个就多一份件的钱）
            GunOrderRequirement target = null;
            List<GunOrderRequirement> mand = order.mandatoryRequirements;
            if (mand != null)
            {
                for (int i = 0; i < mand.Count; i++)
                {
                    if (!IsRequirementMet(mand[i], gun))
                    {
                        target = mand[i];
                        break;
                    }
                }
            }
            if (target == null)
            {
                List<GunOrderRequirement> bonus = order.bonusRequirements;
                if (bonus != null)
                {
                    for (int i = 0; i < bonus.Count; i++)
                    {
                        if (!IsRequirementMet(bonus[i], gun) && StatNameFromRequirement(bonus[i].id) != null)
                        {
                            target = bonus[i];
                            break;
                        }
                    }
                }
            }

            if (target == null)
            {
                Debug.Log("[QoLHelper] Service order requirements met: " + DescribeRequirements(gun, order));
                return;
            }

            string stat = StatNameFromRequirement(target.id);
            if (stat == null)
            {
                Debug.Log("[QoLHelper] Service requirement '" + target.id + "' cannot be helped by mods.");
                return;
            }

            var items = GetAllStoreItems();
            var mods = new List<GameItem>();
            for (int i = 0; i < items.Count; i++)
            {
                GameItem item = items[i];
                if (item == null || item == gun) continue;
                if (!item.IsTag("WEAPON_MOD_TAG")) continue;
                if (!GeneralHelper.IsItemOwned(item)) continue;
                if (item.parentInventory == null) continue;
                if (!IsNotBlockedContainer(item)) continue;
                if (GunModHelper.IsGunAlreadyHaveThisMod(gun, item)) continue;
                if (GunModHelper.HasConflictingMod(gun, item, null, false)) continue;
                mods.Add(item);
            }

            if (mods.Count == 0)
            {
                Debug.Log("[QoLHelper] Service order: no spare weapon mods in stock. " + DescribeRequirements(gun, order));
                return;
            }

            // 对该属性加成最高的配件先试
            mods = mods.OrderByDescending(candidate => PredictStatGain(gun, candidate, stat)).ToList();
            bool[] mandatoryBefore = CaptureRequirementStates(mand, gun);

            for (int i = 0; i < mods.Count; i++)
            {
                GameItem mod = mods[i];
                int gain = PredictStatGain(gun, mod, stat);
                if (gain <= 0)
                {
                    Debug.Log("[QoLHelper] Service order: no part improves " + stat + ". " + DescribeRequirements(gun, order));
                    return;
                }

                GameInventory oldInventory = mod.parentInventory;
                GraphUtils.TryAcceptAll(gun, mod, -1);
                if (!IsInstalledOn(gun, mod))
                {
                    Debug.Log("[QoLHelper] Service order: gun refused " + DescribeItem(mod));
                    continue;
                }

                GunHelper.UpdateCurrentGunStats(gun);
                if (BreaksMetRequirement(mand, mandatoryBefore, gun))
                {
                    if (oldInventory != null) GraphUtils.TryAcceptAll(oldInventory, mod, -1);
                    GunHelper.UpdateCurrentGunStats(gun);
                    Debug.Log("[QoLHelper] Service order: rolled back " + DescribeItem(mod) + " (breaks a met requirement)");
                    continue;
                }

                Debug.Log("[QoLHelper] Installed part " + DescribeItem(mod) + " (+" + gain + " " + stat + ") -> "
                    + DescribeRequirements(gun, order));
                return;
            }
        }

        private void QuickSell(bool isFoodOnly, bool quiet)
        {
            var items = GetAllStoreItems();
            if (items.Count == 0)
            {
                if (!quiet) Debug.Log("[QoLHelper] No selectable item in store.");
                return;
            }

            var clientInstance = PlayerStore.Instance != null ? PlayerStore.Instance.currentClientInstance : null;
            var client = clientInstance != null ? clientInstance.GetClientBlueprint() : null;
            if (client == null)
            {
                if (!quiet) Debug.Log("[QoLHelper] No current client.");
                return;
            }

            // 改装服务单：枪在前台，按 Q 依次往上装我们库存里的配件
            GunOrderData order = client.gunOrderData;
            if (order != null && order.isServiceOrder)
            {
                HandleServiceOrder(client, order);
                return;
            }

            // 枪械订单不看 clientBuyingTagList：只要称重台上放着满足订单要求的枪就算交货
            bool isGunOrder = order != null;
            var wanted = isGunOrder
                ? items.Where(candidate => MeetsGunOrder(candidate, order) || client.IsClientBuyingThisItem(candidate))
                : items.Where(client.IsClientBuyingThisItem);
            if (isFoodOnly) wanted = wanted.Where(IsFood);
            // 预包装商品（净水药片瓶、营养片瓶、啤酒箱…）整包卖，
            // 不把里面的颗粒一颗颗抓出来单独上秤
            int keptPacked = 0;
            wanted = wanted.Where(candidate =>
            {
                if (!IsInsidePreContainer(candidate)) return true;
                keptPacked++;
                return false;
            });
            var wantedItems = wanted.ToList();
            if (keptPacked > 0 && !quiet)
            {
                Debug.Log("[QoLHelper] Kept " + keptPacked + " items inside packaging (not splitting).");
            }
            // 订单枪按“额外要求满足得越多越优先”，先把最值钱的那把推上柜台
            if (isGunOrder && order.bonusRequirements != null)
            {
                wantedItems = wantedItems.OrderByDescending(candidate => order.CountBonusMet(candidate)).ToList();
            }
            if (wantedItems.Count == 0)
            {
                if (quiet) return;
                // 打印扫描到的物品，确认箱子里的东西有没有被枚举到
                StringBuilder sample = new StringBuilder();
                int shown = Math.Min(items.Count, 10);
                for (int i = 0; i < shown; i++)
                {
                    if (i > 0) sample.Append(", ");
                    sample.Append(items[i].GetDisplayName());
                }
                Debug.Log("[QoLHelper] Client wants none of " + items.Count + " items: " + sample);
                int guns = 0;
                for (int i = 0; i < items.Count; i++)
                {
                    if (LooksLikeGun(items[i])) guns++;
                }
                string orderInfo = order == null ? "none" : ("tier" + order.tier + (order.isServiceOrder ? "/service" : ""));
                Debug.Log("[QoLHelper] clientType=" + client.GetType().Name + " buyTags=" + DescribeBuyTags(client) + " security=" + client.isSecurity + " order=" + orderInfo + " gunLikeItems=" + guns);
            }

            var list = wantedItems.Where(candidate => IsSafeForClient(candidate, client)).Where(IsNotBlockedContainer).Where(NotOnSellingCounter).Where(IsQuickSellable).ToList();
            if (list.Count == 0)
            {
                if (quiet) return;
                Debug.Log("[QoLHelper] Nothing placeable. (found=" + items.Count + ", wanted=" + wantedItems.Count + ", gunOrder=" + isGunOrder + ")");
                Debug.Log("[QoLHelper] clientType=" + client.GetType().Name + " buyTags=" + DescribeBuyTags(client) + " security=" + client.isSecurity);
                Debug.Log("[QoLHelper] Containers: " + DescribeContainerStats(items));

                // 逐个列出客户想买、但被某一层过滤拦下的物品，连同标签和所在容器
                int shown = Math.Min(wantedItems.Count, 8);
                for (int i = 0; i < shown; i++)
                {
                    GameItem rejected = wantedItems[i];
                    Debug.Log("[QoLHelper] Reject " + RejectReason(rejected, client) + " " + DescribeItem(rejected)
                        + " tags=" + DescribeTags(rejected) + " chain=" + DescribeChain(rejected));
                }
                return;
            }

            // 柜台可能放不下某个候选（形状、空间），换下一个再试，别整次按键都白按
            int start = (_queryIndex + 1) % list.Count;
            int refusedShown = 0;
            for (int attempt = 0; attempt < list.Count; attempt++)
            {
                int idx = (start + attempt) % list.Count;
                GameItem candidate = list[idx];
                GameInventory origin;
                if (TryPlaceOnSellingCounter(candidate, out origin))
                {
                    _queryIndex = -1;
                    PushSellRecord(candidate, origin);
                    Debug.Log("[QoLHelper] Placed on selling counter: " + candidate.GetDisplayName());
                    return;
                }
                if (refusedShown < 4 && !quiet)
                {
                    refusedShown++;
                    Debug.Log("[QoLHelper] Counter refused: " + DescribeItem(candidate) + " tags=" + DescribeTags(candidate) + " chain=" + DescribeChain(candidate));
                }
            }

            // 柜台满了或形状放不下，退回原来的高亮选取；按住连发时不做，免得一直刷日志
            if (quiet) return;
            _queryIndex = start;
            Select(list[start]);
        }

        private static bool NotOnSellingCounter(GameItem item)
        {
            if (item == null) return false;
            EmporiumEntry entry = EmporiumEntry.Instance;
            if (entry == null) return true;
            var counter = entry.backInvinvElement;
            return counter == null || item.parentInventory != counter;
        }

        private void CycleSelectItem(bool isForward)
        {
            var items = GetSelectableItems();
            if (items.Count == 0)
            {
                Debug.Log("[QoLHelper] No selectable item in store.");
                return;
            }

            if (_cycleIndex < 0 || _cycleIndex >= items.Count)
            {
                _cycleIndex = isForward ? 0 : items.Count - 1;
            }
            else
            {
                _cycleIndex = isForward
                    ? (_cycleIndex + 1) % items.Count
                    : (_cycleIndex - 1 + items.Count) % items.Count;
            }

            Select(items[_cycleIndex]);
        }
    }
}
