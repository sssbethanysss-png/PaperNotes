# PaperNotes UI 三语对照

139 条原文已全部翻译，另附 13 条语言选择和新交互文案。译文已做结构校验，尚未接入应用或做界面验收。

日英品牌暂用 PaperNotes；中文保留纸间。日语统一使用付箋、付箋一覧、チェックリスト。

原始清单的重复 Key 已分开；以 JSON 的唯一 key 接入，source_key 只作追溯。占位符必须完整保留，接入时显式映射。

## 原清单对应翻译

| # | Key | 中文 | 日本語 | English |
|---|---|---|---|---|
| 1 | Notes.Count | {notes.Length} 张便签 | {notes.Length} 枚の付箋 | {notes.Length} notes |
| 2 | Hotkeys.RegisterFailed | 「{chord}」（{actionLabel}）该快捷键无法注册，可能已被占用或受系统限制。 | 「{actionLabel}」のショートカット {chord} を登録できませんでした。他のアプリで使用されているか、システムで制限されています。 | Couldn't register {chord} for “{actionLabel}”. It may be in use by another app or restricted by Windows. |
| 3 | Notes.CategoryEmpty | 「{name}」还没有便签。<br>点击「新建便签」，会直接归入这个分类。 | 「{name}」にはまだ付箋がありません。<br>「新しい付箋」で、このカテゴリに付箋を追加できます。 | No notes in “{name}” yet.<br>Choose “New note” to add one to this category. |
| 4 | Notes.NewButton | ＋  新建便签 | ＋  新しい付箋 | ＋  New note |
| 5 | Note.Translucent | 半透明 | 半透明 | Translucent |
| 6 | Common.Save | 保存 | 保存 | Save |
| 7 | Settings.Save | 保存设置 | 保存 | Save |
| 8 | Note.SaveRetrying | 保存失败 · 请检查磁盘，正在重试 | 保存できませんでした。ディスクを確認してください。再試行中です。 | Couldn't save. Check your disk. Retrying… |
| 9 | Settings.SaveFailedPrefix | 保存失败： | 設定を保存できませんでした： | Couldn't save settings: |
| 10 | Categories.EditTitle | 编辑分类 | カテゴリを編集 | Edit category |
| 11 | Categories.EditMenu | 编辑分类… | カテゴリを編集… | Edit category… |
| 12 | Recovery.Title | 便签恢复提示 | 付箋の復元について | Note recovery |
| 13 | Note.Color | 便签颜色 | 付箋の色 | Note color |
| 14 | Hotkeys.ModifierOnly | 不能仅使用修饰键 | Ctrl、Alt、Shift、Win 以外のキーも組み合わせてください。 | Add a key other than Ctrl, Alt, Shift, or Win. |
| 15 | Export.UnsavedNotes | 部分便签尚未保存，请检查磁盘后重试导出。 | 未保存の付箋があります。ディスクを確認してから、もう一度エクスポートしてください。 | Some notes haven't been saved. Check your disk, then try exporting again. |
| 16 | Notes.HideBlocked | 部分便签尚未保存，暂未隐藏。请检查磁盘空间或文件夹权限。 | 未保存の付箋があるため、非表示にできませんでした。ディスクの空き容量とフォルダーのアクセス権を確認してください。 | Couldn't hide notes because some haven't been saved. Check available disk space and folder permissions. |
| 17 | Error.OperationFailedPrefix | 操作未完成，请检查磁盘空间和文件夹权限。<br> | 操作を完了できませんでした。ディスクの空き容量とフォルダーのアクセス権を確認してください。<br> | Couldn't complete the action. Check available disk space and folder permissions.<br> |
| 18 | Edit.Undo | 撤回 | 元に戻す | Undo |
| 19 | Common.Create | 创建 | 作成 | Create |
| 20 | Format.Bold | 粗体 | 太字 | Bold |
| 21 | Notes.Open | 打开便签 | 付箋を開く | Open note |
| 22 | Notes.OpenListMenu | 打开便签索引 | 付箋一覧を開く | Open note list |
| 23 | Tray.OpenList | 打开索引 | 付箋一覧を開く | Open note list |
| 24 | Colors.Lavender | 淡紫 | ラベンダー | Lavender |
| 25 | Export.TextButton | 导出 TXT | TXT をエクスポート | Export TXT |
| 26 | Export.TextMenu | 导出 TXT… | TXT をエクスポート… | Export TXT… |
| 27 | Export.FailedPrefix | 导出失败：<br> | エクスポートできませんでした：<br> | Couldn't export:<br> |
| 28 | Export.IncompleteTitle | 导出未完成 | エクスポートが完了しませんでした | Export incomplete |
| 29 | Import.Text | 导入 TXT | TXT をインポート | Import TXT |
| 30 | Import.ResultsTitle | 导入结果 | インポート結果 | Import results |
| 31 | Settings.LaunchAtLogin | 登录 Windows 后自动启动 | Windows へのサインイン時に起動する | Launch at Windows sign-in |
| 32 | Hotkeys.RecordHint | 点击后按下快捷键组合 | クリックしてから、キーの組み合わせを押してください。 | Click, then press a key combination. |
| 33 | Categories.Label | 分类 | カテゴリ | Category |
| 34 | Categories.SaveFailedPrefix | 分类保存失败： | カテゴリを保存できませんでした： | Couldn't save category: |
| 35 | Categories.Name | 分类名称 | カテゴリ名 | Category name |
| 36 | Note.AlwaysOnTop | 浮在所有窗口之上 | 常に手前に表示 | Always on top |
| 37 | Edit.Copy | 复制 | コピー | Copy |
| 38 | Format.TextMismatch | 富文本与正文不一致 | 書式付きデータと本文が一致しません。 | The formatted content doesn't match the plain text. |
| 39 | Format.Highlight | 高亮 | 蛍光ペン | Highlight |
| 40 | Format.RestoreFailed | 格式无法恢复，已保留完整正文 | 書式を復元できませんでした。本文はすべて保持されています。 | Couldn't restore formatting. All text has been kept. |
| 41 | Format.MismatchPlainTextKept | 格式与正文不一致，已保留完整正文 | 書式付きデータと本文が一致しないため、本文をすべて保持しました。 | The formatted content didn't match the plain text. All text has been kept. |
| 42 | Note.MoreTooltip | 更多：置顶、固定、颜色、删除 | その他：最前面表示、ピン留め、色、削除 | More: always on top, pin, color, delete |
| 43 | Note.PinInList | 固定到索引顶部 | 一覧の先頭にピン留め | Pin to top of list |
| 44 | Window.Close | 关闭 | 閉じる | Close |
| 45 | Notes.EmptyXaml | 还没有便签。<br>点击「新建便签」，留下第一个想法。 | まだ付箋がありません。<br>「新しい付箋」で、最初のメモを書いてみましょう。 | No notes yet.<br>Choose “New note” to jot down your first thought. |
| 46 | App.ExitBlocked | 还有便签未能保存，请检查磁盘空间或文件夹权限后重试退出。 | 未保存の付箋があるため、終了できませんでした。ディスクの空き容量とフォルダーのアクセス権を確認し、もう一度終了してください。 | Couldn't quit because some notes haven't been saved. Check available disk space and folder permissions, then try again. |
| 47 | Window.Restore | 还原 | 元のサイズに戻す | Restore |
| 48 | Edit.Cut | 剪切 | 切り取り | Cut |
| 49 | Settings.NewNotesOnly | 仅影响之后新建的便签，不改变已有便签。 | 新しく作成する付箋にのみ適用されます。既存の付箋は変わりません。 | Applies only to new notes. Existing notes won't change. |
| 50 | Settings.LaunchAtLoginHelp | 开机后自动打开纸间，不必手动启动。 | Windows にサインインすると、PaperNotes が自動で起動します。 | PaperNotes opens automatically when you sign in to Windows. |
| 51 | Note.BlankTitle | 空白便签 | 空白の付箋 | Blank note |
| 52 | Settings.Shortcuts | 快捷键 | ショートカット | Keyboard shortcuts |
| 53 | Hotkeys.InvalidFormat | 快捷键格式无效 | ショートカットの形式が正しくありません。 | Invalid shortcut format. |
| 54 | Hotkeys.ModifierAndKeyRequired | 快捷键需要包含修饰键和主键 | 修飾キーと、組み合わせるキーを指定してください。 | Choose a modifier and another key. |
| 55 | Hotkeys.KeyRequired | 快捷键需要包含主键 | 組み合わせるキーを指定してください。 | Choose a key for this shortcut. |
| 56 | Hotkeys.Duplicate | 两个全局快捷键不能相同。 | それぞれ別のグローバルショートカットを設定してください。 | Use a different global shortcut for each action. |
| 57 | Notes.UncategorizedEmpty | 没有未分类的便签。<br>新建的便签会先放在这里。 | 未分類の付箋はありません。<br>カテゴリを指定せずに作成した付箋がここに表示されます。 | No uncategorized notes.<br>Notes created without a category appear here. |
| 58 | Notes.NoSearchResults | 没有找到匹配的便签 | 一致する付箋がありません。 | No matching notes. |
| 59 | Colors.Ivory | 米白 | アイボリー | Ivory |
| 60 | Categories.NameHasNewline | 名称不能包含换行 | カテゴリ名に改行は使えません。 | Category names can't contain line breaks. |
| 61 | Categories.NameTooLong | 名称最多 16 个字 | カテゴリ名は16文字以内にしてください。 | Use 16 characters or fewer for the category name. |
| 62 | Settings.DefaultNoteColor | 默认便签颜色 | 新しい付箋の色 | Default note color |
| 63 | Hotkeys.BareLetterOrDigit | 普通字母或数字不能单独作为全局快捷键 | 文字や数字を単独でグローバルショートカットには設定できません。 | A letter or number alone can't be used as a global shortcut. |
| 64 | Colors.PaleYellow | 浅黄 | ペールイエロー | Pale yellow |
| 65 | Colors.PaleGreen | 浅绿 | ペールグリーン | Pale green |
| 66 | Common.Clear | 清除 | クリア | Clear |
| 67 | Hotkeys.ClearTooltip | 清除全局快捷键 | グローバルショートカットを解除 | Clear global shortcut |
| 68 | Hotkeys.Recording | 请按下快捷键组合… | キーの組み合わせを押してください… | Press a key combination… |
| 69 | Categories.NameRequired | 请输入分类名称 | カテゴリ名を入力してください。 | Enter a category name. |
| 70 | Common.Cancel | 取消 | キャンセル | Cancel |
| 71 | Categories.AllTab | 全部 | すべて | All |
| 72 | Notes.AllTitle | 全部便签 | すべての付箋 | All notes |
| 73 | Hotkeys.RequiredModifier | 全局快捷键需要包含 Ctrl、Alt 或 Win | Ctrl、Alt、Win のいずれかを含めてください。 | Include Ctrl, Alt, or Win in the shortcut. |
| 74 | Hotkeys.ScopeHelp | 全局快捷键在应用处于后台时也能使用。应用内快捷键（如索引中的 Ctrl+N）不受这里影响。 | グローバルショートカットは、他のアプリを使用中でも使えます。付箋一覧の Ctrl+N など、アプリ内のショートカットはこの設定の影響を受けません。 | Global shortcuts work even when you're using another app. In-app shortcuts, such as Ctrl+N in the note list, aren't affected by these settings. |
| 75 | Edit.SelectAll | 全选 | すべて選択 | Select all |
| 76 | Settings.Subtitle | 让便签适合你的习惯 | 使い方に合わせてカスタマイズ | Make PaperNotes work for you |
| 77 | Format.Checklist | 任务清单 | チェックリスト | Checklist |
| 78 | Note.DeleteTitle | 删除便签 | 付箋を削除 | Delete note |
| 79 | Note.DeleteMenu | 删除便签… | 付箋を削除… | Delete note… |
| 80 | Categories.DeleteTitle | 删除分类 | カテゴリを削除 | Delete category |
| 81 | Categories.DeleteConfirm | 删除分类「{category.Name}」？<br>其中的便签会回到「未分类」，便签本身不会被删除。 | カテゴリ「{category.Name}」を削除しますか？<br>付箋は削除されず、「未分類」に移動します。 | Delete the category “{category.Name}”?<br>Its notes will move to “Uncategorized” and won't be deleted. |
| 82 | Categories.DeleteMenu | 删除分类… | カテゴリを削除… | Delete category… |
| 83 | Note.DeleteFailedPrefix | 删除失败，便签仍保留。<br> | 削除できませんでした。付箋はそのまま残っています。<br> | Couldn't delete the note. It has been kept.<br> |
| 84 | Note.DeleteConfirm | 删除这张便签？此操作无法撤销。 | この付箋を削除しますか？この操作は元に戻せません。 | Delete this note? This can't be undone. |
| 85 | Settings.Title | 设置 | 設定 | Settings |
| 86 | Settings.RecoveryWarning | 设置文件无法读取，已尝试恢复备份。便签内容不受影响。 | 設定ファイルを読み込めなかったため、バックアップからの復元を試みました。付箋の内容に影響はありません。 | Couldn't read the settings file. An attempt was made to restore a backup. Your notes aren't affected. |
| 87 | Notes.Search | 搜索 | 検索 | Search |
| 88 | Colors.SkyBlue | 天蓝 | スカイブルー | Sky blue |
| 89 | Settings.General | 通用 | 一般 | General |
| 90 | Tray.Quit | 退出纸间 | PaperNotes を終了 | Quit PaperNotes |
| 91 | Settings.Appearance | 外观 | 外観 | Appearance |
| 92 | Categories.Uncategorized | 未分类 | 未分類 | Uncategorized |
| 93 | Note.Untitled | 未命名便签 | 無題の付箋 | Untitled note |
| 94 | Hotkeys.NotSet | 未设置 | 未設定 | Not set |
| 95 | Files.TextFilter | 文本文件 (*.txt)\|*.txt | テキストファイル (*.txt)\|*.txt | Text files (*.txt)\|*.txt |
| 96 | Format.Title | 文字格式 | 書式 | Formatting |
| 97 | Import.Utf8Required | 无法按 UTF-8 读取，请将文件另存为 UTF-8 后重试。 | UTF-8 として読み込めませんでした。UTF-8 形式で保存し直してから、もう一度お試しください。 | Couldn't read this file as UTF-8. Save it with UTF-8 encoding, then try again. |
| 98 | Recovery.NoteReadFailedPrefix | 无法读取便签，原文件已保留： | 付箋を読み込めませんでした。元のファイルは保持されています： | Couldn't read the note. The original file has been kept: |
| 99 | App.DataUnavailablePrefix | 无法访问本地数据，请检查文件夹权限和可用空间。<br>原有便签文件会保留。<br><br> | ローカルデータにアクセスできません。フォルダーのアクセス権とディスクの空き容量を確認してください。<br>既存の付箋ファイルは保持されています。<br><br> | Can't access local data. Check folder permissions and available disk space.<br>Your existing note files have been kept.<br><br> |
| 100 | App.PathUnavailable | 无法确定程序路径 | アプリの保存先を確認できませんでした。 | Couldn't determine the application path. |
| 101 | Hotkeys.UnknownKey | 无法识别按键 | このキーは認識できません。 | This key isn't recognized. |
| 102 | Hotkeys.UnknownKeyPrefix | 无法识别按键： | 認識できないキー： | Unrecognized key: |
| 103 | Colors.MistGray | 雾灰 | ミストグレー | Mist gray |
| 104 | Format.Underline | 下划线 | 下線 | Underline |
| 105 | Tray.ShowNotes | 显示便签 | 付箋を表示 | Show notes |
| 106 | Notes.VisibilitySaveFailedPrefix | 显示状态保存失败： | 付箋の表示状態を保存できませんでした： | Couldn't save note visibility: |
| 107 | Settings.NewNotesOnTop | 新便签默认浮在其他窗口之上 | 新しい付箋を常に手前に表示する | Keep new notes always on top |
| 108 | Hotkeys.CreateAction | 新建便签 | 新しい付箋 | New note |
| 109 | Tray.NewNote | 新建便签     | 新しい付箋 | New note |
| 110 | Categories.NewTitle | 新建分类 | 新しいカテゴリ | New category |
| 111 | Categories.NewMenu | 新建分类… | 新しいカテゴリ… | New category… |
| 112 | Export.SelectFolder | 选择导出文件夹 | エクスポート先のフォルダーを選択 | Choose an export folder |
| 113 | Colors.SelectTooltip | 选择这个颜色 | この色を選択 | Choose this color |
| 114 | Recovery.NoteRestoredPrefix | 已从备份恢复一张便签。原文件已保留： | 付箋を1枚、バックアップから復元しました。元のファイルは保持されています： | Restored one note from a backup. The original file has been kept: |
| 115 | Export.PartialResultPrefix | 已导出 {count} / {notes.Length} 张，其余未导出。<br> | {notes.Length} 枚中 {count} 枚をエクスポートしました。残りの付箋はエクスポートされませんでした。<br> | Exported {count} of {notes.Length} notes. The remaining notes weren't exported.<br> |
| 116 | Import.PartialResultPrefix | 已导入 {imported} 张。以下文件未导入：<br> | {imported} 枚の付箋をインポートしました。次のファイルはインポートできませんでした：<br> | Imported {imported} notes. The following files weren't imported:<br> |
| 117 | Categories.DuplicateName | 已有同名分类 | 同じ名前のカテゴリがすでにあります。 | A category with this name already exists. |
| 118 | Hotkeys.ToggleAllAction | 隐藏 / 显示全部便签 | すべての付箋を表示／非表示 | Show/hide all notes |
| 119 | Note.HideMenu | 隐藏便签 | 付箋を非表示 | Hide note |
| 120 | Note.HideTooltip | 隐藏便签（不会删除） | 付箋を非表示（削除はされません） | Hide note (without deleting) |
| 121 | Notes.HideAll | 隐藏全部便签 | すべての付箋を非表示 | Hide all notes |
| 122 | Colors.CherryPink | 樱粉 | さくらピンク | Cherry blossom pink |
| 123 | Settings.DefaultColorHelp | 用于新建便签的背景色。 | 新しく作成する付箋の背景色です。 | The background color for new notes. |
| 124 | Categories.AssignmentSaveFailed | 有便签的分类未能保存，请检查磁盘空间或文件夹权限。 | 付箋のカテゴリを保存できませんでした。ディスクの空き容量とフォルダーのアクセス権を確認してください。 | Couldn't save a note's category. Check available disk space and folder permissions. |
| 125 | Edit.Paste | 粘贴 | 貼り付け | Paste |
| 126 | Notes.SearchCount | 找到 {notes.Length} 张 | {notes.Length} 枚の付箋が見つかりました | {notes.Length} notes found |
| 127 | Categories.ReservedName | 这个名称已留给系统使用 | この名前はシステムで使用されています。 | This name is reserved by the app. |
| 128 | Notes.EmptyRuntime | 这里还没有便签。<br>点击「新建便签」，留下一个想法。 | まだ付箋がありません。<br>「新しい付箋」で、メモを書いてみましょう。 | No notes here yet.<br>Choose “New note” to jot something down. |
| 129 | Format.MinorMismatchKept | 正文与格式略有差异，已优先保留格式 | 本文にわずかな違いがありますが、書式付きデータを優先しました。 | A small text difference was found. The formatted version has been kept. |
| 130 | App.Name | 纸间 | PaperNotes | PaperNotes |
| 131 | Tray.AppName | 纸间 · PaperNotes | PaperNotes | PaperNotes |
| 132 | Notes.WindowTitle | 纸间 · 便签索引 | PaperNotes · 付箋一覧 | PaperNotes · Notes |
| 133 | Settings.WindowTitle | 纸间 · 设置 | PaperNotes · 設定 | PaperNotes · Settings |
| 134 | App.StartFailedTitle | 纸间启动失败 | PaperNotes を起動できませんでした | Couldn't start PaperNotes |
| 135 | Categories.AccentColor | 主题色 | アクセントカラー | Accent color |
| 136 | Format.IncreaseSize | 字体变大 | 文字を大きく | Increase font size |
| 137 | Format.DecreaseSize | 字体变小 | 文字を小さく | Decrease font size |
| 138 | Window.Maximize | 最大化 | 最大化 | Maximize |
| 139 | Window.Minimize | 最小化 | 最小化 | Minimize |

