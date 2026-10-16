# VBWR B
# Project: VibePanel | https://umbertogiacobbi.biz/vibeware/manifesto
# Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
# VBWR E
"""Stage the optional local Codex plugin for manual install and hook trust."""

from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import shutil
import sys
import tempfile
from uuid import uuid4


PLUGIN_NAME = "vibepanel-codex"
SOURCE_PATH = "./.codex/plugins/vibepanel-codex"


def locations() -> tuple[Path, Path]:
    home = Path.home()
    return (home / ".codex" / "plugins" / PLUGIN_NAME,
            home / ".agents" / "plugins" / "marketplace.json")


def read_marketplace(path: Path) -> dict[str, object]:
    if not path.exists():
        return {"name": "vibepanel-local", "plugins": []}
    if path.stat().st_size > 1_048_576:
        raise ValueError("Personal marketplace is too large to edit automatically.")
    data = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(data, dict) or not isinstance(data.get("name"), str) \
            or not isinstance(data.get("plugins"), list):
        raise ValueError("Personal marketplace has an unsupported shape; no changes were made.")
    return data


def atomic_json(path: Path, value: dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + "." + uuid4().hex + ".tmp")
    try:
        temporary.write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)


def plugin_entry() -> dict[str, object]:
    return {
        "name": PLUGIN_NAME,
        "source": {"source": "local", "path": SOURCE_PATH},
        "policy": {"installation": "AVAILABLE", "authentication": "ON_INSTALL"},
        "category": "Productivity",
        "interface": {"displayName": "VibePanel Codex"},
    }


def stage_plugin(source: Path, destination: Path) -> None:
    if destination.exists():
        manifest = destination / "plugin.json"
        if not manifest.is_file() or json.loads(manifest.read_text(encoding="utf-8")).get("name") != PLUGIN_NAME:
            raise ValueError("The destination already contains another plugin; no changes were made.")
        return
    destination.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix=".vibepanel-", dir=destination.parent) as staging_root:
        staged = Path(staging_root) / PLUGIN_NAME
        shutil.copytree(source, staged, ignore=shutil.ignore_patterns("__pycache__", "*.pyc"))
        os.replace(staged, destination)


def prepare() -> None:
    if sys.version_info < (3, 10):
        raise ValueError("Python 3.10 or newer is required for the VibePanel Codex hook.")
    source = Path(__file__).resolve().parent
    destination, marketplace_path = locations()
    marketplace = read_marketplace(marketplace_path)
    plugins = marketplace["plugins"]
    assert isinstance(plugins, list)
    for entry in plugins:
        if isinstance(entry, dict) and entry.get("name") == PLUGIN_NAME:
            if entry.get("source") != plugin_entry()["source"]:
                raise ValueError("A different VibePanel Codex entry already exists; no changes were made.")
            stage_plugin(source, destination)
            print("VibePanel Codex is already listed in the personal marketplace.")
            return
    stage_plugin(source, destination)
    plugins.append(plugin_entry())
    if marketplace_path.exists():
        backup = marketplace_path.with_name("marketplace.json.vibepanel.bak")
        if not backup.exists():
            shutil.copy2(marketplace_path, backup)
    atomic_json(marketplace_path, marketplace)
    print(f"Staged {destination}")
    print("Restart Codex, install VibePanel Codex from your local Plugins Directory, then review and trust its hooks.")


def status() -> None:
    destination, marketplace_path = locations()
    marketplace = read_marketplace(marketplace_path)
    entries = marketplace["plugins"]
    assert isinstance(entries, list)
    listed = any(isinstance(item, dict) and item.get("name") == PLUGIN_NAME for item in entries)
    print(f"Plugin files: {'present' if destination.is_dir() else 'absent'}")
    print(f"Personal marketplace: {'listed' if listed else 'not listed'}")
    print("Codex installation and hook trust must be checked inside Codex.")


def unlist() -> None:
    _, marketplace_path = locations()
    marketplace = read_marketplace(marketplace_path)
    entries = marketplace["plugins"]
    assert isinstance(entries, list)
    remaining = [item for item in entries if not (isinstance(item, dict) and item.get("name") == PLUGIN_NAME)]
    if len(remaining) == len(entries):
        print("VibePanel Codex is not listed in the personal marketplace.")
        return
    marketplace["plugins"] = remaining
    atomic_json(marketplace_path, marketplace)
    print("Removed VibePanel Codex from the personal marketplace; staged files were preserved.")


def main() -> int:
    parser = argparse.ArgumentParser(description="Prepare my optional VibePanel Codex plugin")
    parser.add_argument("action", choices=("status", "prepare", "unlist"), nargs="?", default="status")
    args = parser.parse_args()
    try:
        {"status": status, "prepare": prepare, "unlist": unlist}[args.action]()
        return 0
    except (OSError, ValueError, json.JSONDecodeError) as error:
        print(f"VibePanel Codex: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
