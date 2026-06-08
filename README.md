# S.T.A.L.K.E.R. Mod Launcher

Windows-лаунчер профилей модов для GOG/DRM-free версии **S.T.A.L.K.E.R.: Shadow of Chernobyl**. Приложение создает изолированную рабочую среду профиля, накладывает моды поверх базы и запускает игру из этой среды, не изменяя оригинальную папку игры.

## Структура проекта

```text
StalkerModLauncher.sln
src/
  StalkerModLauncher/
    App.xaml
    Views/
      MainWindow.xaml
      ProfileSettingsWindow.xaml
      AboutWindow.xaml
      NotesWindow.xaml
      ScreenshotsWindow.xaml
      ProfileHealthWindow.xaml
      ScanResultsWindow.xaml
    ViewModels/
      MainViewModel.cs
      ProfileSettingsViewModel.cs
      ProfileHealthViewModel.cs
      NotesViewModel.cs
      ScreenshotsViewModel.cs
    Models/
      AppSettings.cs
      ModProfile.cs
      ModEntry.cs
      ValidationResult.cs
    Services/
      AppPaths.cs
      SettingsStore.cs
      GameInstallationValidator.cs
      AppServices.cs
      ProfileManager.cs
      WorkspaceBuilder.cs
      ProfileLauncher.cs
      LaunchCoordinator.cs
      GameSessionTracker.cs
      GameExitDiagnosticsService.cs
      ProfileHealthService.cs
      ProfileDataPathResolver.cs
      ModScannerService.cs
      DiscordPresenceService.cs
      DialogService.cs
      FileSystemSafety.cs
    Infrastructure/
      ObservableObject.cs
      RelayCommand.cs
      AsyncRelayCommand.cs
docs/
  example-settings.json
tests/
  StalkerModLauncher.Tests/
```

## Архитектура

