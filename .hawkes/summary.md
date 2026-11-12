# Merge with origin/beta — Slice partner + Hollow / boss entrances

## Conflict
- **`docs/world-bible.md`** (only conflict). Both sides added Story-beats content after "The breach":
  - **Ours (HEAD):** beat 13 "Slice calls the shot" (callouts, Cold Hour parry, Last Light stamp, `slice-partner` proof).
  - **Beta:** "Boss entrances (every boss)" (`CinematicBoss.cs`, `boss-entrances` playtest) from the boss-intros change.
- **Resolution:** kept both sections in order (beat 13, then Boss entrances). Characters / Who-lives Slice partner rows from our branch stayed; beta's Dead Letter Office landmark row and `HollowPostOffice` note stayed. Updated the A Hollow gaps table so it no longer says "no landmark" (Office ruin is built; stamp-press silhouette still open).
- No other conflicts. Auto-merged cleanly: `Docs/Story.md` (partner + boss entrances), `Playtest.cs` (`slice-partner` + `boss-entrances` + `hollow-*`), Hollow / CinematicBoss scripts from beta.

## Prior work (unchanged this pass)
`SliceCallouts`, voice/sfx bake, `slice-partner` playtest — already on the branch. Did not redo the task. Did not touch the queued Hollow landmark change's files beyond accepting beta's merge.

## Checks
- No conflict markers left in the tree.
- World-bible Characters still have Want/Arc/Moment/Personality/Voice/Animations for Wallit, Slice, Yo Bot, The Courier.
- `slice-partner` scenario still registered; SliceCallouts.cs present.
- Unity compile not run here (no editor). Studio cloud build + `slice-partner` playtest should prove AC 1–4.

## Scenario
`slice-partner`
