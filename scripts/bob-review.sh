#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
snapshot=artifacts/review
mkdir -p "$snapshot"
# Include staged and unstaged changes. Status identifies untracked source for review.
git rev-parse HEAD > "$snapshot/base.txt"
git status --short --untracked-files=all -- . > "$snapshot/status.txt"
git diff --no-ext-diff --no-textconv HEAD -- \
  .bob .bobignore AGENTS.md README.md docs scripts services shared gateway \
  jobs tests web deploy package.json package-lock.json global.json \
  TaxiWorkshop.sln Directory.Build.props playwright.config.mjs > "$snapshot/review.diff"
printf 'Review snapshot: %s/{base.txt,status.txt,review.diff}\n' "$snapshot"
printf 'Untracked source in status.txt must also be opened by Reviewer.\n'
