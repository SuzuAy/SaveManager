using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace SaveManager
{
    /// <summary>
    /// ESC 暂停菜单的圆形「存档」按钮。
    /// 做法：克隆游戏自己的圆按钮（圆环/描边/尺寸/悬停效果全复用），只把图标换成软盘，
    /// 然后排到按钮组的最右侧（放不下就往上排），并注册进 buttons + buttonPos 参与原生动画。
    /// </summary>
    internal static class PauseButton
    {
        private static GameObject _go;
        private static Sprite _glyph;
        private static readonly FieldInfo ButtonPosField =
            typeof(PauseHandler).GetField("buttonPos", BindingFlags.NonPublic | BindingFlags.Instance);

        public static void Install(PauseHandler handler)
        {
            try
            {
                if (_go != null) return;
                if (handler == null || handler.buttons == null || handler.buttons.Count == 0) return;

                // 用最后一个按钮当模板（原版的圆环 + 图标层级都在这份 prefab 里）
                int templateIdx = handler.buttons.Count - 1;
                RectTransform template = handler.buttons[templateIdx];
                if (template == null || template.parent == null) return;

                GameObject go = UnityEngine.Object.Instantiate(template.gameObject, template.parent);
                go.name = "SM.SaveButton";
                go.SetActive(true);

                var rt = go.GetComponent<RectTransform>();
                rt.localScale = Vector3.one;

                // 换图标：原版按钮 = 外层圆环 Image + 图标 Image。
                // 不同按钮之间「圆环贴图相同、图标贴图不同」，据此定位图标那一张再替换（不碰圆环）。
                Sprite glyph = Glyph();
                List<Image> selfImgs = ImagesOf(go);
                int iconIdx = FindIconIndex(handler, templateIdx, selfImgs.Count);
                if (iconIdx >= 0 && iconIdx < selfImgs.Count && selfImgs[iconIdx] != null)
                {
                    var target = selfImgs[iconIdx];
                    target.sprite = glyph;
                    target.color = Color.white;
                    target.type = Image.Type.Simple;
                    target.preserveAspect = true;
                }

                var btn = go.GetComponent<Button>();
                if (btn == null) { UnityEngine.Object.Destroy(go); return; }
                btn.onClick = new Button.ButtonClickedEvent();
                btn.onClick.AddListener(() =>
                {
                    try { PlayerCamera.PlayUISound("miniClick"); } catch { }
                    SaveWindow.Open();
                });

                // 原版 tooltip（本地化由本插件自己处理）
                try
                {
                    var tip = go.GetComponent<UITooltip>();
                    if (tip == null) tip = go.AddComponent<UITooltip>();
                    tip.skipLocale = true;
                    tip.tipName = Lang.T("存档");
                    tip.tipDesc = Lang.T("打开存档页面（SM · Save Manager）", "Open the save slots page (SM)");
                }
                catch (Exception e) { Log.Warn("tooltip 设置失败：" + e.Message); }

                // 排位：优先放最右侧一格；超出父容器就改放到按钮上方
                rt.localPosition = ComputePos(handler, template, rt);

                handler.buttons.Add(rt);
                var posList = ButtonPosField != null ? ButtonPosField.GetValue(handler) as List<Vector2> : null;
                if (posList != null) posList.Add(rt.localPosition);

                _go = go;
                Log.Info("ESC 暂停菜单：已加入圆形存档按钮（克隆自原版按钮）");
            }
            catch (Exception e) { Log.Error("加暂停菜单按钮失败：" + e); }
        }

        /// <summary>按「圆环相同、图标不同」找出图标所在的 Image 序号。</summary>
        private static int FindIconIndex(PauseHandler handler, int templateIdx, int selfCount)
        {
            try
            {
                if (handler.buttons.Count < 2) return selfCount > 1 ? selfCount - 1 : 0;

                RectTransform other = null;
                for (int i = 0; i < handler.buttons.Count; i++)
                {
                    if (i == templateIdx || handler.buttons[i] == null) continue;
                    other = handler.buttons[i];
                    break;
                }
                if (other == null) return selfCount > 1 ? selfCount - 1 : 0;

                List<Image> a = ImagesOf(handler.buttons[templateIdx].gameObject);
                List<Image> b = ImagesOf(other.gameObject);
                int n = Mathf.Min(a.Count, b.Count);
                for (int i = 0; i < n; i++)
                {
                    if (a[i] == null || b[i] == null) continue;
                    if (a[i].sprite != b[i].sprite) return i;
                }
                return selfCount > 0 ? selfCount - 1 : 0;
            }
            catch (Exception e) { Log.Warn("定位图标层失败：" + e.Message); return 0; }
        }

        private static List<Image> ImagesOf(GameObject root)
        {
            var list = new List<Image>();
            if (root == null) return list;
            var rootImg = root.GetComponent<Image>();
            if (rootImg != null) list.Add(rootImg);
            foreach (var img in root.GetComponentsInChildren<Image>(true))
                if (img.gameObject != root) list.Add(img);
            return list;
        }

        private static Vector2 ComputePos(PauseHandler handler, RectTransform template, RectTransform self)
        {
            List<RectTransform> list = handler.buttons;
            float side = self.sizeDelta.x > 1f ? self.sizeDelta.x : 64f;
            if (list.Count == 0) return template.localPosition;

            // 找最右那个按钮（齿轮），放在它右上方，避开中间的大圆和按钮行
            RectTransform right = null;
            for (int i = 0; i < list.Count; i++)
            {
                RectTransform b = list[i];
                if (b == null) continue;
                if (right == null || b.localPosition.x > right.localPosition.x) right = b;
            }
            if (right == null) return template.localPosition;

            Vector2 rp = right.localPosition;
            Vector2 target = new Vector2(rp.x + side * 0.95f, rp.y + side * 0.97f);

            var parent = self.parent as RectTransform;
            if (parent != null && parent.rect.width > 1f)
            {
                float halfW = parent.rect.width * 0.5f;
                float halfH = parent.rect.height * 0.5f;
                if (target.x + side * 0.5f > halfW) target.x = halfW - side * 0.7f;
                if (target.y + side * 0.5f > halfH) target.y = halfH - side * 0.7f;
            }
            return target;
        }

        // ------------------------------------------------------------ 软盘图标（纯白剪影，和原版图标同风格）

        private static Sprite Glyph()
        {
            if (_glyph != null) return _glyph;

            const int S = 128;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[S * S];
            var clear = new Color32(0, 0, 0, 0);
            var white = new Color32(255, 255, 255, 255);
            for (int i = 0; i < px.Length; i++) px[i] = clear;

            // 1) 原版风格白圈（细描边、透明底）
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

            // 2) 圈内软盘：实心白盘体 + 挖空标签窗 + 挖空快门（含白色滑片）
            Fill(px, S, 46, 36, 82, 92, white);
            Fill(px, S, 52, 44, 76, 66, clear);
            Fill(px, S, 64, 78, 82, 92, clear);
            Fill(px, S, 68, 82, 76, 90, white);

            tex.SetPixels32(px);
            tex.Apply();
            _glyph = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
            _glyph.name = "SM.FloppyGlyph";
            return _glyph;
        }

        private static void Fill(Color32[] px, int size, int x0, int y0, int x1, int y1, Color32 c)
        {
            for (int y = Mathf.Max(0, y0); y <= Mathf.Min(size - 1, y1); y++)
                for (int x = Mathf.Max(0, x0); x <= Mathf.Min(size - 1, x1); x++)
                    px[y * size + x] = c;
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
