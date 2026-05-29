# TripoGame Agent Notes

## Project

- Unity project root: `E:\Unity\Porject\TripoGame`
- Unity Editor: `E:\Unity\6000.4.8f1\Editor\Unity.exe`
- Project version: Unity `6000.4.8f1`
- Render pipeline: URP `17.4.0`

## Bridge

This project uses CoplayDev MCP for Unity, pinned for reproducibility and embedded locally:

```text
Packages/com.coplaydev.unity-mcp
source: CoplayDev/unity-mcp MCPForUnity at tag v9.7.1
```

Codex project-local MCP config lives at:

```text
E:\Unity\Porject\TripoGame\.codex\config.toml
```

The same `unityMCP` server is also registered in global Codex config at `C:\Users\Administrator\.codex\config.toml`, because the current Codex desktop session did not reliably inject project-local MCP entries from `.codex/config.toml` during validation.

Current working transport is HTTP Local:

```toml
[mcp_servers.unityMCP]
url = "http://127.0.0.1:8080/mcp"
```

After opening a new Codex session in this folder, trust the project config if prompted so Codex loads the Unity MCP server.

## Startup Checklist

1. Open the project in Unity `6000.4.8f1`.
2. In Unity, open `Window -> MCP for Unity`.
3. Confirm dependencies are green and HTTP Local shows `Session Active (TripoGame)`.
4. If the setup wizard appears, let it install or validate Python/uv dependencies.
5. In Codex, confirm the `unityMCP` MCP server is loaded before asking for live Editor changes.

## Working Rules

- Prefer MCP/Unity Editor API inspection over guessing from files when working on scenes, prefabs, materials, packages, console errors, or play/test state.
- Keep generated/editor-only artifacts out of git; `Library`, `Temp`, `Logs`, and `UserSettings` are ignored. The embedded bridge package under `Packages/com.coplaydev.unity-mcp` is project source and should stay tracked.
- Make small, verifiable Unity changes and use the bridge to read the Console and run tests after edits.
- For designer-facing fields in C# or ScriptableObjects, add a clear `[Tooltip]` explaining what the value changes and useful units/ranges.
- Do not rely on Unity official AI MCP as the default bridge unless the user explicitly wants Unity AI subscription/cloud setup.

## Known Caveats

- The project folder is intentionally spelled `Porject`, matching the existing path.
- If Codex starts before Unity is open, the MCP server may load but report no active Unity instance until the Editor plugin connects.
- The official Unity MCP requires Unity AI subscription/seat/cloud setup; this project uses the local open-source MCP bridge instead.
- The MCP for Unity `Install Skills` step is optional; failure there does not block normal scene, prefab, console, or test operations through MCP.
