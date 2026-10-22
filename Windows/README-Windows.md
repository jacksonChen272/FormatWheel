# FormatWheel for Windows — 0.1.0 preview

基於 [Elise616/FormatWheel](https://github.com/Elise616/FormatWheel) 的獨立 Windows 移植。原始基準：`1eb19a1903ffd26ef31a183b7a8f5ea14ae7e4cc`。原版 MIT 版權聲明完整保留。此版本不是原作者官方 Windows 發行，也不宣稱 100% 相同。

## 使用

Windows 10 2004（19041）或更新版本，x64。解壓完整資料夾，開啟 `FormatWheel.exe`。可攜式發行包含 .NET runtime，不必另裝 .NET 或使用管理員權限。請保留同資料夾內的 DLL、模型與授權檔。

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
- 影音功能使用 `ffmpeg.exe`；影片隱私檢查另用 `ffprobe.exe`。支援 PATH 或 `tools/ffmpeg/bin/`。可執行隨附 `setup-media.ps1`，只會下載並解壓至本程式資料夾，不修改系統 PATH。
- 完整 DOC/DOCX/ODT/RTF/PDF 文件互轉需要 [LibreOffice](https://www.libreoffice.org/download/download-libreoffice/)。偵測預設安裝目錄或 PATH 裡的 `soffice.exe`。純文字與 DOCX 提取、DOCX 去隱私不用安裝 Office。
- ZIP、複製路徑／檔名、SHA-256 已內建。

程式本身不發送檔案、不收集遙測，也不自動下載工具或模型。`setup-media.ps1` 和原始碼建置需要網路。設定與操作記錄在 `%LOCALAPPDATA%\FormatWheel`。登入啟動預設關閉，僅使用者開啟設定後寫入 HKCU Run。

## 與 macOS 的已知差異

- 全域拖曳偵測在滑鼠移動中按 Shift 時建立 Windows drop target，收到實際檔案資料後才決定輪盤類型；非檔案拖曳可能短暫顯示等待畫面。不同權限的程式、遠端桌面、虛擬檔案、不同 DPI 的多螢幕仍需更多實機驗證。
- 中心取消不轉換、不搬動原檔。全域拖曳的端到端相容性尚未以實體滑鼠在所有情境驗證；選檔點擊模式提供替代入口。
- 字型換成 Windows 可用字型，格式圖示重新繪製，武器輪廓由原始座標轉換（圓角與細線部分簡化）。音效為重新合成，非原版逐樣本相同。尚未還原原版屬性比較條、完整進場動畫與解除通緝動畫。
- PDF 轉 Word/RTF/HTML/Markdown 是文字提取，不重建原頁面布局。Markdown 輸出不重建標題層級。PDF 灰階／壓縮會重建為圖片 PDF，失去可選取文字、超連結、表單；選項會事先提示。
- 圖片去隱私會重新編碼。PDF 去隱私移除文件資訊與文件／頁面 XMP；DOCX 去隱私移除作者等屬性。這些功能不刪除正文、註解、附件、修訂內容或像素中的個資，不代表匿名化。
- 影片去隱私清除容器／串流 metadata 與章節；不保證清除所有編碼器特有標記。
- 長圖每段最多 15,000 像素；GIF 輸出為單張圖，圖片動畫不保證保留。去背與轉換結果可能與原版不同。
- 尚未實測 LibreOffice 整合、ARM64／x86、程式簽章及正式安裝器。本發行為 x64 預覽版。

## 建置與測試

安裝 .NET 10 SDK，於此目錄執行：

```powershell
dotnet build -c Release
./build.ps1
# 輸出 publish/FormatWheel.exe
./publish/FormatWheel.exe --self-test ./test-results
# 有 FFmpeg 在 PATH 時會額外測試影音功能
./publish/FormatWheel.exe --run img.to.png C:\path\photo.heic
./publish/FormatWheel.exe --ids actions.json
./publish/FormatWheel.exe --snapshot preview.png
```

CLI 轉換結果寫入程式目錄的 `last-result.json`，失敗寫入 `last-error.txt` 並返回非零結束碼。自我測試輸出 `report.json`，測試素材只在指定目錄建立。

## 授權與來源

原作 Copyright (c) 2026 Elise616，MIT。Windows 新增程式同採 MIT。`../LICENSE` 及 `licenses/` 必須隨發行保留。第三方依賴列於 `THIRD-PARTY-NOTICES.md`。沒有移植 Apple 字型、SF Symbols 或原宣傳片配樂。Windows 版的程式、文件、圖示和模型在本資料夾；原 Swift 程式保留在上層 `Sources/`。
