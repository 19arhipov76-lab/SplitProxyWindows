# Сборка Split Proxy Windows

## 1. Что установить на ПК разработчика

Нужно только для сборки, не для конечного пользователя:

1. Windows 10/11 x64.
2. Visual Studio 2022/актуальный Visual Studio с workload **Desktop development with .NET** и **Desktop development with C++**.
3. .NET 10 SDK.
4. CMake 3.20+ (можно компонент Visual Studio).
5. Git.
6. Inno Setup 6.

## 2. Сборка одной командой

Откройте **Developer PowerShell for Visual Studio** от обычного пользователя и выполните:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
cd C:\path\to\SplitProxyWindows-v0.1.0
.\scripts\Build-All.ps1
```

Скрипт:

1. скачает и зафиксирует `wfp-divert` на commit `8179c800...`;
2. скачает WinDivert 2.2.2-A и проверит SHA-256;
3. соберёт нативный C++ сетевой движок x64;
4. опубликует App/Service/Updater как self-contained .NET 10;
5. соберёт один установщик `dist\SplitProxySetup.exe`.

## 3. Что получает пользователь

Пользователю отдаётся только:

```text
SplitProxySetup.exe
```

Установка требует UAC, потому что ставится Windows-служба и используется подписанный WinDivert-драйвер. Отдельно .NET, Visual C++ Redistributable, WinDivert, CMake и другие библиотеки устанавливать не нужно.

## 4. Проверка после установки

```powershell
sc.exe query SplitProxyService
Get-Process SplitProxy -ErrorAction SilentlyContinue
```

В `%ProgramData%\SplitProxy` будут:

- `client-state.json` — выбранные программы и текущий SOCKS5 endpoint без секретов;
- `service-status.json` — состояние службы;
- `engine.log`, `engine-error.log` — диагностика сетевого движка.

Секретные токены и закрытый ECDSA-ключ находятся только в `%LocalAppData%\SplitProxy` и защищены DPAPI.

## 5. Подписание релиза

Перед публичным выпуском желательно подписывать `SplitProxySetup.exe`, `SplitProxy.exe`, `SplitProxy.Service.exe` и `SplitProxy.Updater.exe` Authenticode-сертификатом. WinDivert 2.2.2-A уже содержит подписанный драйвер, но для коммерческого релиза следует отдельно проверить лицензионные требования WinDivert.

## 6. Сборка через GitHub Actions

В комплекте есть `.github/workflows/build-windows.yml`. После загрузки исходников в GitHub можно запустить **Actions → Build Split Proxy Windows → Run workflow**. Результатом будет artifact `SplitProxy-Windows-Setup` с одним `SplitProxySetup.exe`.
