# Contributing

Thanks for your interest in contributing to DrawnUI.

## Reporting Issues

- Check existing issues first
- Include platform (iOS/Android/Windows/Mac)
- Provide minimal reproduction code
- Mention DrawnUI version and .NET version

## Pull Requests

- Fork the repo and create a branch
- Keep changes focused on a single fix or feature
- Test on at least one platform
- Follow existing code style
- Update docs if adding features

## Code Guidelines

- Don't disable or remove features to fix bugs
- Avoid allocations during frame rendering (minimize GC)
- Make methods virtual when possible for extensibility
- Cache drawing operations where appropriate

## Build

```bash
dotnet build src/DrawnUi.Maui.sln
```

See [CLAUDE.md](CLAUDE.md) for project structure and architecture details.

## Questions

Use [GitHub Discussions](https://github.com/taublast/DrawnUi/discussions) for questions about contributing.
