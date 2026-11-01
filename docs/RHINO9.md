Verified on installed Rhino 9.0.26300.09401: 199 core/protocol/provider tests; 11 native MCP lifetime/inspection/edit checks; real STDIO MCP Grasshopper discovery, seven-node graph, idempotent retry and 5 x 4 x 3 closed-solid bake (volume 60); 19 in-panel API-path checks using a mock planner, including real solving, explicit baking, separate Undo/Redo, rollback and .gh export/reopen. No paid provider inference was run. Sanitized reports: rhino-plugin/docs/acceptance/rhino9-*.json.

# Rhino 9 for Windows — compatibility beta

AI Archi-Tec 0.6.1-beta.1 includes separate Yak distributions for Rhino 8.31+ (`rh8_31-win`) and Rhino 9 (`rh9-win`). Windows x64 only. Rhino 7 and macOS are not supported.

## Install

1. Download the Windows installer ZIP from the [GitHub release](https://github.com/MrHs6/ai-architec-rhino/releases/tag/rhino-v0.6.1-beta.1).
2. Extract all files. Save your documents and close every Rhino window.
3. For Rhino 9, double-click **install-rhino9.cmd**. For Rhino 8, use **install.cmd**. Installing into both versions requires running each installer once.
4. Restart the selected Rhino and run **archtec**.

Advanced PowerShell: `./install.ps1 -RhinoMajor 9`. A custom installation can use `-RhinoSystemDir 'D:\Applications\Rhino 9\System'`. The installer verifies the actual executable version. Registrations and package directories are separate for each Rhino major version.

## MCP and API

MCP keeps the conversation in your external AI client. Start MCP in the Rhino instance you want to control. Keep that Rhino open; hiding the panel does not stop the listener. Explicit Stop disables it. Two simultaneous Rhino instances must not use the same port: stop the other listener or assign distinct ports and client configurations. The bridge controls one endpoint, not both Rhino instances at once.

API mode uses the same provider profiles and validated native action catalog. Enter your own provider key and choose an available model; paid requests require provider credit. API credentials and local discovery remain per Windows user. The separate MCP bridge still requires .NET 8; do not force Rhino 9 to use Rhino 8's runtime or .NET Framework.

Grasshopper is the version bundled with the running Rhino. Open Grasshopper before working with definitions. Graph editing, solving and explicit baking use the existing tools. Review geometry before saving production work.

## Verification and release scope

This is a **Rhino 9 compatibility beta**. The shared managed binary targets .NET 8 and the pinned Rhino 8.31 SDK to preserve Rhino 8 support. Rhino 9's API compatibility scanner is used in addition to automated MCP/provider checks. Scanner success is not an end-to-end Rhino 9 UI, solver or paid-provider test. See release notes for the checks actually run; earlier Rhino 8 native acceptance records do not certify Rhino 9.

## نصب فارسی

فایل ZIP نسخهٔ جدید را از GitHub بگیرید و کامل استخراج کنید. پس از ذخیرهٔ فایل‌ها و بستن راینو، برای نسخهٔ ۹ فایل `install-rhino9.cmd` را اجرا کنید. سپس Rhino 9 را باز کنید و دستور `archtec` را بزنید. برای نصب روی نسخهٔ ۸، فایل `install.cmd` جداگانه اجرا می‌شود. مسیر نصب و ثبت دو نسخه جداست. برای اتصال MCP هم‌زمان در دو راینو، پورت‌های متفاوت لازم است. این انتشار، بتای سازگاری Rhino 9 است و به‌معنای تأیید تمام سناریوهای اجرایی روی آن نیست.

