# אימות גרסה 1.1.0

אימות מקומי: 29 בדיקות ליבה/מוזיקה, 7 בדיקות SQLite ו־8 בדיקות ארכיון עברו (44 בסך הכול); בניית Windows עם EnableWindowsTargeting הסתיימה ב־0 שגיאות ו־0 אזהרות.

הבדיקות האוטומטיות מכסות מדידת זמן, סטטיסטיקות, DST, SQLite וייבוא, תזמון קליקים לפי דגימות, עקביות בין גודלי buffer, Tap Tempo, רשימת סולמות, שמירת הקלטות, העשרת משוב, ניקוי אחרי שבעה ימים, הגנת הקלטה פעילה, בידוד מטא־נתונים פגומים, שחזור WAV חלקי וייצוא נבחרות.

```sh
dotnet run --project tests/ZmanLenagen.Tests -c Release
dotnet run --project tests/ZmanLenagen.StorageTests -c Release
dotnet run --project tests/ZmanLenagen.AudioTests -c Release
dotnet build src/ZmanLenagen.App -c Release
```

GitHub Actions מריץ את שלוש סדרות הבדיקות ב־Windows, מפרסם אפליקציה עצמאית x64 ובונה מתקין Inno Setup. תג v1.1.0 מפרסם מתקין ו־SHA256SUMS.

בדיקות מיקרופון/רמקול, תצוגת WPF תחת DPI שונים, התראות, נעילה ושינה דורשות Windows אינטראקטיבי; הן אינן מוכחות על ידי בניית CI. יש לבצע את MANUAL-TESTS.md על מחשב Windows.
