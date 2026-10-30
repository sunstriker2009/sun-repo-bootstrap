# Roadmap

## Milestone 1 — Manual governance bootstrap (this PR)
- [x] .NET 10 CLI supporting audit, plan, apply, verify
- [x] Explicit target confirmation and fail-closed checks on apply
- [x] Strict required CI ruleset and squash-only repository settings
- [x] Manual GitHub Actions dispatch and build CI
- [ ] Validate workflow against a disposable test repository
- [ ] Configure protected apply environment and fine-grained token

## Milestone 2 — Compliance hardening
- [ ] Detect effective/inherited branch rulesets and inspect full rule parameters
- [ ] Detect and safely reconcile existing rulesets without weakening protection
- [ ] Enforce CODEOWNERS and explicit review gates for sensitive paths
- [ ] Conventional Commit PR-title validation and branch naming checks
- [ ] Validate required CI names across pagination, GitHub Actions and external providers
- [ ] Structured JSON reports, tests and exit-code conventions
- [ ] Safely verify failed-CI blocking with test PR

## Milestone 3 — Provisioning templates
- [ ] Optional CI/PR templates for new repositories
- [ ] Idempotent provisioning and drift detection across multiple repositories
- [ ] Release policy and SemVer audit (no automatic publishing)

**Policy:** [SUN Git Workflow & Merge Policy v1.0](https://github.com/sunstriker2009/sun-ecosystem/blob/main/docs/governance/GIT-WORKFLOW.md).
