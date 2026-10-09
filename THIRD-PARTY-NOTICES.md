# wText third-party notices

wText's original code is licensed under the MIT License. Dependencies retain
their own licenses and copyright notices. No Sublime Text or Notepad++ source,
artwork, trademarks, or binaries are included.

| Component | Version | License | Use |
| --- | --- | --- | --- |
| [AvalonEdit](https://github.com/icsharpcode/AvalonEdit) | 6.3.1.120 | MIT | Editing and syntax highlighting |
| [DiffPlex](https://github.com/mmanela/diffplex) | 1.9.0 | Apache-2.0 | Text comparison |
| [.NET / WPF](https://github.com/dotnet/wpf) | 10.0 | MIT and bundled third-party notices | Windows runtime |
| [NSIS](https://nsis.sourceforge.io/License) | 3.13 | zlib/libpng; component licenses | Installer; zlib compression |
| [JetBrains Mono](https://github.com/JetBrains/JetBrainsMono) | 2.304 | SIL OFL 1.1 | Bundled Regular and Bold fonts for Latin text |
| [Noto Sans KR](https://github.com/notofonts/noto-cjk) | 2.004 | SIL OFL 1.1 | Bundled Regular and Bold fonts for Hangul |
| [Flexoki](https://github.com/kepano/flexoki) | Palette snapshot, 2026-10-09 | MIT | Warm dark/orange palette, referenced through wShell |

Full notices are distributed in `licenses/`. The self-contained release includes
the .NET runtime and Windows Desktop third-party notices in that directory.
The installer uses NSIS's zlib compressor, without additional third-party plugins.
Fonts are unmodified, distributed as part of the application, and loaded privately
from `Assets/Fonts`. They are not installed system-wide. Their licenses permit
bundling and redistribution with the included copyright and OFL notices; the font
files retain their own license. Font origins and SHA-256 hashes are recorded in
`src/WookText.App/Assets/README.md` in the source repository.

Bundled font copyright notices (also retained in the font metadata):

- JetBrains Mono: Copyright 2020 The JetBrains Mono Project Authors (https://github.com/JetBrains/JetBrainsMono).
- Noto Sans KR: © 2014-2021 Adobe (http://www.adobe.com/).

The wText document/T icon is original vector artwork. It shares wShell's palette
but does not reuse wShell's lowercase-w mark.

Development-only tools: .NET SDK and Microsoft.NET.Test.Sdk (MIT), xUnit.net and
its Visual Studio runner (Apache-2.0). They are not included in the application.
Resolved application and test dependencies are pinned in `packages.lock.json`.

Source references were checked on 2026-10-09. Preserve these notices when
redistributing wText, and recheck licenses when adding or updating dependencies.
