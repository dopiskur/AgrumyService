#!/usr/bin/env python3
"""Fails if a newly ADDED source line introduces a "Roadmap #NNN"-style ticket
reference inside a code comment - CLAUDE.md's comment-style rule says that
number belongs only in the commit message, never in code, even while fixing
that exact item. Existing violations are left alone (roadmap #269's gradual
cleanup already covers those); this only gates NEW ones from creeping back in.
"""
import os
import re
import subprocess
import sys

SOURCE_GLOBS = ("*.cs", "*.cshtml", "*.cpp", "*.h", "*.hpp")

# Matches CLAUDE.md's own named bad examples: "Roadmap #191", "roadmap #191", "#191:".
TICKET_PATTERN = re.compile(r"(?i)\broadmap\s*#\d+\b|#\d+\s*:")


def find_comment_start(text: str) -> int:
    """Index of the first genuine //, /* or @* comment marker, or -1. A // right after ':' is
    skipped - that's http://, https:// inside a string/URL, not a comment."""
    for marker in ("//", "/*"):
        idx = 0
        while True:
            idx = text.find(marker, idx)
            if idx == -1:
                break
            if idx == 0 or text[idx - 1] != ":":
                return idx
            idx += 1
    return text.find("@*")


def run(*args: str) -> str:
    return subprocess.run(args, capture_output=True, text=True, check=True).stdout


def resolve_base_sha() -> str:
    base = os.environ.get("BASE_SHA", "")
    if base and base != "0" * 40:
        return base
    # New branch with no prior commit to diff against - fall back to the repo's very first commit.
    return run("git", "rev-list", "--max-parents=0", "HEAD").splitlines()[0]


def main() -> int:
    base_sha = resolve_base_sha()
    diff = run("git", "diff", "--unified=0", base_sha, "HEAD", "--", *SOURCE_GLOBS)

    violations: list[tuple[str, int | None, str]] = []
    current_file = None
    current_line_no = None
    for line in diff.splitlines():
        if line.startswith("+++ b/"):
            current_file = line[6:]
            continue
        if line.startswith("@@"):
            # "@@ -a,b +c,d @@" - c is the first new-file line number this hunk touches.
            match = re.search(r"\+(\d+)", line)
            current_line_no = int(match.group(1)) if match else None
            continue
        if line.startswith("+") and not line.startswith("+++"):
            added_text = line[1:]
            comment_start = find_comment_start(added_text)
            if comment_start != -1 and TICKET_PATTERN.search(added_text[comment_start:]):
                violations.append((current_file, current_line_no, added_text.strip()))
            if current_line_no is not None:
                current_line_no += 1

    if violations:
        print("New 'Roadmap #NNN' reference(s) in code comments - not allowed.")
        print("The ticket number belongs only in the commit message (see CLAUDE.md's comment-style rule).\n")
        for file, line_no, text in violations:
            print(f"  {file}:{line_no}: {text}")
        return 1

    print("No new Roadmap #NNN references introduced.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
