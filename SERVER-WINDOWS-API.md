# Что требуется от split-proxy.ru для Windows-клиента

## Активация

Клиент использует уже существующий поток:

```text
POST /api/v1/auth/login
POST /api/v1/auth/register
POST /api/v1/devices/activation/request   platform=windows
POST /api/v1/devices/activation/approve
POST /api/v1/devices/activation/finish
GET  /api/v1/devices/proxy/config?country=NL
```

`ActiveRegisteredDeviceAuth` на сервере должен разрешать `windows` наряду с `browser` и `android`.

## Конфигурация Windows

Для Windows ответ должен быть отдельной веткой, без PAC и без списка браузерных сайтов:

```json
{
  "status": "ready",
  "mode": "process_proxy",
  "platform": "windows",
  "install_id": "...",
  "plan": "...",
  "exit_country": "NL",
  "available_exit_countries": ["NL"],
  "proxy": {
    "server": "proxy.example.net",
    "port": 1080,
    "protocol": "socks5",
    "entry_node": "RF2",
    "exit_node": "ND",
    "exit_country": "NL",
    "checked_at": "2026-10-15T00:00:00Z"
  }
}
```

### Почему только SOCKS5

Нативный Windows-движок маршрутизирует TCP и UDP выбранных процессов. HTTP CONNECT не умеет переносить произвольный UDP. Поэтому Windows-конфигурация не должна выдавать `http`/`https`, иначе клиент намеренно откажется включать маршрут, чтобы не было утечки UDP напрямую.

## Проверка обновлений

Клиент вызывает каждые 2 часа:

```text
GET /api/v1/apps/version?platform=windows&version=0.1.0
```

Рекомендуемый ответ:

```json
{
  "update_available": true,
  "latest_version": "0.1.1",
  "download_url": "https://split-proxy.ru/download/windows/SplitProxySetup.exe",
  "sha256": "HEX_SHA256",
  "mandatory": false,
  "notes": "Описание изменений"
}
```

Клиент также понимает альтернативные поля `version`, `url`, `installer_url`, `checksum_sha256`, но лучше использовать схему выше.
