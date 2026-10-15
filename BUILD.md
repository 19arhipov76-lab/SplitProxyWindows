# Сборка Split Proxy Windows 0.1.1

## GitHub Actions — рекомендуемый способ

Загрузите содержимое этого исходного архива в корень репозитория и запустите:

**Actions → Build Split Proxy Windows → Run workflow**

Workflow использует Windows Server 2025, .NET 10, CMake/MSVC и Inno Setup. Результат — artifact `SplitProxy-Windows-Setup` с файлом:

```text
SplitProxySetup.exe
```

В v0.1.1 исправлена первая ошибка сборки: WinDivert 2.2.2 не имеет release asset `WinDivert-2.2.2-Source.zip`. Сборка теперь использует существующий официальный `WinDivert-2.2.2-A.zip` и фиксированный SHA-256. Также workflow обновлён на актуальные major actions, чтобы убрать предупреждение Node.js 20.

## Локальная сборка

На ПК разработчика нужны:

1. Windows 10/11 x64.
2. Visual Studio с **Desktop development with .NET** и **Desktop development with C++**.
3. .NET 10 SDK.
4. CMake 3.20+.
5. Git.
6. Inno Setup 6.

Developer PowerShell:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
cd C:\path\to\SplitProxyWindows-v0.1.1
.\scripts\Build-All.ps1
```

Скрипт автоматически:

1. клонирует `wfp-divert` на commit `8179c800dafa9efe8f8f27d34098b306c7affd7b`;
2. применяет `patches/wfp-divert-socks-auth.patch`;
3. скачивает WinDivert 2.2.2-A и проверяет SHA-256;
4. собирает C++ route engine x64;
5. публикует App/Service/Updater как self-contained .NET 10;
6. собирает `dist\SplitProxySetup.exe`.

## На ПК пользователя

Пользователю нужен только `SplitProxySetup.exe`. Дополнительные runtime/библиотеки вручную не устанавливаются.

После установки:

```powershell
sc.exe query SplitProxyService
Get-Process SplitProxy -ErrorAction SilentlyContinue
```

Данные:

- `%ProgramData%\SplitProxy\client-state.json` — содержимое защищено DPAPI LocalMachine;
- `%ProgramData%\SplitProxy\service-status.json` — состояние службы;
- `%ProgramData%\SplitProxyService\engine\config.json` — конфиг route engine без SOCKS5 password;
- `%ProgramData%\SplitProxyService\engine.log` и `engine-error.log` — диагностика;
- `%LocalAppData%\SplitProxy` — account/device secrets, DPAPI CurrentUser.

## Релиз

Перед публичной раздачей желательно Authenticode-подписание собственных EXE и установщика. WinDivert поставляется с его подписанным драйвером.
