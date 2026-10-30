# Snapshot Inspector

Open **Tools > Q-SYS Mods > Snapshot Inspector** to inspect snapshot membership, navigate to controls, create a page of linked controls, or compare saved values without recalling a snapshot. Membership dots, hover information, and native snapshot sidebar actions move with this mod.

Inspector and comparison windows use Designer's live light/dark styles and themed grids. Differing comparison cells have an orange outline while preserving the theme's text and background. Current values follow QDS feedback; saved columns refresh on request. The existing 5,000-control and 24-slot limits remain. JSON snapshot variants are not decoded by the comparison reader.

Requires [Q-SYS Mods Core](https://github.com/joshua-small/qsys-mods-core) 0.1.10 or newer within 0.1.x and Mod Manager 0.10.0+. The package retains its Designer 10.5.0 baseline. Startup on the exact 10.5.1 host/common pair also requires Mod Manager's installation-specific exact-build consent through Core; consent does not establish compatibility. Snapshot's hook preflight rejects unknown or mixed pairs, assemblies loaded from another installation, and unsupported hook signatures before patching. Use the bundle migration to prevent duplicate hooks. Better Shortcuts is optional.

Clone Core beside this repository, then build `Designer.Modules.SnapshotInspector.csproj` with .NET 10.0.112 and local `DesignerDir` references. `tests/SnapshotPayloadChecks` accepts an optional retained native fixture directory for long UTF-8 text, scalar, boolean, global, and malformed payload cases. Fixtures and Q-SYS binaries stay local. Core's native driver checks the same inspector across a live theme toggle. Hardware/Core transport behavior has not been revalidated by the split.

Extracted from Schematic Tools commit `13c52e6`. [Split tracking](https://github.com/joshua-small/qsys-schematic-tools/issues/4).

### Offline regression checks

Run `dotnet run --project tests/SnapshotPayloadChecks` with .NET 10 for synthetic scalar/comparison cases and compressed-frame validation. The checks cover overlapping references, all distance-bit combinations through 1,023 bytes, truncated/invalid tokens, and successful decoding at the 32 MiB expansion cap with rejection one byte above it. They also check that reading or rejecting these compressed frames leaves input bytes unchanged. To additionally test a retained native snapshot fixture, append `-- <fixture-directory>`.

CI runs portable synthetic cases only; native fixture, WPF, Designer and hardware acceptance remain separate. The synthetic trailer checks its shape only; these tests do not establish checksum validation or support for additional snapshot formats.

Run `dotnet run --project tests/WorkspaceChecks` for generated-workspace membership, identity, bounds, cancellation and transaction-coordination tests. These link the production planning helpers; they do not simulate Designer acceptance.

Run `dotnet run --project tests/WorkspaceAdapterChecks` for the production adapter against portable host-contract fixtures. These check construction, explicit attachment after insertion into an existing layer, bindings, preserved layout, native-background model values, rollback after attachment failures, no-ops and stale mode/style rejection. They do not execute Designer or establish actual native history or persistence.

Run `dotnet run --project tests/SnapshotStartupChecks` for the startup build gate and complete drawing, hover and sidebar hook preflight. These portable checks cover Core denial, complete and mixed host/common pairs, assembly locations and missing or invalid hook contracts. Native startup and visual behavior require separate application checks.

### Generated workspaces

**Create / update workspace...** previews a new schematic page, a Popup on an existing schematic page, or an update to a workspace created by this feature. Existing controls retain their positions, annotations and bindings. Obsolete controls are kept by default; removal requires an explicit selection in the preview. See [workspace behavior and acceptance limits](WORKSPACES.md).

The native page/Popup adapter uses native Popup content canvases, linked controls and document operations. It checks Design mode, Core's build gate and the required Popup type before editing. Validate generation, updates, persistence, cancellation and Undo/Redo on the Designer build you use before relying on a generated workspace. Portable checks do not establish native application behavior, and results from another build do not establish compatibility.

The unchanged workspace implementation passed targeted native model checks on Designer 10.5.0 with Mods Core 0.1.11 for Popup creation, additions to existing pages and Popups, membership retirement/rejoin, explicit removal, Undo/Redo and safe rejection. The checks include preservation of retained widget references, layout, bindings and ownership. Popup appearance was spot-checked in light and dark themes. The startup correction still requires fresh application acceptance, including corrected-file and plain-Designer reopening and targeted 10.5.1 startup/use. Stored snapshot payloads and runtime values were not validated by these workspace fixtures.

## Window and interaction behavior

Both page-generation paths use the native page background and black model label text so Designer can render the labels in light and dark themes. New Popups keep native background defaults and use black model text. Existing workspaces retain owner colors and styling. Workspace update and Popup rendering have separate acceptance scope.
