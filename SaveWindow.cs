using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SaveManager
{
    /// <summary>
    /// 独立的存档窗口：复用游戏的 SettingsMenu prefab 当「壳」（边框/背景/返回按钮全是原版），
    /// 隐藏掉分页按钮与「重置为默认」，内容换成 SM 自己的存档页。
    /// </summary>
    internal static class SaveWindow
    {
        private static int _styledId;

        public static bool Open()
        {
            try
            {
                if (Plugin.Instance == null) return false;

                SettingsMenu menu = SettingsMenu.instance;
                if (menu == null)
                {
                    Transform parent = null;
                    if (PauseHandler.main != null) parent = PauseHandler.main.transform;
                    else if (PreRunScript.instance != null && PreRunScript.instance.mainCanvas != null)
                        parent = PreRunScript.instance.mainCanvas.transform;

                    if (parent == null)
                    {
                        Ui.Notify(Lang.T("找不到界面，无法打开存档窗口"));
                        return false;
                    }

                    SettingsMenu.OpenMenu(parent);
                    menu = SettingsMenu.instance;
                }
                if (menu == null) return false;

                Plugin.Instance.StartCoroutine(OpenRoutine(menu));
                return true;
            }
            catch (Exception e) { Log.Error("打开存档窗口失败：" + e); return false; }
        }

        private static IEnumerator OpenRoutine(SettingsMenu menu)
        {
            yield return null;      // 等 SettingsMenu.Start 跑完
            yield return null;

            Style(menu);
            SavePage.RenderStandalone(menu);

            yield return null;
            if (menu == null || !menu.gameObject.activeInHierarchy)
                Log.Error("存档窗口渲染后不可见（可能被别的逻辑关掉了）");
        }

        /// <summary>
        /// 逐个隐藏分页按钮（**绝不动它们的父节点**——那里可能同时装着 content，一关整个窗口就没了），
        /// 再按持久化方法名藏掉「重置为默认」。
        /// </summary>
        private static void Style(SettingsMenu menu)
        {
            if (menu == null || menu.GetInstanceID() == _styledId) return;
            try
            {
                int tabs = 0;
                if (menu.buttons != null)
                {
                    foreach (Button b in menu.buttons)
                    {
                        if (b == null) continue;
                        b.gameObject.SetActive(false);
                        tabs++;
                    }
                }

                int resets = 0;
                foreach (Button b in menu.GetComponentsInChildren<Button>(true))
                {
                    if (b == null) continue;
                    if (PersistentMethod(b) == "ResetToDefault")
                    {
                        b.gameObject.SetActive(false);
                        resets++;
                    }
                }

                _styledId = menu.GetInstanceID();
                Log.Info("存档窗口：隐藏分页按钮 " + tabs + " 个、重置按钮 " + resets + " 个");
            }
            catch (Exception e) { Log.Warn("改造存档窗口失败：" + e.Message); }
        }

        private static string PersistentMethod(Button b)
        {
            try
            {
                var ev = b.onClick;
                if (ev == null) return null;
                int n = ev.GetPersistentEventCount();
                for (int i = 0; i < n; i++)
                {
                    string name = ev.GetPersistentMethodName(i);
                    if (!string.IsNullOrEmpty(name)) return name;
                }
            }
            catch { }
            return null;
        }
    }
}
