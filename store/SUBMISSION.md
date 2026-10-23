# Microsoft Store submission setup

## Quick path (no Windows SDK needed)

1. Create a free individual developer account in [Partner Center](https://partner.microsoft.com/dashboard) (Microsoft account + ID check) and reserve the name **Syra**.
2. Copy the three values from **Product management → Product identity** (`Package/Identity/Name`, `Package/Identity/Publisher`, `Package/Properties/PublisherDisplayName`) into `store/store-identity.json` and push.
3. GitHub Actions (**Phone companion** workflow → **Build Microsoft Store package**) builds `Syra-1.0.0.0-x64.msix` as the `Syra-Store-MSIX` artifact.
4. In Partner Center create a submission: upload that `.msix` under **Packages**, fill **Store listings** from [LISTING-DRAFT.md](LISTING-DRAFT.md), complete pricing, age ratings and properties, then submit. Microsoft signs the package.
5. For every update, raise `Version` in `store-identity.json` (e.g. `1.0.1.0`), push, and upload the new package in a new submission.

While `IdentityName` is empty, CI builds only the unsigned development preview to prove the package is valid. Never submit the preview.

The detailed manual steps below remain for building on your own PC.

## What is prepared

Source, bundled dependency licenses, standalone setup recipe, privacy notice, original icon, manifest template and a MakeAppx build script. Microsoft MakeAppx has successfully validated and packed the unsigned development preview. This is a packaging check, **not** certification or a successful packaged install test. The preview identity cannot be used for your Store listing.

## 1. Create the account and reserve the name

Sign in to [Microsoft Partner Center](https://partner.microsoft.com/dashboard), enroll as a Windows app developer, and complete the current identity/business verification requirements shown there. Reserve your available product name (Syra, or a variant if it is taken).

Open the product's **Product management > Product identity** and copy the exact Package/Identity/Name, Package/Identity/Publisher and Package/Properties/PublisherDisplayName. Enter those values locally when building. They are not passwords. Do not share signing keys, passwords or access tokens.

## 2. Build the final identity package

Use Windows 10/11 x64 with .NET Framework 4.8 and Windows SDK packaging tools. Install the Windows SDK, or download the pinned official [Microsoft.Windows.SDK.BuildTools 10.0.28000.2705 NuGet package](https://www.nuget.org/packages/Microsoft.Windows.SDK.BuildTools/10.0.28000.2705), extract it as ZIP, and locate `bin\10.0.28000.0\x64\makeappx.exe`. Keep its adjacent SDK files together.

From a repository checkout, open Windows PowerShell:

```powershell
.\scripts\Build-Release.ps1
.\store\Build-Msix.ps1 -IdentityName 'EXACT NAME FROM PARTNER CENTER' -Publisher 'CN=EXACT PUBLISHER FROM PARTNER CENTER' -PublisherDisplayName 'EXACT DISPLAY NAME' -Version '1.0.0.0' -MakeAppxPath 'C:\SDK-tools\bin\10.0.28000.0\x64\makeappx.exe'
```

For packaging experiments only, use `-DevelopmentPreview` instead of the three identity parameters. Both outputs are unsigned. Never submit the development preview. Store submission MSIX files do not require a purchased signing certificate; Microsoft signs accepted packages. Local sideload testing requires a matching trusted test signature, ideally on a disposable VM. Do not disable signature checking or install an unknown root certificate on your everyday PC.

Startup is declared as a disabled-by-default packaged startup task. In the packaged app, the legacy registry startup switch is disabled; use Windows Settings > Apps > Startup. Test this with the actual signed package.

## 3. Complete the release checks

- Sign and install a test package with the final identity on a clean Windows 10/11 x64 VM. Test first launch, launch from Start, update, uninstall, startup, data paths, and dependencies (.NET / WebView2). Run Windows App Certification Kit; resolve all failures. Packaging success alone is insufficient.
- Test microphone permissions, typed controls without a microphone, English recognizer availability, offline voices and optional online voice failures. Do not promise an actor's voice.
- Test apps that are absent, whitelist path validation, the chosen monitor, DPI scaling, window dragging, Explorer restart, taskbar reveal/restore and app crash recovery. Desktop integration/taskbar manipulation is a known release risk and needs target-PC validation.
- Test missing sensors, localhost feed timeout, fan labeling and thermal-alert cooldown. Do not advertise universal hardware monitoring or replace hardware thermal protection.
- Verify ChatGPT dynamic app registration / account eligibility and provider authorization for distributing this integration. Do not market universal ChatGPT plan access until verified. Document any reviewer account requirements privately in certification notes; never include your own tokens. Verify Azure Speech and Maps integration requirements as well.
- Choose original-code license/distribution terms. Review the included third-party notices and final artwork rights. Replace remaining historical internal branding if required for the final product listing.
- Review the privacy notice against final behavior and verify **SETUP > Privacy** in the signed package. Publish the notice and provide a support contact. Decide pricing, markets, age ratings and accurate data disclosures in Partner Center.
- Capture fresh screenshots from the **signed package** on a clean demo profile. Include HUD, colors/setup and conversation views. Use synthetic agenda/chat/sensor data, label examples where needed, and exclude private locations or real account information. Screenshot assets are still pending; historical development screenshots are intentionally omitted.

## 4. Submit

Create a submission, upload the final-identity MSIX, fill out [LISTING-DRAFT.md](LISTING-DRAFT.md), the age-rating questionnaire, privacy/support links and capability declarations. Explain `runFullTrust`: WPF desktop UI, user-whitelisted application launches, local performance counters, speech recognition, monitor/display integration and companion taskbar recovery process. No arbitrary remote commands or privilege elevation are required by the app. Explain microphone usage for optional voice commands/dictation.

Use staged/private distribution for reviewer validation if available. Resolve certification feedback before public availability. This repository and preview do not create a Partner Center account or publish the app automatically.

## Official references

- [Publish overview and package signing](https://learn.microsoft.com/en-us/windows/apps/publish/get-started)
- [Package an existing desktop app](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/dotnet/package-app)
- [MakeAppx](https://learn.microsoft.com/en-us/windows/msix/package/create-app-package-with-makeappx-tool)
- [StartupTask](https://learn.microsoft.com/en-us/uwp/api/windows.applicationmodel.startuptask)
- [Store policies](https://learn.microsoft.com/en-us/windows/apps/publish/store-policies)