## 新增文案建议

仅接入实际存在或本轮实现所需的控件；不要为了用上这些文案添加无关功能。

| Key | 中文 | 日本語 | English |
|---|---|---|---|
| Settings.Language | 语言 | 言語 | Language |
| Settings.LanguageHelp | 更改界面显示语言，不会翻译便签内容。 | 表示言語を変更します。付箋の内容は翻訳されません。 | Changes the interface language. Your notes won't be translated. |
| Settings.LanguageJapanese | 日本語 | 日本語 | 日本語 |
| Settings.LanguageChinese | 简体中文 | 简体中文 | 简体中文 |
| Settings.LanguageEnglish | English | English | English |
| Hotkeys.Set | 设置 | 設定 | Set |
| Hotkeys.Change | 修改 | 変更 | Change |
| Hotkeys.CancelRecording | 取消录入 | 入力をキャンセル | Cancel recording |
| Hotkeys.RecordingHelp | 请按下快捷键组合，按 Esc 取消。 | キーの組み合わせを押してください。Esc でキャンセルできます。 | Press a key combination. Press Esc to cancel. |
| Hotkeys.RecordTooltip | 设置此操作的全局快捷键 | この操作のグローバルショートカットを設定 | Set a global shortcut for this action |
| Hotkeys.Conflict | 该快捷键已被占用，请换一个组合。 | このショートカットは使用中です。別の組み合わせを指定してください。 | This shortcut is already in use. Choose a different combination. |
| Edit.PastePlainText | 粘贴为纯文本 | テキストのみ貼り付け | Paste as plain text |
| Paste.PlainTextFallback | 无法保留格式，已按纯文本粘贴。 | 書式を保持できなかったため、テキストのみ貼り付けました。 | Formatting couldn't be kept. Pasted as plain text. |

## 接入时核实

- `Edit.Undo`：确认 NoteWindow.cs 中“撤回”是否实际调用 Undo。
- `Notes.UncategorizedEmpty`：译文澄清为未指定分类时创建的新便签，需与实际分类行为核对。
- `Categories.AssignmentSaveFailed`：确认指的是保存便签分类归属失败，而不是分类定义保存失败。
- `Categories.NameTooLong`：保留现有16字符约束；检查实现计数方式，不在本轮改变Unicode计数逻辑。
- `Format.MinorMismatchKept`：按原提示语义翻译；需核实最新粘贴修复后该提示仍适用，不改变数据优先策略。
- `Recovery.NoteRestoredPrefix`：保留恢复成功与“尝试恢复”的区别。
- `App.Name`：日英暂采用现有技术品牌PaperNotes，不另造音译；不改变安装/文件/存储身份。

## 本次自动校验

- 139/139 条原文均有日英译文。
- 152 个资源 Key 唯一，无空译文。
- 三语占位符集合一致。
- 换行实体已转成真实换行；TXT 筛选器结构保持。
- 英文单数变体附在 JSON 中。
- 未验证：实际 WPF 接入、切换行为、运行时插值、字体与高 DPI 布局、用户数据兼容。
