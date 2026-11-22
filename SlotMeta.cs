using System;
using System.Collections.Generic;

namespace CSSaveSlots
{
    /// <summary>一个存档槽的随档元数据（写进 slot 目录的 meta.json，方便游戏外直接看）。</summary>
    [Serializable]
    internal class SlotMeta
    {
        public int format = 1;
        public string name = "";
        public string createdAt = "";
        public string mode = "sp";          // sp | mp
        public bool sandbox = false;        // 是否在沙盒场地里存的
        public int biome = 1;               // 层数（原版 biomeDepth + 1）
        public float runTime = 0f;          // 存活时长（秒）
        public string character = "";
        public int itemCount = 0;
        public bool cheated = false;
        public bool hasBackup = false;
        public long bytes = 0;
        public List<string> players = new List<string>();
        public string note = "";

        public string TimeText()
        {
            try
            {
                if (DateTime.TryParse(createdAt, out var dt)) return dt.ToString("MM-dd HH:mm");
            }
            catch { /* ignore */ }
            return createdAt;
        }

        public string DurationText()
        {
            int total = (int)Math.Floor(runTime);
            int h = total / 3600, m = (total % 3600) / 60;
            if (h > 0) return h + "h" + m + "m";
            return m + "m" + (total % 60) + "s";
        }
    }
}
