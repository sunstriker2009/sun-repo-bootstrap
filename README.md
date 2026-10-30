# SUN Repository Bootstrap

Manually triggered governance provisioner for [SUN Git Workflow & Merge Policy v1.0](https://github.com/sunstriker2009/sun-ecosystem/blob/main/docs/governance/GIT-WORKFLOW.md).

## Operations

| Mode | Effect |
| --- | --- |
| audit | Report settings drift and ruleset presence; exit 2 if drift found |
| plan | Show intended changes without modifying repositories |
| apply | Create a protected-main ruleset and set squash-only merge/auto-merge/branch deletion |
| verify | Check settings and strict CI/conversation-resolution rules |

This is the **first milestone**, not full policy compliance. It intentionally refuses to overwrite an existing SUN-named ruleset, requires existing exact CI check names, and does not install CI or configure CODEOWNERS in target repositories.

## Setup

1. Create a fine-grained `SUN_REPO_ADMIN_TOKEN` secret with repository Administration permissions (write for apply, read for audit), and Checks/Metadata read access. Scope it to approved targets.
2. Configure GitHub Actions environments `repository-administration` (**required reviewers for apply**) and `repository-audit`. The workflow references these names but does not create or protect them automatically.
3. Run CI on the latest `main` commit of the target repository and note the exact check-run names.
4. Go to **Actions → SUN repository provisioning → Run workflow**. Start with `plan`, then `apply`, entering exact required checks and repeating the target repository for confirmation.
5. Run `verify`. Manually test that failed CI blocks merging; separately enforce sensitive-change reviewer rules.

## Local usage

Requires .NET 10 SDK and `GH_TOKEN`:

```sh
dotnet run --project src/Sun.RepoBootstrap -- --repo sunstriker2009/example --mode plan
dotnet run --project src/Sun.RepoBootstrap -- --repo sunstriker2009/example --mode apply --checks build,test --confirm sunstriker2009/example
```

## Safety

- Apply requires explicit matching confirmation and existing CI checks, and fails closed on an existing named ruleset.
- Rule creation precedes merge-setting changes; a partial API failure requires manual inspection.
- GitHub personal-account/private-repository feature availability depends on plan and permissions.
- Version 1 does not yet validate full inherited rulesets, reviewer approvals, conventional commit titles, release procedures, or fail-CI blocking.
- This project does not modify ORYVA or MarketPilot AI unless a future explicitly approved apply operation targets them.

See [roadmap](docs/ROADMAP.md).
