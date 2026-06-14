# Repository Agent Instructions

These instructions apply to all AI-agent work in this repository.

## Project Shape

- This is a .NET/C# solution for Kerajel.NuclearEvaluation.
- Prefer the existing solution structure, project boundaries, and local conventions.
- Keep changes scoped to the requested behavior. Do not reorganize projects, Docker files, or test layout unless the task explicitly calls for it.

## Generated Artifact Hygiene

This repository can become very large after builds, Docker/e2e runs, Playwright runs, or local experiments. Large ignored artifacts have previously made Codex Desktop hang when entering the project.

Do not leave generated artifacts in the repo after a task unless the user explicitly asks to preserve them. In particular, clean up these paths after running related commands:

- `.tmp/`
- `src/**/bin/`
- `src/**/obj/`
- `tests/**/bin/`
- `tests/**/obj/`
- `tests/e2e/playwright-report/`
- `tests/e2e/test-results/`
- `tests/e2e/node_modules/`
- `*.binlog`

Use an external scratch/cache location for large experiments, benchmark files, generated datasets, extracted archives, database files, browser traces, and native binaries. Prefer a path outside the repository, such as `D:\Cache\.codex\Kerajel.NuclearEvaluation`, when available.

If an artifact is useful for debugging and should be kept temporarily, say so explicitly in the final response and include its path and approximate size.

## Cleanup Guardrail

Before finishing any task that ran builds, tests, Docker, Playwright, data generation, or benchmarks:

1. Check for generated output under the artifact paths listed above.
2. Remove generated ignored output that is not needed by the user.
3. Run `git status -sb` and make sure cleanup did not remove or modify source files.
4. Mention any remaining large or suspicious artifacts explicitly.

Do not use broad cleanup commands that can remove user work. Avoid `git clean -xfd`. If a git clean is truly needed, prefer a dry run first and keep it to ignored files only.

A healthy post-cleanup raw tree should normally be hundreds of files, not thousands. If a raw no-ignore walk is unexpectedly large, inspect the largest directories before ending the task.

Useful checks on Windows PowerShell:

```powershell
rg --files --hidden --no-ignore | Measure-Object
Get-ChildItem -Force -Recurse -File | Measure-Object -Property Length -Sum
```

## Tests And Verification

- Run focused unit tests when behavior changes warrant it.
- Do not run the full UI/e2e suite unless the user asks for it or the risk justifies the cost.
- If you add UI/e2e tests but intentionally do not run them, state that clearly in the final response.
- After Playwright/e2e runs, remove report and result directories unless the user needs the traces.

## Safety Notes

- Preserve user changes. This repo may have active modified and untracked files.
- Do not delete `.claude/`, `.codex/`, local settings, or user-created scratch files unless the user explicitly asks.
- Watch for suspicious data, including out-of-sequence IDs, future dates, inconsistent nearby values, malformed IDs, and unexpectedly broad or narrow datasets. Call these out explicitly.
