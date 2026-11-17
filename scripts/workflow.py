#!/usr/bin/env python3
"""Build 1.7 and stage a package with the pinned published legacy payload."""

import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import signal
import struct
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
from zipfile import ZIP_DEFLATED, ZipFile

ROOT = Path(__file__).resolve().parents[1]
FROZEN = ("1.6", "Older", "Defs", "Textures")
ACTIVE_DLLS = ("RimGPT.dll", "CrossPromotion.dll", "Newtonsoft.Json.dll")


def digest(data):
    return hashlib.sha256(data).hexdigest()


def payload_files(root, folders):
    return {
        str(path.relative_to(root)): path
        for folder in folders
        for path in (root / folder).rglob("*")
        if path.is_file() and path.name != ".DS_Store"
        and not path.name.startswith("._") and path.suffix.lower() not in (".pdb", ".mdb")
    }


def verify_files(root, expected):
    for name, expected_hash in expected.items():
        path = root / name
        if not path.is_file() or digest(path.read_bytes()) != expected_hash:
            raise ValueError(f"Baseline mismatch: {path}. Use the published payload recorded in scripts/published-baseline.json.")


def verify_repository(manifest):
    verify_files(ROOT, manifest["repositoryFiles"])
    actual = set(payload_files(ROOT, (*FROZEN, "Libraries")))
    if actual != set(manifest["repositoryFiles"]):
        raise ValueError(f"Frozen repository inventory changed: {sorted(actual ^ set(manifest['repositoryFiles']))}")


def baseline(manifest):
    source = Path(os.environ.get("RIMGPT_PUBLISHED_BASELINE", ROOT / ".builds/published-baseline")).expanduser().resolve()
    verify_files(source, manifest["files"])
    actual = set(payload_files(source, FROZEN))
    if actual != set(manifest["files"]):
        raise ValueError(f"Unexpected frozen payload files in {source}: {sorted(actual ^ set(manifest['files']))}")
    return source


def verify_anycpu(path):
    data = path.read_bytes()
    pe = struct.unpack_from("<I", data, 0x3C)[0]
    if data[pe:pe + 4] != b"PE\0\0":
        raise ValueError(f"Not a PE assembly: {path}")
    machine, sections = struct.unpack_from("<HH", data, pe + 4)
    optional_size = struct.unpack_from("<H", data, pe + 20)[0]
    optional = pe + 24
    if machine != 0x14C or struct.unpack_from("<H", data, optional)[0] != 0x10B:
        raise ValueError(f"Expected an AnyCPU PE32 assembly: {path}")
    cli_rva = struct.unpack_from("<I", data, optional + 96 + 14 * 8)[0]
    for section in range(sections):
        offset = optional + optional_size + section * 40
        size, rva, raw_size, raw = struct.unpack_from("<IIII", data, offset + 8)
        if rva <= cli_rva < rva + max(size, raw_size):
            flags = struct.unpack_from("<I", data, raw + cli_rva - rva + 16)[0]
            if flags & 1 and not flags & (2 | 0x20000):
                return
    raise ValueError(f"Expected IL-only AnyCPU without a 32-bit preference: {path}")


def file_hashes(root):
    return {str(p.relative_to(root)): digest(p.read_bytes()) for p in root.rglob("*") if p.is_file()}


def verify_versions():
    version = ET.parse(ROOT / "Directory.Build.props").findtext(".//ModVersion")
    about = ET.parse(ROOT / "About/About.xml").findtext("modVersion")
    manifest = ET.parse(ROOT / "About/Manifest.xml").findtext("version")
    if not version or (about, manifest) != (version, version):
        raise ValueError("Version metadata differs from Directory.Build.props; run the normal build to synchronize it")


