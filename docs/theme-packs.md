# Vibe Panel theme packs

Vibe Panel is the working product name. `VibePanel` remains the repository and code identifier.

Each pack contains independently selectable themes. The first categories are `multimedia` (Multimedia) and `codex` (OpenAI Codex). A pack can contain either category or both; installing a theme does not install its data provider.

The native engine discovers local manifests and sends the same catalog to the desktop viewer and CLI. Each theme has its own folder, stable ID, display name, category and rendering entry. The viewer selects category first and then a theme from that category.

Python themes receive generic media and activity data. Source integrations stay outside the theme, and unavailable data is shown explicitly. Light and dark appearance and font preferences belong to shared theme settings.
