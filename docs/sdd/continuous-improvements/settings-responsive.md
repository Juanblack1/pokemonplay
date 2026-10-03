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

Reproduction: nativeCI37101680635 at11bf42f failed settings avoid horizontal scrolling at viewport760 mode0. Screenshot inspected shows cropped Testar comandos and binding keys. No fixture/cast failure.

Implementation: width derives from viewport instead of850minimum; DS/audio stack below850. InputWorkbench below800 moves actions to a second row, shifts connection/content down, constrains heading and preserves binding widths. Capture cancellation gets reserved space; hints resize within their cards. Native tests cover four input modes,760/1100,12DSrows, bounds/collisions/bytes, heading and capture cancellation with an explicitly synthetic connected snapshot. Build only passed locally. CI/screenshots pending; no physical controller or emulator execution claim.

Visual evidence refinement: CI37102183223 approved cached ClientSize1100 although PNG was1044px. This disproves adequacy of cached-size assertion, not responsiveness at a real1100 viewport. Fixture now forces MinimumSize to requested viewport plus measured frame border and requires actual outer bounds/capture pixel width; no weakened layout criteria. DESIGN documents intended responsive behavior. Native screenshot evidence must pass these stricter checks before claiming1100.

Strict outer-size gate37102348928 rejected1100 despite MinimumSize, as intended. Test now resizes only its owned host through documented SetWindowPos and checks GetClientRect as native oracle, plus outer/capture width. Does not change product window sizing, display settings, permissions, or App Control. Primary references: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos and https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getclientrect . Evidence still requires current CI.

Native viewport probe37102589668 on94d59d3 passed GetClientRect1100/outer1116; downloaded PNG1116x759 confirmed independently and inspected with full footer. Merge main/PR29 resolved Program hook keeping both checks (db5dbaf); build passed. Additional UI gate opens the real diagnostic from Settings, clicks pause/resume and closes through message queue with exception capture, covering running/stopped requirement without ROM execution or physical-input claims. Needs final current-head CI.
