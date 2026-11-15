# Проверки

## Автономные проверки

На Windows из корня проекта:

```powershell
./docs/verification/regress.ps1
```

Скрипт использует компилятор C# из .NET Framework. Проверяются карта, достижимость ключевых точек,
обходы стен, учёт света, секундное окно и исходные значения сложности. Результаты — в `TestResults`.

## Собранная игра

Сначала соберите Windows-плеер через меню **Pokagoryu → Build Windows Player**, затем:

```powershell
./docs/verification/regress.ps1 -Player ./Builds/Windows/Pokagoryu/Pokagoryu.exe -AI
```

Сценарий запускает настоящий плеер с `-pg-smoke`, подменяет ввод внутри игры и проверяет действия E/F,
ходьбу, бой, запуск автобуса, паузу, меню, настройки, звук, журнал света и повторные игры.
Дополнительный `-pg-ai` проверяет врагов в четырёх районах и отрезки маршрутов всех десяти людей.
Проверочный запуск завершается автоматически и не сохраняет свои настройки в PlayerPrefs.

Для отдельного запуска:

```powershell
./Builds/Windows/Pokagoryu/Pokagoryu.exe -pg-smoke -pg-smoke-out ./TestResults/player
```

## Проверки в редакторе

Постоянный стенд: `Assets/Editor/Verification/PGSmokeTest.cs`.

```powershell
./docs/verification/run_smoke.ps1 -Scenario layout
./docs/verification/run_smoke.ps1 -Scenario enemies
./docs/verification/run_smoke.ps1 -Scenario pizza
```

Unity ищется по `ProjectSettings/ProjectVersion.txt`; другой путь можно передать параметром `-UnityExe`.
Сценарии `menushot`, `introshot`, `thoughts` требуют окна редактора и запуска с `-Windowed`.
Выходной код 0 и строка `SMOKE RESULT: OK` означают успешное выполнение.

## Музыкальные данные

`MusicChecks.cs` вместе с `Assets/Scripts/Core/SuspenseScore.cs` собирается в консольную программу.
Она проверяет четыре слоя на конечные значения, запас уровня, непрерывность циклов и детерминированность,
затем записывает демонстрационный WAV в указанную выходную папку.
