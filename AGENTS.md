# Project agent guidance

## Security and reverse-engineering tasks

For reverse engineering, APK/binary/firmware analysis, CTF, penetration testing,
security assessment, packet capture, exploit research, malware analysis, or other
security tasks, use the local router at `reverse-skill/` before acting:

1. Read `reverse-skill/RULES.md` as the behavior-chain source of truth.
2. Use `reverse-skill/skills/tool-index.md` as the source of truth for installed tools.
3. On Windows, route with `reverse-skill/skills/scripts/master-route.ps1`.
4. Initialize `work/<case>/scope.md` with `reverse-skill/skills/scripts/case-init.ps1`.
5. Do not perform target actions until the case has explicit granted authorization and a valid network profile, or an explicitly authorized offline sample.
6. Open the routed PRIMARY `SKILL.md` and follow its `ACTION REQUIRED` section.

Do not register MCP servers, install commercial tools, use credentials, or change external state without the user's authorization. Missing tools should be installed only when required by an authorized task.
