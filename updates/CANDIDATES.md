# ACTIVE UPDATE CANDIDATES

Updated: 2026-09-02

Active sections contain only unfinished work. A short completed-identifier index prevents obsolete IDs from being reopened; full history is preserved in `VERSION_*.md`, `PROJECT_STATE.md`, Git tags, and `../docs/archive/updates/CANDIDATES_FULL_2026-09-01.md`.

No new gameplay batch is currently approved.

## Validation before another feature batch

### V-001 — Prototype 0.2.5 balance and readability

- Test random-drop pacing at 5.5–9.5 seconds with the three-slot cap.
- Re-test the sniper after its damage reduction and Scatter at point-blank range.
- Confirm the dusk skyline stays calm and fighters, projectiles and platform edges remain readable.
- Record camera comfort, bot behavior and the client's visible round-trip input latency.

### V-002 — Four-player online validation

Relay play is hands-on verified with two instances only. A full four-player room still needs seat, movement, shooting, drop, elimination, rematch and disconnect validation.

## Maintenance candidates

These are technical hardening tasks, not new gameplay:

### M-001 — Deterministic generated scene

`PrototypeSceneBuilder.BuildWindows` currently regenerates scene object file IDs, leaving a semantic no-op diff in `Assets/Scenes/Prototype.unity`. Make repeated builds stable before relying on a clean-worktree version check.

### M-002 — Connection-establishment regression test

`CheckConnectionLost` correctly waits until a client has connected once before treating a later disconnect as connection loss. Add an automated state-transition test; current coverage is manual two-instance verification rather than a dedicated assertion.

### M-003 — Stronger knockback regression test

The current assertion proves that the control lock engages, but does not simulate a physics step or verify that launch velocity survives movement processing. Extend coverage without changing established feel values.

## Deferred gameplay and design candidates

The entries below require explicit approval before implementation.

### U-009 — Ledge recovery

The 0.1.7 implementation was withdrawn in 0.1.8 because bots repeatedly grabbed, timed out and re-grabbed edges. Any return must include AI hang awareness, escape/release decisions and anti-loop coverage.

### U-014 — Low-gravity/moon mode

Create a separate ruleset rather than altering base-mode gravity. Depends on an approved mode batch and preferably U-028.

### U-015 — Scatter point-blank balance

All pellets can connect at contact range. Re-test on the current arena before choosing fewer pellets, lower per-pellet damage/knockback or close-range falloff.

### U-020 / U-025 — Moving and rotating platforms

Requires rewriting the one-way-platform height/carry logic before platforms can move reliably.

### U-022 — Production-quality combat audio and feedback

Current hitstop, camera shake and procedural audio remain prototype-level. Any presentation change still requires hands-on confirmation.

### U-024 — Infection mode

Requires contact rules, team conversion, scoring, mode-specific state visuals and U-028.

### U-026 — Battle-royale mode

Requires a larger arena, shrinking-play-area rules, more content and higher player-count validation; depends on U-028.

### U-027 — Power-weapon mode

A lower-cost rules variant, but still requires U-028 and explicit approval.

### U-028 — Mode framework

Extract pluggable win conditions, scoring, spawn rules, HUD and per-mode camera behavior before implementing a second mode. Do not begin without approval.

### U-029 — Online lobby/waiting room

Show joined players, mode and bot settings, readiness and host-controlled start. Do not begin without approval.

### U-030 — Client prediction

Prediction requires input buffering, replay, reconciliation and smoothing. Treat it as an isolated high-risk networking batch; do not begin without approval.

## Completed identifiers

- U-035 local three-player mixed input: integrated into 0.3.0 with P1 keyboard, P2/P3 as separate Input System gamepads, no AI, three-target camera, device names/disconnect warning and three-seat assertions. Live Windows detection confirmed Xbox Controller + DualSense Wireless Controller; physical button gameplay remains user hands-on.
- U-034 local two-player milestone: implemented in 0.3.0 with two human seats/no AI, P1 keyboard, P2 controller/keyboard fallback, shared camera and separate offscreen indicators. The same milestone now includes LT/RT firing, physical weapon-specific shooter recoil and display settings up to 4K/Borderless. P2 movement and A jump passed hands-on; the integrated refinements remain pending hands-on confirmation.
- U-033 sniper identity tuning: implemented in 0.2.6 with knockback `(6.4,2.2)` → `(7.0,2.4)` and cooldown `1.5s` → `1.7s`; damage remains10 and ammo remains3. Hands-on feel pending.
- U-032 arena-space tuning: implemented in 0.2.6; every combat platform is 10% wider on X and ring-out bounds are extended to `|x|>19 / y<-9.5` so air control and double jump can recover. Hands-on feel pending.
- U-031 transparent cloud/fog approach: invalidated after hands-on review because jelly transparency made the shapes look reflective. Keep the 0.2.5 background simple; do not restore this approach.
- U-007 Host/Join proof: completed and verified with two instances; four-player testing is V-002.
- U-011 local-player offscreen guidance: completed in 0.1.7.
- U-016 rocket-specific issue: obsolete because the rocket was replaced by the sniper.
- U-017 tighter ring-out bounds and projectiles passing through one-way platforms: completed in 0.1.7.
- U-018/U-019/U-021/U-023: Geometry Fighters direction, visual pass, ArenaBuilder extraction and geometric bodies are complete.
