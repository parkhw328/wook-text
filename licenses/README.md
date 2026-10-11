# Bundled notices

- `AvalonEdit.txt`: upstream MIT notice for AvalonEdit 6.3.1.120.
- `DiffPlex.txt`: upstream Apache-2.0 license for DiffPlex 1.9.0.
- `Prettier.txt` and `Prettier-THIRD-PARTY-NOTICES.md`: unmodified notices from the verified Prettier 3.9.6 npm archive. Bundled parser dependencies use MIT, ISC, BSD, Apache-2.0, and BlueOak-1.0.0 licenses allowing redistribution with notices.
- `Nodejs.txt`: complete [Node.js v26.10.0 license](https://github.com/nodejs/node/blob/v26.10.0/LICENSE), including embedded third-party notices. SHA-256: `6b85ee1983d01fa2cdcdd5d1a3053c6221fae41d6a54e43b416bf35969fc4128`.
- `NSIS.txt`: `COPYING` from the NSIS 3.13 compiler distribution.
- `JetBrainsMono-OFL.txt`: SIL Open Font License 1.1 for unmodified JetBrains Mono 2.304 Regular/Bold; copied with the fonts from the local wShell repository, originally from [JetBrains Mono](https://github.com/JetBrains/JetBrainsMono).
- `NotoSansKR-OFL.txt`: [upstream OFL](https://github.com/notofonts/noto-cjk/blob/main/Sans/LICENSE) for Noto Sans KR 2.004 Regular/Bold, from the official `Sans/SubsetOTF/KR/` distribution.
- `Flexoki-MIT.txt`: Steph Ango's MIT notice for the Flexoki palette, preserved from the local wShell reference project.
- `WPF-THIRD-PARTY-NOTICES.txt`: [WPF v10.0.12 notices](https://github.com/dotnet/wpf/blob/v10.0.12/THIRD-PARTY-NOTICES.TXT).
- `WindowsForms-THIRD-PARTY-NOTICES.txt`: [Windows Forms v10.0.12 notices](https://github.com/dotnet/winforms/blob/v10.0.12/THIRD-PARTY-NOTICES.TXT), retained for the bundled Windows Desktop runtime.

`scripts/package.ps1` also copies the exact .NET and Windows Desktop runtime
licenses, plus the .NET runtime third-party notices, from their NuGet packages
into the release's `licenses` directory. Review and update the checked-in desktop
notices when changing the runtime version. Keep upstream license text unmodified.
