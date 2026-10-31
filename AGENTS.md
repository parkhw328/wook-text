# Repository Guidelines

## Required Reading for Every New Task

Before investigating, planning, editing, or running project commands for each new work instruction, read these files in order:

1. [`rules/rules.md`](rules/rules.md): product decisions, development rules, validation, and completion requirements.
2. [`rules/requirements.md`](rules/requirements.md): accumulated user requirements, acceptance criteria, changed decisions, and deferred scope.

Read both again when a new task starts, even if a previous task already used them. Then inspect the relevant source and documentation. Record new or changed rules in `rules/rules.md` and user requirements in `rules/requirements.md` in the same change, preserving the history of superseded decisions. Do not treat deferred candidates as approved work. A newer explicit user instruction takes precedence over an older recorded decision.

## Project Structure & Module Organization

`wook-text` contains **wText**, a Windows x64 text editor built with .NET 10, C#, WPF, and AvalonEdit. The installer uses NSIS following the user's replacement of the original InstallShield requirement.

- `src/WookText.App/`: WPF editor, comparison, explorer, settings, help, and UI assets.
- `src/WookText.Core/`: file encoding, saving, comparison, search, formatting, workspace, and session logic.
- `tests/WookText.Core.Tests/`: xUnit regression tests; `tests/WookText.App.SmokeTests/`: WPF behavior and screenshot checks.
- `scripts/` and `installer/`: development, packaging, and NSIS installation tooling.
- `rules/`: project rules and the canonical requirements record.
- `docs/`: feature guide, embedded user manual, development and release documentation, and screenshots.
- `licenses/` and `THIRD-PARTY-NOTICES.md`: dependency licenses and redistribution notices.
- `README.md`: project entry point and downloads; `CHANGELOG.md`: release history.

Document new directories in `docs/DEVELOPMENT.md` and keep the README's structure summary accurate. Local instruction originals `resources/settings.txt` and `resources/setting.txt` remain ignored and must not be committed.

## Build, Test, and Development Commands

Run these from the repository root in Windows x64 PowerShell:

- `.\scripts\bootstrap.ps1`: prepare the pinned development tools.
- `.\scripts\build.ps1`: restore locked dependencies, build Release, and run Core and WPF checks.
- `.\scripts\run.ps1`: run the editor.
- `.\scripts\package.ps1`: create the self-contained installer and portable ZIP.
- `.\scripts\package.ps1 -SmokeInstaller`, then `.\scripts\test-installer.ps1`: create and validate an isolated test installation.

See `docs/DEVELOPMENT.md` and `docs/RELEASING.md` for prerequisites and Linux limitations. Build and packaging share `obj` directories; do not run them concurrently. The user's CI suspension remains in effect until they explicitly request resumption.

For documentation changes, use:

- `git status --short`: inspect modified and untracked files.
- `git diff`: review unstaged changes to tracked files.
- `git diff --check`: check tracked changes for whitespace errors.
- `git diff --cached --check`: check staged changes, including new files, before committing.

Review untracked files directly and validate relative links. Document verified command changes in the development guide and keep README commands current.

## Coding Style & Naming Conventions

Follow `.editorconfig`: UTF-8, LF, C# indentation of four spaces, and file-scoped namespaces. Use PascalCase for types, methods, and properties; camelCase for parameters; `_camelCase` for private fields. Use `dotnet format` as documented in `docs/DEVELOPMENT.md`. Keep documentation filenames descriptive and Markdown headings concise.

## Testing Guidelines

Name tests `Method_Condition_ExpectedResult`. Include regression tests for file saving, encoding, and comparison changes, and appropriate WPF checks for UI behavior. There is no numeric coverage threshold. For documentation-only changes, check paths, commands, and whitespace without implying application tests were run.

Distinguish Linux Core tests and Windows-targeted compilation from actual Windows WPF, input, and installer execution. Record untested behavior and use verified Windows screenshots for UI documentation. Follow the release checks in `rules/rules.md` and `docs/RELEASING.md` for feature releases.

## Commit & Pull Request Guidelines

Use concise, imperative subjects, such as `Add file comparison tests`. Keep commits focused.

Pull requests should describe the change, reference relevant requirements or issues, and state validation performed. Include screenshots for UI changes and identify untested behavior.

## Licensing & Project Rules

Follow the free-licensing requirement in `rules/requirements.md`; check dependency and redistribution licenses before adoption. Distribute project code under MIT and preserve bundled notices. Record every release in `CHANGELOG.md`. Keep requirements in `rules/requirements.md`; `docs/REQUIREMENTS.md` only points to that canonical file. Do not modify `AGENTS.md` or local instruction originals without an explicit user request.
