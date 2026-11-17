# Build and package

`Directory.Build.props` owns the mod version. Bump it for each port.
Every normal build syncs both About metadata versions from that owner before packaging.
Packaging rejects metadata that differs from the version owner.

Run `env -u RIMWORLD_MOD_DIR ./scripts/build-quiet.sh -c Release`.
The command builds RimWorld 1.7, stages the complete mod and creates a verified ZIP.
The command also checks package provenance, installation rollback and build cancellation in disposable directories.
Each run keeps its own full log under `.builds/`.
`.builds/build.log` links to the latest completed run. Successful runs print only `ok`.
The staged mod is `.builds/package/RimGPT`. The ZIP is `.builds/RimGPT.zip`.

Set `RIMWORLD_MOD_DIR` to the game's 1.7 Mods directory when installation is authorized.
On macOS, use the outer `RimWorld/Mods` path, even when it is a symlink into `RimWorldMac.app`.
The legacy `.app/Mods` alias is rejected.
The same command then installs the staged package and ZIP and verifies their contents.
Installation requires a stopped game. The command never starts or stops RimWorld.
For an existing installation, every frozen file must match the published staged baseline.
Missing, changed or additional frozen files stop installation before any installed file changes.
Matching frozen files and directories remain in place with their original inodes and modification times.
Installation updates only `1.7/`, About metadata and root metadata/documentation files.
Direct `dotnet build` performs no installation or packaging.

# Published legacy baseline

The repository tracks a 2.2.3 DLL for RimWorld 1.6.
The installed public Workshop item contains the 2.2.2 DLL.
The GitHub 2.2.3 ZIP matches the tracked DLL, so it cannot supply the Workshop baseline.

`published-baseline.json` pins every published legacy DLL and shared resource by SHA-256.
It records the separate repository hashes to detect accidental changes to frozen files.
Packaging copies the published files into staging, then adds the current About metadata, load folders and 1.7 payload.
Neither baseline preparation nor packaging changes the repository's frozen files or the installed Workshop item.
Debug files and Finder metadata are excluded from the package.

Set `RIMGPT_PUBLISHED_BASELINE` to a directory containing the pinned Workshop payload.
For Workshop item `2960127000`, this is its directory under `steamapps/workshop/content/294100/`.
The workflow verifies all pinned hashes and rejects additional legacy runtime files.
It never substitutes the tracked DLL or a different release.
If the variable is unset, the workflow uses `.builds/published-baseline`.
The local cache is ignored by Git and must contain the same pinned files.
An absent or changed baseline stops the workflow before compilation or installation.

The 1.7 build uses .NET 10 and AnyCPU.
NuGet supplies Newtonsoft.Json for 1.7. The old vendored DLL remains frozen.
