<p align="center"><img src="rhino-plugin/src/ArchiTec.Rhino/Assets/logo.png" width="90" alt="AI Archi-Tec logo"></p>
<h1 align="center">AI Archi-Tec for Rhino 8</h1>
<p align="center">دستیار مدل‌سازی معماری داخل راینو — با API شخصی یا اتصال محلی MCP</p>
<p align="center"><a href="docs/README.en.md">English</a> · <a href="docs/INSTALLATION.fa.md">راهنمای نصب</a> · <a href="docs/CONNECTIONS.fa.md">اتصال API و MCP</a> · <a href="https://github.com/MrHs6/ai-architec-rhino/releases">دانلودها</a></p>

[![Windows build](https://github.com/MrHs6/ai-architec-rhino/actions/workflows/rhino-plugin.yml/badge.svg?branch=main)](https://github.com/MrHs6/ai-architec-rhino/actions/workflows/rhino-plugin.yml)
![Rhino 8.31+](https://img.shields.io/badge/Rhino-8.31%2B-703CE8)
![Windows x64](https://img.shields.io/badge/Windows-x64-0078D4)
![Beta](https://img.shields.io/badge/status-beta-orange)

ایدهٔ معماری را در پنل **AI Archi-Tec** وارد کنید، طرح پیشنهادی را در همان سند راینو ببینید و با **Apply to Rhino** به هندسهٔ قابل ویرایش تبدیل کنید. برای استفاده از هوش مصنوعی، سرویس و کلید شخصی خودتان را تنظیم کنید؛ یا کلاینت دسکتاپ سازگار را به MCP محلی پلاگین وصل کنید.

**Beta: `0.2.0-beta.3` · Windows x64 · Rhino 8.31+ (.NET 8)**

نیازمند راینو ۸.۳۱ یا جدیدتر از شاخهٔ Rhino 8. این یک نسخهٔ آزمایشی برای مدل‌سازی مفهومی است؛ محدودیت‌ها و وضعیت آزمون‌ها پایین همین صفحه آمده‌اند.

## دانلود و نصب سریع

**[دانلود نصب‌کنندهٔ ویندوز ZIP](https://github.com/MrHs6/ai-architec-rhino/releases/download/rhino-v0.2.0-beta.3/ai-architec-rhino-0.2.0-beta.3-windows-installer.zip)** · [دانلود مستقیم Yak](https://github.com/MrHs6/ai-architec-rhino/releases/download/rhino-v0.2.0-beta.3/ai-architec-rhino-0.2.0-beta.3-rh8_31-win.yak) · [صفحهٔ نسخه و فایل‌ها](https://github.com/MrHs6/ai-architec-rhino/releases/tag/rhino-v0.2.0-beta.3)

1. نصب‌کنندهٔ ZIP را دانلود و **Extract All** کنید؛ فایل‌ها را کنار هم نگه دارید.
2. فایل **`install.cmd`** را اجرا کنید. این فایل ابزار رسمی **Yak** همراه راینو را فراخوانی می‌کند.
3. سندتان را ذخیره و راینو را مجدداً اجرا کنید.
4. در خط فرمان راینو بنویسید **`archtec`** و **Enter** بزنید.
5. در **Settings** سرویس، API Key و مدل خودتان را انتخاب کنید؛ یا از دکمهٔ **MCP** استفاده کنید.

برای نصب نسخهٔ آماده، **Visual Studio، Node و SDK لازم نیست**. کاربران نهایی فقط به راینو با لایسنس معتبر نیاز دارند. هزینه و دسترسی مدل آنلاین تابع سرویس انتخابی کاربر است؛ هیچ کلید یا اعتبار اشتراکی در بسته قرار ندارد.

نصب مستقیم در CMD نیز ممکن است:

```cmd
"%ProgramFiles%\Rhino 8\System\Yak.exe" install "D:\Downloads\ai-architec-rhino-0.2.0-beta.3-rh8_31-win.yak"
```

مسیر فایل را با محل دانلود خودتان جایگزین کنید. راه‌های دیگر نصب، مسیر سفارشی راینو، به‌روزرسانی، حذف و رفع خطا در **[راهنمای نصب فارسی](docs/INSTALLATION.fa.md)** توضیح داده شده‌اند.

## محیط واقعی پلاگین

<p align="center"><img src="rhino-plugin/docs/studio-panel.png" width="500" alt="پنل واقعی AI Archi-Tec داخل Rhino؛ نمونهٔ آفلاین ساختمان پنج‌طبقه"></p>

تصویر بالا پنل واقعی پلاگین است. این نمونه با حالت محدود **Offline examples** ساخته شده است؛ تصویر، مدرک آزمون مدل آنلاین نیست.

![هندسهٔ واقعی ساختمان نمونه در Rhino](rhino-plugin/docs/building-output.png)

[دانلود فایل قابل ویرایش ساختمان نمونه 3DM](https://github.com/MrHs6/ai-architec-rhino/releases/download/rhino-v0.2.0-beta.3/demo-building.3dm) — ۴۶ جزء بومی راینو شامل دال‌ها، ستون‌ها، نما و بام.

## چه کارهایی انجام می‌دهد؟

| قابلیت | رفتار نسخهٔ فعلی |
|---|---|
| پنل داخل راینو | فرمان `archtec`، پنل قابل Dock و حالت شناور با `AIArchitectFloat` |
| API شخصی | OpenRouter، OpenAI و سرویس‌های سازگار با OpenAI با نشانی و کلید خود کاربر |
| انتخاب مدل | فهرست مدل‌ها از سرویس دریافت می‌شود؛ شناسهٔ انتخابی بدون تعویض مخفی ارسال می‌شود |
| ورودی تصویر | ضمیمه‌کردن تصویر برای مدل‌های OpenRouter که قابلیت vision آن‌ها تأیید شده است |
| ساختمان مفهومی | ساختمان مستطیلی چندطبقه با اجزای جدا، لایه‌بندی و مشخصات طبقه/نقش |
| مدل‌سازی پایه | باکس، خط، پلی‌لاین، منحنی و extrusion؛ جابه‌جایی، تغییر مقیاس، انتخاب و خواندن مشخصات |
| کنترل خروجی | اعتبارسنجی طرح، پیش‌نمایش در viewport، اجرای صریح با Apply و Undo در راینو |
| Analyze | دریافت زمینهٔ محدود مدل و پیشنهاد تحلیلی؛ در این حالت تغییری در هندسه پیشنهاد نمی‌شود |
| MCP محلی | سرور localhost با توکن و پل stdio برای کلاینت‌های دسکتاپ سازگار |
| تنظیمات مستقل | تنظیمات هر حساب ویندوز جداست؛ ذخیرهٔ اختیاری کلید با Windows DPAPI |

## روند کار

```mermaid
flowchart LR
    A["پرامپت و مرجع اختیاری"] --> B["API شخصی یا کلاینت MCP"]
    B --> C["طرح ساختاریافته و اعتبارسنجی"]
    C --> D["پیش‌نمایش در سند فعلی"]
    D --> E["Apply توسط کاربر"]
    E --> F["هندسهٔ بومی و قابل ویرایش Rhino"]
    F --> G["ادامهٔ طراحی یا Undo"]
```

**Generate به‌تنهایی سند را تغییر نمی‌دهد.** ابتدا پیش‌نمایش را ببینید؛ با **Apply to Rhino** اجرا و با **Ctrl+Z در راینو** همان بستهٔ عملیات را برگردانید. سند، واحد یا هندسهٔ هدف که تغییر کند، باید طرح تازه تولید شود.

برای اولین آزمون بدون API، از این مثال محدود استفاده کنید:

```text
Create a 15 x 20 meter building with 5 floors and 3.2 meter floor heights and a glazed facade.
```

برای کار آزاد با زبان طبیعی باید مدل آنلاین سازگار را وصل کنید. حالت آفلاین فقط چند مثال عددی ساختمان، باکس و جابه‌جایی را می‌فهمد.

## اتصال API و MCP

در پنل **Settings** یا بخش **Tools → Options → AI Archi-Tec** تنظیمات شخصی را وارد کنید. در Studio، **Load compatible models** را بزنید و مدل را انتخاب کنید. سرویس سازگار باید `/models`، `/chat/completions` و خروجی `json_schema` را پشتیبانی کند. فهرست مدل‌ها به معنی سازگاری قطعی همهٔ مدل‌های سرویس نیست.

دکمهٔ **MCP** سرور `http://127.0.0.1:5188/mcp` را روی همان کامپیوتر روشن می‌کند. **Copy desktop client configuration** تنظیمات پل محلی را آماده می‌کند. کلاینت می‌تواند زمینهٔ سند را بخواند و طرح پیشنهاد کند؛ اجرای طرح همچنان با Apply در راینو است. **یک سرویس ابری به localhost ویندوز دسترسی مستقیم ندارد** و به کلاینت/پل سازگار روی کامپیوتر نیاز دارد. بازکردن سایت ChatGPT اتصال MCP را خودکار تنظیم نمی‌کند.

**[راهنمای کامل اتصال و نمونهٔ تنظیمات](docs/CONNECTIONS.fa.md)** · [نحوهٔ استفاده از داده‌ها](rhino-plugin/PRIVACY.md)

## وضعیت بتا و محدودیت‌ها

- ساخت کامل در Windows و **۳۰ آزمون خودکار هسته، provider و MCP** اجرا شده‌اند. آزمون داخلی Rhino شامل **۱۹ بررسی هندسی + ۴ بررسی ساختمان** بوده است؛ پیشنهاد/اجرای واقعی MCP، جابه‌جایی فقط طبقهٔ پنجم و بازگشت دقیق با Undo نیز بررسی شده‌اند.
- آزمایش پاسخ زندهٔ سرویس‌های پولی و کیفیت تفسیر تصویر با حساب واقعی هنوز تکمیل نشده است. آزمون کامل نصب تمیز و شروع مجدد روی دستگاه دوم نیز باقی است. [گزارش اعتبارسنجی](rhino-plugin/docs/VALIDATION.md)
- خروجی فعلی هندسهٔ مفهومی است. بازسازی دقیق هر تصویر، طراحی خودکار کامل هر ساختمان، BIM، نقشهٔ اجرایی، Grasshopper و حافظهٔ پایدار پروژه در این نسخه پیاده‌سازی نشده‌اند.
- این نسخه مخصوص Windows و Rhino 8 است؛ برای macOS، Rhino 7 یا همهٔ نسخه‌های اولیهٔ Rhino 8 بستهٔ سازگار ارائه نشده است.
- انتشار GitHub مستقل از Food4Rhino/Yak عمومی است؛ موجودبودن این مخزن به معنی درج در آن کاتالوگ‌ها نیست.

## سورس و توسعه

```powershell
git clone https://github.com/MrHs6/ai-architec-rhino.git
cd ai-architec-rhino\rhino-plugin
.\scripts\build.ps1 -UseNuGet
.\scripts\package.ps1
```

برای ساخت، Windows و **.NET SDK 8** لازم است. SDK رسمی Rhino با نسخهٔ مشخص از NuGet دریافت می‌شود؛ برای کامپایل، نصب راینو ضروری نیست. اجرای رابط و آزمون هندسه نیازمند راینو با لایسنس معتبر است. خروجی بسته‌ها در `rhino-plugin/artifacts/releases` قرار می‌گیرد.

GitHub Actions سورس را می‌سازد، آزمون‌ها را اجرا می‌کند و فایل نصب تولید می‌کند. تگ `rhino-v<version>` پیش‌نویس prerelease می‌سازد تا نگهدارنده آن را بازبینی و منتشر کند. قالب GitLab با Windows runner نیز در سورس موجود است. [راهنمای ساخت و انتشار](rhino-plugin/docs/DISTRIBUTION.md)

## پشتیبانی و معرفی

خطا یا پیشنهاد را در **[GitHub Issues](https://github.com/MrHs6/ai-architec-rhino/issues)** ثبت کنید؛ نسخهٔ Rhino، نسخهٔ پلاگین و مراحل بازتولید را بنویسید. کلید API و توکن محلی را در گزارش قرار ندهید.

آدرس مناسب برای معرفی در شبکه‌های اجتماعی: **https://github.com/MrHs6/ai-architec-rhino**

[متن آمادهٔ معرفی فارسی و انگلیسی](docs/SOCIAL-MEDIA.md) · [وب‌سایت پروژه](https://ai-arch.ir)

طراحی و مالکیت: **Hasan Sharif Askari**. سورس برای مشاهده منتشر شده است؛ مجوز متن‌باز برای تغییر یا بازتوزیع سورس تعیین نشده است. [وضعیت حقوق و مجوز](LICENSE.md)
