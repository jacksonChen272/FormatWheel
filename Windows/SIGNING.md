# Windows code signing / Windows 程式碼簽章

This fork's ordinary GitHub Actions builds are **unsigned test artifacts**. A green CI check only proves the build and tests succeeded; it does not establish that Windows Smart App Control will trust the binary.

## Supported optional release path: Authenticode PFX

The development branch includes `Windows/sign-release.ps1`. When explicitly enabled, GitHub Actions signs the standalone EXE and any unsigned EXE/DLL binaries in the portable build, verifies the signatures, rebuilds the portable ZIP, and regenerates SHA-256 checksums. It publishes a separate `FormatWheel-Windows-x64-SIGNED` artifact **only after successful signing**. Pull requests never receive signing credentials.

Requirements:
1. Obtain a **trusted Authenticode code-signing certificate** that supports signing through a PFX file with its private key. A self-signed certificate does **not** satisfy Smart App Control. Many newly issued CA certificates require hardware-backed keys and cannot be exported to PFX; in that case, use the CA's supported HSM/cloud signing integration instead. Do not try to export or bypass a hardware-backed key.
2. In this repository's **Settings → Secrets and variables → Actions → Repository secrets**, create:
   - `FORMATWHEEL_SIGNING_PFX_BASE64`: base64 of the complete PFX file, without newlines.
   - `FORMATWHEEL_SIGNING_PFX_PASSWORD`: the PFX password.
3. Only after the secrets are set, create the repository **variable** `FORMATWHEEL_ENABLE_SIGNING` with the value `true`.
4. Push a trusted change to `codex/windows-stability-20261027`. GitHub Actions builds and tests first, signs only on a push to that branch, and uploads the signed artifact. Disable the variable again to return to unsigned test builds.

**Security:** Never commit a certificate, private key, password, or base64-encoded PFX. Restrict repository write access and review changes before allowing signed builds. For production releases, prefer a hardware-backed signing service with approval gates rather than a long-lived exportable key in repository secrets.

**Signing is not automatically enabled.** If the variable is absent, CI publishes `FormatWheel-Windows-x64-UNSIGNED`. If signing is enabled but secrets are absent or signing fails, the workflow fails and does not publish a signed artifact.

## Alternatives

- **Microsoft Store MSIX**: Microsoft signs MSIX packages distributed through the Store after certification, without requiring you to buy a separate signing certificate. This repository does not yet include MSIX packaging or Store submission. The existing EXE is not magically Store-signed.
- **Azure Artifact Signing**: Microsoft's hosted signing service; eligibility depends on geography and account type. As of the linked Microsoft documentation, individual accounts are limited to the US and Canada, so it may not be an option for a Taiwan-based individual.
- **Hardware-backed CA certificate**: use your certificate provider's supported signing service; the PFX script is not suitable if the private key cannot be exported.

## Validation and limitations

- Check the GitHub Actions log for the signing step and inspect the signed EXE's **Properties → Digital Signatures** tab.
- Windows PowerShell: `Get-AuthenticodeSignature .\FormatWheel-Windows-x64.exe | Format-List Status,SignerCertificate`.
- Verify the downloaded release's SHA-256 against `SHA256SUMS.txt`.
- A trusted signature helps Smart App Control identify the publisher, but it does not guarantee every Windows security check will permit execution. SmartScreen reputation can still require time to build.
- Dependencies downloaded or installed **after first launch**, such as FFmpeg and LibreOffice, are separate binaries and are not covered by the FormatWheel EXE signature. Investigate unexpected elevation prompts (such as `FTPServer.exe`) before approving them.

References:
- https://learn.microsoft.com/en-us/windows/apps/develop/smart-app-control/code-signing-for-smart-app-control
- https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options
- https://learn.microsoft.com/en-us/azure/artifact-signing/how-to-signing-integrations