def stage(manifest, source):
    verify_versions()
    builds = ROOT / ".builds"
    with tempfile.TemporaryDirectory(prefix="package-", dir=builds) as temporary:
        package = Path(temporary) / "RimGPT"
        package.mkdir()
        for name, expected_hash in manifest["files"].items():
            data = (source / name).read_bytes()
            if digest(data) != expected_hash:
                raise ValueError(f"Published baseline changed during staging: {name}")
            target = package / name
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
        for name in ("LICENSE", "README.md", "LoadFolders.xml"):
            shutil.copy2(ROOT / name, package / name)
        shutil.copytree(ROOT / "About", package / "About", ignore=shutil.ignore_patterns(".DS_Store", "._*", "*.psd"))
        active = package / "1.7/Assemblies"
        active.mkdir(parents=True)
        for name in ACTIVE_DLLS:
            shutil.copy2(ROOT / "1.7/Assemblies" / name, active / name)
        for folder in ("Defs", "Textures", "Languages"):
            if (ROOT / "1.7" / folder).is_dir():
                shutil.copytree(ROOT / "1.7" / folder, package / "1.7" / folder,
                                ignore=shutil.ignore_patterns(".DS_Store", "._*"))
        verify_files(package, manifest["files"])
        verify_anycpu(active / "RimGPT.dll")
        expected = file_hashes(package)
        archive = Path(temporary) / "RimGPT.zip"
        with ZipFile(archive, "w", ZIP_DEFLATED) as zip_file:
            for name in sorted(expected):
                zip_file.write(package / name, name)
        with ZipFile(archive) as zip_file:
            if {name: digest(zip_file.read(name)) for name in zip_file.namelist()} != expected:
                raise ValueError("ZIP differs from the staged package")
        destination = builds / "package/RimGPT"
        destination.parent.mkdir(exist_ok=True)
        if destination.exists():
            shutil.rmtree(destination)
        shutil.move(str(package), destination)
        os.replace(archive, builds / "RimGPT.zip")
        (builds / "package-hashes.json").write_text(json.dumps(expected, indent=2) + "\n")
    return destination


def ensure_stopped():
    result = subprocess.run(["ps", "-A", "-o", "comm="], check=True, capture_output=True, text=True)
    for command in result.stdout.splitlines():
        name = Path(command.strip()).name.lower()
        if name in ("rimworld", "rimworldlinux", "rimworldlinux.x86_64", "rimworldwin64.exe"):
            raise ValueError("RimWorld is running. Installation requires a stopped game.")


def validate_mods_root(value):
    mods = Path(os.path.abspath(os.path.expanduser(value)))
    physical = mods.resolve(strict=True)
    parent = mods.parent
    if parent.suffix.casefold() == ".app":
        outer = parent.parent / "Mods"
        if outer.is_dir() and outer.resolve(strict=True) == physical:
            raise ValueError(f"Use the outer RimWorld 1.7 Mods path: {outer}")
    markers = (parent / "RimWorld.app/Contents/MacOS/Assembly-CSharp.dll",
               parent / "Contents/MacOS/Assembly-CSharp.dll", parent / "Assembly-CSharp.dll")
    if mods.name != "Mods" or physical.name != "Mods" or not mods.is_dir() or not any(p.is_file() for p in markers):
        raise ValueError(f"RIMWORLD_MOD_DIR must name a RimWorld 1.7 Mods directory: {mods}")
    if (mods / "RimGPT").is_symlink():
        raise ValueError("Refusing to replace a symlink at Mods/RimGPT")
    return mods


def verify_installed_legacy(destination, expected):
    if not destination.exists():
        return
    frozen = {name: value for name, value in expected.items() if name.split("/", 1)[0] in FROZEN}
    installed = {folder + "/" + name: value for folder in FROZEN
                 for name, value in file_hashes(destination / folder).items()}
    if installed != frozen:
        changed = sorted(name for name in installed.keys() | frozen.keys() if installed.get(name) != frozen.get(name))
        raise ValueError(f"Installed legacy payload differs from the published baseline; no changes made: {changed}")