- **UI:** WPF на .NET 8, экран сразу является рабочим лаунчером: профили слева, детали и моды справа, журнал действий снизу.
- **MVVM:** `MainViewModel` управляет состоянием, командами и привязками; файловая логика вынесена в сервисы.
- **Композиция зависимостей:** `AppServices` централизованно создает сервисы и передает их окнам и view model через конструкторы.
- **Профили:** `ProfileManager` отвечает за создание, дублирование, импортные значения по умолчанию и безопасное удаление профилей.
- **Хранилище:** JSON в `%AppData%\StalkerModLauncher\settings.json`.
- **Workspaces:** управляемая папка лаунчера. Если игра находится на диске вроде `D:\`, новые профили по умолчанию используют `D:\StalkerModLauncher\Workspaces`, чтобы Windows могла делать hard links вместо полной копии базы. Резервный путь: `%LocalAppData%\StalkerModLauncher\Workspaces`.
- **Валидация игры:** проверяется корневая папка SoC GOG, `fsgame.ltx` и известные исполняемые файлы.
- **Запуск:** `WorkspaceBuilder` готовит изолированный workspace, `ProfileLauncher` запускает процесс игры, а `LaunchCoordinator` связывает запуск с отслеживанием игровой сессии.
- **Тесты безопасности:** интеграционные тесты проверяют порядок наложения модов, неизменность исходных файлов, изоляцию `userdata`, кэш workspace и защиту от удаления неуправляемых директорий.
- **Автономные профили:** крупные моды со своим движком запускаются непосредственно из папки мода без создания overlay-workspace.
- **Дополнительные возможности:** импорт/экспорт профилей, поиск модов, заметки, просмотр скриншотов, учет игрового времени и Discord Rich Presence.
- **Дублирование профиля:** кнопка `Копия` или `Ctrl+D` создает профиль с новыми ID и отдельным читаемо названным workspace, сохраняя настройки и список модов без переноса сохранений и игрового времени.
- **Диагностика завершения:** при быстром завершении игры лаунчер показывает код выхода и пути к свежим игровым логам или crash dump в журнале.
- **Состояние профиля:** отдельное read-only окно проверяет игру, моды, бинарник, workspace, сохранения, логи и crash dump; для автономных модов учитываются `fsgame.ltx`, `appdata`, `userdata` и варианты `_appdata_`.

## Почему оригинальная игра не меняется

Лаунчер не пишет в выбранную папку установки игры. Крупные неизменяемые ресурсы подключаются через hard link или symbolic link. Потенциально изменяемые конфигурации, скрипты, текстовые файлы и каталоги пользовательских данных всегда копируются в workspace, поэтому запись движка в них не затрагивает оригинальную игру или мод. При наложении мода существующая запись workspace сначала удаляется, и только потом создается защищенная копия или ссылка модового файла.

Workspace содержит маркер `.stalker-launcher-workspace`; лаунчер пересоздает только управляемую подпапку `current`.

При проверке кэша лаунчер один раз создает снимок метаданных файлов игры и включенных модов. Тот же снимок используется для пересборки workspace, поэтому большие каталоги не сканируются повторно в рамках одного запуска.

Проводник Windows может показывать большой `Size` для workspace, потому что считает длину файлов, на которые указывают hard links. Это не всегда равно реально занятому месту. В журнале лаунчера после подготовки профиля выводится количество linked/symlinked/copied файлов; чем меньше `copied`, тем меньше реальная прибавка к расходу диска.

`fsgame.ltx` внутри workspace генерируется отдельно для профиля: `$app_data_root$` указывает на `userdata` в папке профиля, чтобы сейвы, логи и пользовательские конфиги не смешивались между профилями.

## Сборка и запуск

Нужен .NET 8 SDK на Windows 10/11:

```powershell
dotnet build .\StalkerModLauncher.sln
dotnet run --project .\src\StalkerModLauncher\StalkerModLauncher.csproj
dotnet test .\StalkerModLauncher.sln -c Release
```

Публикация:

```powershell
dotnet publish .\src\StalkerModLauncher\StalkerModLauncher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none
```

## Использование

1. Выберите папку GOG-игры, например `D:\Games\S.T.A.L.K.E.R. Shadow of Chernobyl`.
2. Создайте профиль.
3. Добавьте одну или несколько папок модов. Папки можно перетаскивать в список модов.
4. Меняйте порядок модов drag-and-drop или кнопками Up/Down.
5. Укажите исполняемый файл относительно workspace.
   - Vanilla SoC GOG обычно использует `bin\xr_3da.exe`; на Windows регистр не важен, поэтому подойдет и фактический `bin\XR_3DA.exe`.
   - OGSR-моды с собственным движком могут использовать `bin_x64\xrEngine.exe`.
6. Нажмите Launch.

Для тестового мода `D:\Games\Mods\Zona_pokayaniya` найден исполняемый файл `D:\Games\Mods\Zona_pokayaniya\bin_x64\xrEngine.exe`, поэтому в профиле нужно указать `bin_x64\xrEngine.exe`.

## Пример JSON

```json
{
  "gameInstallPath": "D:\\Games\\S.T.A.L.K.E.R. Shadow of Chernobyl",
  "profiles": [
    {
      "id": "zona-pokayaniya",
      "name": "Zona pokayaniya",
      "description": "OGSR test profile",
      "isEnabled": true,
      "launchArguments": "-nointro",
      "executableRelativePath": "bin_x64\\xrEngine.exe",
      "workspacePath": "D:\\StalkerModLauncher\\Workspaces\\Zona-pokayaniya",
      "configNotes": "Mod files stay outside the game installation.",
      "mods": [
        {
          "id": "zona-pokayaniya-main",
          "name": "Zona pokayaniya",
          "sourcePath": "D:\\Games\\Mods\\Zona_pokayaniya",
          "isEnabled": true,
          "order": 1,
          "notes": "Main mod folder"
        }
      ]
    }
  ]
}
```

Лаунчер сам записывает абсолютный `workspacePath` при создании профиля. Если workspace находится на том же диске, что и игра, подготовка профиля обычно быстрее и экономнее по месту.
