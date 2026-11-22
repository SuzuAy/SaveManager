# Changelog

## 0.3.0

- **改名为 SM (Save Manager)**：插件 GUID `suzuay.savemanager`，DLL 与插件目录改为 `SaveManager`
- 发布到 Thunderstore 社区 `casualties-unknown`（因此也会出现在 Gale 的模组浏览里）
- 旧版配置 `xiaomi.cssaveslots.cfg` 不再读取，首次启动会生成 `suzuay.savemanager.cfg`

## 0.2.4

- 存档页所有行改用游戏原生 prefab；分页宽度按布局组的真实 `spacing` + `padding` 计算，不再溢出
- 沙盒档 / 实机档**双向隔离**：列表过滤 + 读档前校验
- 插件选项搬进「存档设置」独立子页；`MarkCheatedSave` 默认改为关；改完配置显式写盘
- 分页标签改为「禁用克隆来的 TMP / 贴图 + 自绘文字」，修掉显示成「画面/语言」的问题
- 左栏独立滚动、行点击带拖动判定，修掉「滑一下就跳回顶部」

## 0.2.0

- 界面改到设置菜单的第 6 个分页（原生 UI）
- 游戏内直接读档（解除暂停 → 回主菜单 → 铺档 → 继续）默认开启
- 沙盒模式识别（不标作弊、槽上标「沙盒」）

## 0.1.0

- 初版：`F5` 随时存档、无上限槽、`backup\` 回滚、控制台命令、CasualtiesTogether `mp_save` 兼容