def install(package, mods):
    ensure_stopped()
    expected = file_hashes(package)
    destination = mods / "RimGPT"
    verify_installed_legacy(destination, expected)
    existing = destination.exists()
    with tempfile.TemporaryDirectory(prefix=".RimGPT-", dir=mods) as temporary:
        temporary = Path(temporary)
        candidate = temporary / "RimGPT"
        shutil.copytree(package, candidate)
        if file_hashes(candidate) != expected:
            raise ValueError("Installation staging differs from the package")
        archive = temporary / "RimGPT.zip"
        shutil.copy2(ROOT / ".builds/RimGPT.zip", archive)
        with ZipFile(archive) as zip_file:
            if {name: digest(zip_file.read(name)) for name in zip_file.namelist()} != expected:
                raise ValueError("Installation ZIP differs from the package")
        # Keep the backup outside automatic temporary cleanup if rollback fails.
        backup = mods / (temporary.name + "-previous")
        ensure_stopped()
        verify_installed_legacy(destination, expected)
        changed = []
        try:
            if existing:
                backup.mkdir()
                # Frozen directories and the installed mod directory stay in place.
                for item in sorted(candidate.iterdir()):
                    if item.name in FROZEN:
                        continue
                    target = destination / item.name
                    if target.exists():
                        target.rename(backup / item.name)
                    changed.append(item.name)
                    item.rename(target)
            else:
                candidate.rename(destination)
            if file_hashes(destination) != expected:
                raise ValueError("Installed files differ from the package")
            os.replace(archive, mods / "RimGPT.zip")
        except BaseException:
            if existing:
                for name in reversed(changed):
                    target = destination / name
                    if target.is_dir():
                        shutil.rmtree(target)
                    elif target.exists():
                        target.unlink()
                    if (backup / name).exists():
                        (backup / name).rename(target)
            elif destination.exists():
                shutil.rmtree(destination)
            if backup.exists() and not any(backup.iterdir()):
                backup.rmdir()
            raise
        if backup.exists():
            shutil.rmtree(backup)


def run_command(command):
    environment = dict(os.environ)
    environment.pop("RIMWORLD_MOD_DIR", None)
    process = subprocess.Popen(command, cwd=ROOT, env=environment, start_new_session=True)
    try:
        if process.wait() != 0:
            raise ValueError(f"Command failed: {command[0]}")
    except BaseException:
        if process.poll() is None:
            os.killpg(process.pid, signal.SIGTERM)
            try:
                process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                os.killpg(process.pid, signal.SIGKILL)
                process.wait()
        raise


def run_build(arguments):
    run_command(["dotnet", "build", str(ROOT / "Source/RimGPT.csproj"), "-v:q", "-clp:ErrorsOnly", *arguments])


def main(arguments):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("-c", "--configuration", choices=("Debug", "Release"), default="Release")
    configuration = parser.parse_args(arguments).configuration
    builds = ROOT / ".builds"
    builds.mkdir(exist_ok=True)
    lock = builds / "workflow.lock"
    try:
        lock.mkdir()
    except FileExistsError:
        raise ValueError(f"Another workflow owns {lock}. Check its process before removing an abandoned lock.")
    try:
        (lock / "pid").write_text(str(os.getpid()))
        manifest = json.loads((ROOT / "scripts/published-baseline.json").read_text())
        verify_repository(manifest)
        source = baseline(manifest)
        mods = validate_mods_root(os.environ["RIMWORLD_MOD_DIR"]) if os.environ.get("RIMWORLD_MOD_DIR") else None
        if mods:
            ensure_stopped()
            verify_installed_legacy(mods / "RimGPT", manifest["files"])
        print("Step: build RimWorld 1.7", flush=True)
        run_build(["-c", configuration])
        verify_repository(manifest)
        print("Step: verify package and installation contracts", flush=True)
        run_command([sys.executable, str(ROOT / "scripts/test_workflow.py")])
        print("Step: stage published baseline and create ZIP", flush=True)
        package = stage(manifest, source)
        if mods:
            print("Step: install and verify", flush=True)
            install(package, mods)
        verify_repository(manifest)
    finally:
        shutil.rmtree(lock)


if __name__ == "__main__":
    signal.signal(signal.SIGTERM, lambda *_: sys.exit(143))
    try:
        main(sys.argv[1:])
    except (OSError, ValueError, subprocess.SubprocessError, struct.error) as error:
        print(f"Failed: {error}", file=sys.stderr)
        sys.exit(1)
