#!/usr/bin/env bash
#
# scrub-history.sh — one-time history rewrite before this repo goes public.
#
# WHY THIS IS A SEPARATE MANUAL STEP (not done in a PR):
#   `git rm --cached` (already committed on the feature branch) stops tracking the
#   Obsidian vault and the stray .canvas file from HEAD onward, but they remain in
#   older commits. A truly clean "clean-room" history requires rewriting every
#   commit with git-filter-repo, which produces new commit hashes for the WHOLE
#   repo and therefore REQUIRES a coordinated force-push. That cannot be done from
#   inside a feature-branch worktree/PR — it must run once on `main`, before any
#   collaborator clones the repo.
#
# PREREQUISITES:
#   - git-filter-repo installed:  pipx install git-filter-repo   (or: pip install git-filter-repo)
#   - A FRESH, full clone of the repo (filter-repo refuses to run on a worktree/
#     non-fresh clone by design). Do NOT run this in the dev worktree.
#
# USAGE:
#   git clone <repo> gphotos-scrub && cd gphotos-scrub
#   bash scripts/scrub-history.sh
#   # inspect the result, then:
#   git push --force --all && git push --force --tags
#
set -euo pipefail

if ! command -v git-filter-repo >/dev/null 2>&1 && ! git filter-repo --version >/dev/null 2>&1; then
  echo "ERROR: git-filter-repo is not installed. Install it first:" >&2
  echo "  pipx install git-filter-repo   # or: pip install git-filter-repo" >&2
  exit 1
fi

echo ">> Scanning history for the files to purge (pre-flight)…"
git log --all --oneline --name-only -- '.obsidian/*' '*.canvas' || true

echo
echo ">> Rewriting history to remove the Obsidian vault and the .canvas file…"
git filter-repo \
  --path-glob '.obsidian/*' \
  --path-glob '*.canvas' \
  --invert-paths \
  --force

echo
echo ">> Done. Verify nothing remains:"
git log --all --oneline -- '.obsidian/*' '*.canvas' && echo "(empty above = clean)"

cat <<'EOF'

NEXT STEPS (manual, destructive — coordinate before collaborators clone):
  git remote add origin <your-remote-url>   # filter-repo removes the remote by design
  git push --force --all
  git push --force --tags

SECURITY NOTE: before force-pushing, confirm no secrets ever lived in history:
  git log -p --all | grep -iE 'password|secret|token|api[_-]?key|BEGIN .*PRIVATE KEY' || echo "no obvious secrets"
EOF
