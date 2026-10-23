# FormatWheel for Windows — 0.3.0 preview

**[Download Windows EXE / 下載 / 下载](https://github.com/rickuuyu-create/FormatWheel/releases/latest)**

English: Download `FormatWheel-Windows-x64.exe` and run it. No separate .NET installation is needed. On first launch, choose English / 繁體中文 / 简体中文, optionally install FFmpeg and LibreOffice, then follow the eight-step tour. Right-click the tray icon (or home window) to install tools later; the tray menu also offers language selection and tour replay/reset. The EXE is unsigned. This is an independent community Windows preview, not an official upstream release.

简体中文：下载 `FormatWheel-Windows-x64.exe` 后直接打开，无需另装 .NET。首次启动可切换语言、选择安装 FFmpeg / LibreOffice，并观看新手教学。暂时跳过后，也可右键点击系统托盘图标补装或重看教学。

基於 [Elise616/FormatWheel](https://github.com/Elise616/FormatWheel) 的獨立 Windows 移植。原始基準：`1eb19a1903ffd26ef31a183b7a8f5ea14ae7e4cc`。原版 MIT 版權聲明完整保留。此版本不是原作者官方 Windows 發行，也不宣稱 100% 相同。

## 0.3.0 屬性比較與通緝星級 / File comparisons and wanted stars

- 選項旁的浮動面板顯示原檔大小、格式與可讀取的尺寸／影音資料，以及目前操作的影響。移開即隱藏；回到中心可查看檔案摘要。
- 五段屬性條比較來源與結果：白色保留、藍色提升、紅色犧牲。畫質／音質是「本次轉換保留程度」，不是原檔品質測量；轉成無損格式不會恢復先前損失的細節。壓縮效率及相容性是相對預估，實際大小變化在完成後顯示。
- 透明屬性依來源透明通道與實際轉換路徑評估。PDF 轉文字／Word 明確提示排版不保留；一般 EXE／複製路徑等不顯示虛構的品質條。
- 多選圖片時 PDF 格子顯示「合併」，面板列出圖片數和頁數；每張圖片一頁，合成一份 PDF。
- 通緝星級代表常見隱私中繼資料：GPS 兩星，裝置、拍攝時間、作者／序號各一星；PDF／DOCX 按作者等欄位計算。重複標籤只計一次。未支援、失敗、部分檢查與批次上限會明確標示，未知不當成零星。最多檢查 24 個檔案，批次顯示已檢查範圍與最高星級。
- 指向去隱私時星星緩慢閃動；轉換後重新檢查，只有完整檢查確認清除的輸出才播放消退效果。圖片去隱私仍會重新編碼；星級只涵蓋常見中繼資料，不分析畫面或正文中的個資。
- 繁體／簡體／英文均支援新面板。遵循系統減少動畫設定。

English: Floating cards now compare source properties and expected conversion effects. Five-part bars show retained (white), gained (blue), and sacrificed (red) traits. Estimates are distinct from measured file metadata and the actual size change shown after conversion. Privacy stars reflect detected metadata, with explicit incomplete/unsupported states and an output rescan after stripping. Multiple images merge into one PDF, one image per page.

简体中文：浮动面板加入文件属性与转换前后比较；白色保留、蓝色提升、红色牺牲。通缉星级按实际中继资料计算，未检查不当作零星，去隐私后重新检查。多图 PDF 显示合并数量与页数，转换完成后显示实际大小变化。

## 0.2.2 教學排版 / Roomier quick tour

- 教學視窗加闊，左側示範區由 520 × 580 增至 660 × 660，保留輪盤大小並增加藍框四周留白。
- 範例檔案、輪盤及系統匣示意拉開距離；各步驟聚光框跟隨新位置，避免壓住圖示和文字。
- 右側說明增加段落間距，小視窗可捲動文字，導覽按鈕保持可見。繁體／簡體／英文沿用相同寬鬆排版。
- 此版發布為 GitHub Latest，下載連結可直接取得最新版。更新時先從系統匣結束舊版。

English: A wider tour window, a larger practice canvas, more space inside the blue spotlight and between the sample file, wheel and tray. Text can scroll on smaller windows while navigation stays visible.

简体中文：加宽新手教学窗口及演示区域，增加蓝框内留白，拉开示例文件、轮盘及托盘示意的距离。小窗口可滚动说明，导航按钮保持可见。
## 0.2.1 修正 / Feedback fixes

- 開啟輪盤、切換選項及翻頁都有各自的音效。音效在背景依序播放，快速移動不會切斷開啟音效；同一選項不會因滑鼠微動而連續發聲。系統匣的音效開關仍有效。
- 選項簡介改為浮在該選項旁邊，移入立即顯示，移出輪盤、回到中心或進入空白格便隱藏；切換頁面時即時更新。未安裝工具的原因也會列出。
- 簡介保留檔名與隱私摘要，取消固定右上角資訊框；依螢幕邊界調整位置。點擊或放開檔案時需位於可見的輪盤格子內。

English: Opening, hover and page-change cues no longer interrupt one another. Contextual descriptions appear beside the current option, update immediately and disappear on leaving. The sound preference is respected.

简体中文：修复打开轮盘及切换选项时的音效。简介浮动显示在当前选项旁边，移入即显示、移开即隐藏，翻页时同步更新。
## 使用

Windows 10 2004（19041）或更新版本，x64。下載單檔 `FormatWheel-Windows-x64.exe` 即可開啟，不必另裝 .NET。第一次執行會在使用者資料夾解開內建模型及資源；.NET 原生程式庫快取在 Windows 暫存資料夾。另提供完整 ZIP，可解壓後開啟 `FormatWheel.exe`；ZIP 版本須保留隨附檔案。

首次開啟會詢問是否安裝 FFmpeg／LibreOffice，可分別勾選或暫時略過。程式會下載、核對固定 SHA-256、安裝到 `%LOCALAPPDATA%\FormatWheel\tools`，再實際啟動工具檢查。安裝為使用者私有副本，不變更預設文件關聯、不替換已有的 LibreOffice、不需要系統管理員安裝。企業政策或網路限制可能造成失敗，畫面會顯示原因並保留重試入口；解壓／驗證階段須等候完成，下載階段可取消。

接著會顯示八步新手教學，支援上一步／下一步／跳過與左右方向鍵／Esc。右鍵系統匣圖示可重播或設定下次啟動重播。繁體中文、簡體中文、英文可在主畫面及系統匣切換，設定會記住。

1. 在桌面或檔案總管拖住檔案，按一下 Shift。
2. 朝輪盤上的格式方向移動，放開滑鼠轉換。中心放開取消。
3. Shift、Tab 或滾輪切換第二頁工具；Esc 關閉。
4. 也可以雙擊右下角系統匣圖示，選擇檔案後點選格子。

輸出放在來源檔案旁，撞名會編號，原檔不覆寫。無寫入權限時會報錯。混合類型改用通用輪盤；PDF 合併及圖片轉 PDF 依傳入順序處理。處理失敗若已有部分輸出，錯誤會列出檔案。資料夾 ZIP 不跟隨 junction/symlink。

## 功能與依賴

- 內建圖片 PNG/JPG/HEIC/WebP/AVIF/GIF/TIFF、壓縮、縮放、旋轉、灰階、去隱私、漸層背景。
- 內建離線 U²-Net 小型模型去背，無需 Python。辨識品質與 Apple Vision 不同。
- OCR 使用 Windows 的本機 OCR 語言。繁體、簡體或英文辨識取決於 Windows 已安裝的語言套件。
- PDF 轉圖、長圖、拆分、合併、旋轉、文字提取與去除文件屬性已內建。
- 影音功能使用 `ffmpeg.exe`；影片隱私檢查另用 `ffprobe.exe`。右鍵 → 安裝轉換工具會下載 Gyan.dev 的 FFmpeg 9.0.2，保留上游完整授權與文件。也支援既有 PATH 或 `tools/ffmpeg/bin/`。
- 完整 DOC/DOCX/ODT/RTF 文件轉換需要 [LibreOffice](https://www.libreoffice.org/download/)。內建安裝器取得官方 26.8.1 MSI，使用 Windows Installer `/a` 建立私有執行副本並保留附帶的 VC runtime、授權及文件。也偵測既有系統安裝或 PATH 中的 `soffice.com` / `soffice.exe`。純文字與 DOCX 提取、DOCX 去隱私不用安裝 Office。PDF 轉文件仍是文字提取，見下方限制。
- ZIP、複製路徑／檔名、SHA-256 已內建。

轉換不發送檔案、不收集遙測。只有按下安裝才會連線下載工具；去背模型已內建。設定與操作記錄在 `%LOCALAPPDATA%\FormatWheel`。登入啟動預設關閉，僅使用者開啟設定後寫入 HKCU Run。右鍵「開源授權與來源」可查看內嵌的授權文件。刪除下載的 EXE 及上述私有資料夾可移除程式與工具；如果曾開啟登入啟動，先將它關閉。

## 與 macOS 的已知差異

- 全域拖曳偵測在滑鼠移動中按 Shift 時建立 Windows drop target，收到實際檔案資料後才決定輪盤類型；非檔案拖曳可能短暫顯示等待畫面。不同權限的程式、遠端桌面、虛擬檔案、不同 DPI 的多螢幕仍需更多實機驗證。
- 中心取消不轉換、不搬動原檔。全域拖曳的端到端相容性尚未以實體滑鼠在所有情境驗證；選檔點擊模式提供替代入口。
- 字型換成 Windows 可用字型，格式圖示重新繪製，武器輪廓由原始座標轉換（圓角與細線部分簡化）。音效為重新合成，非原版逐樣本相同。尚未還原原版屬性比較條、完整進場動畫與解除通緝動畫。
- PDF 轉 Word/RTF/HTML/Markdown 是文字提取，不重建原頁面布局。Markdown 輸出不重建標題層級。PDF 灰階／壓縮會重建為圖片 PDF，失去可選取文字、超連結、表單；選項會事先提示。
- 圖片去隱私會重新編碼。PDF 去隱私移除文件資訊與文件／頁面 XMP；DOCX 去隱私移除作者等屬性。這些功能不刪除正文、註解、附件、修訂內容或像素中的個資，不代表匿名化。
- 影片去隱私清除容器／串流 metadata 與章節；不保證清除所有編碼器特有標記。
- 長圖每段最多 15,000 像素；GIF 輸出為單張圖，圖片動畫不保證保留。去背與轉換結果可能與原版不同。
- 已驗證 LibreOffice PDF、DOCX、RTF、ODT、DOC 輸出及文字讀回。ARM64／x86 不支援，本版沒有程式碼簽章；Windows 可能顯示未知發行者。此發行為 x64 預覽版。
- 本次測試環境只有一個啟用中的螢幕。四種 DPI 的邊界計算通過自動測試，實際雙螢幕與混合 DPI 的跨螢幕拖曳仍未驗證。Computer Use API 不提供「維持拖曳同時按 Shift」的組合操作，全域手勢的完整端到端驗證仍待實體操作；已測試觸發狀態與輪盤點選轉換。

## 建置與測試

安裝 .NET 10 SDK，於此目錄執行：

```powershell
dotnet build -c Release
./build.ps1
# 輸出 publish/FormatWheel.exe、release/FormatWheel-Windows-x64.exe 及 ZIP
./publish/FormatWheel.exe --self-test ./test-results
# 偵測到 FFmpeg / LibreOffice 時會額外測試影音／文件功能
./publish/FormatWheel.exe --run img.to.png C:\path\photo.heic
./publish/FormatWheel.exe --ids actions.json
./publish/FormatWheel.exe --snapshot preview.png
```

CLI 轉換結果寫入程式目錄的 `last-result.json`，失敗寫入 `last-error.txt` 並返回非零結束碼。自我測試輸出 `report.json`，測試素材只在指定目錄建立。

開發測試可設定 `FORMATWHEEL_DATA` 為絕對路徑，以隔離首次啟動狀態與工具。`--install-test ffmpeg` / `--install-test libreoffice` 執行同一套下載安裝流程；可再傳入本機套件路徑，但仍必須通過固定 SHA-256 校驗。這些測試命令不會自動發布任何內容。

## 授權與來源

原作 Copyright (c) 2026 Elise616，MIT。Windows 新增程式同採 MIT。`../LICENSE` 及 `licenses/` 必須隨發行保留。第三方依賴列於 `THIRD-PARTY-NOTICES.md`。沒有移植 Apple 字型、SF Symbols 或原宣傳片配樂。Windows 版的程式、文件、圖示和模型在本資料夾；原 Swift 程式保留在上層 `Sources/`。
