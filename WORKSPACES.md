# Generated snapshot workspaces

This is an unreleased feature. Portable planning/adapter checks and compilation do not establish native Designer acceptance. Use Design mode and Core 0.1.10 or newer within 0.1.x. Untested Designer builds require Mod Manager 0.10.0+ exact-build consent; 10.5.0 acceptance does not carry over to 10.5.1.

## Create and update

1. Choose a bank, or all user-defined banks, and filter the Inspector results.
2. Open **Create / update workspace...**. Choose a new schematic page, a new Popup on an existing schematic page, or an existing generated workspace.
3. Review the added, retained, obsolete and removed controls. Refresh the preview after a source or destination change.
4. Apply once, or Cancel without changing the design. A successful update is one native undoable operation. Undo/Redo from the Inspector uses Core's operation-window routing; text entry keeps local text undo.

Controls shared by banks are deduplicated by component identity and control ID. Renaming a component or bank does not replace its retained widget. Each new widget is a native linked control copy; generation creates no DSP components, wrapper logic, Named Controls or Script Access changes. Snapshot recall, store and control-value setters are not part of workspace editing.

Existing widgets are retained in place, including their visual styles and bindings. New widgets and readable component/path/control/bank captions are appended below the current layout. A Popup grows only when additions require more space; it is never automatically shrunk. Page generation remains available.

New pages use the native page background and black model label text. New Popups retain native background defaults, with black model captions and button text. Existing workspace colors are not rewritten during an update. Rendering must be verified in both themes on the target build.

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

## Native acceptance still required

On a disposable design, verify both page and Popup generation, real linked bindings, ownership persistence after save/reopen, and plain-Designer reopen without the mod. Exercise overlapping banks, add/remove/rejoin cycles, edited widget positions/styles, owner annotations, grouped/locked objects and repeated no-op updates. Confirm the exact Popup child route and sizing visually.

Check one-step Undo/Redo after creation and an update, including Inspector focus and search-box local undo. Check cancellation, injected setter/verification failure with rollback, source/destination/document changes between preview and Apply, and uncertain-commit reporting. Confirm the original controls and saved snapshots remain unchanged. Viewer/Core behavior and other host versions need separate acceptance.
