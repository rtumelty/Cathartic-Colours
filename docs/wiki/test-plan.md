# Manual test plan — Cathartic Colours

Comprehensive manual test plan, organised by area. Each feature folds its cases in when it completes ([feature pages](features/README.md)).

## How to use

1. Run the areas **in the order listed below**. Each area is ordered so one pass covers its cases without retreading paths.
2. Within an area, run the cases top to bottom.
3. For each case, record **pass or fail, the date, and the build** (version or build number) you tested. Note any defect with its case id.
4. A failed case that is a known issue names its roadmap id (each id links to its issue in [roadmap.md](roadmap.md#id-to-issue)). A new defect becomes a new Backlog issue (project `CLAUDE.md` §6).

## Case format

Each case has an id and these fields:

| Field | Content |
|---|---|
| Id | `TP-<area>-<nn>`, where `<area>` is one of the codes below and `<nn>` is a two-digit number, unique within the area |
| Preconditions | State the tester must start from |
| Steps | Numbered actions |
| Expected | The observable result that means pass |
| Roadmap id | The roadmap item the case covers, or `—` for existing behaviour |

**Area codes:** `MENU` Main menu · `STD` Standard mode · `CM` Color Merge mode · `ACM` Advanced Color Merge mode · `HUD` Gameplay HUD · `OVER` Game over · `SAVE` Save/Resume · `AUDIO` Audio · `TOOL` Editor and tooling · `TRACK` Project tracking (GitHub)

**Example template (not a real case):**

| Field | Content |
|---|---|
| Id | `TP-<AREA>-<nn>` |
| Preconditions | <state before the test> |
| Steps | 1. <action> 2. <action> |
| Expected | <observable result> |
| Roadmap id | — |

---

**Notation used in the cases.** Grid cells are `(column, row)` counted from 0, column 0 at the left and row 0 at the bottom. "Up" is towards the top of the screen. `R`, `G`, `B` are Red, Green, Blue; `Y`, `M`, `C` are Yellow, Magenta, Cyan; `W` is White. `s`, `m`, `l` are Small, Medium, Large (Large is the biggest visual size). The **white indicator** is the white Small block that shows where the next block will appear; it takes a random colour (Red, Green or Blue) on the next move. Block movement is **one cell per move** by design (it matches the PuzzleScript prototype). A block only merges when the cell ahead is occupied at the moment it is processed, so to merge two blocks, push them along their line until the front block is against a wall or an unmergeable block, then press once more. Random spawns mean a tester cannot script a board: play until the situation described arises, and mark the case "not reached" if it does not within a reasonable number of moves.

## Main menu (MENU)

Pass setup: fresh launch on the main menu with no save file (delete `savegame.json` from the app's persistent data folder first). Make no moves in the game started in TP-MENU-05, so no save is written.

### TP-MENU-01 — Menu defaults on first launch

| Field | Content |
|---|---|
| Id | `TP-MENU-01` |
| Preconditions | Fresh launch, no save file |
| Steps | 1. Look at the main menu without touching anything. |
| Expected | The menu shows a preset dropdown, a game mode dropdown with a mode description, a width slider and a height slider with value labels, and a Start button. Mode is Standard and both sliders read 6. There is no Resume button. |
| Roadmap id | — |

### TP-MENU-02 — Mode dropdown and description

| Field | Content |
|---|---|
| Id | `TP-MENU-02` |
| Preconditions | Main menu showing |
| Steps | 1. Open the game mode dropdown. 2. Select each entry in turn. |
| Expected | The dropdown lists "Standard", "Color Merge" and "Advanced Color Merge". The description label shows text for the selected mode and the text changes when the mode changes. |
| Roadmap id | — |

### TP-MENU-03 — Size sliders range and labels

| Field | Content |
|---|---|
| Id | `TP-MENU-03` |
| Preconditions | Main menu showing |
| Steps | 1. Drag the width slider fully left, then fully right. 2. Do the same for the height slider. 3. Set width to 5 and height to 7. |
| Expected | Each slider ranges from 4 to 10. The value label next to each slider shows the slider's current value. The two sliders are independent. After step 3 the labels read 5 and 7. |
| Roadmap id | — |

### TP-MENU-04 — Presets set mode and size

| Field | Content |
|---|---|
| Id | `TP-MENU-04` |
| Preconditions | Main menu showing |
| Steps | 1. Open the preset dropdown. 2. Select the Color Merge 4x4 preset. 3. Select the Advanced Color Merge 6x6 preset. 4. Select the Standard 6x6 preset. 5. Set the sliders back to 5 and 7. |
| Expected | The dropdown offers "Custom" plus the three presets. After each preset the mode and the grid size are the preset's (Color Merge 4x4, Advanced Color Merge 6x6, Standard 6x6). Whether the mode dropdown and sliders visibly update to the preset values is not confirmed in the wiki: record what you see. |
| Roadmap id | — |

### TP-MENU-05 — Start loads a board of the chosen size

| Field | Content |
|---|---|
| Id | `TP-MENU-05` |
| Preconditions | Main menu showing, mode Standard, width 5 and height 7 |
| Steps | 1. Tap Start. 2. Count the columns and rows of the board. 3. Find the white block. |
| Expected | The gameplay scene loads with a board of 5 columns and 7 rows. The score label reads "Score: 0" and the move label "Moves: 0". One white block is on the board at (2, 3), the cell at half the width and half the height, rounded down. |
| Roadmap id | — |

### TP-MENU-06 — Footer Main Menu keeps the settings

| Field | Content |
|---|---|
| Id | `TP-MENU-06` |
| Preconditions | The game from TP-MENU-05 is running and no move has been made |
| Steps | 1. Tap the Main Menu button in the footer. 2. Look at the dropdowns and sliders. |
| Expected | The main menu loads. The mode and sliders still show Standard, width 5, height 7 (the last runtime settings are kept). There is no Resume button, because no move was made. |
| Roadmap id | — |

## Standard mode (STD)

Pass setup: start on the main menu, choose the Standard 6x6 preset and tap Start. Keep the same game for TP-STD-01 to TP-STD-07 (return to the menu only if the board becomes full). TP-STD-08 and TP-STD-09 need a touch device and a gamepad. Since CC-006 the colour of each new block and the cell of the next white block come from separate random draws, so they are independent: do not expect any pattern linking them. (Not reliably observable by hand, so there is no case for it.)

### TP-STD-01 — First move converts the white block

| Field | Content |
|---|---|
| Id | `TP-STD-01` |
| Preconditions | New Standard 6x6 game, no moves made. One white block at (3, 3) |
| Steps | 1. Press the left arrow key once. |
| Expected | The white block stays in its cell and becomes a Red, Green or Blue Small block. A new white block appears in a different, previously empty cell (always, while any cell is empty; CC-021). Moves reads 1, Score reads 0. |
| Roadmap id | CC-021 (fixed in CC-006) |

### TP-STD-02 — One cell per move, all four directions

| Field | Content |
|---|---|
| Id | `TP-STD-02` |
| Preconditions | After TP-STD-01; a coloured block has an empty cell next to it in the direction you press |
| Steps | 1. Press an arrow key towards an empty neighbouring cell of a coloured block. 2. Repeat with the other three arrow keys, then with W, A, S and D. |
| Expected | Each coloured block moves exactly one cell per key press, never all the way to the wall. Up/Down/Left/Right arrows and W/A/S/D move blocks up/down/left/right on screen respectively. The block that was white before the press changes colour but does not move on that press. |
| Roadmap id | — |

### TP-STD-03 — Walls and the white block stop movement

| Field | Content |
|---|---|
| Id | `TP-STD-03` |
| Preconditions | A game in progress with a coloured block on the board |
| Steps | 1. Press one direction repeatedly until a coloured block is against the wall. 2. Press the same direction again and note the Moves label. 3. Set up a coloured block with the white block directly ahead of it in its line, and press towards the white block. |
| Expected | A block at the wall does not move or leave the board. The move still counts: Moves increases by 1 on the extra press. A block next to the white block does not move into it and does not merge with it. The white block stays in its cell and turns into a coloured block on that move, as on every move. |
| Roadmap id | — |

### TP-STD-04 — Small pair merges to Medium

| Field | Content |
|---|---|
| Id | `TP-STD-04` |
| Preconditions | Two Small blocks of the same colour in one row or column, with the front one against a wall or a block that cannot merge with it |
| Steps | 1. Note the Score. 2. Press once towards the front block (for example `[. . Rs Rs]` pressing right). |
| Expected | The moving block disappears. The block it moved into becomes a Medium block (visibly larger) of the same colour: `[. . . Rm]`. Score increases by 10 and Moves by 1. Particles in that colour appear at the merge cell. |
| Roadmap id | — |

### TP-STD-05 — Blocks that must not merge

| Field | Content |
|---|---|
| Id | `TP-STD-05` |
| Preconditions | Two adjacent blocks in a line pressed against a wall that differ in colour (for example Rs next to Bs) or in size (a Small next to a Medium of the same colour) |
| Steps | 1. Note the Score. 2. Press towards the wall, along the line of the pair. |
| Expected | Neither block merges or changes. Score does not change. Moves increases by 1. |
| Roadmap id | — |

### TP-STD-06 — Only one merge per block in a move

| Field | Content |
|---|---|
| Id | `TP-STD-06` |
| Preconditions | Three same-colour Small blocks in a row with the front one against a wall, with no other block in that row, for example `[. Rs Rs Rs]` |
| Steps | 1. Note the Score. 2. Press once towards the wall. |
| Expected | Only one merge happens: `[. . Rs Rm]`. The middle block merges into the front block and the back block moves up one cell. Score increases by 10 once. |
| Roadmap id | — |

### TP-STD-07 — Medium pair and Large pair

| Field | Content |
|---|---|
| Id | `TP-STD-07` |
| Preconditions | Extended play. Two same-colour Medium blocks, front one against a wall; later two same-colour Large blocks, front one against a wall |
| Steps | 1. Press towards the Medium pair. 2. Later, press towards the Large pair. |
| Expected | Step 1: `[. . Rm Rm]` becomes `[. . . Rl]`, Score +50. Step 2: `[. . Rl Rl]` becomes `[. . . .]`, both blocks disappear, Score +100. |
| Roadmap id | — |

### TP-STD-08 — Swipe on a touch device

| Field | Content |
|---|---|
| Id | `TP-STD-08` |
| Preconditions | Game in progress on a touch device (or the editor with a touch simulator). A coloured block has an empty neighbouring cell |
| Steps | 1. Swipe a long distance (at least 50 pixels) in each of the four directions. 2. Make a short touch drag of about 10 pixels. |
| Expected | Each long swipe makes the same move as the matching arrow key and Moves increases by 1. The short drag makes no move and Moves does not change. |
| Roadmap id | — |

### TP-STD-09 — Gamepad moves blocks

| Field | Content |
|---|---|
| Id | `TP-STD-09` |
| Preconditions | Game in progress; a gamepad is connected |
| Steps | 1. Push the left stick in each direction. 2. Press each D-pad direction. |
| Expected | Each input moves blocks one cell in that direction and Moves increases by 1. Known to fail until CC-030. |
| Roadmap id | CC-030 |

## Color Merge mode (CM)

Pass setup: return to the main menu with the footer Main Menu button, choose the Color Merge 4x4 preset and tap Start. Blocks are always Small and only Red, Green or Blue spawn.

### TP-CM-01 — 4x4 board and first move

| Field | Content |
|---|---|
| Id | `TP-CM-01` |
| Preconditions | New Color Merge 4x4 game, no moves made |
| Steps | 1. Count the cells. 2. Find the white block. 3. Press any direction once. |
| Expected | The board is 4 by 4. The white block is at (2, 2). After the move it is a Red, Green or Blue block, a new white block appears in an empty cell, and Moves reads 1. |
| Roadmap id | — |

### TP-CM-02 — Two different primaries make a secondary

| Field | Content |
|---|---|
| Id | `TP-CM-02` |
| Preconditions | Two Small blocks of different primary colours adjacent in a line, front one against a wall or a block that cannot merge with it |
| Steps | 1. Note the Score. 2. Press along the line towards the front block (for example `[Rs Gs . .]` pressing left). |
| Expected | The moving block disappears and the block it moved into takes the combined colour: Red+Green gives Yellow, Red+Blue gives Magenta, Green+Blue gives Cyan (`[Ys . . .]`). Score +10. Try at least two of the three pairs. |
| Roadmap id | — |

### TP-CM-03 — Equal colours do not merge

| Field | Content |
|---|---|
| Id | `TP-CM-03` |
| Preconditions | Two same-colour primaries adjacent in a line against a wall (`[Rs Rs . .]`) |
| Steps | 1. Note the Score. 2. Press towards the wall. |
| Expected | Nothing merges and the blocks stay as they are. Score does not change. Moves increases by 1. |
| Roadmap id | — |

### TP-CM-04 — Secondary plus its missing primary clears both

| Field | Content |
|---|---|
| Id | `TP-CM-04` |
| Preconditions | A secondary block adjacent to the primary it lacks (Yellow and Blue, Magenta and Green, or Cyan and Red), front one against a wall or unmergeable block |
| Steps | 1. Note the Score. 2. Press along the line towards the front block (for example `[Ys Bs . .]` pressing left). |
| Expected | Both blocks disappear. Score +100. The particles at the merge cell are white. |
| Roadmap id | — |

### TP-CM-05 — Pairs that do not merge

| Field | Content |
|---|---|
| Id | `TP-CM-05` |
| Preconditions | Either two different secondaries adjacent (for example Yellow and Magenta), or a secondary next to a primary it already contains (for example Yellow and Red), against a wall |
| Steps | 1. Note the Score. 2. Press towards the wall along the line. |
| Expected | Nothing merges. Score does not change. |
| Roadmap id | — |

### TP-CM-06 — White block never merges

| Field | Content |
|---|---|
| Id | `TP-CM-06` |
| Preconditions | A coloured block with the white block directly ahead in its line |
| Steps | 1. Press towards the white block. |
| Expected | The coloured block does not move into or merge with the white block, and Score does not change. The white block then becomes a coloured block on that same move (resolved by the move), and a new white block appears elsewhere. |
| Roadmap id | — |

## Advanced Color Merge mode (ACM)

Pass setup: return to the main menu, choose the Advanced Color Merge 6x6 preset and tap Start. Cases 04 to 07 need extended play to build Medium and Large blocks; mark "not reached" if they do not arise.

### TP-ACM-01 — 6x6 board and first move

| Field | Content |
|---|---|
| Id | `TP-ACM-01` |
| Preconditions | New Advanced Color Merge 6x6 game, no moves made |
| Steps | 1. Press any direction once. |
| Expected | The white block at (3, 3) becomes a Small Red, Green or Blue block and a new white block appears. Moves reads 1. |
| Roadmap id | — |

### TP-ACM-02 — Small pair of one colour grows to Medium

| Field | Content |
|---|---|
| Id | `TP-ACM-02` |
| Preconditions | Two Small blocks of the same primary colour adjacent in a line, front one against a wall or unmergeable block |
| Steps | 1. Note the Score. 2. Press towards the front block (`[. . Rs Rs]` pressing right). |
| Expected | `[. . . Rm]`: one block disappears and the other becomes a larger Medium block of the same colour. Score +10. |
| Roadmap id | — |

### TP-ACM-03 — Small blocks that do not merge

| Field | Content |
|---|---|
| Id | `TP-ACM-03` |
| Preconditions | Against a wall: two Small blocks of different colours (`[. . Rs Gs]`), or a Small next to a Medium (`[. . Rs Rm]`) |
| Steps | 1. Note the Score. 2. Press towards the wall. |
| Expected | Nothing merges and Score does not change. (These are current rules; the dead ends are under review in CC-026.) |
| Roadmap id | — |

### TP-ACM-04 — Two different Medium primaries make a Large secondary

| Field | Content |
|---|---|
| Id | `TP-ACM-04` |
| Preconditions | Two Medium blocks of different primary colours adjacent in a line, front one against a wall or unmergeable block |
| Steps | 1. Note the Score. 2. Press towards the front block (`[. . Rm Bm]` pressing right). |
| Expected | `[. . . Ml]`: one block disappears and the other becomes a Large Magenta block (Red+Blue). Red+Green gives Yellow and Green+Blue gives Cyan the same way. Score +50. |
| Roadmap id | — |

### TP-ACM-05 — Same-colour Medium blocks do not merge

| Field | Content |
|---|---|
| Id | `TP-ACM-05` |
| Preconditions | Two Medium blocks of the same colour adjacent in a line against a wall (`[. . Rm Rm]`) |
| Steps | 1. Note the Score. 2. Press towards the wall. |
| Expected | Nothing merges and Score does not change. (Current rule; under review in CC-026.) |
| Roadmap id | — |

### TP-ACM-06 — Large secondary plus its missing Medium primary makes White

| Field | Content |
|---|---|
| Id | `TP-ACM-06` |
| Preconditions | A Large secondary block adjacent to a Medium block of the primary it lacks (Yellow with Blue, Magenta with Green, Cyan with Red), in either order |
| Steps | 1. Note the Score. 2. Press along the line so one is blocked by the wall or the other block (`[. . Gm Ml]` pressing right, or `[. . Ml Gm]` pressing right). |
| Expected | One block disappears and the other becomes a Large White block (`[. . . Wl]`). Score +100. |
| Roadmap id | — |

### TP-ACM-07 — Two Large White blocks clear

| Field | Content |
|---|---|
| Id | `TP-ACM-07` |
| Preconditions | Two Large White blocks adjacent in a line, front one against a wall or unmergeable block |
| Steps | 1. Note the Score. 2. Press towards the front block (`[. . Wl Wl]` pressing right). |
| Expected | Both blocks disappear and Score +200. Particles at the merge cell are expected to be visible. Known to fail until CC-032 (the clear result colour is None, which the colour profile maps to transparent). |
| Roadmap id | CC-032 |

## Gameplay HUD (HUD)

Pass setup: return to the main menu, choose the Standard 6x6 preset, tap Start and make no move yet.

### TP-HUD-01 — Header and footer content at start

| Field | Content |
|---|---|
| Id | `TP-HUD-01` |
| Preconditions | New Standard 6x6 game, no moves made |
| Steps | 1. Read the header and footer. |
| Expected | The header shows the title, "Moves: 0" and "Score: 0". The footer shows an instruction line describing the mode, the text "Swipe / Arrow Keys / WASD to Move" and a Main Menu button. |
| Roadmap id | — |

### TP-HUD-02 — Board fits between header and footer

| Field | Content |
|---|---|
| Id | `TP-HUD-02` |
| Preconditions | New game just loaded (wait about a second for the layout to settle) |
| Steps | 1. Look at the full board. 2. Check that every edge row and column is visible. |
| Expected | The whole board is visible and centred in the free area between header and footer, with a margin around it. No row or column is hidden behind the header or footer. |
| Roadmap id | — |

### TP-HUD-03 — Status label shows whose turn it is

| Field | Content |
|---|---|
| Id | `TP-HUD-03` |
| Preconditions | Game waiting for input |
| Steps | 1. Look for a status line in the HUD. |
| Expected | A status line reading "Your turn..." is visible while the game waits for input. Known to fail until CC-028 (the element is missing, so no status text is displayed). |
| Roadmap id | CC-028 |

### TP-HUD-04 — Move counter

| Field | Content |
|---|---|
| Id | `TP-HUD-04` |
| Preconditions | New game, "Moves: 0" |
| Steps | 1. Press an arrow key. 2. Press the same direction repeatedly until nothing on the board can move. 3. Press once more. |
| Expected | Moves reads 1 after step 1 and rises by exactly 1 per key press, including presses where no block moves. |
| Roadmap id | — |

### TP-HUD-05 — Score label shows the running total

| Field | Content |
|---|---|
| Id | `TP-HUD-05` |
| Preconditions | Game in progress, score 0 |
| Steps | 1. Play until two merges have happened (see TP-STD-04). 2. Compare the score after each. |
| Expected | Score stays unchanged on moves with no merge. Each Small-pair merge adds 10 and the label shows the running total (for example 10, then 20), not the last gain. |
| Roadmap id | — |

### TP-HUD-06 — Move and merge feedback

| Field | Content |
|---|---|
| Id | `TP-HUD-06` |
| Preconditions | Game in progress with blocks on the board |
| Steps | 1. Press a direction and watch the blocks. 2. Cause a merge and watch the merge cell. |
| Expected | Blocks wobble for about 0.6 seconds after each accepted move. A merge emits particles at the merge cell in the colour of the merge result (for a Standard Large-pair clear, the pair's colour). Blocks jump to their new cell without sliding. |
| Roadmap id | — |

## Game over (OVER)

Pass setup: from the main menu choose Standard mode, width 4 and height 4 (a small board fills fastest), tap Start and play until the board is full. No save file should exist when the pass starts. TP-OVER-07 is checked on the way, while the 4x4 board fills up, before TP-OVER-01.

### TP-OVER-07 — A free cell always gets the new white block

| Field | Content |
|---|---|
| Id | `TP-OVER-07` |
| Preconditions | Standard 4x4 game, played as in TP-OVER-01 while the board fills up. Repeat every time a move leaves exactly one empty cell |
| Steps | 1. Make a move that slides or merges so that exactly one cell is empty just before the new white block is placed (in practice: the board has one empty cell before the move, and your move merges nothing; sliding never changes the number of empty cells). 2. Check the board after that move. 3. If the board is now full but a merge is still possible, make a move that merges nothing (for example, towards a wall the blocks are already against) and check the board. |
| Expected | Step 2: the new white block appears in the one free cell, every time (before CC-021 it sometimes failed to appear). When two or more cells are free after a move, the white block is in one of them, and over many moves it appears in different cells (not always the same one). Step 3: the board stays full, the white block on it turns coloured and no new white block appears (there is no free cell); the move still counts (Moves rises by 1) and play continues while a merge is possible. Mark "not reached" if the board never has exactly one free cell after a move. |
| Roadmap id | CC-021 (fixed in CC-006) |

### TP-OVER-01 — Full board with a merge available keeps playing

| Field | Content |
|---|---|
| Id | `TP-OVER-01` |
| Preconditions | Standard 4x4 game; play until all 16 cells are occupied (the white block counts as occupied; the board usually becomes full when a new white block takes the last free cell, see TP-OVER-07) |
| Steps | 1. When the board becomes full, check whether two same-colour, same-size blocks touch. 2. If so, press along them to merge. |
| Expected | While the board is full but at least one adjacent pair can merge, the game-over panel does not appear and the move that merges is accepted. If the board fills with no mergeable pair the game ends instead: continue with TP-OVER-02. |
| Roadmap id | — |

### TP-OVER-02 — Game over appears when full and no merge exists

| Field | Content |
|---|---|
| Id | `TP-OVER-02` |
| Preconditions | Continuing from TP-OVER-01; keep pressing directions until the board is full and no adjacent pair can merge |
| Steps | 1. Make the move that fills the last cell with no merge available. |
| Expected | A full-screen game-over panel appears over the board with a Restart button and a Main Menu button. Score and Moves keep their final values. |
| Roadmap id | — |

### TP-OVER-03 — Moves, sound and Moves counter are unchanged after game over

| Field | Content |
|---|---|
| Id | `TP-OVER-03` |
| Preconditions | Game-over panel showing ("Grid Full!"), sound audible |
| Steps | 1. Note Score, Moves and the board. 2. Press the up, down, left and right arrow keys, then W, A, S and D, one at a time. 3. Swipe in all four directions if on a touch device. |
| Expected | No move sound or any other sound plays. The board, Score and Moves do not change (Moves does not rise). The panel stays up. |
| Roadmap id | CC-023 (fixed in CC-006) |

### TP-OVER-04 — Restart starts a fresh game

| Field | Content |
|---|---|
| Id | `TP-OVER-04` |
| Preconditions | Game-over panel showing |
| Steps | 1. Tap Restart once. |
| Expected | The panel disappears. The board is empty apart from one white block at (2, 2). Score reads 0 and Moves reads 0. The game accepts moves again. One click currently restarts twice (CC-028) and leaves old grid cells behind (CC-029); the visible effect is not confirmed, so record anything unusual. |
| Roadmap id | — |

### TP-OVER-05 — Restart does not duplicate grid cells (editor only)

| Field | Content |
|---|---|
| Id | `TP-OVER-05` |
| Preconditions | Running in the Unity editor (Play mode). The Standard 4x4 game from TP-OVER-02 was the first game since the gameplay scene loaded, and Restart was clicked once in TP-OVER-04. On a device build, mark this case "not applicable" |
| Steps | 1. In the Hierarchy, expand the object that carries `GameRenderer` (the GameManager prefab instance). 2. Type `Cell_` in the Hierarchy search box and count the results. |
| Expected | Exactly 16 `Cell_<x>_<y>` objects (one per cell of the 4x4 board). Known to fail until CC-029: restart keeps the old cells and the single click restarts twice (CC-028), so code reading predicts 48 today (16 + 2 × 16); record the actual count. |
| Roadmap id | CC-029 |

### TP-OVER-06 — No resume offered after a finished game

| Field | Content |
|---|---|
| Id | `TP-OVER-06` |
| Preconditions | The game restarted in TP-OVER-04 (and checked in TP-OVER-05), no move made since |
| Steps | 1. Tap the footer Main Menu button. 2. Look at the main menu. |
| Expected | The main menu shows no Resume button, because the save file is deleted when the game ends and no move has been made since. |
| Roadmap id | — |

## Save/Resume (SAVE)

Pass setup: start on the main menu with no Resume button (the state after TP-OVER-06). Use the Standard 6x6 preset. TP-SAVE-06 needs a build running on a device.

### TP-SAVE-01 — Resume button appears after a move

| Field | Content |
|---|---|
| Id | `TP-SAVE-01` |
| Preconditions | Main menu with no Resume button |
| Steps | 1. Choose Standard 6x6 and tap Start. 2. Make one move. 3. Tap the footer Main Menu button. |
| Expected | The main menu now shows a Resume button. |
| Roadmap id | — |

### TP-SAVE-02 — Resume restores the saved game

| Field | Content |
|---|---|
| Id | `TP-SAVE-02` |
| Preconditions | Resume button showing; first start a new game, make several moves until you have a score above 0, and write down the Moves, Score and the position, colour and size of every coloured block, then tap Main Menu |
| Steps | 1. On the menu select the Color Merge 4x4 preset. 2. Tap Resume. |
| Expected | The saved game loads as a Standard 6x6 game, not as Color Merge 4x4. Moves and Score equal the noted values and every coloured block is in its noted position with its colour and size. |
| Roadmap id | — |

### TP-SAVE-03 — White block is unchanged after resume

| Field | Content |
|---|---|
| Id | `TP-SAVE-03` |
| Preconditions | The game resumed in TP-SAVE-02; you noted the white block's cell before leaving |
| Steps | 1. Without pressing any key, compare the white block's cell and colour with the noted values. |
| Expected | The white block is in the same cell as before leaving, is still white, and no additional white block appeared. |
| Roadmap id | CC-022 (fixed in CC-006) |

### TP-SAVE-07 — Nothing changes after resume until the first move

| Field | Content |
|---|---|
| Id | `TP-SAVE-07` |
| Preconditions | The game resumed in TP-SAVE-02; you noted Moves, Score and the board before leaving |
| Steps | 1. Without pressing any key, wait 5 seconds and compare Moves, Score, every block and the white block with the noted values. 2. Make one move. |
| Expected | Step 1: everything matches the noted values and no sound plays. Step 2: Moves rises by exactly 1, the white block turns into a coloured block (it does not move), and one new white block appears. |
| Roadmap id | CC-022 (fixed in CC-006) |

### TP-SAVE-04 — Play continues and later moves are saved

| Field | Content |
|---|---|
| Id | `TP-SAVE-04` |
| Preconditions | Resumed game from TP-SAVE-02 |
| Steps | 1. Make two moves and note Moves, Score and the board. 2. Tap Main Menu. 3. Tap Resume. |
| Expected | Moves continues from the resumed count (it rises by 1 per move). The second resume shows the state after the two further moves, not the earlier save. |
| Roadmap id | — |

### TP-SAVE-05 — A new game keeps the old save until its first move

| Field | Content |
|---|---|
| Id | `TP-SAVE-05` |
| Preconditions | Main menu with a Resume button |
| Steps | 1. Tap Start (a new game). 2. Without moving, tap Main Menu. 3. Tap Resume and note the board. 4. Tap Main Menu, tap Start, make one move, tap Main Menu, tap Resume. |
| Expected | Step 3 shows the old saved game from TP-SAVE-04. In step 4 the Resume button loads the new game's state (Moves 1) instead of the old one. |
| Roadmap id | — |

### TP-SAVE-06 — Force-closing the app keeps the last move

| Field | Content |
|---|---|
| Id | `TP-SAVE-06` |
| Preconditions | Build installed on a device; game in progress with at least 3 moves made |
| Steps | 1. Note Moves and Score. 2. Make one more move and note the new values. 3. Force-close the app from the system app switcher. 4. Relaunch and tap Resume. |
| Expected | The Resume button shows. The game loads with the Moves and Score from after the last completed move in step 2. |
| Roadmap id | — |

## Audio (AUDIO)

Pass setup: start on the main menu, choose the Standard 4x4 settings (custom width and height 4) and tap Start with speakers or headphones on and the device volume up. The Color Merge and Advanced Color Merge clear sounds are the same as the Standard Large pair sound; test them only if time allows. TP-AUDIO-05 is a separate check in FMOD Studio and needs no game running.

### TP-AUDIO-01 — Move sound on each move

| Field | Content |
|---|---|
| Id | `TP-AUDIO-01` |
| Preconditions | New game, sound audible |
| Steps | 1. Press an arrow key once. 2. Repeat four more times. |
| Expected | A move sound plays on each key press, at the moment of the press (the merge sound for a move also plays with that move, not later). |
| Roadmap id | — |

### TP-AUDIO-02 — Merge sound follows the moving block's size

| Field | Content |
|---|---|
| Id | `TP-AUDIO-02` |
| Preconditions | Game in progress; extended play for Medium and Large merges |
| Steps | 1. Merge two Small blocks (see TP-STD-04). 2. Later merge two Medium blocks (TP-STD-07). 3. Later clear two Large blocks (TP-STD-07). |
| Expected | A merge sound plays in each merge. The sound is picked by the size of the moving block, not the block produced: the small merge sound for Small + Small, the medium merge sound for Medium + Medium (although a Large block results), the large merge sound for Large + Large. If the three sounds are distinct to the ear, the tester can tell them apart. |
| Roadmap id | — |

### TP-AUDIO-03 — Game-over sound

| Field | Content |
|---|---|
| Id | `TP-AUDIO-03` |
| Preconditions | Game in progress on a 4x4 board, played until the board is full with no merge available |
| Steps | 1. Make the move that ends the game. 2. Listen. |
| Expected | The game-over sound plays once when the game-over panel appears and does not repeat. |
| Roadmap id | — |

### TP-AUDIO-04 — No move sound for a rejected move

| Field | Content |
|---|---|
| Id | `TP-AUDIO-04` |
| Preconditions | Game-over panel showing |
| Steps | 1. Press an arrow key. 2. Press it again. |
| Expected | No sound plays, because moves are ignored after game over. |
| Roadmap id | CC-023 (fixed in CC-006) |

### TP-AUDIO-05 — FMOD project contains the five game events

| Field | Content |
|---|---|
| Id | `TP-AUDIO-05` |
| Preconditions | A fresh clone of the repo; FMOD Studio 2.03 installed; no `.unsaved/` folder in `Cathartic Colours - FMOD/` |
| Steps | 1. Open `Cathartic Colours - FMOD/Cathartic Colours - FMOD.fspro` in FMOD Studio. 2. Open the Events browser. |
| Expected | The project lists the five events `Block_Move`, `Block_Merge_Small`, `Block_Merge_Medium`, `Block_Merge_Large` and `GameOver`, assigned to the Master bank. Known to fail until CC-003. |
| Roadmap id | CC-003 |

## Editor and tooling (TOOL)

Pass setup: Unity Hub with editor **6000.6.9f1** installed, a clean checkout of the branch under test, and the editor closed. These cases check the development tooling set up in CC-033, not the game. Run TP-TOOL-02 first, since the command-line runs need the editor closed.

### TP-TOOL-02 — Command-line test runs exit cleanly

| Field | Content |
|---|---|
| Id | `TP-TOOL-02` |
| Preconditions | Editor closed |
| Steps | 1. Run the EditMode `-runTests` command from project `CLAUDE.md` §4. 2. Run the same command with `-testPlatform PlayMode`. |
| Expected | Each run exits with code 0 and writes its results XML. The EditMode XML reports the `CatharticColours.Simulation.Tests` cases (CC-006), all passed; PlayMode reports 0 test cases until CC-002 adds PlayMode tests. |
| Roadmap id | CC-033, CC-006 |

### TP-TOOL-01 — Project opens on 6000.6.9f1 without errors

| Field | Content |
|---|---|
| Id | `TP-TOOL-01` |
| Preconditions | Editor closed |
| Steps | 1. Open the project from Unity Hub with 6000.6.9f1. 2. Wait for the import to finish. 3. Check the Console. |
| Expected | No upgrade or version prompt. The Console has no compile errors and no "below the supported minimum" warnings about `.meta` files. (A first import of a fresh checkout may log two "Host type is not matching any asset type" errors from the URP package; they don't come back on the next open.) |
| Roadmap id | CC-033 |

### TP-TOOL-03 — Only the kept packages are installed

| Field | Content |
|---|---|
| Id | `TP-TOOL-03` |
| Preconditions | Project open (after TP-TOOL-01) |
| Steps | 1. Open **Window → Package Management → Package Manager**. 2. Select **In Project**. |
| Expected | The direct packages are Entities, Input System, In-App Purchasing, Universal RP, Test Framework, JetBrains Rider Editor and the embedded FMOD for Unity 2.04, matching the Kept table in the CC-033 feature page. Other entries are their dependencies (uGUI and 2D Physics come back this way, through In-App Purchasing). Collab, Visual Scripting, Timeline, the AI and multiplayer packages and the other removed packages are not listed. |
| Roadmap id | CC-033 |

## Project tracking (TRACK)

Pass setup: signed in to GitHub as a repo admin, with the [project board](https://github.com/users/rtumelty/projects/1) and the repo's Issues tab open. These cases check the tracking set up in CC-034 ([ADR-002](decisions/ADR-002-issue-tracking.md)), not the game.

### TP-TRACK-01 — Board columns match the items

| Field | Content |
|---|---|
| Id | `TP-TRACK-01` |
| Preconditions | Board view open |
| Steps | 1. Open the **Board** view. 2. Compare a few items with what you know of them (for example CC-001, CC-006, CC-033). |
| Expected | Columns are Backlog, Planning, Approved, In progress, In review, Done. Every `CC-nnn` item is in the right column. CC-001 (#3) and its four sub-issues are in Done and closed. |
| Roadmap id | CC-034 |

### TP-TRACK-02 — Item issue has fields, sub-issues and working links

| Field | Content |
|---|---|
| Id | `TP-TRACK-02` |
| Preconditions | — |
| Steps | 1. Open issue #12 (CC-006). 2. Follow its feature page link. 3. Follow the `Depends on` links. |
| Expected | Labels `type:chore`, `severity:high`, `size:L`. Six sub-issues CC-006.1 to CC-006.6, with Risk and Worker in their bodies. The feature page opens once the CC-006 branch is merged (before that it shows 404). `Depends on` opens #46 (CC-034) and #45 (CC-033). |
| Roadmap id | CC-034 |

### TP-TRACK-03 — Closing an issue moves it to Done

| Field | Content |
|---|---|
| Id | `TP-TRACK-03` |
| Preconditions | — |
| Steps | 1. Create a scratch issue and add it to the board with Status Backlog. 2. Close it. 3. Check the board. 4. Delete the scratch issue. |
| Expected | After closing, the item moves to Done without any manual edit ("Item closed" workflow). |
| Roadmap id | CC-034 |

### TP-TRACK-04 — Merging a PR with `Closes #n` moves the item to Done

| Field | Content |
|---|---|
| Id | `TP-TRACK-04` |
| Preconditions | A PR whose body says `Closes #<issue>` for an item on the board (for example the CC-034 PR and #46) |
| Steps | 1. Merge the PR. 2. Check the issue and the board. |
| Expected | The issue is closed and its board item is in Done ("Pull request merged" / "Item closed" workflows). |
| Roadmap id | CC-034 |

## Regression passes

Record of manual passes run against a change. Add a line per pass.

- 2026-11-19 — CC-006.3 (BoardStepSystem swap): Standard, Color Merge, Advanced Color Merge, Game over, Save/Resume — passed (Ronan). CC-006.5: TP-SAVE-02, -03, -07 passed (Ronan).
