using System;
using UnityEngine;

namespace SaveManager
{
    /// <summary>程序生成的图标（原版风格：透明底 + 细白圈 + 白色剪影）。</summary>
    internal static class Icons
    {
        private static Sprite _floppyRing;

        /// <summary>白圈 + 软盘（ESC 按钮、主菜单按钮共用）。</summary>
        public static Sprite FloppyRing()
        {
            if (_floppyRing != null) return _floppyRing;

            const int S = 128;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[S * S];
            var clear = new Color32(0, 0, 0, 0);
            var white = new Color32(255, 255, 255, 255);
            for (int i = 0; i < px.Length; i++) px[i] = clear;

            float cx = (S - 1) * 0.5f, cy = (S - 1) * 0.5f;
            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d <= 61f && d >= 56.5f) px[y * S + x] = white;
                }
            }

            Fill(px, S, 46, 36, 82, 92, white);      // 盘体
            Fill(px, S, 52, 44, 76, 66, clear);      // 标签窗
            Fill(px, S, 64, 78, 82, 92, clear);      // 快门缺口
            Fill(px, S, 68, 82, 76, 90, white);      // 快门滑片

            tex.SetPixels32(px);
            tex.Apply();
            _floppyRing = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
            _floppyRing.name = "SM.FloppyRing";
            return _floppyRing;
        }

        private static void Fill(Color32[] px, int size, int x0, int y0, int x1, int y1, Color32 c)
        {
            for (int y = Mathf.Max(0, y0); y <= Mathf.Min(size - 1, y1); y++)
                for (int x = Mathf.Max(0, x0); x <= Mathf.Min(size - 1, x1); x++)
                    px[y * size + x] = c;
        }
    }
}
