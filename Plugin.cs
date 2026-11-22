using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace CSSaveSlots
{
    [BepInPlugin(Guid, "Casualties Save Slots", VersionStr)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "xiaomi.cssaveslots";
        public const string VersionStr = "0.2.0";

        internal static Plugin Instance;

        internal static ConfigEntry<KeyCode> CfgKeySave;
        internal static ConfigEntry<KeyCode> CfgKeyPanel;
        internal static ConfigEntry<KeyCode> CfgKeyLoad;
        internal static ConfigEntry<bool> CfgCheatMark;
        internal static ConfigEntry<bool> CfgSandboxNoCheat;
        internal static ConfigEntry<bool> CfgInGameLoad;
        internal static ConfigEntry<bool> CfgAllowMp;
        internal static ConfigEntry<bool> CfgKeepBackup;
        internal static ConfigEntry<bool> CfgBypassGate;
        internal static ConfigEntry<bool> CfgAutoSave;
        internal static ConfigEntry<int> CfgAutoSaveMinutes;
        internal static ConfigEntry<string> CfgRootDir;

        internal static bool MpAllowed => CfgAllowMp == null || CfgAllowMp.Value;

        private float _autoTimer;

        private void Awake()
        {
            Instance = this;
            Log.Init(Logger);

            CfgKeySave = Config.Bind("键位 Keybinds", "QuickSave", KeyCode.F5, "游戏内随时存一个新槽");
            CfgKeyLoad = Config.Bind("键位 Keybinds", "QuickLoad", KeyCode.F9, "读最近一次存档（需启用 ExperimentalInGameLoad）");
            CfgKeyPanel = Config.Bind("键位 Keybinds", "Panel", KeyCode.F8, "打开设置菜单里的「存档」分页");

            CfgCheatMark = Config.Bind("存档 Slot", "MarkCheatedSave", false,
                "存档时把本局标记为 cheated（原版语义：成就关闭到重启）。默认关。");
            CfgSandboxNoCheat = Config.Bind("存档 Slot", "SandboxNoCheatMark", true,
                "沙盒模式里不标记作弊（沙盒本身就允许随便用控制台）。");
            CfgAllowMp = Config.Bind("存档 Slot", "AllowInMultiplayer", true,
                "联机时也允许存/读档（会连同 mp_save 一起收进槽里）。");
            CfgKeepBackup = Config.Bind("存档 Slot", "KeepLastBackup", true,
                "覆盖同名槽前，把上一份挪进 backup/。");
            CfgRootDir = Config.Bind("存档 Slot", "SlotRootOverride", "",
                "留空 = 游戏存档目录下的 SaveSlots\\。");

            CfgBypassGate = Config.Bind("兼容 Compatibility", "BypassTutorialGate", true,
                "没做完基础教程(didbasiccourse)时也允许读档：跳过原版「先做教程」的拦截，直接铺档读。沙盒/教程玩家需要。");
            CfgInGameLoad = Config.Bind("兼容 Compatibility", "ExperimentalInGameLoad", true,
                "允许游戏内直接读档：存档页点「读取此档」/ F9 会自动回主菜单→铺档→继续。默认开。");

            CfgAutoSave = Config.Bind("自动存档 AutoSave", "Enabled", false, "定时自动存一个新槽（默认关）");
            CfgAutoSaveMinutes = Config.Bind("自动存档 AutoSave", "Minutes", 5, "自动存档间隔（分钟）");

            try
            {
                new Harmony(Guid).PatchAll();
                Log.Info("Harmony 补丁已打上");
            }
            catch (Exception e) { Log.Error("Harmony 补丁失败：" + e); }

            Log.Info("存档根目录：" + Paths.SlotRoot);
            Log.Info("联机存档目录存在：" + Paths.MpPresent);
        }

        private void Update()
        {
            if (CfgAutoSave == null || !CfgAutoSave.Value) return;
            if (!SlotStore.InWorld) { _autoTimer = 0f; return; }

            _autoTimer += Time.unscaledDeltaTime;
            float limit = Mathf.Max(1, CfgAutoSaveMinutes.Value) * 60f;
            if (_autoTimer < limit) return;
            _autoTimer = 0f;

            string slot, err;
            if (!SlotStore.Capture(null, out slot, out err)) { Log.Warn("自动存档失败：" + err); return; }
            Ui.Notify("已自动存档：" + slot);
        }

        // ---------------------------------------------------------------- 动作

        /// <summary>把配置立刻写盘，下次启动直接读回来。</summary>
        public static void SaveConfig()
        {
            try
            {
                if (Instance != null && Instance.Config != null) Instance.Config.Save();
            }
            catch (Exception e) { Log.Warn("保存配置失败：" + e.Message); }
        }

        public static bool DoSave(string name, out string message)
        {
            string slot, err;
            if (!SlotStore.Capture(name, out slot, out err)) { message = err; return false; }
            message = "已存档：" + slot;
            Ui.Notify(message);
            return true;
        }

        public static bool DoLoad(string slotName, out string message)
        {
            if (string.IsNullOrEmpty(slotName)) { message = "没有选中存档槽"; return false; }
            if (!Directory.Exists(Paths.SlotDir(slotName))) { message = "槽不存在：" + slotName; return false; }

            // 沙盒档 / 实机档 互不相通
            SlotMeta meta = SlotStore.ReadMeta(slotName);
            bool slotSandbox = meta != null && meta.sandbox;
            if (slotSandbox != Sandbox.IsActive)
            {
                message = slotSandbox
                    ? "这是沙盒存档，只能在沙盒场地里读"
                    : "这是实机存档，沙盒场地里读不了";
                return false;
            }
            if (SlotStore.InWorld && (CfgInGameLoad == null || !CfgInGameLoad.Value))
            {
                message = "游戏内读档没开（ExperimentalInGameLoad=false）：先在暂停菜单里选档";
                return false;
            }
            message = "正在读档：" + slotName;
            Instance.StartCoroutine(LoadRoutine(slotName));
            return true;
        }

        public static bool DoQuickLoad(out string message)
        {
            bool sandbox = Sandbox.IsActive;
            var list = SlotStore.List().Where(s => s.sandbox == sandbox).ToList();
            if (list.Count == 0) { message = "当前模式还没有任何存档槽"; return false; }
            return DoLoad(list[0].name, out message);
        }

        public static void OpenSavePage()
        {
            SavePage.Open();
        }

        private static IEnumerator LoadRoutine(string slotName)
        {
            bool wasInWorld = SlotStore.InWorld;
            if (wasInWorld)
            {
                // 从暂停菜单点进来时要先解除暂停，否则 ToMainMenu 会卡住
                try
                {
                    if (PauseHandler.main != null && PauseHandler.paused) PauseHandler.TogglePause();
                }
                catch (Exception e) { Log.Warn("解除暂停失败：" + e.Message); }

                PreRunScript oldPre = PreRunScript.instance;
                try { PlayerCamera.main.ToMainMenu(); }
                catch (Exception e) { Log.Error("回主菜单失败：" + e.Message); }

                // 等「新的」主菜单脚本就位（老实例在过场中可能还活着，不能直接拿来 LoadRun）
                float waited = 0f;
                while (waited < 12f)
                {
                    PreRunScript cur = PreRunScript.instance;
                    if (cur != null && cur != oldPre) break;
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
                Log.Info("等主菜单结束，等待 " + waited.ToString("0.0") + "s");
                yield return null;
                yield return null;
            }

            string err;
            if (!SlotStore.PrepareLoad(slotName, out err))
            {
                Log.Error(err);
                Ui.Notify("读档失败：" + err);
                yield break;
            }

            SaveSystem.loadedRun = true;

            PreRunScript pre = PreRunScript.instance;
            if (pre == null)
            {
                Log.Error("找不到 PreRunScript.instance");
                Ui.Notify("读档失败：找不到主菜单脚本");
                yield break;
            }

            if (PlayerPrefs.GetInt("didbasiccourse") == 0)
            {
                if (CfgBypassGate == null || !CfgBypassGate.Value)
                {
                    Ui.Notify("原版要求先做完基础教程才能读档（BypassTutorialGate 可关掉这条限制）");
                    yield break;
                }
                Ui.Notify("没做完基础教程 → 走直连读档");
                RawLoad(pre);
                yield break;
            }

            try { pre.LoadRun(); }
            catch (Exception e) { Log.Error("LoadRun 抛异常：" + e); }

            Log.Info("已触发读档：" + slotName);
        }

        /// <summary>绕过 LoadRun 的教程门禁，自己走一遍「变暗 → 载入 SampleScene」。</summary>
        private static void RawLoad(PreRunScript pre)
        {
            Type t = typeof(PreRunScript);
            try
            {
                FieldInfo savedChar = t.GetField("savedCharacter", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo selected = t.GetField("characterSelected", BindingFlags.Public | BindingFlags.Static);
                if (savedChar != null && selected != null) selected.SetValue(null, savedChar.GetValue(pre));

                FieldInfo started = t.GetField("startedLoading", BindingFlags.NonPublic | BindingFlags.Instance);
                if (started != null) started.SetValue(pre, true);

                if (pre.tutorialWarning != null) pre.tutorialWarning.SetActive(false);
                if (pre.runSettingsScreen != null) pre.runSettingsScreen.SetActive(false);
            }
            catch (Exception e) { Log.Warn("直连读档准备失败：" + e.Message); }

            Instance.StartCoroutine(SceneLoadRoutine());
        }

        private static IEnumerator SceneLoadRoutine()
        {
            try { if (GlobalDark.main != null) GlobalDark.main.Darken(); } catch { }

            float t = 0f;
            while (t < 6f)
            {
                bool darkening = false;
                try { darkening = GlobalDark.main != null && GlobalDark.main.IsDarkening(); } catch { }
                if (!darkening) break;
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            UnityEngine.SceneManagement.SceneManager.LoadScene("SampleScene");
        }
    }

    internal static class Log
    {
        private static ManualLogSource _src;

        public static void Init(ManualLogSource src) { _src = src; }

        public static void Info(string m) { if (_src != null) _src.LogInfo(m); else Debug.Log("[CSSaveSlots] " + m); }
        public static void Warn(string m) { if (_src != null) _src.LogWarning(m); else Debug.LogWarning("[CSSaveSlots] " + m); }
        public static void Error(string m) { if (_src != null) _src.LogError(m); else Debug.LogError("[CSSaveSlots] " + m); }
    }
}
