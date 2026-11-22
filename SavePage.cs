using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SaveManager
{
    /// <summary>
    /// 存档页 = 游戏设置菜单里的第 6 个分页。
    /// 分页按钮克隆游戏原生按钮并写进 SettingsMenu.buttons（游戏的分页目录），宽度按 (n*w-gap)/(n+1) 等比缩；
    /// 页面 = 左侧可滚动操作栏（含本插件所有选项）+ 右侧可滚动存档列表。
    /// </summary>
    internal static class SavePage
    {
        public const int TabIndex = 5;
        private const string RowPrefab = "Special/GameSettingLanguage";
        private const string InputPrefab = "Special/GameSettingInt";

        private const float LeftWidth = 196f;
        private const float Gap = 8f;
        private const float TabGap = 4f;

        private static readonly List<GameObject> _rows = new List<GameObject>();
        private static RectTransform _content;
        private static RectTransform _leftContent;
        private static RectTransform _rightContent;
        private static ScrollRect _leftScroll;
        private static ScrollRect _rightScroll;
        private static SettingsMenu _menu;
        private static Button _tabButton;
        private static string _selected;
        private static float _pageHeight = 430f;
        private static float _keepLeft = 1f;
        private static float _keepRight = 1f;

        private enum Screen { Main, Options }
        private static Screen _screen = Screen.Main;
        private static int _lastCat = -1;

        // ------------------------------------------------------------ 分页按钮

        public static void InstallTab(SettingsMenu menu)
        {
            _menu = menu;
            if (menu == null || menu.buttons == null || menu.buttons.Count == 0) return;
            if (_tabButton != null && _tabButton.gameObject != null) return;

            Button last = menu.buttons[menu.buttons.Count - 1];
            if (last == null || last.transform.parent == null) return;

            GameObject go = UnityEngine.Object.Instantiate(last.gameObject, last.transform.parent);
            go.name = "SaveManagerTab";
            go.SetActive(true);

            // 几何参数向最后一个分页看齐（只动自己，不动别人）
            var srcRt = last.GetComponent<RectTransform>();
            var rt = go.GetComponent<RectTransform>();
            if (srcRt != null && rt != null)
            {
                rt.anchorMin = srcRt.anchorMin;
                rt.anchorMax = srcRt.anchorMax;
                rt.pivot = srcRt.pivot;
                rt.sizeDelta = srcRt.sizeDelta;
                rt.anchoredPosition = srcRt.anchoredPosition;
            }

            // 标签：克隆来的文字可能是「烘进贴图」的、也可能每帧被游戏刷回原文，
            // 所以把所有子级 TMP 直接禁用（不是只改文字），再无条件加自己的「存档」标签。
            foreach (var t0 in go.GetComponentsInChildren<TextMeshProUGUI>(true)) t0.enabled = false;
            foreach (var img in go.GetComponentsInChildren<Image>(true))
            {
                if (img.gameObject != go) img.enabled = false;   // 只留根节点那层外框（状态贴图）
            }
            AddLabel(go);

            var btn = go.GetComponent<Button>();
            if (btn == null) { UnityEngine.Object.Destroy(go); return; }
            btn.onClick = new Button.ButtonClickedEvent();
            btn.onClick.AddListener(() =>
            {
                if (SettingsMenu.instance != null) SettingsMenu.instance.SelectTab(TabIndex);
            });

            menu.buttons.Add(btn);
            _tabButton = btn;

            RelayoutTabRow();
            Log.Info("设置页已注册「存档」分页（buttons index " + (menu.buttons.Count - 1) + "）");
        }

        /// <summary>
        /// 宽度按「行的可用宽 + 布局组真实 spacing」精确算：newW = (rowWidth - n*spacing) / (n+1)。
        /// 不猜 spacing（之前固定按 4 猜，实际间距更大 ⇒ 第 6 个向右溢出、贴到边框外）。
        /// </summary>
        private static void RelayoutTabRow()
        {
            try
            {
                if (_menu == null || _menu.buttons == null) return;
                int total = _menu.buttons.Count;
                int n = total - 1;
                if (n < 1) return;

                RectTransform layoutRoot = FindLayoutGroup(_tabButton != null ? _tabButton.transform : null);
                HorizontalLayoutGroup hlg = layoutRoot != null ? layoutRoot.GetComponent<HorizontalLayoutGroup>() : null;
                GridLayoutGroup grid = layoutRoot != null ? layoutRoot.GetComponent<GridLayoutGroup>() : null;

                // 原来的单页宽（无布局组时兜底用）
                float w = 0f;
                for (int i = 0; i < n; i++)
                {
                    var r = _menu.buttons[i] != null ? _menu.buttons[i].GetComponent<RectTransform>() : null;
                    if (r == null) continue;
                    float cw = r.rect.width > 1f ? r.rect.width : Mathf.Abs(r.sizeDelta.x);
                    if (cw > w) w = cw;
                }

                float spacing = TabGap;
                float rowWidth = 0f;
                if (hlg != null)
                {
                    spacing = hlg.spacing;
                    rowWidth = layoutRoot.rect.width - hlg.padding.left - hlg.padding.right;
                }
                else if (grid != null)
                {
                    spacing = grid.spacing.x;
                    rowWidth = layoutRoot.rect.width - grid.padding.left - grid.padding.right;
                }

                float newW;
                if (rowWidth > 1f)
                    newW = Mathf.Floor((rowWidth - n * spacing) / (float)(n + 1)) - 1f;   // 再让 1px，别贴边
                else if (w > 1f)
                    newW = Mathf.Floor((n * w - TabGap) / (float)(n + 1)) - 1f;
                else
                    return;

                newW = Mathf.Max(24f, newW);

                for (int i = 0; i < total; i++)
                {
                    var r = _menu.buttons[i] != null ? _menu.buttons[i].GetComponent<RectTransform>() : null;
                    if (r == null) continue;
                    r.sizeDelta = new Vector2(newW, r.sizeDelta.y);

                    var le = r.GetComponent<LayoutElement>();
                    if (le != null)
                    {
                        le.preferredWidth = newW;
                        if (le.minWidth > newW) le.minWidth = newW;
                    }
                }

                if (layoutRoot != null)
                {
                    LayoutRebuilder.MarkLayoutForRebuild(layoutRoot);
                    Canvas.ForceUpdateCanvases();
                    return;
                }

                float x0 = float.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    var r = _menu.buttons[i].GetComponent<RectTransform>();
                    if (r != null) x0 = Mathf.Min(x0, r.anchoredPosition.x);
                }
                if (x0 > 1e6f) return;

                for (int i = 0; i < total; i++)
                {
                    var r = _menu.buttons[i].GetComponent<RectTransform>();
                    if (r == null) continue;
                    r.anchoredPosition = new Vector2(x0 + i * (newW + TabGap), r.anchoredPosition.y);
                }
            }
            catch (Exception e) { Log.Warn("分页排版修正失败：" + e.Message); }
        }

        /// <summary>给分页按钮补一个 TMP 文字（字体从设置菜单里借，保证中文字形一致）。</summary>
        private static void AddLabel(GameObject go)
        {
            try
            {
                var label = new GameObject("Label", typeof(RectTransform));
                label.transform.SetParent(go.transform, false);
                var rt = label.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(2f, 2f);
                rt.offsetMax = new Vector2(-2f, -2f);

                var t = label.AddComponent<TextMeshProUGUI>();
                var font = BorrowFont();
                if (font != null) t.font = font;
                t.text = "存档";
                t.fontSize = 15f;
                t.alignment = TextAlignmentOptions.Center;
                t.color = new Color(0.92f, 0.92f, 0.88f, 1f);
                t.raycastTarget = false;
            }
            catch (Exception e) { Log.Warn("分页文字创建失败：" + e.Message); }
        }

        private static TMP_FontAsset _font;

        private static TMP_FontAsset BorrowFont()
        {
            if (_font != null) return _font;
            try
            {
                if (_menu != null)
                {
                    foreach (var t in _menu.GetComponentsInChildren<TextMeshProUGUI>(true))
                        if (t.font != null) { _font = t.font; return _font; }
                }
                var all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                if (all != null && all.Length > 0) _font = all[0];
            }
            catch { }
            return _font;
        }

        private static RectTransform FindLayoutGroup(Transform from)
        {
            Transform t = from;
            for (int depth = 0; depth < 6 && t != null; depth++)
            {
                var lg = t.GetComponent<LayoutGroup>();
                if (lg != null) return lg.transform as RectTransform;
                t = t.parent;
            }
            return null;
        }

        // ------------------------------------------------------------ 渲染

        public static void OnTabSelected(SettingsMenu menu, Setting.SettingCategory category)
        {
            _menu = menu;
            ClearRows();
            if ((int)category != TabIndex) { _lastCat = (int)category; return; }
            if (_lastCat != (int)category) { _screen = Screen.Main; _lastCat = (int)category; }
            try { BuildPage(); }
            catch (Exception e) { Log.Error("渲染存档页失败：" + e); }
        }

        private static void ClearRows()
        {
            if (_leftScroll != null) _keepLeft = _leftScroll.verticalNormalizedPosition;
            if (_rightScroll != null) _keepRight = _rightScroll.verticalNormalizedPosition;

            for (int i = _rows.Count - 1; i >= 0; i--)
                if (_rows[i] != null) UnityEngine.Object.Destroy(_rows[i]);
            _rows.Clear();

            _leftScroll = null;
            _rightScroll = null;
            _leftContent = null;
            _rightContent = null;
        }

        private static void BuildPage()
        {
            if (_menu == null || _menu.content == null) return;
            _content = _menu.content;

            float width = PageWidth();
            _pageHeight = PageHeight();

            RectTransform root = NewRect("SaveManager.Page", _content);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.sizeDelta = new Vector2(width, _pageHeight);
            root.anchoredPosition = Vector2.zero;
            _rows.Add(root.gameObject);

            if (_screen == Screen.Options) BuildOptions(root, width);
            else
            {
                BuildLeft(NewColumn("Left", root, 0f, LeftWidth, out _leftScroll, out _leftContent));
                BuildRight(NewColumn("Right", root, LeftWidth + Gap, width - LeftWidth - Gap, out _rightScroll, out _rightContent));
            }

            // 整页固定一屏高：外层设置滚动条无可滚内容，滚轮留给两列
            _content.sizeDelta = new Vector2(_content.sizeDelta.x, _pageHeight);

            if (Plugin.Instance != null) Plugin.Instance.StartCoroutine(RestoreScroll());
        }

        /// <summary>造一个带 ScrollRect 的列（viewport + RectMask2D + 可接收滚轮的透明 Image）。</summary>
        private static RectTransform NewColumn(string name, RectTransform parent, float x, float width,
            out ScrollRect scroll, out RectTransform content)
        {
            RectTransform root = NewRect(name, parent);
            root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.sizeDelta = new Vector2(width, _pageHeight);
            root.anchoredPosition = new Vector2(x, 0f);
            _colWidth = width;
            root.gameObject.AddComponent<RectMask2D>();
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0.001f);

            content = NewRect(name + ".Content", root);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 0f);

            scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = root;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            return root;
        }

        private static IEnumerator RestoreScroll()
        {
            yield return null;
            if (_leftScroll != null) _leftScroll.verticalNormalizedPosition = Mathf.Clamp01(_keepLeft);
            if (_rightScroll != null) _rightScroll.verticalNormalizedPosition = Mathf.Clamp01(_keepRight);
        }

        // ------------------------------------------------------------ 左栏（操作 + 插件选项）

        private static void BuildLeft(RectTransform column)
        {
            float y = 0f;
            bool sandbox = Sandbox.IsActive;

            Section(Lang.T("操作"), ref y);
            LRow(Lang.T("＋ 新建存档"), ref y, () =>
            {
                string msg;
                bool ok = Plugin.DoSave(null, out msg);
                Ui.Notify(ok ? msg : "存档失败：" + msg);
                Rebuild();
            }, sandbox ? new Color(0.98f, 0.86f, 0.52f, 1f) : (Color?)null);

            LRow(Lang.T("读取最近一档"), ref y, () =>
            {
                string msg;
                bool ok = Plugin.DoQuickLoad(out msg);
                Ui.Notify(ok ? msg : "读档失败：" + msg);
            });

            LRow(Lang.T("打开文件夹"), ref y, () =>
            {
                try
                {
                    if (!Directory.Exists(Paths.SlotRoot)) Directory.CreateDirectory(Paths.SlotRoot);
                    PreRunScript.OpenInFileBrowser(Paths.SlotRoot);
                }
                catch (Exception e) { Ui.Notify("打开目录失败：" + e.Message); }
            });

            if (_selected != null)
            {
                Section(Lang.T("已选存档"), ref y);
                LRow(Lang.T("读取此档"), ref y, () =>
                {
                    string msg;
                    bool ok = Plugin.DoLoad(_selected, out msg);
                    Ui.Notify(ok ? msg : "读档失败：" + msg);
                });
                LRow(Lang.T("回滚上一档"), ref y, () =>
                {
                    string err;
                    bool ok = SlotStore.RestoreBackup(_selected, out err);
                    Ui.Notify(ok ? "已回滚：" + _selected : "回滚失败：" + err);
                    Rebuild();
                });
                LRow(Lang.T("删除此档"), ref y, () =>
                {
                    string err;
                    bool ok = SlotStore.Delete(_selected, out err);
                    Ui.Notify(ok ? "已删除：" + _selected : "删除失败：" + err);
                    _selected = null;
                    Rebuild();
                });
            }

            // 左栏底部固定一行「设置」入口（不再用标题行 + 内联行）
            float bottom = Mathf.Max(y + 8f, _pageHeight - 46f);
            var settingsRow = NewRow(RowPrefab, _leftContent, _colWidth, ref bottom);
            if (settingsRow != null)
            {
                Bind(settingsRow, Lang.T("设置", "Settings"), () => { _screen = Screen.Options; Rebuild(); },
                    new Color(0.86f, 0.9f, 0.98f, 1f));
                var srt = settingsRow.GetComponent<RectTransform>();
                if (srt != null) srt.anchoredPosition = new Vector2(srt.anchoredPosition.x, -(_pageHeight - 27f));
            }

            _leftContent.sizeDelta = new Vector2(_leftContent.sizeDelta.x, Mathf.Max(bottom + 8f, _pageHeight));
        }

        /// <summary>「设置」页：可折叠分区 + 原版设置行（复选框 / 数字框 / 下拉框，和游戏设置页同款）。</summary>
        private static void BuildOptions(RectTransform root, float width)
        {
            NewColumn("Options", root, 0f, width, out _leftScroll, out _leftContent);
            _optY = 0f;

            var back = NewRow(RowPrefab, _leftContent, _colWidth, ref _optY);
            if (back != null)
                Bind(back, Lang.T("← 返回存档列表"), () => { _screen = Screen.Main; Rebuild(); },
                    new Color(0.98f, 0.86f, 0.52f, 1f));

            Fold("behaviour", Lang.T("行为"), () =>
            {
                BoolRow(Lang.T("游戏内读档"), Plugin.CfgInGameLoad);
                BoolRow(Lang.T("自动存档"), Plugin.CfgAutoSave);
                IntRow(Lang.T("自动存档间隔"), Plugin.CfgAutoSaveMinutes, 1, 120);
            });

            Fold("marking", Lang.T("存档标记"), () =>
            {
                BoolRow(Lang.T("标记作弊（成就关闭）"), Plugin.CfgCheatMark);
                BoolRow(Lang.T("沙盒不标作弊"), Plugin.CfgSandboxNoCheat);
            });

            Fold("other", Lang.T("其它"), () =>
            {
                BoolRow(Lang.T("联机允许存读档"), Plugin.CfgAllowMp);
                BoolRow(Lang.T("覆盖前保留上一档"), Plugin.CfgKeepBackup);
                BoolRow(Lang.T("绕过教程门禁"), Plugin.CfgBypassGate);
            });

            Fold("lang", Lang.T("界面", "Interface"), () =>
            {
                DropdownRow("Language / 语言",
                    new[] { Lang.T("跟随游戏", "Follow game"), "中文", "English" },
                    Lang.Mode == "zh" ? 1 : (Lang.Mode == "en" ? 2 : 0),
                    idx =>
                    {
                        Plugin.CfgLang.Value = idx == 1 ? "zh" : (idx == 2 ? "en" : "auto");
                        Plugin.SaveConfig();
                        Lang.Invalidate();
                        Rebuild();
                    });
            });

            _leftContent.sizeDelta = new Vector2(_leftContent.sizeDelta.x, _optY + 8f);
        }

        // ---- 折叠分区 & 原版设置行（复选框 / 数字框 / 下拉框）----

        private static float _optY;
        private static readonly HashSet<string> Folded = new HashSet<string>();

        private static void Fold(string key, string title, Action build)
        {
            bool folded = Folded.Contains(key);
            var go = NewRow(RowPrefab, _leftContent, _colWidth, ref _optY);
            if (go != null)
            {
                Bind(go, (folded ? "▸ " : "▾ ") + title, () =>
                {
                    if (Folded.Contains(key)) Folded.Remove(key); else Folded.Add(key);
                    Rebuild();
                }, new Color(0.98f, 0.86f, 0.52f, 1f));
            }
            if (!folded) build();
        }

        private static void BoolRow(string label, BepInEx.Configuration.ConfigEntry<bool> entry)
        {
            var go = NewRow("Special/GameSettingBool", _leftContent, _colWidth, ref _optY);
            if (go == null || entry == null) return;
            var lt = go.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
            if (lt != null) lt.text = label;
            var tg = go.transform.GetChild(1).GetComponent<Toggle>();
            if (tg != null)
            {
                tg.SetIsOnWithoutNotify(entry.Value);
                tg.onValueChanged.AddListener(v => { entry.Value = v; Plugin.SaveConfig(); });
            }
        }

        private static void IntRow(string label, BepInEx.Configuration.ConfigEntry<int> entry, int min, int max)
        {
            var go = NewRow("Special/GameSettingInt", _leftContent, _colWidth, ref _optY);
            if (go == null || entry == null) return;
            var lt = go.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
            if (lt != null) lt.text = label + Lang.T("（分钟）", " (min)");
            var f = go.transform.GetChild(1).GetComponent<TMP_InputField>();
            if (f != null)
            {
                f.SetTextWithoutNotify(entry.Value.ToString());
                f.onEndEdit.AddListener(v =>
                {
                    if (!int.TryParse(v, out int n)) n = entry.Value;
                    n = Mathf.Clamp(n, min, max);
                    f.SetTextWithoutNotify(n.ToString());
                    entry.Value = n;
                    Plugin.SaveConfig();
                });
            }
        }

        private static void DropdownRow(string label, string[] options, int current, Action<int> pick)
        {
            var go = NewRow("Special/GameSettingDropdown", _leftContent, _colWidth, ref _optY);
            if (go == null) return;
            var lt = go.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
            if (lt != null) lt.text = label;
            var dd = go.transform.GetChild(1).GetComponent<TMP_Dropdown>();
            if (dd == null) return;
            dd.ClearOptions();
            var list = new List<TMP_Dropdown.OptionData>();
            foreach (var o in options) list.Add(new TMP_Dropdown.OptionData(o));
            dd.AddOptions(list);
            dd.SetValueWithoutNotify(Mathf.Clamp(current, 0, options.Length - 1));
            dd.onValueChanged.RemoveAllListeners();
            dd.onValueChanged.AddListener(i => { try { pick(i); } catch (Exception e) { Log.Error("下拉框异常：" + e); } });
        }

        private static string LangModeLabel()
        {
            string m = Lang.Mode;
            if (m == "zh") return "中文";
            if (m == "en") return "English";
            return Lang.T("跟随游戏", "Follow game");
        }

        // ------------------------------------------------------------ 右栏（存档列表）

        private static void BuildRight(RectTransform column)
        {
            List<SlotMeta> slots = SlotStore.List();
            bool sandboxNow = Sandbox.IsActive;
            // 沙盒档与实机档互不相见、互不相通
            slots = slots.Where(s => s.sandbox == sandboxNow).ToList();
            if (_selected != null && slots.All(s => s.name != _selected)) _selected = null;

            float w = column.sizeDelta.x;
            float y = 0f;

            if (_selected != null)
            {
                SlotMeta sel = slots.FirstOrDefault(s => s.name == _selected);
                RRow(Lang.T("已选：") + _selected, ref y, w, null, new Color(0.98f, 0.86f, 0.52f, 1f));

                GameObject igo = NewRow(InputPrefab, _rightContent, w, ref y);
                if (igo != null)
                {
                    try
                    {
                        var lt = igo.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
                        if (lt != null) lt.text = Lang.T("新名字");
                        var f = igo.transform.GetChild(1).GetComponent<TMP_InputField>();
                        if (f != null)
                        {
                            f.SetTextWithoutNotify(sel != null ? sel.name : _selected);
                            f.onEndEdit.AddListener(v =>
                            {
                                try
                                {
                                    string err;
                                    if (!SlotStore.Rename(_selected, v, out err)) { Ui.Notify("改名失败：" + err); return; }
                                    Ui.Notify("已改名：" + v);
                                    _selected = Paths.SafeName(v);
                                    Rebuild();
                                }
                                catch (Exception e) { Log.Error("改名异常：" + e); }
                            });
                        }
                    }
                    catch (Exception e) { Log.Warn("改名输入行失败：" + e.Message); }
                }
            }

            RRow(Lang.T("共 ", "") + slots.Count + Lang.T(" 档（无上限，按时间倒序）", " slots (unlimited, newest first)"), ref y, w, null, new Color(0.72f, 0.72f, 0.68f, 1f));

            if (slots.Count == 0)
            {
                RRow(Lang.T("（还没有存档：进游戏按 F5，或点左边「新建存档」）"), ref y, w, null, new Color(0.72f, 0.72f, 0.68f, 1f));
            }
            else
            {
                foreach (SlotMeta m in slots)
                {
                    SlotMeta mm = m;
                    string mark = m.name == _selected ? "▶ " : "\u2003";
                    string tag = m.mode == "mp" ? Lang.T("联机", "MP") : Lang.T("单人", "SP");
                    if (m.sandbox) tag += Lang.T("·沙盒", "·sbx");
                    if (m.cheated) tag += Lang.T("·作弊", "·cheat");
                    RRow(string.Format(Lang.T("{0}{1}\u2003D{2} · {3} · {4}件 · {5}", "{0}{1}\u2003D{2} · {3} · {4} items · {5}"), mark, m.name, m.biome, m.DurationText(), m.itemCount, tag),
                        ref y, w,
                        () => { _selected = mm.name; Rebuild(); },
                        null);
                }
            }

            _rightContent.sizeDelta = new Vector2(0f, Mathf.Max(y, _pageHeight));
        }

        private static void Rebuild()
        {
            OnTabSelected(_menu, (Setting.SettingCategory)TabIndex);
        }

        /// <summary>独立窗口模式：直接把存档页渲染进 SettingsMenu 的 content（不经过分页）。</summary>
        public static void RenderStandalone(SettingsMenu menu)
        {
            if (menu == null || menu.content == null) return;
            _menu = menu;
            _content = menu.content;
            _screen = Screen.Main;
            ClearRows();
            ClearVanillaRows(menu);
            try { BuildPage(); }
            catch (Exception e) { Log.Error("渲染存档窗口失败：" + e); }
        }

        /// <summary>清掉 SettingsMenu.Start 里生成的「画面」页设置行，否则会和存档页叠在一起。</summary>
        private static void ClearVanillaRows(SettingsMenu menu)
        {
            try
            {
                var f = typeof(SettingsMenu).GetField("spawnedSettings",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (f != null)
                {
                    var list = f.GetValue(menu) as List<GameObject>;
                    if (list != null)
                    {
                        foreach (var go in list) if (go != null) UnityEngine.Object.Destroy(go);
                        list.Clear();
                    }
                }

                if (_content == null) return;
                for (int i = _content.childCount - 1; i >= 0; i--)
                {
                    GameObject child = _content.GetChild(i).gameObject;
                    if (_rows.Contains(child)) continue;
                    UnityEngine.Object.Destroy(child);
                }
                Log.Info("存档窗口：已清空原版设置行");
            }
            catch (Exception e) { Log.Warn("清空原版设置行失败：" + e.Message); }
        }

        // ------------------------------------------------------------ 原生行

        private static GameObject NewRow(string prefab, RectTransform parent, float width, ref float y)
        {
            GameObject go;
            try { go = Utils.Create(prefab, parent); }
            catch (Exception e) { Log.Warn("生成行失败 [" + prefab + "]：" + e.Message); return null; }
            if (go == null) return null;

            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                float h = rt.sizeDelta.y > 1f ? rt.sizeDelta.y : 44f;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(width, h);
                rt.anchoredPosition = new Vector2(0f, -(y + h * 0.5f));
                y += h + 2f;
            }
            _rows.Add(go);
            return go;
        }

        private static float _colWidth = LeftWidth;

        private static void Section(string title, ref float y)
        {
            GameObject go = NewRow(RowPrefab, _leftContent, _colWidth, ref y);
            if (go == null) return;
            var tmp = go.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.text = title;
                tmp.color = new Color(0.98f, 0.86f, 0.52f, 1f);
            }
            var btn = go.GetComponent<Button>();
            if (btn != null) btn.onClick = new Button.ButtonClickedEvent();
        }

        private static void LRow(string label, ref float y, Action onClick, Color? color = null)
        {
            GameObject go = NewRow(RowPrefab, _leftContent, _colWidth, ref y);
            if (go == null) return;
            Bind(go, label, onClick, color);
        }

        private static void ToggleRow(string label, ref float y, BepInEx.Configuration.ConfigEntry<bool> entry, string tip = null)
        {
            bool on = entry != null && entry.Value;
            LRow(label + (on ? Lang.T("：开", " : ON") : Lang.T("：关", " : OFF")), ref y, () =>
            {
                if (entry != null) entry.Value = !on;
                Plugin.SaveConfig();
                Rebuild();
            }, on ? new Color(0.72f, 0.92f, 0.72f, 1f) : (Color?)null);
        }

        private static void RRow(string label, ref float y, float width, Action onClick, Color? color = null)
        {
            GameObject go = NewRow(RowPrefab, _rightContent, width, ref y);
            if (go == null) return;
            Bind(go, label, onClick, color, true);
        }

        private static void Bind(GameObject go, string label, Action onClick, Color? color, bool dragGuard = true)
        {
            var tmp = go.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.text = label;
                if (color.HasValue) tmp.color = color.Value;
            }

            var btn = go.GetComponent<Button>();
            if (btn != null) btn.onClick = new Button.ButtonClickedEvent();   // 用 RowClick 代替，避免拖动滚动时误触发

            if (onClick == null) return;

            var click = go.GetComponent<RowClick>();
            if (click == null) click = go.AddComponent<RowClick>();
            click.dragGuard = dragGuard;
            click.Tap = () => { try { onClick(); } catch (Exception e) { Log.Error("行点击异常：" + e); } };
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        // ------------------------------------------------------------ 尺寸

        private static float PageWidth()
        {
            float w = _content.rect.width;
            if (w <= 1f) w = _content.sizeDelta.x;
            if (w <= 1f && _content.parent is RectTransform p) w = p.rect.width;
            if (w <= 1f) w = 760f;
            return w;
        }

        private static float PageHeight()
        {
            if (_content.parent is RectTransform p && p.rect.height > 1f) return p.rect.height;
            float h = _content.rect.height;
            if (h <= 1f) h = _content.sizeDelta.y;
            if (h <= 1f) h = 430f;
            return h;
        }

        public static void CommitLayout()
        {
            if (_content != null) _content.sizeDelta = new Vector2(_content.sizeDelta.x, _pageHeight);
        }

        // ------------------------------------------------------------ 入口

        public static bool Open()
        {
            if (Plugin.Instance == null) return false;
            Plugin.Instance.StartCoroutine(OpenRoutine());
            return true;
        }

        private static IEnumerator OpenRoutine()
        {
            if (PauseHandler.main != null)
            {
                if (!PauseHandler.paused) PauseHandler.TogglePause();
                yield return null;
                yield return null;
                if (PauseHandler.main != null) PauseHandler.main.Settings();
            }
            else
            {
                Transform parent = null;
                try
                {
                    if (PreRunScript.instance != null && PreRunScript.instance.mainCanvas != null)
                        parent = PreRunScript.instance.mainCanvas.transform;
                    else if (PlayerCamera.main != null && PlayerCamera.main.mainCanvas != null)
                        parent = PlayerCamera.main.mainCanvas.transform;
                }
                catch { }

                if (parent == null) { Ui.Notify("找不到 UI，无法打开存档页"); yield break; }
                SettingsMenu.OpenMenu(parent);
            }

            yield return null;
            yield return null;

            if (SettingsMenu.instance != null)
            {
                try { SettingsMenu.instance.SelectTab(TabIndex); }
                catch (Exception e) { Log.Error("跳转存档页失败：" + e); }
            }
        }
    }

    /// <summary>行点击：拖动滚动时不触发（否则一滑就选中并重置滚动位置）。</summary>
    internal class RowClick : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
    {
        public Action Tap;
        public bool dragGuard = true;
        private Vector2 _down;

        public void OnPointerDown(PointerEventData e) { _down = e.position; }

        public void OnPointerClick(PointerEventData e)
        {
            if (dragGuard && (e.position - _down).sqrMagnitude > 12f * 12f) return;
            try { Tap?.Invoke(); } catch (Exception ex) { Log.Error("行点击异常：" + ex); }
        }
    }

    // ---------------------------------------------------------------- Harmony

    [HarmonyPatch(typeof(SettingsMenu), "Start")]
    internal static class Patch_SettingsMenu_Start
    {
        private static void Postfix(SettingsMenu __instance)
        {
            try { SavePage.InstallTab(__instance); }
            catch (Exception e) { Log.Error("装存档分页失败：" + e); }
        }
    }

    [HarmonyPatch(typeof(SettingsMenu), "SelectTab", new[] { typeof(Setting.SettingCategory) })]
    internal static class Patch_SettingsMenu_SelectTab
    {
        private static void Postfix(SettingsMenu __instance, Setting.SettingCategory category)
        {
            try { SavePage.OnTabSelected(__instance, category); }
            catch (Exception e) { Log.Error("渲染存档分页失败：" + e); }
        }
    }
}


