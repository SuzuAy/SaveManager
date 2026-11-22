using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SaveManager
{
    /// <summary>所有路径都从游戏自己的 persistentDataPath 推出来，不写死。</summary>
    internal static class Paths
    {
        public static string DataPath => Application.persistentDataPath;

        /// <summary>原版单机存档。</summary>
        public static string VanillaSave => Path.Combine(DataPath, "save.sv");

        /// <summary>CasualtiesTogether 多人存档目录。</summary>
        public static string MpSaveDir => Path.Combine(DataPath, "mp_save");

        /// <summary>存档槽根目录（cfg 可覆盖）。</summary>
        public static string SlotRoot
        {
            get
            {
                string custom = Plugin.CfgRootDir != null ? Plugin.CfgRootDir.Value : string.Empty;
                if (!string.IsNullOrWhiteSpace(custom))
                {
                    try { return Path.GetFullPath(custom.Trim()); }
                    catch { /* 用户写错就退回默认 */ }
                }
                return Path.Combine(DataPath, "SaveSlots");
            }
        }

        public static bool MpPresent => Directory.Exists(MpSaveDir);

        public static void EnsureRoot()
        {
            Directory.CreateDirectory(SlotRoot);
        }

        public static string SlotDir(string slotName) => Path.Combine(SlotRoot, slotName);

        public static string SlotSave(string slotName) => Path.Combine(SlotDir(slotName), "save.sv");

        public static string SlotMp(string slotName) => Path.Combine(SlotDir(slotName), "mp");

        public static string SlotMeta(string slotName) => Path.Combine(SlotDir(slotName), "meta.json");

        public static string SlotBackup(string slotName) => Path.Combine(SlotDir(slotName), "backup");

        public static string SafeName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var sb = new System.Text.StringBuilder(raw.Length);
            foreach (char c in raw.Trim())
            {
                if (Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 || c == '\\' || c == '/') sb.Append('_');
                else sb.Append(c);
            }
            string s = sb.ToString().Trim('.', ' ');
            if (s.Length > 80) s = s.Substring(0, 80);
            return s.Length == 0 ? null : s;
        }
    }
}

