using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SaveManager
{
    // ------------------------------------------------------------------ 热键
    internal static class Hotkeys
    {
        public static void PollInGame()
        {
            if (Plugin.CfgKeyPanel != null && Input.GetKeyDown(Plugin.CfgKeyPanel.Value)) SavePage.Open();

            if (Plugin.CfgKeySave != null && Input.GetKeyDown(Plugin.CfgKeySave.Value))
            {
                string msg;
                Plugin.DoSave(null, out msg);
            }

            if (Plugin.CfgKeyLoad != null && Input.GetKeyDown(Plugin.CfgKeyLoad.Value))
            {
                if (Plugin.CfgInGameLoad != null && Plugin.CfgInGameLoad.Value)
                {
                    string msg;
                    Plugin.DoQuickLoad(out msg);
                    Ui.Notify(msg);
                }
                else
                {
                    SavePage.Open();
                    Ui.Notify("游戏内读档没开：在「存档」分页里选档，或打开 ExperimentalInGameLoad");
                }
            }
        }

        public static void PollMenu()
        {
            if (Plugin.CfgKeyPanel != null && Input.GetKeyDown(Plugin.CfgKeyPanel.Value)) SavePage.Open();
        }
    }

    [HarmonyPatch(typeof(PlayerCamera), "HandleInput")]
    internal static class Patch_PlayerCamera_HandleInput
    {
        private static void Prefix()
        {
            try { Hotkeys.PollInGame(); }
            catch (Exception e) { Log.Error("热键处理异常：" + e); }
        }
    }

    [HarmonyPatch(typeof(PreRunScript), "Update")]
    internal static class Patch_PreRunScript_Update
    {
        private static void Prefix()
        {
            try { Hotkeys.PollMenu(); }
            catch (Exception e) { Log.Error("主菜单热键异常：" + e); }
        }
    }

    // ------------------------------------------------------------------ 控制台命令
    [HarmonyPatch(typeof(ConsoleScript), "RegisterAllCommands")]
    internal static class Patch_ConsoleCommands
    {
        private static void Postfix(ConsoleScript __instance)
        {
            try { ConsoleCommands.Register(__instance); }
            catch (Exception e) { Log.Error("注册控制台命令失败：" + e); }
        }
    }

    internal static class ConsoleCommands
    {
        private static readonly FieldInfo CommandsField =
            typeof(ConsoleScript).GetField("Commands", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);

        private static IList _list;

        public static void Register(ConsoleScript instance)
        {
            if (CommandsField == null) { Log.Warn("找不到 ConsoleScript.Commands 字段"); return; }
            _list = CommandsField.GetValue(CommandsField.IsStatic ? null : (object)instance) as IList;
            if (_list == null) { Log.Warn("ConsoleScript.Commands 不是 IList"); return; }
            if (_list.Cast<object>().Any(c => ((Command)c).name == "cssave")) return;

            Add("cssave", "[存档] 把当前局面存成一个新槽（不传名字=自动命名）",
                a => { string n = a.Length > 1 ? string.Join(" ", a.Skip(1)) : null; string m; Ui.Notify(Plugin.DoSave(n, out m) ? m : "失败：" + m); },
                ("name", "可选，存档名（可含中文）"));

            Add("cslots", "[存档] 列出所有存档槽",
                a => Ui.Notify(SlotStore.List().Count == 0
                    ? "还没有任何存档槽"
                    : string.Join("\n", SlotStore.List().Select(s =>
                        $"{s.name}  D{s.biome}  {s.DurationText()}  {(s.mode == "mp" ? "联机" : "单人")}{(s.sandbox ? " 沙盒" : "")}{(s.cheated ? " 作弊" : "")}"))), null);

            Add("csload", "[存档] 读取指定槽（回主菜单流程）",
                a => { if (a.Length < 2) throw new Exception("用法：csload <槽名>"); string m; Ui.Notify(Plugin.DoLoad(a[1], out m) ? m : "失败：" + m); },
                ("name", "槽名（cslots 里看到的那个）"));

            Add("csdir", "[存档] 打开存档目录",
                a => { if (!Directory.Exists(Paths.SlotRoot)) Directory.CreateDirectory(Paths.SlotRoot); PreRunScript.OpenInFileBrowser(Paths.SlotRoot); Ui.Notify("已打开：" + Paths.SlotRoot); }, null);

            Add("csdel", "[存档] 删除一个槽",
                a => { if (a.Length < 2) throw new Exception("用法：csdel <槽名>"); string e; Ui.Notify(SlotStore.Delete(a[1], out e) ? "已删除：" + a[1] : "失败：" + e); },
                ("name", "槽名"));

            Add("csrename", "[存档] 给槽改名",
                a => { if (a.Length < 3) throw new Exception("用法：csrename <旧名> <新名>"); string e; Ui.Notify(SlotStore.Rename(a[1], a[2], out e) ? "已改名" : "失败：" + e); },
                ("old", "旧名"), ("new", "新名"));

            Add("csrollback", "[存档] 回滚到该槽的上一档备份",
                a => { if (a.Length < 2) throw new Exception("用法：csrollback <槽名>"); string e; Ui.Notify(SlotStore.RestoreBackup(a[1], out e) ? "已回滚：" + a[1] : "失败：" + e); },
                ("name", "槽名"));

            Add("csexport", "[存档] 把槽铺成游戏当前存档（游戏外换档同效）",
                a => { if (a.Length < 2) throw new Exception("用法：csexport <槽名>"); string e; Ui.Notify(SlotStore.ExportToGameSave(a[1], out e) ? "已铺成 save.sv：" + a[1] : "失败：" + e); },
                ("name", "槽名"));

            Add("csslots", "[存档] 打开设置里的「存档」分页",
                a => SavePage.Open(), null);

            Log.Info("控制台命令已注册（cssave / cslots / csload / csdir / csdel / csrename / csrollback / csexport / csslots）");
        }

        private static void Add(string name, string desc, Command.Action action, params (string, string)[] argDesc)
        {
            try { _list.Add(new Command(name, desc, action, null, argDesc)); }
            catch (Exception e) { Log.Warn("加命令失败 " + name + "：" + e.Message); }
        }
    }
}

