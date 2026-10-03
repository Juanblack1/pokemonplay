# C14 candidate — settings usable without horizontal scrolling

Evidence: CI37101243775 native screenshots at760 show controls/cmd selectors and keyboard bindings outside viewport and a horizontal scrollbar. SettingsView.LayoutCards clamps canvas width to850. InputWorkbench.Arrange has fixed top console selector250 and single row for mode/controller/test buttons. The main launcher minimum width can yield a settings content viewport near760. This is a UX gap, not an emulator execution defect.

Proposed outcome: configure controls, audio, DS layout and RetroArch in760px settings content viewport using vertical scrolling. Preserve established Pixel aesthetic and wide layout at1100.

Acceptance candidate:
- At viewport widths760/1100, horizontal scroll unnecessary, visible actions/selectors contained in card; no negative/zero selector/binding widths or overlaps. Test native bounds after layout settles, not source constants.
- Header/console selector and mode/slot/detect/test stack into additional rows at narrow breakpoint. Hidden controls do not reserve misleading slots. All four input modes stay usable; test/cancel state and controller connection/dead-zone controls remain reachable.
- Keyboard binding rows show label and key without clipping; own vertical mapping scroll preserves12actions. Visual diagnostic may become shorter/staggered but labels remain legible. No changes to mappings or input polling behavior for a layout fix.
- DS/audio cards stack when needed. RetroArch path selectors show file names via ellipsis with full accessible descriptions; account/support buttons and GBA/DS choices fit without overlap.
- Footer save/restore remain accessible, native recovery notice wraps completely at760; general status accessibility retained.
- Verify actual native screenshots760/1100 for keyboard and controller modes, plus test running/stopped states; use fake UI input state only where explicit and never claim physical controller works. No personal ROMs.
- Preserve config/save bytes during layout changes; run existing611+ suite after implementing. Red native layout reproduction before code edit, full installer/update gates before publication. Release after v171.10.5 and settings-recovery release are verified public in order.

Next: wait for C13 immutable final head/full gate, then new branch from updated main, spec under docs/sdd/continuous-improvements, native regression reproducing out-of-viewport bounds at760. This candidate is not implementation or proof that all existing layouts are broken.
