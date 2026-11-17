"""Exercise package provenance and installation failures in disposable directories."""

import copy
import json
import os
from pathlib import Path
import shutil
import signal
import struct
import subprocess
import sys
import tempfile
import time
import unittest
import xml.etree.ElementTree as ET
from unittest.mock import patch
from zipfile import ZipFile

import workflow


class WorkflowTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.original_root = workflow.ROOT
        cls.manifest_template = json.loads((cls.original_root / "scripts/published-baseline.json").read_text())
        names = set(cls.manifest_template["repositoryFiles"])
        names.update(("LICENSE", "README.md", "LoadFolders.xml", "Directory.Build.props"))
        names.update(str(p.relative_to(cls.original_root)) for p in (cls.original_root / "About").rglob("*") if p.is_file())
        names.update("1.7/Assemblies/" + name for name in workflow.ACTIVE_DLLS)
        cls.repository_data = {name: (cls.original_root / name).read_bytes() for name in names}
        source = workflow.baseline(cls.manifest_template)
        cls.published_data = {name: (source / name).read_bytes() for name in cls.manifest_template["files"]}

    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix="rimgpt-workflow-test-")
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name) / "repo"
        self.root.mkdir()
        self.manifest = copy.deepcopy(self.manifest_template)
        for name, data in self.repository_data.items():
            self.write(self.root / name, data)
        self.source = self.root / ".builds/published-baseline"
        for name, data in self.published_data.items():
            self.write(self.source / name, data)
        self.write(self.root / "scripts/published-baseline.json", json.dumps(self.manifest).encode())
        self.enterContext(patch.object(workflow, "ROOT", self.root))
        self.enterContext(patch.dict(os.environ))
        os.environ.pop("RIMWORLD_MOD_DIR", None)
        os.environ.pop("RIMGPT_PUBLISHED_BASELINE", None)

    @staticmethod
    def write(path, data):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)

    def stage(self):
        return workflow.stage(self.manifest, workflow.baseline(self.manifest))

    def mods(self):
        mods = self.root.parent / "fake-game/Mods"
        mods.mkdir(parents=True)
        self.write(mods.parent / "Assembly-CSharp.dll", b"test marker, never executed")
        return mods.resolve()

    def test_package_uses_published_dll_and_preserves_tracked_dll(self):
        name = "1.6/Assemblies/RimGPT.dll"
        tracked = (self.root / name).read_bytes()
        self.assertNotEqual(tracked, self.published_data[name])
        package = self.stage()
        self.assertEqual((package / name).read_bytes(), self.published_data[name])
        self.assertEqual((self.root / name).read_bytes(), tracked)
        workflow.verify_repository(self.manifest)
        with ZipFile(self.root / ".builds/RimGPT.zip") as archive:
            self.assertEqual({name: workflow.digest(archive.read(name)) for name in archive.namelist()}, workflow.file_hashes(package))
            self.assertFalse(any(name.endswith((".pdb", ".mdb", ".deps.json")) or "0Harmony" in name for name in archive.namelist()))
        self.assertEqual({p.name for p in (package / "1.7/Assemblies").iterdir()}, set(workflow.ACTIVE_DLLS))

    def test_stale_metadata_cannot_be_packaged(self):
        metadata = self.root / "About/Manifest.xml"
        document = ET.parse(metadata)
        document.find("version").text = "0.0.0.0"
        document.write(metadata)
        with self.assertRaisesRegex(ValueError, "Version metadata differs"):
            self.stage()
        self.assertFalse((self.root / ".builds/package").exists())

    def test_explicit_wrong_baseline_never_falls_back_to_cache(self):
        os.environ["RIMGPT_PUBLISHED_BASELINE"] = str(self.root)
        with self.assertRaisesRegex(ValueError, "Baseline mismatch"):
            workflow.baseline(self.manifest)

    def test_absent_baseline_stops_before_build(self):
        (self.source / "1.6/Assemblies/RimGPT.dll").unlink()
        with patch.object(workflow, "run_build") as build:
            with self.assertRaisesRegex(ValueError, "Baseline mismatch"):
                workflow.main([])
            build.assert_not_called()
        self.assertFalse((self.root / ".builds/workflow.lock").exists())

    def test_extra_baseline_runtime_file_is_rejected(self):
        self.write(self.source / "1.6/Assemblies/extra.dll", b"unexpected")
        with self.assertRaisesRegex(ValueError, "Unexpected frozen payload"):
            workflow.baseline(self.manifest)

    def test_debug_and_finder_files_are_excluded(self):
        for name in ("1.6/Assemblies/extra.pdb", "1.6/.DS_Store", "Textures/._ToggleAI.png"):
            self.write(self.source / name, b"excluded")
        package = self.stage()
        self.assertFalse(any((package / name).exists() for name in ("1.6/Assemblies/extra.pdb", "1.6/.DS_Store", "Textures/._ToggleAI.png")))

    def test_changed_baseline_after_validation_preserves_previous_package(self):
        source = workflow.baseline(self.manifest)
        marker = self.root / ".builds/package/RimGPT/previous"
        self.write(marker, b"keep")
        self.write(source / "1.6/Assemblies/RimGPT.dll", b"changed")
        with self.assertRaisesRegex(ValueError, "changed during staging"):
            workflow.stage(self.manifest, source)
        self.assertEqual(marker.read_bytes(), b"keep")

    def test_changed_or_additional_repository_payload_is_rejected(self):
        target = self.root / "1.6/Assemblies/RimGPT.dll"
        original = target.read_bytes()
        target.write_bytes(b"changed")
        with self.assertRaisesRegex(ValueError, "Baseline mismatch"):
            workflow.verify_repository(self.manifest)
        target.write_bytes(original)
        self.write(self.root / "Older/Assemblies/extra.dll", b"unexpected")
        with self.assertRaisesRegex(ValueError, "inventory changed"):
            workflow.verify_repository(self.manifest)

    def test_missing_current_dependency_preserves_previous_package(self):
        marker = self.root / ".builds/package/RimGPT/previous"
        self.write(marker, b"keep")
        (self.root / "1.7/Assemblies/Newtonsoft.Json.dll").unlink()
        with self.assertRaises(FileNotFoundError):
            self.stage()
        self.assertEqual(marker.read_bytes(), b"keep")

    def test_anycpu_gate_rejects_x64(self):
        path = self.root / "1.7/Assemblies/RimGPT.dll"
        workflow.verify_anycpu(path)
        data = bytearray(path.read_bytes())
        pe = struct.unpack_from("<I", data, 0x3C)[0]
        struct.pack_into("<H", data, pe + 4, 0x8664)
        path.write_bytes(data)
        with self.assertRaisesRegex(ValueError, "AnyCPU"):
            workflow.verify_anycpu(path)

    def test_output_override_is_rejected_before_build(self):
        with patch.object(workflow, "run_build") as build:
            with self.assertRaises(SystemExit) as error:
                workflow.main(["-p:OutputPath=../1.6/Assemblies"])
            self.assertEqual(error.exception.code, 2)
            build.assert_not_called()

    def test_existing_workflow_lock_is_preserved(self):
        lock = self.root / ".builds/workflow.lock"
        lock.mkdir()
        with self.assertRaisesRegex(ValueError, "Another workflow owns"):
            workflow.main([])
        self.assertTrue(lock.exists())

    def test_install_root_and_symlink_are_rejected(self):
        with self.assertRaisesRegex(ValueError, "Mods directory"):
            workflow.validate_mods_root(str(self.root))
        mods = self.mods()
        self.assertEqual(workflow.validate_mods_root(str(mods)), mods)
        (mods / "RimGPT").symlink_to(self.source, target_is_directory=True)
        with self.assertRaisesRegex(ValueError, "symlink"):
            workflow.validate_mods_root(str(mods))

    def mac_mods_alias(self):
        game = self.root.parent / "mac-game"
        legacy = game / "RimWorldMac.app/Mods"
        legacy.mkdir(parents=True)
        outer = game / "Mods"
        outer.symlink_to(legacy, target_is_directory=True)
        self.write(game / "RimWorld.app/Contents/MacOS/Assembly-CSharp.dll", b"1.7 marker, never executed")
        return outer, legacy

    def test_outer_17_mods_symlink_keeps_logical_path(self):
        outer, legacy = self.mac_mods_alias()
        self.assertEqual(outer.resolve(), legacy.resolve())
        self.assertEqual(workflow.validate_mods_root(str(outer)), outer)

    def test_legacy_app_alias_is_rejected_even_with_runtime_marker(self):
        outer, legacy = self.mac_mods_alias()
        self.write(legacy.parent / "Contents/MacOS/Assembly-CSharp.dll", b"alias marker, never executed")
        with self.assertRaisesRegex(ValueError, "outer.*1.7"):
            workflow.validate_mods_root(str(legacy))

    def test_running_game_refuses_installation_without_changes(self):
        package = self.stage()
        mods = self.mods()
        with patch.object(workflow.subprocess, "run", return_value=subprocess.CompletedProcess([], 0, "/fake/RimWorld\n")):
            with self.assertRaisesRegex(ValueError, "running"):
                workflow.install(package, mods)
        self.assertEqual(list(mods.iterdir()), [])

    def test_install_copies_exact_package_and_zip_in_fake_game(self):
        package = self.stage()
        mods = self.mods()
        with patch.object(workflow, "ensure_stopped"):
            workflow.install(package, mods)
        self.assertEqual(workflow.file_hashes(mods / "RimGPT"), workflow.file_hashes(package))
        self.assertEqual((mods / "RimGPT.zip").read_bytes(), (self.root / ".builds/RimGPT.zip").read_bytes())
        self.assertFalse(list(mods.glob(".RimGPT-*")))

    def legacy_state(self, destination):
        paths = [destination / name for name in self.manifest["files"]]
        paths.extend(destination / folder for folder in workflow.FROZEN)
        return {str(p): (p.stat().st_ino, p.stat().st_mtime_ns) for p in paths}

    def test_existing_install_preserves_legacy_inodes_and_mtimes(self):
        package = self.stage()
        outer, _ = self.mac_mods_alias()
        mods = workflow.validate_mods_root(str(outer))
        destination = mods / "RimGPT"
        shutil.copytree(package, destination)
        self.write(destination / "1.7/Assemblies/RimGPT.dll", b"previous 1.7")
        before = self.legacy_state(destination)
        root_inode = destination.stat().st_ino
        with patch.object(workflow, "ensure_stopped"):
            workflow.install(package, mods)
        self.assertEqual(self.legacy_state(destination), before)
        self.assertEqual(destination.stat().st_ino, root_inode)
        self.assertEqual(workflow.file_hashes(destination), workflow.file_hashes(package))

    def test_mismatched_installed_legacy_refuses_all_changes(self):
        package = self.stage()
        mods = self.mods()
        destination = mods / "RimGPT"
        shutil.copytree(package, destination)
        self.write(destination / "1.6/Assemblies/RimGPT.dll", b"different legacy build")
        before = workflow.file_hashes(destination), self.legacy_state(destination)
        with patch.object(workflow, "ensure_stopped"):
            with self.assertRaisesRegex(ValueError, "Installed legacy"):
                workflow.install(package, mods)
        self.assertEqual((workflow.file_hashes(destination), self.legacy_state(destination)), before)
        self.assertFalse((mods / "RimGPT.zip").exists())

    def test_additional_installed_legacy_refuses_all_changes(self):
        package = self.stage()
        mods = self.mods()
        destination = mods / "RimGPT"
        shutil.copytree(package, destination)
        self.write(destination / "1.6/Assemblies/additional.dll", b"preserve")
        before = workflow.file_hashes(destination)
        with patch.object(workflow, "ensure_stopped"):
            with self.assertRaisesRegex(ValueError, "Installed legacy"):
                workflow.install(package, mods)
        self.assertEqual(workflow.file_hashes(destination), before)

    def test_failed_install_verification_restores_previous_mod_and_zip(self):
        package = self.stage()
        mods = self.mods()
        shutil.copytree(package, mods / "RimGPT")
        self.write(mods / "RimGPT/1.7/previous", b"old")
        self.write(mods / "RimGPT.zip", b"previous ZIP")
        real_hashes = workflow.file_hashes
        def hashes(path):
            return {} if path == mods / "RimGPT" else real_hashes(path)
        with patch.object(workflow, "ensure_stopped"), patch.object(workflow, "file_hashes", side_effect=hashes):
            with self.assertRaisesRegex(ValueError, "Installed files differ"):
                workflow.install(package, mods)
        self.assertEqual((mods / "RimGPT/1.7/previous").read_bytes(), b"old")
        self.assertEqual((mods / "RimGPT.zip").read_bytes(), b"previous ZIP")

    def test_failed_rollback_retains_previous_mod_outside_temporary_cleanup(self):
        package = self.stage()
        mods = self.mods()
        shutil.copytree(package, mods / "RimGPT")
        self.write(mods / "RimGPT/1.7/previous", b"old")
        real_hashes, real_rename = workflow.file_hashes, Path.rename
        def hashes(path):
            return {} if path == mods / "RimGPT" else real_hashes(path)
        def rename(path, target):
            if path.parent.name.endswith("-previous"):
                raise PermissionError("simulated rollback failure")
            return real_rename(path, target)
        with patch.object(workflow, "ensure_stopped"), patch.object(workflow, "file_hashes", side_effect=hashes), patch.object(Path, "rename", rename):
            with self.assertRaisesRegex(PermissionError, "rollback failure"):
                workflow.install(package, mods)
        backups = list(mods.glob(".RimGPT-*-previous"))
        self.assertEqual(len(backups), 1)
        self.assertEqual((backups[0] / "1.7/previous").read_bytes(), b"old")

    def test_cancellation_stops_owned_child_and_releases_lock(self):
        script = self.root / "scripts/workflow.py"
        shutil.copy2(self.original_root / "scripts/workflow.py", script)
        executable = self.root / "fake-bin/dotnet"
        pid_file = self.root / "child.pid"
        self.write(executable, (f"#!{sys.executable}\nimport os,time\nfrom pathlib import Path\nPath({str(pid_file)!r}).write_text(str(os.getpid()))\ntime.sleep(30)\n").encode())
        executable.chmod(0o755)
        environment = dict(os.environ, PATH=str(executable.parent) + os.pathsep + os.environ["PATH"])
        process = subprocess.Popen([sys.executable, str(script)], env=environment, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
        try:
            deadline = time.monotonic() + 5
            while not pid_file.exists() and time.monotonic() < deadline:
                time.sleep(0.02)
            self.assertTrue(pid_file.exists(), "Fake build did not start")
            child = int(pid_file.read_text())
            process.send_signal(signal.SIGTERM)
            output, _ = process.communicate(timeout=8)
            self.assertEqual(process.returncode, 143, output)
            with self.assertRaises(ProcessLookupError):
                os.kill(child, 0)
            self.assertFalse((self.root / ".builds/workflow.lock").exists())
        finally:
            if process.poll() is None:
                process.kill()
                process.wait()


if __name__ == "__main__":
    unittest.main(verbosity=2)
