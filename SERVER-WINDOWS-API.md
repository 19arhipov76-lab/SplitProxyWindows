# Windows API contract (current)

Windows authenticates exactly like the registered Android client, but with device `platform=windows`.

Flow:

```text
POST /api/v1/auth/login or /auth/register
POST /api/v1/devices/activation/request   platform=windows
POST /api/v1/devices/activation/approve
POST /api/v1/devices/activation/finish
GET  /api/v1/devices/proxy/config?country=NL
```

Proxy config request headers:

```text
X-Device-Key: SP-...
Authorization: Bearer <device_token>
```

Windows requires a SOCKS5 response:

```json
{
  "status": "ready",
  "mode": "vpn",
  "platform": "windows",
  "exit_country": "NL",
  "proxy": {
    "entry_node": "rf1",
    "exit_node": "nd",
    "exit_country": "NL",
    "server": "195.209.208.251",
    "port": 29443,
    "protocol": "socks5",
    "username": "...",
    "password": "..."
  }
}
```

The controller may return another healthy entry node (for example RF2). The route is sticky per device. Browser/Android endpoints remain HTTP/HTTPS; Windows requests/receives SOCKS5 only.
