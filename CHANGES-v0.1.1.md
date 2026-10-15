# v0.1.1

- Windows SOCKS5 username/password from server API.
- RFC 1929 authentication in native route engine for TCP and UDP.
- Proxy credentials no longer written to route-engine JSON.
- Shared client state protected with DPAPI LocalMachine so desktop app and LocalSystem service can share it.
- Route engine work/log directory moved to `%ProgramData%\SplitProxyService`.
- Fixed WinDivert dependency download: removed nonexistent `WinDivert-2.2.2-Source.zip` release asset.
- GitHub Actions updated from Node.js-20-era action majors.
- Version bumped to 0.1.1.
