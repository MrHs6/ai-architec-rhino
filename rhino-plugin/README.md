# AI Archi-Tec for Rhino 8

نسخهٔ 0.3.0-beta.2 با سه فضای کاری مستقل، پروفایل مستقل سرویس‌ها و لوگوی اصلی پروژه.

**[صفحهٔ عمومی و دانلود](https://github.com/MrHs6/ai-architec-rhino)** · [نصب فارسی](https://github.com/MrHs6/ai-architec-rhino/blob/main/docs/INSTALLATION.fa.md) · [English](https://github.com/MrHs6/ai-architec-rhino/blob/main/docs/README.en.md)

بستهٔ Windows installer را استخراج و install.cmd را اجرا کنید؛ یا Yak همان نسخه را با ابزار راینو نصب کنید. راینو را مجدداً اجرا و فرمان **archtec** را وارد کنید. Windows x64، Rhino 8.31+ از شاخهٔ Rhino 8 و runtime دات‌نت ۸ لازم است.

- Generate: ساخت هندسهٔ جدید؛ تنظیم نما و لایهٔ خروجی.
- Analyze: گزارش محلی ابعاد، سلامت هندسه و لایه‌ها؛ تحلیل AI اختیاری و فقط خواندنی.
- Model Assist: ویرایش انتخاب یا خروجی اخیر با Move/Scale/Extrude، مقدار، محور و پیش‌نمایش.
- متن، مرجع و مکالمهٔ هر بخش در همان بخش می‌ماند. تغییر بخش فقط پیش‌نمایش فعال را لغو می‌کند.
- API profiles: OpenRouter، OpenAI، xAI/Grok، Google Gemini، Anthropic/Claude و OpenAI-compatible/local؛ کلید و مدل مستقل برای هر سرویس.
- MCP connection: پورت، Start/Stop و کپی تنظیم کلاینت؛ شروع صریح سرور روی localhost.
- Preferences: فضای کاری پیش‌فرض، تراکم مکالمه و Zoom پیش‌نمایش.
- Connections: نمای کلی مستقل وضعیت API و MCP.
- About the creator: Hasan Sharif Askari، روند پروژه و پیوندها با لوگوی اصلی.

Generate و عملیات سریع Assist فقط پیش‌نمایش می‌سازند؛ Apply هندسهٔ بومی را به سند اضافه/اعمال و Undo راینو همان بستهٔ عملیات را برمی‌گرداند. اجرای کد آزاد مدل وجود ندارد.

کلید اختیاری ذخیره‌شده با Windows DPAPI برای همان حساب رمزگذاری می‌شود. بدون Remember، کلید در حافظه می‌ماند. داده‌های مکالمه/تصویر workspaceها در اتصال‌ها ذخیره نمی‌شوند. [Privacy](PRIVACY.md)

[راهنمای API و MCP](https://github.com/MrHs6/ai-architec-rhino/blob/main/docs/CONNECTIONS.fa.md) · [قرارداد سرویس‌ها](https://github.com/MrHs6/ai-architec-rhino/blob/main/docs/PROVIDERS.md)

ساخت سورس روی Windows با .NET SDK 8:

```powershell
.\scripts\build.ps1 -UseNuGet
.\scripts\package.ps1
```

SDK رسمی Rhino از NuGet دریافت می‌شود. برای کامپایل نصب محلی راینو لازم نیست؛ اجرا و بررسی هندسه به راینو معتبر نیاز دارد. قالب‌های GitHub و GitLab در ci قرار دارند. [راهنمای توزیع](docs/DISTRIBUTION.md)

این یک بتای مدل‌سازی مفهومی است. آزمون زنده با API Key واقعی و نصب تمیز روی رایانهٔ دوم باقی است. خروجی BIM/نقشهٔ اجرایی و بازسازی دقیق هر تصویر ادعا نمی‌شود. [گزارش آزمون](docs/VALIDATION.md)

طراحی و مالکیت: **Hasan Sharif Askari**.
