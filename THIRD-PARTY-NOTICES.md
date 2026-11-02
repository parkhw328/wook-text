# wText third-party notices

wText's original code is licensed under the MIT License. Dependencies retain
their own licenses and copyright notices. No Sublime Text or Notepad++ source,
artwork, trademarks, or binaries are included.

| Component | Version | License | Use |
| --- | --- | --- | --- |
| [AvalonEdit](https://github.com/icsharpcode/AvalonEdit) | 6.3.1.120 | MIT | Editing and syntax highlighting |
| [Prettier](https://github.com/prettier/prettier) | 3.9.6 | MIT and bundled third-party notices | Offline document formatting |
| [Node.js](https://nodejs.org/) | 26.10.0 | MIT and bundled third-party notices | Private Windows x64 formatting runtime |
| [PDFium binaries](https://github.com/bblanchon/pdfium-binaries) | 157.0.8100 (chromium/8100) | MIT packaging; PDFium BSD/Apache-2.0 and bundled component licenses | PDF rendering, extraction and editing |
| [Markdig](https://github.com/xoofx/markdig) | 1.5.1 | BSD-2-Clause | Markdown parsing |
| [WebView2 SDK](https://www.nuget.org/packages/Microsoft.Web.WebView2) | 1.0.4314.35 | BSD-3-Clause | WPF browser control |
| [WebView2 Fixed Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) | 156.0.4314.58 x64 | Microsoft Software License Terms; bundled component notices | Private static document preview runtime |
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

Formatter licenses were reviewed on 2026-10-11. Prettier standalone and parser
bundles retain their full upstream notices in `licenses/Prettier.txt` and
`licenses/Prettier-THIRD-PARTY-NOTICES.md`. The complete Node.js license, including
its embedded dependencies, is in `licenses/Nodejs.txt`. These permit free
redistribution with the included notices; neither tool is installed system-wide.
Downloads and checksums are pinned in `scripts/prepare-formatter.ps1`.

PDF and preview dependencies were reviewed on 2026-11-02. Windows x64 uses
`bblanchon.PDFium.Win32`; `bblanchon.PDFium.Linux` supplies the engine for local
Core tests. The Windows package contains only its target native runtime.
PDFium is accessed through wText's own C API declarations, without PDFiumCore or
a commercial SDK. The exact chromium/8100 distribution's LICENSE and all files
from its licenses directory are retained in `licenses/PDFium/` and collected in
`licenses/PDFium-NOTICES.txt` for the in-app license viewer. Markdig's license is
in `licenses/Markdig.txt`. PDFium's font embedding uses the unmodified bundled
Noto Sans KR font under its existing OFL notice.

WebView2 was reviewed on 2026-11-02. `licenses/WebView2-SDK.txt` is the SDK's
original license. `licenses/WebView2-Runtime.txt` contains the Fixed Version terms
retrieved from Microsoft's official endpoint:
https://developer.microsoft.com/microsoft-edge/api/eula/webview2?locale=en-us&fixed=true
The runtime is acquired directly from Microsoft, unmodified, and included in full
under `WebView2Runtime/`; it is licensed separately from wText's MIT code. The app
asks for acceptance before first use, including for portable users. Distributors
must preserve these terms and the end-user acceptance requirement. Section 2 of
the runtime terms governs redistribution. The runtime's supplied
`show_third_party_software_licenses.bat` opens its embedded third-party notices.

The preview includes Microsoft Defender SmartScreen, which collects and sends
end-user information to Microsoft as described in Microsoft's privacy statement
(https://aka.ms/privacy) and Edge privacy whitepaper
(https://learn.microsoft.com/en-us/microsoft-edge/privacy-whitepaper#smartscreen).
The runtime may also collect usage information as described in its terms.
Document JavaScript and external page resources are disabled; this is separate
from the runtime's own diagnostic and SmartScreen services.
