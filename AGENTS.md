# Project workflow

- Run `env -u RIMWORLD_MOD_DIR ./scripts/build-quiet.sh -c Release` for local build, package staging and ZIP verification.
- The staged mod is `.builds/package/RimGPT`. The ZIP is `.builds/RimGPT.zip`.
- Set `RIMWORLD_MOD_DIR` to the RimWorld 1.7 Mods directory to build, package, install and verify together.
- Installation refuses a running game. The workflow never stops or starts RimWorld.
- Direct `dotnet build` compiles only. Use the quiet workflow for packaging and installation.
- Read `scripts/README.md` for the required published baseline and its provenance.
- Preserve `1.6/`, `Older/`, `Defs/`, `Textures/` and `Libraries/`. Put current builds and resource changes under `1.7/`.
- The tracked 1.6 DLL differs from the published Workshop baseline. Never package the tracked DLL as the legacy payload.

- `Directory.Build.props` owns the mod version. Bump it for a port.
- Every normal build syncs `About/About.xml` and `About/Manifest.xml` from that version owner.
- Installation must preserve matching frozen payload files in place, including their inodes and modification times.
- If an existing installation has missing, changed or additional frozen payload files, fail before changing installed files.
