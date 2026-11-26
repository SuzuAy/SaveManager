using System;
using System.Collections.Generic;
using UnityEngine;

namespace SaveManager
{
    /// <summary>极简双语：跟随游戏当前语言，中文原样返回，英文查表。</summary>
    internal static class Lang
    {
        private static bool? _zh;

        private static readonly Dictionary<string, string> En = new Dictionary<string, string>
        {
            // 暂停菜单 / tooltip
            ["存档"] = "Save",
            ["打开存档页面（SM · Save Manager）"] = "Open the save slots page (SM)",
            ["打开存档页面"] = "Open the save slots page",
            ["保存并退出到主菜单"] = "Save & quit to menu",
            ["管理"] = "Manage",
            ["批量管理"] = "Batch mode",
            ["删除所选"] = "Delete selected",
            ["确认删除所选（再点一次）"] = "Confirm delete selected (click again)",
            ["确认删除？再点一次"] = "Confirm delete? click again",
            ["再点一次「确认删除」才会真的删除"] = "Click again to really delete it",
            ["还没有勾选存档"] = "No slot selected yet",
            ["已存档并退出："] = "Saved, leaving: ",
            ["找不到界面，无法打开存档窗口"] = "UI not found, cannot open the save window",

            // 存档页 - 左栏
            ["操作"] = "Actions",
            ["＋ 新建存档"] = "+ New save",
            ["读取最近一档"] = "Load latest",
            ["打开文件夹"] = "Open folder",
            ["已选存档"] = "Selected slot",
            ["读取此档"] = "Load this slot",
            ["回滚上一档"] = "Restore previous",
            ["删除此档"] = "Delete this slot",
            ["存档设置"] = "Save options",
            ["设置"] = "Settings",
            ["存档选项与设置 ▸"] = "Save options & settings ▸",
            ["← 返回存档列表"] = "← Back to slots",

            // 存档页 - 选项
            ["游戏内读档"] = "In-game load",
            ["自动存档"] = "Auto save",
            ["标记作弊（成就关闭）"] = "Mark cheated (no achievements)",
            ["沙盒不标作弊"] = "No cheat mark in sandbox",
            ["联机允许存读档"] = "Allow in multiplayer",
            ["覆盖前保留上一档"] = "Keep previous backup",
            ["绕过教程门禁"] = "Bypass tutorial gate",
            ["行为"] = "Behaviour",
            ["存档标记"] = "Save marking",
            ["其它"] = "Other",
            ["开"] = "ON",
            ["关"] = "OFF",

            // 存档页 - 右栏
            ["新名字"] = "New name",
            ["单人"] = "SP",
            ["联机"] = "MP",
            ["沙盒"] = "sandbox",
            ["作弊"] = "cheated",
            ["（还没有存档：进游戏按 F5，或点左边「新建存档」）"] = "(No saves yet: press F5 in game, or use \"+ New save\")",
            ["档"] = "slots",
            ["（无上限，按时间倒序）"] = "  (unlimited, newest first)",
            ["已选："] = "Selected: ",
            ["共 "] = "",
        };

        /// <summary>当前模式：auto / zh / en</summary>
        public static string Mode
        {
            get
            {
                try
                {
                    string v = Plugin.CfgLang != null ? (Plugin.CfgLang.Value ?? "auto") : "auto";
                    v = v.Trim().ToLowerInvariant();
                    return v == "zh" || v == "en" ? v : "auto";
                }
                catch { return "auto"; }
            }
        }

        /// <summary>配置改动后调用，让下次取 Zh 时重新判断。</summary>
        public static void Invalidate() { _zh = null; }

        public static bool Zh
        {
            get
            {
                string mode = Mode;
                if (mode == "zh") return true;
                if (mode == "en") return false;

                if (_zh.HasValue) return _zh.Value;
                bool zh = false;
                try
                {
                    string n = Locale.currentLangName ?? "";
                    zh = n.IndexOf('中') >= 0 || n.IndexOf("zh", StringComparison.OrdinalIgnoreCase) >= 0
                         || n.IndexOf("Chinese", StringComparison.OrdinalIgnoreCase) >= 0
                         || n.IndexOf("Mandarin", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!zh && !string.IsNullOrEmpty(n)) zh = false;
                    if (string.IsNullOrEmpty(n))
                    {
                        var l = Application.systemLanguage;
                        zh = l == SystemLanguage.Chinese || l == SystemLanguage.ChineseSimplified || l == SystemLanguage.ChineseTraditional;
                    }
                }
                catch { zh = false; }
                _zh = zh;
                return zh;
            }
        }

        /// <summary>中文为键查表；没有译文就原样返回。</summary>
        public static string T(string zh)
        {
            if (zh == null) return null;
            if (Zh) return zh;
            return En.TryGetValue(zh, out var en) ? en : zh;
        }

        public static string T(string zh, string en) => Zh ? zh : en;
    }
}
