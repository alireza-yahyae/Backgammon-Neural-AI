# هوش مصنوعی عصبی (backgammon_ai_v5) داخل این بازی

حالا حریف کامپیوتری بازی (بازیکن ۲، مهره‌های قهوه‌ای) می‌تونه با شبکه‌ی عصبی‌ای بازی کنه که خودت با self-play آموزش دادی؛ همون شبکه‌ی ۲۱۴ ورودی / ۵ خروجی و همون جستجوی ۲ لایه (۲۱ تاس حریف، ۵ حرکت برتر).

## راه‌اندازی (۳ مرحله)

1. وزن‌های آموزش‌دیده رو به فرمت بازی تبدیل کن (فقط numpy لازمه):

   ```
   python tools/export_weights.py مسیر/models/latest.npz BackgammonByHoratiu/Ai/net.bgnn
   ```

2. بازی رو مثل همیشه بساز و اجرا کن:

   ```
   dotnet run --project BackgammonByHoratiu
   ```

   فایل `Ai/net.bgnn` خودکار کنار فایل اجرایی کپی می‌شه.

3. اگه فایل رو جای دیگه‌ای گذاشتی، مسیرش رو با متغیر محیطی `BACKGAMMON_AI_NET` بده.

اگه `net.bgnn` نباشه یا خراب باشه، بازی بدون مشکل با هوش مصنوعی قدیمی خودش ادامه می‌ده.

## چی تغییر کرده

| فایل | کار |
|---|---|
| `GameLogic/AI/BackgammonAi.cs` | حرکت‌ها رو از شبکه می‌گیره؛ محاسبه روی thread جدا انجام می‌شه تا پنجره قفل نکنه؛ اگه بازی یه حرکت رو رد کنه، همون نوبت با AI قدیمی ادامه می‌ده |
| `GameLogic/AI/Neural/NeuralNetwork.cs` | خوندن `.bgnn` و محاسبه‌ی شبکه (همون `model.py`) |
| `GameLogic/AI/Neural/FeatureEncoder.cs` | تبدیل صفحه به ۲۱۴ عدد (همون `features.py`) |
| `GameLogic/AI/Neural/BgRules.cs` | قوانین کامل برای جستجو (همون `backgammon.py`) |
| `GameLogic/AI/Neural/NeuralPolicy.cs` | انتخاب نوبت، عمق ۱ و ۲ (همون `selfplay.py` و `search.py`) |
| `GameLogic/AI/Neural/NeuralMovePlanner.cs` | اتصال به `IGameManager` |
| `tools/export_weights.py` | تبدیل `.npz` به `.bgnn` |
| `tools/make_golden.py` | ساخت داده‌ی تست از روی کد پایتون |

رابط گرافیکی، `Table.cs` و قوانین بازی دست نخورده‌اند.

## تست

```
dotnet test
```

تست‌های `NeuralAiTests` چک می‌کنن که C# دقیقاً مثل پایتون عمل کنه: ویژگی‌ها، خروجی شبکه، مجموعه‌ی حرکت‌های مجاز، و انتخاب حرکت در عمق ۱ و ۲. داده‌ی مرجع با `python tools/make_golden.py مسیر/backgammon_ai_v5` ساخته شده.

## نکته‌ها

- جهت‌ها: بازیکن ۲ این بازی (ستون‌ها کاهشی، خونه‌ی ۰ تا ۵) همون «بازیکن ۰» شبکه‌ست؛ تخته با علامت برعکس خونده می‌شه.
- عمق و تعداد کاندیدها در `NeuralMovePlanner.cs` (`DefaultDepth`، `DefaultTopCandidates`) تنظیم می‌شن.
