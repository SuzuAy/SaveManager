using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace SaveManager
{
    /// <summary>
    /// ESC 暂停菜单里的圆形「存档」按钮：注册进 PauseHandler.buttons / buttonPos，
    /// 因此跟原生按钮共用同一套「从中心弹出 + 缩放渐入」动画。
    /// </summary>
    internal static class PauseButton
    {
        private static GameObject _go;
        private static Sprite _icon;
        private static readonly FieldInfo ButtonPosField =
            typeof(PauseHandler).GetField("buttonPos", BindingFlags.NonPublic | BindingFlags.Instance);

        public static void Install(PauseHandler handler)
        {
            try
            {
                if (_go != null) return;
                if (handler == null || handler.buttons == null || handler.buttons.Count == 0) return;

                RectTransform src = handler.buttons[0];
                if (src == null || src.parent == null) return;

                float side = src.sizeDelta.y > 1f ? src.sizeDelta.y : 64f;
                if (src.sizeDelta.y <= 1f) side = 64f;
                side = Mathf.Clamp(side, 48f, 128f);

                var go = new GameObject("SM.SaveButton", typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(src.parent, false);

                var rt = go.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(side, side);
                rt.localScale = Vector3.one;
                rt.localPosition = ComputePos(handler, side);

                var img = go.GetComponent<Image>();
                img.sprite = Icon();
                img.type = Image.Type.Simple;
                img.preserveAspect = true;
                img.color = Color.white;

                var btn = go.GetComponent<Button>();
                btn.targetGraphic = img;
                ColorBlock cb = btn.colors;
                cb.normalColor = Color.white;
                cb.highlightedColor = new Color(1.25f, 1.25f, 1.2f, 1f);
                cb.pressedColor = new Color(0.75f, 0.75f, 0.7f, 1f);
                btn.colors = cb;
                btn.onClick.AddListener(() =>
                {
                    try { PlayerCamera.PlayUISound("miniClick"); } catch { }
                    SavePage.Open();
                });

                try
                {
                    var tip = go.AddComponent<UITooltip>();
                    tip.skipLocale = true;
                    tip.tipName = "存档";
                    tip.tipDesc = "打开存档页面（SM · Save Manager）";
                }
                catch (Exception e) { Log.Warn("存档按钮 tooltip 失败：" + e.Message); }

                // 注册进暂停菜单的按钮序列（动画循环遍历 buttons，用 buttonPos[i] 取基准位置）
                handler.buttons.Add(rt);
                var list = ButtonPosField != null ? ButtonPosField.GetValue(handler) as List<Vector2> : null;
                if (list != null) list.Add(rt.localPosition);

                _go = go;
                Log.Info("ESC 暂停菜单已加入圆形存档按钮");
            }
            catch (Exception e) { Log.Error("加暂停菜单按钮失败：" + e); }
        }

        /// <summary>按现有按钮的排列方向，把新按钮排在末尾（竖排往下、横排往右）。</summary>
        private static Vector2 ComputePos(PauseHandler handler, float side)
        {
            List<RectTransform> list = handler.buttons;
            RectTransform last = list[list.Count - 1];
            Vector2 lp = last.localPosition;

            if (list.Count >= 2)
            {
                Vector2 fp = list[0].localPosition;
                float dx = Mathf.Abs(lp.x - fp.x);
                float dy = Mathf.Abs(lp.y - fp.y);
                float step = Mathf.Max(dx, dy) / (list.Count - 1);
                if (step < 1f) step = side + 8f;
                if (dy >= dx) return new Vector2(lp.x, lp.y - step);
                return new Vector2(lp.x + step, lp.y);
            }
            return new Vector2(lp.x, lp.y - side - 8f);
        }

        // ------------------------------------------------------------ 图标（圆环 + 软盘，跟着游戏的黑底白线风格）

        private static Sprite Icon()
        {
            if (_icon != null) return _icon;

            const int S = 128;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            var px = new Color32[S * S];
            float cx = (S - 1) * 0.5f;
            float cy = (S - 1) * 0.5f;
            var white = new Color32(238, 238, 232, 255);
            var fill = new Color32(13, 14, 18, 255);
            var clear = new Color32(0, 0, 0, 0);

            const float ringOuter = 60f;
            const float ringInner = 51f;
            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    Color32 c;
                    if (d <= ringInner) c = fill;
                    else if (d <= ringOuter + 1f)
                    {
                        float a = Mathf.Clamp01(ringOuter + 1f - d) * Mathf.Clamp01(d - ringInner + 1f);
                        c = new Color32(white.r, white.g, white.b, (byte)(255 * Mathf.Clamp01(a)));
                    }
                    else c = clear;
                    px[y * S + x] = c;
                }
            }

            // 软盘：盘体描边 + 上部快门 + 下部标签
            OutlineRect(px, S, 42, 46, 86, 84, 4, white);
            FillRect(px, S, 58, 70, 80, 84, white);
            OutlineRect(px, S, 50, 52, 78, 66, 3, white);

            tex.SetPixels32(px);
            tex.Apply();
            _icon = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
            _icon.name = "SM.SaveIcon";
            return _icon;
        }

        private static void FillRect(Color32[] px, int size, int x0, int y0, int x1, int y1, Color32 c)
        {
            for (int y = Mathf.Max(0, y0); y <= Mathf.Min(size - 1, y1); y++)
                for (int x = Mathf.Max(0, x0); x <= Mathf.Min(size - 1, x1); x++)
                    px[y * size + x] = c;
        }

        private static void OutlineRect(Color32[] px, int size, int x0, int y0, int x1, int y1, int t, Color32 c)
        {
            FillRect(px, size, x0, y0, x1, y0 + t - 1, c);
            FillRect(px, size, x0, y1 - t + 1, x1, y1, c);
            FillRect(px, size, x0, y0, x0 + t - 1, y1, c);
            FillRect(px, size, x1 - t + 1, y0, x1, y1, c);
        }
    }

    [HarmonyPatch(typeof(PauseHandler), "Start")]
    internal static class Patch_PauseHandler_Start
    {
        private static void Postfix(PauseHandler __instance)
        {
            try { PauseButton.Install(__instance); }
            catch (Exception e) { Log.Error("暂停按钮注入失败：" + e); }
        }
    }
}
