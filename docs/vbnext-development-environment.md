# VB.Next development environment

This is the operational policy for Cursor, Grok, and other development agents. It does not change the language mission in `docs/vbnext-mission.md`.

Agents find this document from `AGENTS.md` and `.github/copilot-instructions.md`. Cursor also loads `.cursor/rules/vbnext-development-environment.mdc`, which points here.

## Designated directories

Each contributor designates separate local directories for these roles:

- Active source: the compiler repository checkout.
- Test projects: generated or experimental projects, including a working copy made so a retired test can be run again.
- Temporary output: scratch files and other short-lived build output.
- Evidence: completion reports and verification records that are kept.
- Historical archives: retired work kept after a verified copy.

Do not create an arbitrary test directory at the root of a system drive. Unrelated personal projects stay outside these roles. Do not inspect, move, or delete them as part of VB.Next work.

Machine-specific paths, package-cache locations, and free-space numbers belong in that contributor's local configuration. Building VB.Next does not require a particular drive letter or directory layout.

## Temporary artifacts

A temporary artifact has an identifiable purpose and a lifecycle: what it is for, and when it is removed. A temporary directory is not an archive.

## Archives

Do not remove a unique historical tree until a verified archive exists and the owner has explicitly authorized that removal. Historical archives are records. Do not use them as writable development workspaces. To rerun an archived test, make a separate working copy under the designated test-project directory and correct paths there.

## Environment and preflight

When the local configuration supplies an environment initialization step, run it before a compiler restore, build, or test. Before a substantial restore, build, or test, check free disk space and available memory, including pagefile allocation when the local configuration calls for it. Stop when that configuration's safe threshold is reached. Leave the pagefile unchanged unless the owner asks for a change.

## Destructive cleanup

Do not delete package caches, editor profiles, personal projects, historical compiler snapshots, or archives without explicit owner authorization for that deletion.
