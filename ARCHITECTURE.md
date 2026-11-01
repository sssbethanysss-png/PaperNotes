# PaperNotes 技术设计

Windows 10/11，C# / .NET 8 WPF，无第三方运行时包。原生多窗口、RichTextBox 编辑、Windows Forms NotifyIcon 托盘、Win32 RegisterHotKey。

## 目录
- src/Stickies：程序、数据模型、持久化、窗口与平台集成。
- tests：无外部测试框架的 Windows STA 集成测试。
- work：测试数据及开发中间文件（已由 .gitignore 排除）。
- outputs：使用/验证文档，以及 `build.ps1` 生成的可执行目录与 ZIP。

## 主要源码模块（src/Stickies）
- Models.cs / NoteStore.cs / Settings.cs：便签与偏好的字段及 JSON 持久化。
- NoteManager.cs / NoteWindow.cs / IndexWindow：多窗口生命周期、索引与分类筛选。
- NoteEditor.cs：WPF RichTextBox、格式菜单、选区/后续输入格式、富文本序列化。
- TaskParagraph.cs：段落级任务清单（附加属性、标记圆点、完成态样式与加载再水合）。
- CollapsePreview.cs：折叠条上只读的首行富文本预览（克隆 Inline，不挪动活动文档节点）。
- CategoryDialog.cs：新建/编辑分类名称与主题色的对话框。
- TxtTransfer.cs：TXT 编码检测、文件名与导入导出写出。
- WindowEffects.cs / WindowPlacement.cs / DesktopIntegration.cs：置顶/透明度、多屏几何、托盘与热键、单实例。

## 状态与通信
Note 包含 Id、Content、CreatedAt、UpdatedAt、X/Y、Width/Height、Color、IsAlwaysOnTop、IsPinned、IsHidden，以及后续增量字段 IsTranslucent、FormatVersion、RichTextRtfBase64、CategoryId、IsCollapsed、ExpandedWidth/ExpandedHeight。管理服务以 ID 为键维护窗口，每个便签是无 Owner 的独立 WPF Window。UI 线程共享模型，以事件更新索引。置顶和索引固定是两个独立字段。

## 持久化
每条便签一个 JSON 文件，400ms debounce，写入临时文件并 Flush(true)，随后 File.Replace 原子替换并保留上一版 .bak。不是仅退出保存。单条写入故障不会覆盖其他便签。损坏文件保留，优先恢复备份；读写错误明确提示。正文停止输入到落盘前最多约 400ms 的变更仍可能因强制终止丢失。

富文本：JSON 字段名仍为 `RichTextRtfBase64`（兼容旧文件）。当前写入为 FlowDocument 的 Base64 **XAML**（`XamlWriter.Save`，方法名 `SaveRtf` 为历史命名）。加载时若 payload 像 XAML 则 `XamlReader.Parse`；否则按 legacy **RTF** 读取。`Content` 仍为搜索/TXT/纯文本权威；富文本与正文不一致或不完整时可回退纯文字。不批量迁移旧便签。

## 窗口与 Windows 集成
窗口大小使用 WPF DIP；位置由 Win32 物理像素边界保存并通过屏幕工作区校正，兼容负坐标和混合 DPI。窗口关闭为隐藏，程序退出单独处理。批量隐藏不改变单条隐藏偏好。
托盘常驻，索引关闭不退出。全局快捷键注册失败提示且继续运行。单实例通过命名互斥量和命名事件唤醒已有索引。开机启动默认关闭，用户开启后设置当前用户 Run 项。

## 开发顺序
1. 单窗口、输入、自动保存。
2. 多窗口、位置大小恢复。
3. 置顶、隐藏、删除。
4. 索引与搜索。
5. 托盘、快捷键、单实例。
6. 设置、开机启动。
7. 多屏、DPI、异常恢复和完整回归。

## 第二轮增量设计

UI 和编辑层按 A—E 修改。Note 新增 IsTranslucent（false）、FormatVersion（1）和可选 RichTextRtfBase64。旧 Content 和所有位置/状态字段继续保留。富文本与 Content 必须可对齐，否则回退纯文字；不批量迁移。仅在编辑或格式变动时更新富文本字段，沿用原 400ms debounce 和原子写入。

RichTextBox 的原生空选区格式可能扩展到所在单词，因此无选区格式暂存为后续输入偏好，待原生输入完成后只应用到新增字符范围。不会修改输入法正在组合中的文档。选区格式和剪贴板、撤销仍由 WPF 支持。

WindowChrome 的 6 DIP 缩放边框保留，启用 WPF 透明窗口合成；高度低于 96 DIP 折叠工具区。最小高度改为 40 DIP。置顶在窗口状态事件后用 HWND_TOPMOST/NOTOPMOST 和 NOACTIVATE 校正，无定时抢焦点逻辑。

TXT 的编码、命名和写出集中在 TxtTransfer。严格 UTF-8 + BOM 检测，批量输出用 CreateNew 避免覆盖及竞争。索引多选按 ID 在刷新时保留；普通单击打开，Ctrl/Shift 多选不激活便签。

参考：[WPF RichTextBox](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/richtextbox)、[TextRange.Save](https://learn.microsoft.com/en-us/dotnet/api/system.windows.documents.textrange.save)、[WPF TextSelection 实现](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Documents/TextSelection.cs)。
