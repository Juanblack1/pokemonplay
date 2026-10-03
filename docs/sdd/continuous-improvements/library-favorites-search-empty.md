# C20 — clarify an empty search in Favorites

## Problem

With Favorites enabled, a query or other active filter can hide every saved favorite. `LibraryView.Rebuild` currently labels every empty Favorites result “Nenhum favorito ainda” and asks the player to star a game, even when favorites are already persisted. This confuses “no favorites have been saved” with “saved favorites do not match the current view.”

## Behavior

- When Favorites is enabled and at least one favorite is persisted, an empty filtered result says “Nenhum favorito encontrado” and explains that the current search or filters can be adjusted. This includes a query with no matches and combinations such as Favorites + Recentes.
- When no favorites are persisted, retain “Nenhum favorito ainda” and the existing star guidance.
- Keep the current `MOSTRAR TODOS` action and its behavior. Favorites and imported game data must not be modified by searching or by rendering either empty state.

## Acceptance checks

1. Seed one synthetic imported title as a favorite, enable the real Favorites filter, and search for a nonexistent title. No cards appear; the empty-state title and description distinguish a search miss from an empty saved list.
2. With Favorites and Recentes active together, a nonmatching search still explains that saved favorites did not match rather than implying that no games were recently opened.
3. The favorites file bytes remain unchanged after rendering the filtered result and using `MOSTRAR TODOS`; the action clears the search/filter and restores cards.
4. With the same catalog but no saved favorites, Favorites still shows “Nenhum favorito ainda” and the existing instruction to star a card.
5. The focused `--library-search` check and full Windows CI pass. Fixtures use temporary synthetic metadata only; no ROM or save contents are read or changed.

## Scope and verification boundary

Only empty-state copy selection changes. Search matching, favorite persistence, result ordering, and the Favorites toggle remain unchanged. This cycle prepares a reviewable PR; current thread instructions prohibit creating a release tag or publishing a release.
