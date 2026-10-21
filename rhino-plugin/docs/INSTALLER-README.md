# AI Archi-Tec for Rhino 8 — 0.3.0-beta.2

Project and complete guides / پروژه و راهنمای کامل:

https://github.com/MrHs6/ai-architec-rhino

## نصب سریع

1. همهٔ فایل‌های ZIP را استخراج کنید؛ `install.cmd` و `.yak` باید کنار هم باشند.
2. سندتان را ذخیره کنید، تمام پنجره‌های راینو را ببندید و `install.cmd` را اجرا کنید. این نصب‌کننده Yak همراه راینو را اجرا و ثبت دستی قدیمی همین پلاگین را با پشتیبان اصلاح می‌کند.
3. پس از موفقیت، راینو را اجرا کنید.
4. فرمان `archtec` را تایپ و Enter بزنید.
5. در Settings سرویس، کلید API و مدل شخصی را انتخاب کنید، یا MCP محلی را روشن کنید.

Generate فقط پیش‌نمایش می‌سازد. پس از بررسی Apply to Rhino بزنید؛ Ctrl+Z در راینو عملیات را برمی‌گرداند.

## Quick start

Extract all files, save your work, close every Rhino window, then run `install.cmd` and reopen Rhino. Type `archtec` and press Enter. In Settings, connect your own compatible API service and select a model, or start MCP for a compatible desktop client.

Requires Windows x64 and licensed Rhino 8.31+ within Rhino 8 with .NET 8 runtime. No Visual Studio, Node or SDK is required for this installer. API usage is billed by your provider; no shared API key is included.

For a custom Rhino path, set `ARCHITEC_RHINO_SYSTEM` to its System directory in CMD. `install.cmd /check` checks only paths. PowerShell users whose device policy permits scripts can run `install.ps1 -CheckOnly` for package/checksum verification, then `install.ps1` to install. Neither installer changes execution policy. The CMD installer runs its native `repair-registration.cmd` companion after Yak to repair an older manually registered prototype; PowerShell script execution is not required for this path. `install.ps1` uses the equivalent `repair-registration.ps1`. If registration repair fails, the installer reports incomplete installation. Direct Yak installation does not repair an old manual registration.

Registration repair backs up only this plugin's HKCU registry key to `%LOCALAPPDATA%\AIArchiTec\InstallationBackups`. It preserves other plugins, license settings, API profiles and Rhino documents. About displays the actual loaded version and file path.

Native preferences: Tools > Options > AI Archi-Tec. Plugin loading: Tools > Options > Plug-ins.

This is a concept-modeling beta. Live provider/vision trials and clean startup on another licensed workstation remain outstanding. Arbitrary image reconstruction, full BIM and automatic cloud-to-localhost connectivity are not provided.

Installation guide: https://github.com/MrHs6/ai-architec-rhino/blob/main/docs/INSTALLATION.fa.md

API/MCP guide: https://github.com/MrHs6/ai-architec-rhino/blob/main/docs/CONNECTIONS.fa.md

English: https://github.com/MrHs6/ai-architec-rhino/blob/main/docs/README.en.md

Issues: https://github.com/MrHs6/ai-architec-rhino/issues

Source/license terms: https://github.com/MrHs6/ai-architec-rhino/blob/main/LICENSE.md

Copyright © 2026 Hasan Sharif Askari.
