# FormatWheel Windows 程式碼簽章

本專案的 GitHub Actions 一般 build-and-test 會持續產生**未簽章**的測試 EXE；這些檔案可能遭 Windows 11 Smart App Control 封鎖。單純修改 C# 程式碼或使用自簽憑證，無法讓大眾 Windows 電腦自動信任。**不要為了測試而關閉系統保護、安裝陌生根憑證，或允許不明的 FTPServer.exe 取得系統管理員權限。**

## 可行發行路線

1. **開源專案申請 SignPath Foundation**：可能免費，但有資格審核；本專案是他人的 Fork，必須向 SignPath 確認目前維護者與授權條件是否符合。網站：https://signpath.org/。
2. **Microsoft Store MSIX**：由商店審查並簽署 MSIX，免購買簽章憑證，但需要另行封裝與送審，不是把目前 EXE 直接上傳就能自動獲得簽章。
3. **由可信任 CA 核發之程式碼簽章**：適合自行發行 EXE，但通常有費用，可能需要硬體金鑰/HSM，不應將私人金鑰直接放進 Repository。

Azure Artifact Signing 對個人帳號有國家/地區資格限制，台灣個人開發者不應預設可直接申請。

## SignPath 啟用步驟（取得核准後）

1. SignPath 專案的 Repository 綁定 https://github.com/jacksonChen272/FormatWheel，並設置 GitHub Trusted Build System 與來源驗證政策。
2. 在 SignPath 建立對應 Artifact Configuration，將 `.signpath/artifact-configurations/standalone-exe.xml` 的 XML 貼入；GitHub 送出的 artifact ZIP 只有 `FormatWheel-Windows-x64.exe`。
3. 設定僅允許經審核、受信任分支的正式簽章政策，避免不受信任的 PR 或任意分支可拿到正式簽章。
4. 於 GitHub **Settings → Secrets and variables → Actions** 設定：
   - Secret `SIGNPATH_API_TOKEN`（只放 Secret；不要貼進聊天或 commit）
   - Variables `SIGNPATH_ORGANIZATION_ID`、`SIGNPATH_PROJECT_SLUG`、`SIGNPATH_POLICY_SLUG`
   - Variable `SIGNPATH_ENABLED` = `true`（前面的設定與審查完成後才開啟）
5. 到 Actions → Windows build and tests，手動 **Run workflow**，選擇 `codex/windows-stability-20261027`。成功通過一般 build-and-test 後，才會進入需要授權的 SignPath 簽章 job。
6. 只有當 SignPath 簽章完成、回傳檔案，並通過 Windows `Get-AuthenticodeSignature` 的 `Valid` 驗證時，才會產生 **FormatWheel-Windows-x64-SIGNED** artifact。

此簽章工作只在手動觸發且 GitHub 變數 `SIGNPATH_ENABLED=true` 時執行；`push` 與 `pull_request` 繼續執行普通 CI，不會消耗正式簽章額度。簽章來源 `FormatWheel-signing-input-unsigned` **並不是簽章版**，不可混淆。一般 CI 產物名稱為 `FormatWheel-Windows-x64-UNSIGNED`，仍未簽章。另一套 `FORMATWHEEL_ENABLE_SIGNING` / PFX 方案也存在於專案中，但需要真正受信任且可匯出的憑證；目前以 SignPath 作為建議的雲端簽章方式。不要同時啟用兩套發行簽章設定。

## 驗證與注意事項

在 Windows 上對下載的簽章版執行：

```powershell
Get-AuthenticodeSignature .\FormatWheel-Windows-x64.exe | Format-List Status,SignerCertificate
Get-FileHash .\FormatWheel-Windows-x64.exe -Algorithm SHA256
```

須顯示 `Status: Valid`，並與該次 CI 的 `SHA256SUMS.txt` 一致。簽章只能證明發行者/檔案完整性，**不是**惡意程式保證；此外 Smart App Control 的雲端判斷、非主程式 DLL、其他不明的 FTPServer.exe 仍需要分別確認。若仍封鎖，保留安全性設定並蒐集攔截資訊，不要繞過。

官方參考：
- https://learn.microsoft.com/en-us/windows/apps/develop/smart-app-control/overview
- https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options
- https://docs.signpath.io/trusted-build-systems/github
