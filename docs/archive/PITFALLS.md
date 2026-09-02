# PITFALLS — 原创2.5D在线平台射击游戏

Record only demonstrated failed, misleading, unsafe, or persistently low-value routes. Do not add speculative warnings.

## P-001 — Changing the color of primitive default materials is not a valid URP material path

- Date: 2026-08-31
- Demonstrated failure: Procedural platforms, background pieces, fighters, and projectiles were created through `GameObject.CreatePrimitive`; code changed `Renderer.material.color` but retained the primitive's built-in default material. The URP Windows player rendered large surfaces bright magenta and much of the foreground black.
- Evidence: User screenshot `codex-clipboard-65cb87fe-b53d-4782-a47b-7af2a0921017.png`; creation/color assignments in `game-client/Assets/ChaosArena/Runtime/PrototypeBootstrap.cs` and `PrototypeProjectile.cs`.
- Lesson: Runtime procedural geometry must receive a known URP-compatible material whose shader is explicitly included in the player build. Headless initialization smoke tests cannot validate rendered output.
- Avoid recurrence: Keep reusable URP Lit/Unlit material assets in the project, assign them rather than relying on primitive defaults, and include a rendered-frame visual check in visual-pass acceptance.
- Resolution: Fixed on 2026-08-31 through `PrototypeMaterials.cs` and generated `Assets/Resources/ChaosArenaMaterials/PrototypeLit.mat` / `PrototypeUnlit.mat`. Rebuild, smoke, runtime log scan, and direct Windows window capture passed.
