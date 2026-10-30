# Generated snapshot workspaces

Portable planning/adapter checks and compilation do not establish native Designer acceptance. Use Design mode and Core 0.1.10 or newer within 0.1.x. Untested Designer builds require Mod Manager 0.10.0+ exact-build consent; 10.5.0 acceptance does not carry over to 10.5.1.

## Create and update

1. Choose a bank, or all user-defined banks, and filter the Inspector results.
2. Open **Create / update workspace...**. Choose a new schematic page, a new Popup on an existing schematic page, or an existing generated workspace.
3. Review the added, retained, obsolete and removed controls. Refresh the preview after a source or destination change.
4. Apply once, or Cancel without changing the design. A successful update is one native undoable operation. Undo/Redo from the Inspector uses Core's operation-window routing; text entry keeps local text undo.

Controls shared by banks are deduplicated by component identity and control ID. Renaming a component or bank does not replace its retained widget. Each new widget is a native linked control copy; generation creates no DSP components, wrapper logic, Named Controls or Script Access changes. Snapshot recall, store and control-value setters are not part of workspace editing.

Existing widgets are retained in place, including their visual styles and bindings. New widgets and readable component/path/control/bank captions are appended below the current layout. A Popup grows only when additions require more space; it is never automatically shrunk. Page generation remains available.

New pages use the native page background and black model label text. New Popups retain native background defaults, with black model captions and button text. Existing workspace colors are not rewritten during an update. Layout and theme combinations beyond the acceptance scope below need their own visual checks on the target build.

Obsolete controls are kept by default and recorded as inactive membership. If they rejoin the source, their original widget is reused. **Remove obsolete generated controls** removes only those identified widgets. All existing captions and annotations remain, including captions associated with removed widgets; owners can edit or remove those manually. Captions describe the source at creation and are not automatically rewritten. Repeating the same update creates no changes or new undo operation.

## Preservation and rejection rules

- Ownership uses a hidden native TextBlock containing a bounded versioned manifest, scoped to the destination canvas ID. Newly generated controls receive an ownership token alongside their existing CSS classes. The token does not alter their native component/control binding. No external file is required to identify a workspace.
- Keep the hidden manifest and `qsi-control-` class tokens when editing an updatable workspace. Unmarked graphics and controls are never adopted or removed automatically. Duplicated tokens, deleted owned controls, changed bindings, a copied canvas identity, corrupt metadata and unsupported versions stop the update rather than guessing ownership.
- Pages created by earlier versions do not have this ownership information. They remain usable, but are not automatically converted into update targets. Create a new managed workspace when updates are needed.
- Locked destinations and removal of locked controls are rejected. Grouped or moved obsolete controls must be kept or explicitly ungrouped before removal. Retained grouped controls are not repositioned.
- Source membership, component/bank object identity, descriptor shape, destination content/styles, Design mode and document identity are checked again immediately before mutation. No preview is applied to a different document or deleted destination.
- At most 1,000 distinct owned controls and 2 MiB of UTF-8 ownership text are allowed. Unsupported native visuals, invalid geometry and excess counts are rejected as a whole. No selection is silently truncated.
- A host operation already in progress is not ended or borrowed. Failure or cancellation before commit requests native cancellation. An error during commit is reported as an uncertain commit state; it does not blindly undo the document.

The destination scope is schematic pages and native schematic Popups. UCI, plugin-panel and Container destinations are not implemented by this feature.

## Acceptance scope and checks for other designs

The release DLL passed targeted normal startup and Inspector opening on the inspected Designer 10.5.0 and 10.5.1 builds with Mods Core 0.1.11. The 10.5.1 workspace run recorded 160 native/file comparisons covering the retained corrected file's cold open, rediscovery of both workspaces, four no-ops, page and Popup additions, two native Undo/Redo pairs, saving and fresh-process reopening, and reopening plus a separate SaveAs without Snapshot Inspector or Mods Core. These are recorded comparisons, not 160 independent GUI tests or physical shortcut tests. Undo was compared with separately captured previews; Redo restored the applied state. Each update added four controls in one native transaction while preserving retained identity and audited layout/style, label, ownership and binding fields.

Both reopened workspaces contained eight controls and one persisted ownership manifest. The separate SaveAs retained all sixteen audited bindings and the recorded source, bank, canvas and object fields. This establishes those fields for the bounded fixture, not whole-document equivalence. Visual spot checks covered the Inspector, membership indicators, sidebar actions, the generated page and the Popup's visible upper portion. The tall Popup extended beyond the viewport. Hover information was **not demonstrated** on 10.5.1; installation of six hooks is separate from observing hover text.

The preceding build's unchanged workspace implementation passed targeted 10.5.0 checks for Popup creation, appending controls, retirement/rejoin, explicit removal, native Undo/Redo and safe rejection, with a Popup light/dark spot check. Those results retain their prior-build scope. A full rendered Popup/layout/light-dark matrix on the release DLL, stored snapshot payloads and runtime values, injected native attachment failures, uncertain-commit paths, Viewer, connected Core and hardware are outside this acceptance. Compilation and portable checks do not substitute for those observations, and this release does not claim general 10.5.1 compatibility.

For other designs or builds, use a disposable copy to check page and Popup generation, linked bindings, ownership persistence after save/reopen, and reopening without the mod. Useful broader cases include overlapping banks, add/remove/rejoin cycles, edited widget positions/styles, owner annotations, grouped/locked objects and repeated no-op updates. Check the Popup child route and sizing visually in the themes you use.

Additional history and failure checks include one-step Undo/Redo after creation and updates with Inspector focus, search-box local undo, cancellation, injected setter/verification failure with rollback, source/destination/document changes between preview and Apply, and uncertain-commit reporting. Confirm the original controls and saved snapshots remain unchanged. These broader checks are recommendations for extending the documented scope; they are not claimed as completed by this release.
