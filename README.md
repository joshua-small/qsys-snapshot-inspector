# Snapshot Inspector

Open **Tools > Q-SYS Mods > Snapshot Inspector** to inspect snapshot membership, navigate to controls, create a page of linked controls, or compare saved values without recalling a snapshot. Membership dots, hover information, and native snapshot sidebar actions move with this mod.

Inspector and comparison windows use Designer's live light/dark styles and themed grids. Differing comparison cells have an orange outline while preserving the theme's text and background. Current values follow QDS feedback; saved columns refresh on request. The existing 5,000-control and 24-slot limits remain. JSON snapshot variants are not decoded by the comparison reader.

Requires [Q-SYS Mods Core](https://github.com/joshua-small/qsys-mods-core) 0.1.x, Mod Manager 0.8+, and the exact supported Designer 10.5 build. Use the bundle migration to prevent duplicate hooks. Better Shortcuts is optional.

Clone Core beside this repository, then build `Designer.Modules.SnapshotInspector.csproj` with .NET 10.0.112 and local `DesignerDir` references. `tests/SnapshotPayloadChecks` takes a retained native fixture directory as its sole argument; its 23 checks pass against long UTF-8 text, scalar, boolean, global, and malformed payload cases. Fixtures and Q-SYS binaries stay local. Core's native driver checks the same inspector across a live theme toggle. Hardware/Core transport behavior has not been revalidated by the split.

Extracted from Schematic Tools commit `13c52e6`. [Split tracking](https://github.com/joshua-small/qsys-schematic-tools/issues/4).
