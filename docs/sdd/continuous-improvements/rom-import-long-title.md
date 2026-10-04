# C21 — start long ROM imports with a valid editable title

## Problem

When a newly imported ROM has a basename longer than 80 UTF-16 code units, `RomImportDialog` copies the complete basename into the title field. The field's `MaxLength` does not shorten text assigned by the constructor, so the dialog starts with a value that `SaveResult` rejects until the player edits it.

## Behavior

- A new import whose filename-derived suggestion exceeds the 80-character title limit starts with a non-empty suggestion no longer than 80 UTF-16 code units.
- The suggestion does not end with an unmatched high surrogate.
- Short filename suggestions and existing custom titles remain unchanged.
- The selected ROM path and physical filename remain unchanged; the title is still editable.
- No ROM content is opened or modified by this title suggestion behavior.

## Acceptance checks

1. A synthetic filename with a long basename opens the real import dialog with a non-empty title suggestion within the accepted length limit.
2. A synthetic basename with a surrogate pair crossing the truncation boundary yields valid UTF-16 text without a dangling surrogate.
3. A short filename keeps the current suggestion; editing an existing imported record preserves its custom title.
4. The selected ROM path remains the original full path, regardless of the suggested title.
5. Focused Windows `ProfilesCheck` and full Windows CI pass. Fixtures use metadata and paths only; they never read or execute ROM contents.

## Scope and verification boundary

Only the initial editable title suggestion changes. Physical filenames, catalog identity, existing titles, save naming, and ROM bytes remain untouched. This cycle prepares a reviewable PR and does not publish a release.
