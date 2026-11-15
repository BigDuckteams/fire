# Пока горю

Игра от первого лица на Unity. Свет героя — здоровье, фонарь и оружие. Проведите выживших через
тёмный город до автобуса: берите свет у ламп, делитесь им с людьми и отбивайтесь от Тьмы.

## Разработка

В репозитории находится полный исходный Unity-проект. Для работы на другом компьютере:

```powershell
git clone https://github.com/BigDuckteams/fire.git
cd fire
```

1. Установите **Unity 6000.6.9f1** через Unity Hub. Для сборки Windows нужен модуль Windows Build Support (Mono).
2. В Unity Hub выберите **Add → Add project from disk** и укажите папку `fire`, где находятся `Assets`, `Packages` и `ProjectSettings`.
3. Дождитесь импорта ресурсов и восстановления пакетов. Их версии закреплены в `Packages/packages-lock.json`.
4. Откройте `Assets/Scenes/Main.unity` и нажмите **Play**.

Используется **Built-in Render Pipeline** и старый Input Manager. Геометрия, текстуры, эффекты и музыка
создаются кодом. Локальные DLL из готовой игры, скачанные архивы, секреты и файлы с другого компьютера
для разработки не требуются. `Library`, `Temp` и файлы IDE Unity создаст самостоятельно.

## Структура

```text
Assets/
  Scenes/Main.unity      стартовая сцена
  Scripts/Core/          конфигурация, ввод, звук, графика, учёт света
  Scripts/Game/          состояние игры, меню, предыстория
  Scripts/Player/        движение, свет героя, руки и бой
  Scripts/World/         город, лампы, люди, автобус, предметы
  Scripts/Enemies/       враги и управление их появлением
  Scripts/Verification/  проверочный режим собранной игры
  Resources/Shaders/     шейдеры Built-in RP
  Editor/                инструменты сборки и редакторские проверки
Packages/                зависимости и lock-файл
ProjectSettings/         настройки Unity и версия редактора
docs/                    устройство игры и проверки
tools/                   сборка и подготовка Windows-релиза
```

Сохраняйте `.meta` вместе с ресурсами, а изменения `Packages` и `ProjectSettings` коммитьте вместе с кодом.
Обычный рабочий цикл: `git pull` → изменения в Unity → проверка → `git add` / `git commit` / `git push`.

## Сборка Windows

В Unity: **Pokagoryu → Build Windows Player**.
Результат: `Builds/Windows/Pokagoryu/Pokagoryu.exe` и необходимые ему папки и библиотеки.

Из PowerShell:

```powershell
./tools/build_windows.ps1
# Development Build с отладочными клавишами:
./tools/build_windows.ps1 -Development
```

Сборки не хранятся в Git. Готовые версии публикуются в [Releases](https://github.com/BigDuckteams/fire/releases).
Windows-EXE релиза содержит всю игру: запустите его, выберите папку распаковки и затем запускайте
`Pokagoryu/Pokagoryu.exe`. Отдельный маленький Unity launcher без `Pokagoryu_Data`, `UnityPlayer.dll`
и `MonoBleedingEdge` работать не будет.

Для подготовки такого EXE из своей сборки установите WinRAR и выполните:

```powershell
./tools/package_windows.ps1
```

WinRAR нужен только для подготовки релиза; получателю игры он не требуется. В репозиторий
добавляются исходники, а созданный файл из `Releases` загружается в GitHub Releases.

## Управление

| Клавиша | Действие |
|---|---|
| WASD, мышь, Shift | движение, обзор, бег |
| ЛКМ | выстрел; удержание — слитый луч |
| ПКМ | вспышка света |
| E | вдох у лампы, подбор осколка, пицца |
| F | пробуждение и лечение людей, передача света, запуск автобуса |
| Q | прикрыть огонь |
| G | Проводник к автобусу |
| Колесо мыши | радиус ауры |
| Tab | посмотреть на полоску света на руке |
| Esc | пауза |
| Пробел | пропустить предысторию |

В настройках есть три сложности, угол обзора, инверсия Y, размер подсказок и отдельные группы громкости.
Пункт «Свет за забег» в паузе и после окончания игры показывает доходы и расходы света.

## Проверки

```powershell
# Карта, учёт света и стандартные значения баланса:
./docs/verification/regress.ps1
# Проверки собранной игры и навигации:
./docs/verification/regress.ps1 -Player ./Builds/Windows/Pokagoryu/Pokagoryu.exe -AI
```

Подробности — [docs/verification/README.md](docs/verification/README.md).
Архитектура — [docs/DESIGN.md](docs/DESIGN.md), изменения версий — [CHANGELOG.md](CHANGELOG.md).
