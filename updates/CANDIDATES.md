# ACTIVE UPDATE CANDIDATES

Updated: 2026-09-02

This file contains only unfinished feature candidates. The original detailed backlog is archived at `../docs/archive/updates/CANDIDATES_FULL_2026-09-01.md`.

## Highest priority

### U-009 — Ledge recovery

Add one readable, learnable recovery opportunity near arena edges.

### U-011 — Remaining offscreen guidance

Prototype 0.1.4 completed arena-anchored local-player soft follow. A clear local-player offscreen/ring-out direction indicator remains unfinished; two-character dynamic framing was rejected for the future per-client multiplayer camera direction.

## Carried over from 0.1.4 playtest (balance, not yet approved)

### U-015 — Scatter point-blank burst

All five pellets connect at contact range for 17.5 internal damage and 6.25 base knockback in one trigger pull, before the health multiplier. Consider fewer pellets, lower per-pellet knockback, or close-range falloff.

### U-016 — Rocket self-knockback

`Explode` skips the owner entirely, so firing a rocket at an adjacent enemy carries no risk. Decide whether this stays forgiving or gains self-knockback for risk/reward.

### U-017 — Ring-out margin and rocket detonation on one-way platforms

Ring-out triggers only past |x|>16 while platforms end near ±9.5, creating a long invisible fall. Rockets also detonate on the underside of one-way platforms. Tune alongside U-009.

## Following candidates

### U-007 — Internet duel

Prove a two-player Host/Join match before expanding the networking design.

### U-014 — Low-gravity party mode

Create a separate moon/low-gravity ruleset.

## Completed candidate groups

- U-003–U-006: platforms, fighter collision rules, hidden values and AI difficulty presets.
- U-012–U-013: shooting feedback, AI behavior, hit impact and air-combat tuning.
- U-008: immediate-start stock match, final elimination, winner and rematch loop.
- U-010: Pulse SMG, Scatter Blaster and Rocket Launcher fixed pickup loop.
- 0.1.5: muzzle-flash collider fix (projectiles self-destructed at the barrel), auto-rematch, respawn protection shield and balanced auto-zoom camera.
- U-011 partial: arena-anchored local-player soft-follow camera; offscreen direction guidance remains active.
