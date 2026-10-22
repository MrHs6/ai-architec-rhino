<p align="center"><img src="rhino-plugin/src/ArchiTec.Rhino/Assets/logo.png" width="90" alt="AI Archi-Tec logo"></p>
<h1 align="center">AI Archi-Tec for Rhino 8</h1>
<p align="center">دستیار مدل‌سازی معماری داخل راینو — با API شخصی یا اتصال محلی MCP</p>
<p align="center"><a href="docs/README.en.md">English</a> · <a href="docs/INSTALLATION.fa.md">راهنمای نصب</a> · <a href="docs/CONNECTIONS.fa.md">اتصال API و MCP</a> · <a href="https://github.com/MrHs6/ai-architec-rhino/releases">دانلودها</a></p>

[![Windows build](https://github.com/MrHs6/ai-architec-rhino/actions/workflows/rhino-plugin.yml/badge.svg?branch=main)](https://github.com/MrHs6/ai-architec-rhino/actions/workflows/rhino-plugin.yml)
![Rhino 8.31+](https://img.shields.io/badge/Rhino-8.31%2B-703CE8)
![Windows x64](https://img.shields.io/badge/Windows-x64-0078D4)
![Beta](https://img.shields.io/badge/status-beta-orange)

ایدهٔ معماری را در پنل **AI Archi-Tec** وارد کنید، طرح پیشنهادی را در همان سند راینو ببینید و با **Apply to Rhino** به هندسهٔ قابل ویرایش تبدیل کنید. برای استفاده از هوش مصنوعی، سرویس و کلید شخصی خودتان را تنظیم کنید؛ یا کلاینت دسکتاپ سازگار را به MCP محلی پلاگین وصل کنید.

**Beta: `0.4.2-beta.1` · Windows x64 · Rhino 8.31+ (.NET 8)**

نیازمند راینو ۸.۳۱ یا جدیدتر از شاخهٔ Rhino 8. این یک نسخهٔ آزمایشی برای مدل‌سازی مفهومی است؛ محدودیت‌ها و وضعیت آزمون‌ها پایین همین صفحه آمده‌اند.

در نسخهٔ 0.4، پروفایل API پیش از اتصال ذخیره و فعال می‌شود؛ خطای شبکه با وضعیت ذخیره مخلوط نمی‌شود. پس از Apply، چت نتیجهٔ واقعی را گزارش می‌کند و ادامهٔ ویرایش به همان مدل وصل است. [آموزش ساخت و ادامهٔ ویرایش](docs/CONVERSATIONAL-WORKFLOW.fa.md) · [بررسی پلاگین‌های مرجع](docs/REFERENCE-REVIEW.fa.md)

نسخهٔ 0.4.1 رابط و راهنمای داخل پلاگین را کاملاً انگلیسی ارائه می‌کند. وضعیت سرور، نام اعلام‌شدهٔ کلاینت، آخرین تماس و مراحل درخواست/پیش‌نمایش/اجرا جدا دیده می‌شوند. خروجی تنظیمات Claude و Codex متفاوت و بدون توکن است. [MCP setup and chat guide — English](docs/MCP-QUICKSTART.en.md) · [راهنمای فارسی](docs/MCP-QUICKSTART.fa.md)


در نسخهٔ **0.4.2** اتصال Codex/Claude Desktop با دکمهٔ **Connect** خودکار ثبت می‌شود. در حالت MCP گفتگو داخل کلاینت است و راینو فقط نتیجه و پیش‌نمایش را نمایش می‌دهد. قطع‌و‌وصل MCP با همان کلاینت باز بازیابی می‌شود؛ فقط اولین نصب یا ارتقای مسیر پل به یک refresh کلاینت نیاز دارد. [آموزش سادهٔ اتصال](docs/MCP-QUICKSTART.fa.md)

## دانلود و نصب سریع

**[دانلود نصب‌کنندهٔ ویندوز ZIP](https://github.com/MrHs6/ai-architec-rhino/releases/download/rhino-v0.4.2-beta.1/ai-architec-rhino-0.4.2-beta.1-windows-installer.zip)** · [دانلود مستقیم Yak](https://github.com/MrHs6/ai-architec-rhino/releases/download/rhino-v0.4.2-beta.1/ai-architec-rhino-0.4.2-beta.1-rh8_31-win.yak) · [صفحهٔ نسخه و فایل‌ها](https://github.com/MrHs6/ai-architec-rhino/releases/tag/rhino-v0.4.2-beta.1)

1. نصب‌کنندهٔ ZIP را دانلود و **Extract All** کنید؛ فایل‌ها را کنار هم نگه دارید.
2. سندتان را ذخیره و **تمام پنجره‌های راینو را ببندید**؛ سپس فایل **`install.cmd`** را اجرا کنید. این فایل ابزار رسمی **Yak** همراه راینو را فراخوانی می‌کند.
3. راینو را دوباره اجرا کنید.
4. در خط فرمان راینو بنویسید **`archtec`** و **Enter** بزنید.
5. در **Settings** سرویس، API Key و مدل خودتان را انتخاب کنید؛ یا از دکمهٔ **MCP** استفاده کنید.

برای نصب نسخهٔ آماده، **Visual Studio، Node و SDK لازم نیست**. کاربران نهایی فقط به راینو با لایسنس معتبر نیاز دارند. هزینه و دسترسی مدل آنلاین تابع سرویس انتخابی کاربر است؛ هیچ کلید یا اعتبار اشتراکی در بسته قرار ندارد.

نصب مستقیم در CMD نیز ممکن است:

```cmd
"%ProgramFiles%\Rhino 8\System\Yak.exe" install "D:\Downloads\ai-architec-rhino-0.4.2-beta.1-rh8_31-win.yak"
```

مسیر فایل را با محل دانلود خودتان جایگزین کنید. راه‌های دیگر نصب، مسیر سفارشی راینو، به‌روزرسانی، حذف و رفع خطا در **[راهنمای نصب فارسی](docs/INSTALLATION.fa.md)** توضیح داده شده‌اند.

## محیط واقعی پلاگین

![آزمون واقعی Codex، باکس و اتصال مجدد در نسخهٔ 0.4.2](rhino-plugin/docs/codex-box-042.png)

در آزمون نسخهٔ 0.4.2، همین چت Codex طرح باکس را فرستاد؛ کاربر Apply را زد و کدکس یک Brep با ابعاد دقیق ۱ × ۲ × ۳ متر را خواند. قطع‌و‌وصل مجدد همان GUID را حفظ کرد و مدل تکراری ساخته نشد. این تصویر خروجی واقعی آزمون است.

<p align="center"><img src="rhino-plugin/docs/studio-panel.png" width="500" alt="English AI Archi-Tec workspace in Rhino with an explicitly labeled local MCP protocol test"></p>

تصویر بالا رابط انگلیسی واقعی و آزمون پروتکل MCP محلی را نشان می‌دهد؛ این آزمون حساب آنلاین نیست. نمونهٔ ساختمان پایین با حالت محدود **Offline examples** ساخته شده است.

![هندسهٔ واقعی ساختمان نمونه در Rhino](rhino-plugin/docs/building-output.png)

[دانلود فایل قابل ویرایش ساختمان نمونه 3DM](https://github.com/MrHs6/ai-architec-rhino/releases/download/rhino-v0.4.1-beta.2/demo-building.3dm) — ۴۶ جزء بومی راینو شامل دال‌ها، ستون‌ها، نما و بام.

## چه کارهایی انجام می‌دهد؟

| قابلیت | رفتار نسخهٔ فعلی |
|---|---|
| پنل داخل راینو | فرمان `archtec`، پنل قابل Dock و حالت شناور با `AIArchitectFloat` |
| API شخصی | پروفایل مستقل OpenRouter، OpenAI، xAI/Grok، Google Gemini، Anthropic/Claude و OpenAI-compatible/local |
| فضای کاری مستقل | Generate، Analyze و Model Assist هرکدام گزینه‌ها، ورودی، مرجع و مکالمهٔ جدا دارند |
| انتخاب مدل | فهرست مدل‌ها از سرویس دریافت می‌شود؛ شناسهٔ انتخابی بدون تعویض مخفی ارسال می‌شود |
| ورودی تصویر | ورودی بومی Gemini؛ OpenRouter، xAI و Anthropic طبق قابلیت تصویری اعلام‌شدهٔ مدل |
| ساختمان مفهومی | ساختمان مستطیلی چندطبقه با اجزای جدا، لایه‌بندی و مشخصات طبقه/نقش |
| مدل‌سازی پایه | باکس، خط، پلی‌لاین، منحنی و extrusion؛ جابه‌جایی، تغییر مقیاس، انتخاب و خواندن مشخصات |
| کنترل خروجی | اعتبارسنجی طرح، پیش‌نمایش در viewport، اجرای صریح با Apply و Undo در راینو |
| Analyze | گزارش محلی ابعاد، اعتبار/بسته‌بودن هندسه و لایه‌ها؛ انتخاب scope و تفسیر اختیاری AI، بدون تغییر هندسه |
| Model Assist | Move، Scale و Extrude روی انتخاب یا خروجی اخیر، با مقدار و محور مشخص و پیش‌نمایش قبل از Apply |
| تنظیمات و About | صفحات جدا برای API، MCP، Preferences و نمای کلی اتصال؛ معرفی سازنده با لوگوی اصلی |
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

صفحهٔ **API profiles** را از آیکون کلید باز کنید؛ هر سرویس کلید، نشانی و مدل مستقل دارد. **Connect and discover models** فهرست حساب همان سرویس را دریافت می‌کند. Grok از API مستقیم xAI، Gemini از GenerateContent و Claude از Anthropic Messages استفاده می‌کند؛ OpenRouter، OpenAI و سرویس سفارشی از قالب سازگار OpenAI استفاده می‌کنند. [تفاوت پروتکل‌ها و منابع رسمی](docs/PROVIDERS.md)

آیکون کره، **MCP connection** را باز می‌کند؛ آیکون چرخ‌دنده، **Preferences** را برای فضای کاری پیش‌فرض، تراکم مکالمه و Zoom پیش‌نمایش باز می‌کند. دکمهٔ دوطرفهٔ بالای پنل، نمای کلی اتصال را نشان می‌دهد. بخش **About the creator** سازنده و روند کار پروژه را معرفی می‌کند.

در **MCP connection** کلاینت را انتخاب و **Connect to Codex/Claude Desktop** بزنید؛ ثبت پل برای همین کاربر خودکار است. گفتگو در کلاینت و نتیجه، پیش‌نمایش و Apply در راینو است. فقط اولین نصب یا ارتقای پل به یک refresh کلاینت نیاز دارد؛ قطع‌و‌وصل MCP راینو با همان کلاینت باز خودکار بازیابی می‌شود. تنظیمات دستی در بخش پیشرفته‌اند. بازکردن سایت ChatGPT اتصال محلی را ثبت نمی‌کند؛ مسیر ابری به connector یا تونل پشتیبانی‌شده نیاز دارد. [آموزش ساده](docs/MCP-QUICKSTART.fa.md)

**[راهنمای کامل اتصال و نمونهٔ تنظیمات](docs/CONNECTIONS.fa.md)** · [نحوهٔ استفاده از داده‌ها](rhino-plugin/PRIVACY.md)

## وضعیت بتا و محدودیت‌ها

- **۶۷ آزمون خودکار** شامل سه پروتکل جدید، صفحه‌بندی مدل‌ها، ورودی تصویر، هدر کلید و استقلال workspaceها گذشتند. در نسخهٔ جدید، **۱۹ آزمون هندسه و ۲۰ بررسی رابط/گزارش/پروفایل** در خود راینو نیز گذشتند. چهار بررسی ساختمان و آزمون Apply/Undo از نسخهٔ قبلی ثبت شده‌اند.
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
