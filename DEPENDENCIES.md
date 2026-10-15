# Зафиксированные зависимости

## .NET

- Target: `.NET 10` / `net10.0-windows`.
- App/Service/Updater публикуются `self-contained win-x64`.

## Сетевой движок

- `okatodev/wfp-divert`
- commit: `8179c800dafa9efe8f8f27d34098b306c7affd7b`
- license: MIT
- используется как C++17 TCP/UDP per-application SOCKS5 redirector.

## WinDivert

- Version: `2.2.2-A`
- Binary archive SHA-256: `63cb41763bb4b20f600b6de04e991a9c2be73279e317d4d82f237b150c5f3f15`
- Source archive SHA-256: `65ec79c9e6afa99f648a3f4d1f6db794640b40d0b65bd438770ea503ee14ecb7`
- Используется динамически (`WinDivert.dll`) вместе с подписанным `WinDivert64.sys`.
- Скрипт сборки кладёт точный source archive в релиз для соблюдения условий LGPL.

Версии зафиксированы намеренно: обновлять зависимости нужно отдельным осознанным изменением с повторным тестированием Windows 10/11.
