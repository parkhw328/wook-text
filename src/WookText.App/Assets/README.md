# wText visual assets

The theme follows wShell's warm charcoal/orange palette: background `#100F0F`,
panels `#1C1B1A` / `#282726`, text `#CECDC3`, accent `#DA702C`. Flexoki's MIT
notice is included in `licenses/Flexoki-MIT.txt` at the repository root.

## Icon

`../Themes/Branding.xaml` is the original vector source. The orange folded
document and dark **T** distinguish wText from wShell's orange lowercase **w**.
The same mark appears in the header, About dialog, window/taskbar, executable,
installer and uninstaller.

Run `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/make-icon.ps1`
from the repository root to regenerate `wText.png` and `wText.ico`. The ICO
contains 16, 20, 24, 32, 40, 48, 64, 128 and 256 px frames. Do not hand-edit
generated frames; update the vector source first.

## Fonts

The unmodified fonts below are loaded from `Assets/Fonts` beside the application.
Regular and Bold cover normal text and syntax emphasis. Keep this directory in
portable distributions. Latin text defaults to JetBrains Mono; Hangul Unicode
ranges explicitly use Noto Sans KR. No system font installation is required.

- JetBrains Mono **2.304**: copied from `C:\Project\wook-shell\assets\fonts`,
  upstream [JetBrains Mono](https://github.com/JetBrains/JetBrainsMono/tree/v2.304/fonts/ttf).
- Noto Sans KR **2.004**: official
  [Korean subset OTF files](https://github.com/notofonts/noto-cjk/tree/main/Sans/SubsetOTF/KR),
  downloaded on 2026-10-09.
- Both are SIL OFL 1.1. Preserve `licenses/JetBrainsMono-OFL.txt` and
  `licenses/NotoSansKR-OFL.txt` in every distribution.

| File | SHA-256 |
| --- | --- |
| `JetBrainsMono-Regular.ttf` | `a0bf60ef0f83c5ed4d7a75d45838548b1f6873372dfac88f71804491898d138f` |
| `JetBrainsMono-Bold.ttf` | `5590990c82e097397517f275f430af4546e1c45cff408bde4255dad142479dcb` |
| `NotoSansKR-Regular.otf` | `69975a0ac8472717870aefeab0a4d52739308d90856b9955313b2ad5e0148d68` |
| `NotoSansKR-Bold.otf` | `5a6ceb287ed2fc6cfc6213144ebea68cbd94b20fc9eb873d8486493bf02d9bda` |
