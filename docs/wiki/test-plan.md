# Manual test plan — Cathartic Colours

Comprehensive manual test plan, organised by area. Each feature folds its cases in when it completes ([feature pages](features/README.md)).

## How to use

1. Run the areas **in the order listed below**. Each area is ordered so one pass covers its cases without retreading paths.
2. Within an area, run the cases top to bottom.
3. For each case, record **pass or fail, the date, and the build** (version or build number) you tested. Note any defect with its case id.
4. A failed case that is a known issue links to its roadmap id. A new defect becomes a backlog item in the [roadmap](roadmap.md).

## Case format

Each case has an id and these fields:

| Field | Content |
|---|---|
| Id | `TP-<area>-<nn>`, where `<area>` is one of the codes below and `<nn>` is a two-digit number, unique within the area |
| Preconditions | State the tester must start from |
| Steps | Numbered actions |
| Expected | The observable result that means pass |
| Roadmap id | The roadmap item the case covers, or `—` for existing behaviour |

**Area codes:** `MENU` Main menu · `STD` Standard mode · `CM` Color Merge mode · `ACM` Advanced Color Merge mode · `HUD` Gameplay HUD · `OVER` Game over · `SAVE` Save/Resume · `AUDIO` Audio

**Example template (not a real case):**

| Field | Content |
|---|---|
| Id | `TP-<AREA>-<nn>` |
| Preconditions | <state before the test> |
| Steps | 1. <action> 2. <action> |
| Expected | <observable result> |
| Roadmap id | — |

---

## Main menu (MENU)

_No cases yet; seeded in CC-001.4._

## Standard mode (STD)

_No cases yet; seeded in CC-001.4._

## Color Merge mode (CM)

_No cases yet; seeded in CC-001.4._

## Advanced Color Merge mode (ACM)

_No cases yet; seeded in CC-001.4._

## Gameplay HUD (HUD)

_No cases yet; seeded in CC-001.4._

## Game over (OVER)

_No cases yet; seeded in CC-001.4._

## Save/Resume (SAVE)

_No cases yet; seeded in CC-001.4._

## Audio (AUDIO)

_No cases yet; seeded in CC-001.4._
