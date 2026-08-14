# AI Skills

DrawnUI ships agent skills — focused instruction files that teach an AI coding agent how this framework actually works, so it stops guessing at APIs and layout rules.

They are maintained in the repository under [`docs/skills/`](https://github.com/taublast/DrawnUi/tree/main/docs/skills) and published here, so the version you download always matches the current codebase.

## Quick install

Paste this into your agent (Claude Code, Cursor, Copilot — anything that can fetch URLs and write files):

```
Fetch https://drawnui.net/llms.txt and install the DrawnUI skills
```

The agent downloads every skill (including multi-file bundles) into its own skills folder. Done.

## Installing manually

Save each skill as `SKILL.md` inside a folder named after the skill:

```
~/.claude/skills/
    drawnui/
        SKILL.md
    drawnui-fluent/
        SKILL.md
```

Claude Code loads them from `~/.claude/skills/`; other agents use their own skills folder — the files are plain markdown with YAML frontmatter and are not tied to any specific agent.

Some skills are multi-file bundles. If a `SKILL.md` links to `references/*.md`, download those into the same folder keeping the relative paths, otherwise the links break.

Agents that understand [llms.txt](https://drawnui.net/llms.txt) can discover and fetch all of this on their own — point yours at `https://drawnui.net/llms.txt`.

## Available skills

| Skill | Load it for |
|---|---|
| [drawnui](https://drawnui.net/skills/drawnui/SKILL.md) | Always, for any DrawnUI work: controls, layouts, caching, gestures, virtualization |
| [drawnui-fluent](https://drawnui.net/skills/drawnui-fluent/SKILL.md) | Writing C# code-behind / fluent composition, porting XAML to C# |
| [drawnui-game](https://drawnui.net/skills/drawnui-game/SKILL.md) | Games on `DrawnUi.Gaming.DrawnGame`: game loop, sprites, pooling, collision, WASM startup |
| [drawnui-blazor](https://drawnui.net/skills/drawnui-blazor/SKILL.md) | Blazor WASM heads: startup, rendering modes, single-threaded divergences, font subsetting, GitHub Pages |
| [drawnui-web-app](https://drawnui.net/skills/drawnui-web-app/SKILL.md) | Pure-WebAssembly `DrawnUi.Web` apps (no Blazor), shared-source projects, WASM bug hunting |
| [drawnui-opentk](https://drawnui.net/skills/drawnui-opentk/SKILL.md) | OpenTK desktop apps: `DrawnUiWindow`, `CanvasHost` GL overlays, window chrome, Linux fixes |
| [drawnui-net-harness](https://drawnui.net/skills/drawnui-net-harness/SKILL.md) | Headless testing and repros — render frames and simulate gestures with no device or GPU |
| [skmech](https://drawnui.net/skills/skmech/SKILL.md) | SkiaSharp `SKMesh` custom mesh drawing with SkSL. Bundle: also fetch [references/api-overview.md](https://drawnui.net/skills/skmech/references/api-overview.md) and [references/examples.md](https://drawnui.net/skills/skmech/references/examples.md) |
| [drawnui-fiddle](https://fiddle.drawnui.net/skills/drawnui-fiddle/SKILL.md) | Driving the in-browser [Fiddle](https://fiddle.drawnui.net) programmatically via its `window.fiddle` API |

Load `drawnui` for everything, then add whichever ones match the target head and the kind of code you are writing.
