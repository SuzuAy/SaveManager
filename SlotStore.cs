using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CSSaveSlots
{
    /// <summary>
    /// 存档槽的落盘逻辑：快照 / 还原 / 列表 / 改名 / 删除 / 回滚。
    /// 只搬运游戏自己的 save.sv 与 mp_save 目录，不重新实现序列化 —— 格式永远跟原版一致。
    /// </summary>
    internal static class SlotStore
    {
        public static bool InWorld => PlayerCamera.main != null && PlayerCamera.main.body != null;

        // ---------------------------------------------------------------- 存档

        /// <summary>把当前局面存成一个新槽（requestedName 为空则自动命名）。</summary>
        public static bool Capture(string requestedName, out string slotName, out string error)
        {
            slotName = null;
            error = null;
            if (!InWorld) { error = "当前不在游戏世界内，无法存档"; return false; }
            if (!Plugin.MpAllowed && Paths.MpPresent) { error = "联机存档被配置禁用（AllowInMultiplayer=false）"; return false; }

            bool sandbox = Sandbox.IsActive;
            try
            {
                bool markCheat = Plugin.CfgCheatMark != null && Plugin.CfgCheatMark.Value;
                bool sandboxExempt = sandbox && (Plugin.CfgSandboxNoCheat == null || Plugin.CfgSandboxNoCheat.Value);
                if (markCheat && !sandboxExempt && !SteamManager.disableAchievements)
                {
                    // 与原版 cheatedSave 语义一致：一旦标记，本局成就关闭直到重启
                    SteamManager.disableAchievements = true;
                }
                SaveSystem.SaveGame();
            }
            catch (Exception e)
            {
                error = "调用游戏 SaveGame 失败：" + e.Message;
                Log.Error(error + "\n" + e.StackTrace);
                return false;
            }

            bool mp = Paths.MpPresent;
            string probePath = File.Exists(Paths.VanillaSave)
                ? Paths.VanillaSave
                : (mp ? Path.Combine(Paths.MpSaveDir, "save.sv") : null);
            if (probePath == null || !File.Exists(probePath))
            {
                error = "游戏没有产出 save.sv（可能处于不允许存档的层）";
                return false;
            }

            SlotMeta meta = Probe(probePath);
            meta.mode = mp ? "mp" : "sp";
            meta.sandbox = sandbox;
            meta.createdAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            if (mp) meta.players = ListMpPlayers();

            string name = Paths.SafeName(requestedName);
            if (name == null) name = UniqueAutoName(meta);

            try
            {
                Paths.EnsureRoot();
                string dir = Paths.SlotDir(name);
                Directory.CreateDirectory(dir);

                // 覆盖同名槽时，先把旧档挪进 backup（回滚用）
                if (Plugin.CfgKeepBackup != null && Plugin.CfgKeepBackup.Value)
                {
                    if (File.Exists(Paths.SlotSave(name)) || Directory.Exists(Paths.SlotMp(name)))
                    {
                        RotateBackup(name);
                        meta.hasBackup = true;
                    }
                }

                try { File.Copy(Paths.VanillaSave, Paths.SlotSave(name), true); }
                catch (FileNotFoundException) { /* 联机时可能没有根 save.sv */ }

                if (Directory.Exists(Paths.SlotMp(name))) Directory.Delete(Paths.SlotMp(name), true);
                if (mp) CopyDir(Paths.MpSaveDir, Paths.SlotMp(name));

                meta.name = name;
                meta.bytes = new FileInfo(probePath).Length;
                WriteMeta(name, meta);
                slotName = name;
                Log.Info(string.Format("已存档 -> {0}（{1}，{2}）", name, meta.mode, FormatBytes(meta.bytes)));
                return true;
            }
            catch (Exception e)
            {
                error = "写入存档槽失败：" + e.Message;
                Log.Error(error + "\n" + e.StackTrace);
                return false;
            }
        }

        // ---------------------------------------------------------------- 读档

        /// <summary>把槽里的文件铺回游戏读档位置（不负责触发读档）。</summary>
        public static bool PrepareLoad(string slotName, out string error)
        {
            error = null;
            string dir = Paths.SlotDir(slotName);
            if (!Directory.Exists(dir)) { error = "槽不存在：" + slotName; return false; }
            try
            {
                string slotSave = Paths.SlotSave(slotName);
                string slotMp = Paths.SlotMp(slotName);

                if (Directory.Exists(slotMp))
                {
                    // 旧的 mp_save 先挪开，避免残留别的玩家文件
                    if (Directory.Exists(Paths.MpSaveDir))
                    {
                        string old = Paths.MpSaveDir + ".old_" + DateTime.Now.ToString("HHmmss");
                        try { Directory.Move(Paths.MpSaveDir, old); } catch { Directory.Delete(Paths.MpSaveDir, true); }
                    }
                    CopyDir(slotMp, Paths.MpSaveDir);
                }

                if (File.Exists(slotSave))
                {
                    File.Copy(slotSave, Paths.VanillaSave, true);
                }
                else if (!Directory.Exists(slotMp))
                {
                    error = "槽里没有 save.sv：" + slotName;
                    return false;
                }

                Log.Info("已铺档 -> " + slotName);
                return true;
            }
            catch (Exception e)
            {
                error = "铺档失败：" + e.Message;
                Log.Error(error + "\n" + e.StackTrace);
                return false;
            }
        }

        /// <summary>游戏外换档用：把某个槽直接铺成游戏的 save.sv。</summary>
        public static bool ExportToGameSave(string slotName, out string error)
        {
            return PrepareLoad(slotName, out error);
        }

        // ---------------------------------------------------------------- 列表 / 维护

        public static List<SlotMeta> List()
        {
            var result = new List<SlotMeta>();
            try
            {
                if (!Directory.Exists(Paths.SlotRoot)) return result;
                foreach (string dir in Directory.GetDirectories(Paths.SlotRoot))
                {
                    string name = Path.GetFileName(dir);
                    var meta = ReadMeta(name);
                    if (meta == null)
                    {
                        if (!File.Exists(Path.Combine(dir, "save.sv")) && !Directory.Exists(Path.Combine(dir, "mp"))) continue;
                        meta = new SlotMeta { name = name };
                    }
                    meta.name = name;
                    if (string.IsNullOrEmpty(meta.createdAt))
                    {
                        try { meta.createdAt = File.GetLastWriteTime(dir).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture); }
                        catch { }
                    }
                    meta.hasBackup = Directory.Exists(Paths.SlotBackup(name));
                    result.Add(meta);
                }
            }
            catch (Exception e) { Log.Error("列存档失败：" + e.Message); }
            return result.OrderByDescending(m => m.createdAt, StringComparer.Ordinal).ToList();
        }

        public static bool Delete(string slotName, out string error)
        {
            error = null;
            try
            {
                if (!Directory.Exists(Paths.SlotDir(slotName))) { error = "槽不存在"; return false; }
                Directory.Delete(Paths.SlotDir(slotName), true);
                Log.Info("已删除槽：" + slotName);
                return true;
            }
            catch (Exception e) { error = "删除失败：" + e.Message; return false; }
        }

        public static bool Rename(string oldName, string newNameRaw, out string error)
        {
            error = null;
            string newName = Paths.SafeName(newNameRaw);
            if (newName == null) { error = "名字不合法"; return false; }
            try
            {
                if (!Directory.Exists(Paths.SlotDir(oldName))) { error = "槽不存在"; return false; }
                if (Directory.Exists(Paths.SlotDir(newName))) { error = "目标名字已存在"; return false; }
                Directory.Move(Paths.SlotDir(oldName), Paths.SlotDir(newName));
                var meta = ReadMeta(newName) ?? new SlotMeta();
                meta.name = newName;
                WriteMeta(newName, meta);
                Log.Info("已改名：" + oldName + " -> " + newName);
                return true;
            }
            catch (Exception e) { error = "改名失败：" + e.Message; return false; }
        }

        /// <summary>把 backup 里的上一档还原成当前槽内容。</summary>
        public static bool RestoreBackup(string slotName, out string error)
        {
            error = null;
            string bak = Paths.SlotBackup(slotName);
            try
            {
                if (!Directory.Exists(bak)) { error = "这个槽没有上一档备份"; return false; }
                if (File.Exists(Path.Combine(bak, "save.sv")))
                {
                    File.Copy(Path.Combine(bak, "save.sv"), Paths.SlotSave(slotName), true);
                }
                string bakMp = Path.Combine(bak, "mp");
                if (Directory.Exists(bakMp))
                {
                    if (Directory.Exists(Paths.SlotMp(slotName))) Directory.Delete(Paths.SlotMp(slotName), true);
                    CopyDir(bakMp, Paths.SlotMp(slotName));
                }
                Log.Info("已回滚上一档：" + slotName);
                return true;
            }
            catch (Exception e) { error = "回滚失败：" + e.Message; return false; }
        }

        // ---------------------------------------------------------------- 内部

        private static void RotateBackup(string slotName)
        {
            string bak = Paths.SlotBackup(slotName);
            try
            {
                if (Directory.Exists(bak)) Directory.Delete(bak, true);
                Directory.CreateDirectory(bak);
                if (File.Exists(Paths.SlotSave(slotName)))
                    File.Copy(Paths.SlotSave(slotName), Path.Combine(bak, "save.sv"), true);
                if (Directory.Exists(Paths.SlotMp(slotName)))
                    CopyDir(Paths.SlotMp(slotName), Path.Combine(bak, "mp"));
            }
            catch (Exception e) { Log.Warn("备份上一档失败：" + e.Message); }
        }

        private static string UniqueAutoName(SlotMeta meta)
        {
            string baseName = string.Format("{0}_D{1}", DateTime.Now.ToString("yyyy-MM-dd_HHmm"), Math.Max(1, meta.biome));
            string name = baseName;
            int i = 2;
            while (Directory.Exists(Paths.SlotDir(name))) name = baseName + "_" + i++;
            return name;
        }

        private static List<string> ListMpPlayers()
        {
            var list = new List<string>();
            try
            {
                foreach (string d in Directory.GetDirectories(Paths.MpSaveDir))
                {
                    string n = Path.GetFileName(d);
                    if (n.StartsWith("STEAM_")) list.Add(n);
                }
            }
            catch { }
            return list;
        }

        /// <summary>从 save.sv（gzip+JSON）里读出展示用的元信息。</summary>
        public static SlotMeta Probe(string path)
        {
            var meta = new SlotMeta();
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                meta.bytes = bytes.Length;
                string json = SaveSystem.Unzip(bytes);
                JObject o = JObject.Parse(json);

                meta.biome = I(o, "biome", 1);
                meta.runTime = F(o, "runTime", 0f);
                meta.character = S(o, "character");
                meta.cheated = B(o, "cheatedSave");
                JToken items = o["items"];
                meta.itemCount = items is JArray arr ? arr.Count : 0;
            }
            catch (Exception e)
            {
                Log.Warn("解析存档元信息失败（" + path + "）：" + e.Message);
            }
            return meta;
        }

        private static int I(JObject o, string k, int d) { try { return o[k] != null ? o[k].Value<int>() : d; } catch { return d; } }
        private static float F(JObject o, string k, float d) { try { return o[k] != null ? o[k].Value<float>() : d; } catch { return d; } }
        private static bool B(JObject o, string k) { try { return o[k] != null && o[k].Value<bool>(); } catch { return false; } }
        private static string S(JObject o, string k) { try { return o[k] != null ? o[k].Value<string>() : ""; } catch { return ""; } }

        public static SlotMeta ReadMeta(string slotName)
        {
            try
            {
                string p = Paths.SlotMeta(slotName);
                if (!File.Exists(p)) return null;
                return JsonConvert.DeserializeObject<SlotMeta>(File.ReadAllText(p));
            }
            catch { return null; }
        }

        public static void WriteMeta(string slotName, SlotMeta meta)
        {
            try
            {
                Directory.CreateDirectory(Paths.SlotDir(slotName));
                File.WriteAllText(Paths.SlotMeta(slotName), JsonConvert.SerializeObject(meta, Formatting.Indented));
            }
            catch (Exception e) { Log.Warn("写 meta 失败：" + e.Message); }
        }

        public static void CopyDir(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (string file in Directory.GetFiles(from))
                File.Copy(file, Path.Combine(to, Path.GetFileName(file)), true);
            foreach (string dir in Directory.GetDirectories(from))
                CopyDir(dir, Path.Combine(to, Path.GetFileName(dir)));
        }

        public static string FormatBytes(long b)
        {
            if (b < 1024) return b + "B";
            if (b < 1024 * 1024) return (b / 1024.0).ToString("0.0") + "KB";
            return (b / 1024.0 / 1024.0).ToString("0.0") + "MB";
        }
    }
}
