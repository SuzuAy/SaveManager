using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace SaveManager
{
    /// <summary>主菜单右上/右下角的圆形存档图标，点了直接开独立存档窗口。</summary>
    internal static class MenuButton
    {
        private static GameObject _go;

        public static void Install(PreRunScript pre)
        {
            try
            {
                if (pre == null || pre.mainCanvas == null) return;
                if (_go != null) return;

                var canvasRt = pre.mainCanvas.transform as RectTransform;
                var go = new GameObject("SM.MenuSaveButton", typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(pre.mainCanvas.transform, false);

                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                float side = 84f;
                if (canvasRt != null && canvasRt.rect.height > 1f)
                    side = Mathf.Clamp(canvasRt.rect.height * 0.12f, 64f, 132f);
                rt.sizeDelta = new Vector2(side, side);
                rt.anchoredPosition = new Vector2(-side * 0.6f, -side * 0.6f);

                var img = go.GetComponent<Image>();
                img.sprite = Icons.FloppyRing();
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
                    SaveWindow.Open();
                });

                try
                {
                    var tip = go.AddComponent<UITooltip>();
                    tip.skipLocale = true;
                    tip.tipName = Lang.T("存档");
                    tip.tipDesc = Lang.T("打开存档窗口（SM · Save Manager）", "Open the save window (SM)");
                }
                catch { }

                _go = go;
                Log.Info("主菜单：已加入存档图标按钮");
            }
            catch (Exception e) { Log.Error("主菜单按钮注入失败：" + e); }
        }
    }

    [HarmonyPatch(typeof(PreRunScript), "Start")]
    internal static class Patch_PreRunScript_Start
    {
        private static void Postfix(PreRunScript __instance)
        {
            try { MenuButton.Install(__instance); }
            catch (Exception e) { Log.Error("主菜单按钮异常：" + e); }
        }
    }
}
