# AI Archi-Tec for Rhino 8 — 0.3.0-beta.1

Project and complete guides / پروژه و راهنمای کامل:

https://github.com/MrHs6/ai-architec-rhino

## نصب سریع

1. همهٔ فایل‌های ZIP را استخراج کنید؛ `install.cmd` و `.yak` باید کنار هم باشند.
2. `install.cmd` را اجرا کنید. این نصب‌کننده Yak همراه راینو را اجرا می‌کند.
3. پس از موفقیت، سند را ذخیره و راینو را مجدداً اجرا کنید.
4. فرمان `archtec` را تایپ و Enter بزنید.
5. در Settings سرویس، کلید API و مدل شخصی را انتخاب کنید، یا MCP محلی را روشن کنید.

Generate فقط پیش‌نمایش می‌سازد. پس از بررسی Apply to Rhino بزنید؛ Ctrl+Z در راینو عملیات را برمی‌گرداند.

## Quick start

Extract all files, run `install.cmd`, save your work and restart Rhino. Type `archtec` and press Enter. In Settings, connect your own compatible API service and select a model, or start MCP for a compatible desktop client.

Requires Windows x64 and licensed Rhino 8.31+ within Rhino 8 with .NET 8 runtime. No Visual Studio, Node or SDK is required for this installer. API usage is billed by your provider; no shared API key is included.

For a custom Rhino path, set `ARCHITEC_RHINO_SYSTEM` to its System directory in CMD. `install.cmd /check` checks only paths. PowerShell users whose device policy permits scripts can run `install.ps1 -CheckOnly` for package/checksum verification, then `install.ps1` to install. Neither installer changes execution policy.

Native preferences: Tools > Options > AI Archi-Tec. Plugin loading: Tools > Options > Plug-ins.

This is a concept-modeling beta. Live provider/vision trials and clean startup on another licensed workstation remain outstanding. Arbitrary image reconstruction, full BIM and automatic cloud-to-localhost connectivity are not provided.

Installation guide: https://github.com/MrHs6/ai-architec-rhino/blob/main/docs/INSTALLATION.fa.md

API/MCP guide: https://github.com/MrHs6/ai-architec-rhino/blob/main/docs/CONNECTIONS.fa.md

English: https://github.com/MrHs6/ai-architec-rhino/blob/main/docs/README.en.md

Issues: https://github.com/MrHs6/ai-architec-rhino/issues

Source/license terms: https://github.com/MrHs6/ai-architec-rhino/blob/main/LICENSE.md

Copyright © 2026 Hasan Sharif Askari.
