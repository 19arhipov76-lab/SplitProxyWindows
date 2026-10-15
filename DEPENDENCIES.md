# Third-party dependencies

Split Proxy Windows v0.1.1 builds the route engine from:

- `okatodev/wfp-divert`, pinned commit `8179c800dafa9efe8f8f27d34098b306c7affd7b` (MIT).
- WinDivert `2.2.2`, official binary release `WinDivert-2.2.2-A.zip`.

The build applies `patches/wfp-divert-socks-auth.patch` to add RFC 1929 SOCKS5 username/password authentication for both TCP CONNECT and UDP ASSOCIATE. Credentials are supplied to the native engine through child-process environment variables and are not written to the route engine JSON configuration.

WinDivert 2.2.2 publishes the binary release asset but does **not** publish the previously referenced `WinDivert-2.2.2-Source.zip`; that invalid URL was the cause of the first GitHub Actions build failure. The corresponding source tag/commit is recorded in `licenses/WinDivert-SOURCE.txt` during build.
