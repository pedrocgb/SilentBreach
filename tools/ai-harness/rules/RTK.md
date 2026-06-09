# RTK - Rust Token Killer (Codex CLI)

**Usage**: Token-optimized CLI proxy for shell commands.

## Rule

Always prefix shell commands with `rtk`.

Examples:

```bash
rtk git status
rtk cargo test
rtk npm run build
rtk pytest -q
```

## Meta Commands

```bash
rtk gain            # Token savings analytics
rtk gain --history  # Recent command savings history
rtk proxy <cmd>     # Run raw command without filtering
```

## Verification

```bash
rtk --version
rtk gain
which rtk
```

## RTK usage

RTK binary path:

`/home/pedrohcg/.local/bin/rtk`

When running shell commands that can produce long output, prefer RTK. Do not use raw commands unless RTK is unavailable or the command is not supported.

## Git

- Use `/home/pedrohcg/.local/bin/rtk git status` instead of `git status`
- Use `/home/pedrohcg/.local/bin/rtk git log` instead of `git log`
- Use `/home/pedrohcg/.local/bin/rtk git diff` instead of `git diff`
- Use `/home/pedrohcg/.local/bin/rtk git show` instead of `git show`
- Use `/home/pedrohcg/.local/bin/rtk git stash list` instead of `git stash list`

## GitHub CLI

- Use `/home/pedrohcg/.local/bin/rtk gh pr view` instead of `gh pr view`
- Use `/home/pedrohcg/.local/bin/rtk gh pr checks` instead of `gh pr checks`
- Use `/home/pedrohcg/.local/bin/rtk gh run list` instead of `gh run list`
- Use `/home/pedrohcg/.local/bin/rtk gh issue view` instead of `gh issue view`

## Graphite

- Use `/home/pedrohcg/.local/bin/rtk gt log` instead of `gt log`
- Use `/home/pedrohcg/.local/bin/rtk gt status` instead of `gt status`

## Rust / Cargo

- Use `/home/pedrohcg/.local/bin/rtk cargo test` instead of `cargo test`
- Use `/home/pedrohcg/.local/bin/rtk cargo nextest` instead of `cargo nextest`
- Use `/home/pedrohcg/.local/bin/rtk cargo build` instead of `cargo build`
- Use `/home/pedrohcg/.local/bin/rtk cargo check` instead of `cargo check`
- Use `/home/pedrohcg/.local/bin/rtk cargo clippy` instead of `cargo clippy`

## JavaScript / TypeScript

- Use `/home/pedrohcg/.local/bin/rtk jest` instead of `jest`
- Use `/home/pedrohcg/.local/bin/rtk vitest` instead of `vitest`
- Use `/home/pedrohcg/.local/bin/rtk tsc` instead of `tsc`
- Use `/home/pedrohcg/.local/bin/rtk eslint` instead of `eslint`
- Use `/home/pedrohcg/.local/bin/rtk pnpm list` instead of `pnpm list`
- Use `/home/pedrohcg/.local/bin/rtk pnpm outdated` instead of `pnpm outdated`
- Use `/home/pedrohcg/.local/bin/rtk next build` instead of `next build`
- Use `/home/pedrohcg/.local/bin/rtk prisma migrate` instead of `prisma migrate`
- Use `/home/pedrohcg/.local/bin/rtk playwright test` instead of `playwright test`

## Python

- Use `/home/pedrohcg/.local/bin/rtk pytest` instead of `pytest`
- Use `/home/pedrohcg/.local/bin/rtk ruff check` instead of `ruff check`
- Use `/home/pedrohcg/.local/bin/rtk mypy` instead of `mypy`
- Use `/home/pedrohcg/.local/bin/rtk pip install` instead of `pip install`

## Go

- Use `/home/pedrohcg/.local/bin/rtk go test` instead of `go test`
- Use `/home/pedrohcg/.local/bin/rtk golangci-lint run` instead of `golangci-lint run`
- Use `/home/pedrohcg/.local/bin/rtk go build` instead of `go build`

## Ruby

- Use `/home/pedrohcg/.local/bin/rtk rspec` instead of `rspec`
- Use `/home/pedrohcg/.local/bin/rtk rubocop` instead of `rubocop`
- Use `/home/pedrohcg/.local/bin/rtk rake` instead of `rake`

## .NET

- Use `/home/pedrohcg/.local/bin/rtk dotnet build` instead of `dotnet build`
- Use `/home/pedrohcg/.local/bin/rtk dotnet test` instead of `dotnet test`
- Use `/home/pedrohcg/.local/bin/rtk dotnet format` instead of `dotnet format`

## Docker / Kubernetes

- Use `/home/pedrohcg/.local/bin/rtk docker ps` instead of `docker ps`
- Use `/home/pedrohcg/.local/bin/rtk docker images` instead of `docker images`
- Use `/home/pedrohcg/.local/bin/rtk docker logs` instead of `docker logs`
- Use `/home/pedrohcg/.local/bin/rtk docker compose up` instead of `docker compose up`
- Use `/home/pedrohcg/.local/bin/rtk kubectl get pods` instead of `kubectl get pods`
- Use `/home/pedrohcg/.local/bin/rtk kubectl logs` instead of `kubectl logs`

## Files and Search

- Use `/home/pedrohcg/.local/bin/rtk ls` instead of `ls`
- Use `/home/pedrohcg/.local/bin/rtk find` instead of `find`
- Use `/home/pedrohcg/.local/bin/rtk grep` instead of `grep`
- Use `/home/pedrohcg/.local/bin/rtk rg` instead of raw `rg` when output may be large
- Use `/home/pedrohcg/.local/bin/rtk diff` instead of `diff`
- Use `/home/pedrohcg/.local/bin/rtk wc` instead of `wc`
- Use `/home/pedrohcg/.local/bin/rtk read <file>` instead of `cat <file>`
- Use `/home/pedrohcg/.local/bin/rtk read <file>` instead of `head <file>`
- Use `/home/pedrohcg/.local/bin/rtk read <file>` instead of `tail <file>`
- Use `/home/pedrohcg/.local/bin/rtk smart <file>` for a compact code summary

## Cloud and Data

- Use `/home/pedrohcg/.local/bin/rtk aws` instead of `aws`
- Use `/home/pedrohcg/.local/bin/rtk psql` instead of `psql`
- Use `/home/pedrohcg/.local/bin/rtk curl` instead of `curl`

## RTK utility commands

- Use `/home/pedrohcg/.local/bin/rtk --version` to check RTK version
- Use `/home/pedrohcg/.local/bin/rtk gain` to check token savings
- Use `/home/pedrohcg/.local/bin/rtk discover` to check missed optimization opportunities
- Use `/home/pedrohcg/.local/bin/rtk proxy <command>` for unsupported commands that should still be tracked
- Use `/home/pedrohcg/.local/bin/rtk init --show` to inspect RTK hook status
- Use `/home/pedrohcg/.local/bin/rtk init --global --dry-run -v` to preview global RTK setup changes

## Global flags

- Add `--ultra-compact` when the output should be as small as possible
- Add `-v`, `-vv`, or `-vvv` when debugging RTK behavior
- Prefer `--ultra-compact` instead of `-u`, because Git uses `-u` for other behavior

## Failure behavior

If a command fails and RTK gives a tee log path, read that file only if more detail is needed.

If RTK is unavailable, check:

`ls -la /home/pedrohcg/.local/bin/rtk`

Do not fall back to raw commands unless `/home/pedrohcg/.local/bin/rtk` is missing or the RTK command fails.
