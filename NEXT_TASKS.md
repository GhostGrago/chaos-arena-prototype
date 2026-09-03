# NEXT TASKS — 原创2.5D在线平台射击游戏

Updated: 2026-09-03

## P0 — Current

- Hands-on the 2026-09-03 batch. Nothing in it has been played yet; the build and smoke test only prove it runs.
  - Speed streak: is the 9.5 threshold right, and are the length and width readable without smearing?
  - Bot ladder: play EASY through MASTER. MASTER is deliberately unfair; NORMAL is the old HARD plus the new accuracy systems, so the default is considerably stronger than before.
  - Launch recovery: bots now spend the air jump to climb back onto the deck. Decide whether this makes a launch feel worthless; if so, restrict it to EXPERT and MASTER.
  - Weapon orientation: confirm the SMG, scatter and sniper now point outward, and confirm the pistol genuinely needed no change.
  - Charge ring: check it does not obscure the fighter or the line of fire.
  - Four arenas: play NEON CITY, DESERT, STARSHIP and OCEAN. Check moving-platform pacing and whether any layout has a gap that plays worse than it measures.
  - Power-ups: judge the 7s shield, the 9s shrink and the roughly one-in-three drop share.
  - Four-player couch play has never been run with three physical gamepads.
- Moving platforms derive position from the clock, so every peer computes the same path without replication. That reasoning has not been checked against a live two-instance online match.
- Continue the integrated Prototype 0.3.0 check: weapon-specific physical recoil, 4K/Borderless switching, persistence after restart and high-resolution UI scale. P2 movement and A-button jump have passed.
- Explicitly confirm B/down drop-through, simultaneous inputs, shared-camera extremes, offscreen indicators, rematch and menu return.
- Prioritize game feel over background scope: movement, air control, knockback readability, weapon identity, drop pacing, AI pressure and online responsiveness.
- Playtest 0.2.6 and confirm whether the 10% wider platforms plus `±19 / -9.5` death bounds provide a satisfying double-jump recovery window without making ring-outs drag on.
- Confirm whether the sniper's `(7.0, 2.4)` base knockback and 1.7s cooldown make each hit more valuable than a short SMG burst without feeling unfair.
- Continue checking drop pacing, sniper strength after the nerf, and whether the simple dusk background stays readable.
- Never tested with a full four players; only two have been verified.
- Collect external feedback on impact feel, camera comfort, preferred bot count and any readability problems.
- Re-test balance from scratch: the new arena invalidates the 0.1.4/0.1.5 weapon and ring-out conclusions.
- Validate a four-player full room when enough machines/instances are available; only two instances are currently verified.
- Convert concrete feedback into tuning or bug-fix tasks.

## P1 — Maintenance candidates (not started)

1. Make `PrototypeSceneBuilder` generate stable scene file IDs so a verification build does not create meaningless Git diffs.
2. Add an automated regression test proving a client is not treated as disconnected before it has connected once.
3. Strengthen the knockback assertion so it simulates movement/physics and verifies launch velocity survives control processing.
4. Reconcile any additional concrete issues found during the 0.2.5 playtest.

## P2 — Deferred design candidates

- U-030 client prediction, U-029 lobby, U-028 mode framework, U-009 ledge recovery and all new game modes require a separately approved batch.
- Do not start low gravity, infection, rotating-platform, battle-royale or power-weapon modes from this list alone.

## Recently completed

- Three-player physical input confirmed by the user (keyboard + Xbox + DualSense). The 0.3.0 three-seat item is closed.
- 2026-09-03 batch inside 0.3.0: layered impact feedback and a continuous speed streak; a five-tier bot ladder with lead prediction, line-of-sight, range discipline, dodging and launch recovery; a rebuilt menu, couch lobby and lives-only HUD; four-player local seats; weapon orientation, drop size and charge ring; four themed arenas with moving platforms; shield and shrink power-ups.
- Fixed a self-inflicted regression in the same batch where bots moved and dodged but never fired, and added a negative-tested assertion covering it.

- Prototype 0.3.0 milestone in progress: local two-player, controller input, dual-trigger firing, physical shooter recoil and display settings are maintained as one integrated version rather than separate patch-version increments.
- U-035 integrated into the same 0.3.0 milestone: local three-player menu mode, Unity Input System 1.19.0, separate Xbox/DualSense device slots, three-target camera and live two-gamepad detection; physical three-person gameplay pending.
- Prototype 0.2.6: all combat platforms widened by 10% on X only; Windows build and smoke passed, hands-on feel pending.
- Withdrawn local 0.2.6 experiment: transparent cloud/fog shapes looked reflective and were removed before commit.
- Prototype 0.2.5: verified online play, random weapon drops, CC0 weapon/city art, dusk skyline, sniper replaces rocket.
- Prototype 0.2.0: Netcode for GameObjects over Relay, host/join menu, host-authoritative replication.
- Prototype 0.1.11: knockback hitstun fix, fighter edge frame removed.
- Prototype 0.1.10: real transparent jelly material asset and transparency assertion.
- Prototype 0.1.5: muzzle-flash collider fix and projectile-survival assertion.
- 2026-09-02 reproducibility check: Windows build and `-chaosSmokeTest` passed from `v0.2.5` source.

Historical task detail is preserved under `docs/archive/`.
