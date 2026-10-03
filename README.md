# Sagi Block

Native helper for blocking scareware pages and cleaning scam notification permissions.

Initial target:

- Windows native tray app
- macOS folder reserved for the later Network Extension / browser support plan

The Windows app checks every 30 seconds:

- suspicious browser windows that look like fake Microsoft / Defender warning pages
- suspicious allowed web notification origins in Chromium-based browser profiles

## License

[MIT License](LICENSE) — use, modify, and redistribute freely (keep copyright and license notice).
