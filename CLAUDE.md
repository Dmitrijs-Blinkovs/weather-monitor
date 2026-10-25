@AGENTS.md

## Optional Claude integration

Shared contributor rules live in `AGENTS.md`; task procedures live in `docs/runbooks/`.
The files in `.claude/skills/` are discovery wrappers that link to those procedures.
`.claude/launch.json` optionally starts Azurite, Functions and Web through Claude preview tools.
Neither Claude nor these integration files is required to build, test or run the application.
