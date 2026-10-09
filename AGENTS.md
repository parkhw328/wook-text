# Repository Guidelines

## Project Structure & Module Organization

`wook-text` is currently a planning-stage repository for a Windows x64 text editor. Planned features include two-file text comparison and an InstallShield installer.

- `README.md`: project entry point; currently contains only the project name.
- `resources/settings.txt`: Korean-language project rules and product requirements.
- `rules/rules.md`: designated location for future project rules; not yet present.

No application source, tests, or UI assets exist yet. When adding them, use clearly named directories and document their responsibilities in `README.md`.

## Build, Test, and Development Commands

No build system, dependency manifest, development server, or test runner is configured. From the repository root, use:

- `git status --short`: inspect modified and untracked files.
- `git diff`: review unstaged changes to tracked files.
- `git diff --check`: check tracked changes for whitespace errors.

Review new files directly; these diff commands omit untracked files. Add verified build, run, test, and installer commands to `README.md` when introducing tooling.

## Coding Style & Naming Conventions

No programming language, indentation standard, formatter, or linter has been selected. Preserve existing formatting, save documentation as UTF-8, and use descriptive filenames and concise Markdown headings. When introducing source code, establish language-appropriate naming and indentation conventions alongside formatter or linter configuration.

## Testing Guidelines

No testing framework or coverage threshold exists. For documentation changes, check paths, command accuracy, and whitespace. When implementing the editor, add tests for file opening and saving, text encoding, and two-file comparison. Document the chosen test naming convention and execution command. Validate Windows x64 behavior and installation when those components exist.

## Commit & Pull Request Guidelines

Git history contains only `Initial commit`, so no established message convention exists. Use concise, imperative subjects, such as `Add file comparison tests`. Keep commits focused.

Pull requests should describe the change, reference relevant requirements or issues, and state validation performed. Include screenshots for UI changes and identify untested behavior.

## Licensing & Project Rules

Follow the free-licensing requirement in `resources/settings.txt`; check dependency and redistribution licenses before adoption. Record version history for every release, and place new project rules in `rules/rules.md`, creating it when needed.
