# SteamPipeGUI — Setup Guide

## Зависимости

### Unity
- Unity 2022.3+ (LTS)
- com.unity.ui (встроен начиная с 2021.2)
- **Newtonsoft.Json** — через Package Manager:
  `com.unity.nuget.newtonsoft-json` (Add package by name)

### Linux (на машине где запускается приложение)
```bash
# Ubuntu / Debian
sudo apt install steamcmd zenity

# Arch / Manjaro
sudo pacman -S steamcmd zenity

# Fedora
sudo dnf install steamcmd zenity
```

---

## Структура файлов

```
Assets/
├── Scripts/
│   ├── Core/
│   │   ├── UnityMainThreadDispatcher.cs   ← добавь на любой постоянный GO
│   │   ├── NativeFilePicker.cs            ← статический, не нужен на GO
│   │   ├── AppConfig.cs                   ← статический Load/Save
│   │   ├── DepotManager.cs                ← генерация VDF
│   │   └── SteamCmdWrapper.cs             ← запуск steamcmd
│   └── UI/
│       └── MainWindowController.cs        ← на GO с UIDocument
├── UI/
│   ├── UXML/
│   │   └── MainWindow.uxml
│   └── USS/
│       └── MainStyle.uss
```

---

## Настройка сцены

1. Создай пустой GameObject → `UIRoot`
2. Добавь компонент `UIDocument`
3. Назначь `MainWindow.uxml` в поле `Source Asset`
4. Добавь компонент `MainWindowController`
5. Создай ещё один GameObject → `Dispatcher`
6. Добавь компонент `UnityMainThreadDispatcher`
7. Убедись что `Dispatcher` помечен `DontDestroyOnLoad` (это делается автоматически в Awake)

---

## Player Settings (Build Settings → Linux)

- **Scripting Backend**: Mono или IL2CPP
- **Api Compatibility Level**: .NET Standard 2.1 или .NET 4.x
- **Run in Background**: ✓ (чтобы steamcmd не прерывался)
- **Display Resolution Dialog**: Disabled

---

## Конфиг сохраняется в

```
~/.config/SteamPipeGUI/config.json
```

## VDF файлы создаются в

```
/tmp/SteamPipeGUI/
```

---

## Как добавить Settings панель в UXML

В `MainWindow.uxml` добавь внутрь `content-area`:

```xml
<ui:VisualElement name="panel-settings" class="panel">
    <ui:Label text="Настройки" class="panel-title"/>
    <ui:TextField name="field-steamcmd-path" label="Путь к steamcmd" class="input-field"/>
    <ui:Button name="btn-browse-steamcmd" text="Обзор..." class="secondary-button"/>
    <ui:TextField name="field-log-lines" label="Макс. строк лога" value="500" class="input-field"/>
    <ui:Button name="btn-save-settings" text="Сохранить" class="primary-button"/>
    <ui:Button name="btn-clear-log" text="Очистить лог" class="secondary-button"/>
</ui:VisualElement>
```

И добавь кнопку выхода в панель Login:

```xml
<ui:Button name="btn-do-logout" text="Выйти" class="secondary-button"/>
```
