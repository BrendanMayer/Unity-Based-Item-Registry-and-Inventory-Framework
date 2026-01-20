# Unity Item Registry and Inventory Toolchain

## Overview

This project is a custom Unity Editor toolchain designed to streamline the creation, management, and validation of in-game items. It replaces manual asset setup with automated editor workflows, reducing human error and improving consistency across large item collections.

The system includes an item registry for unique ID management, an item creation wizard, validation utilities, and integration with runtime inventory and container systems.

## Features

### Item Creation Wizard
- Custom EditorWindow for creating new `ItemData` assets
- Automatically creates required folders and assets
- Assigns unique item IDs through a central registry
- Prevents ID duplication and offers conflict resolution when needed

### Item ID Registry
- Centralized registry asset that tracks all used item IDs
- Automatically assigns the next available ID
- Can be rebuilt by scanning existing items in the project
- Eliminates manual ID assignment and related errors

### Validation Tools
Scans all item assets and reports issues such as:
- Invalid or missing item IDs
- Folder names not matching item names
- Missing prefabs for placeable items
- Incorrect file paths or asset structure

Results are displayed in a simple editor window for quick iteration and fixes.

### Editor Utilities
- Menu options to rebuild the item registry
- Quick access to open the registry in the Inspector
- One-click maintenance tools to support refactors or asset imports

### Runtime Integration
- Items are loaded dynamically at runtime using their assigned IDs
- Integrated inventory and container systems
- Designed to support persistent saving and loading (e.g. bundled save files, Steam Cloud compatibility)

## Technologies Used

- C#
- Unity Editor Scripting
- `EditorWindow`, `MenuItem`, `EditorGUILayout`
- `AssetDatabase`
- ScriptableObjects
- Custom tooling and workflow automation

## Goals and Design Philosophy

- Reduce repetitive manual work in Unity
- Prevent common asset and data errors early in development
- Create scalable tooling that supports growing content pipelines
- Keep tools simple, fast, and tightly integrated with Unity’s editor workflow

## Status

The core editor tools, registry system, and runtime inventory integration are complete. Ongoing work includes expanding save/load functionality and improving long-term data persistence.
