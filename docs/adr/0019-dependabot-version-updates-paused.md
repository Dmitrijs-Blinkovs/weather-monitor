# 0019. Pause Dependabot version updates

Amends [0012](0012-ci-cd-with-github-actions.md).

## Context

Dependabot reads its configuration from the default branch only. When `.github/dependabot.yml` reached `main`, it
opened every pending update at once: about ten pull requests, each with its own branch. The project was just
submitted and has no maintainer reviewing updates, so they would only pile up, and an update merged without review
would change what was submitted.

## Decision

- Version updates stay configured (ecosystems, groups, 3-day cooldown) but are paused: `open-pull-requests-limit: 0`
  for NuGet, npm and GitHub Actions. Dependabot opens no version-update pull requests.
- The pull requests already opened are closed; Dependabot deletes their branches.
- Known vulnerabilities are still caught: NuGet Audit fails the build on high and critical advisories (also
  transitive) and `npm audit` gates production dependencies in CI. Dependabot security updates are a repository
  setting, independent of this file.

## Consequences

- Dependencies and pinned actions no longer move on their own; outdated versions show up only through the audits.
- Resuming is a one-line change per ecosystem (limit 5 for NuGet and npm, no limit for GitHub Actions), after which
  Dependabot opens the accumulated updates again, grouped as before.

## Alternatives considered

- Delete `dependabot.yml`: the same effect, but the grouping and cooldown decided in 0012 would have to be rewritten.
- Keep updates on and merge them weekly: right for a maintained service, not for a submitted home task.
