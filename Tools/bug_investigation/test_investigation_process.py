import ctypes
import os
from pathlib import Path
import subprocess
import sys
from tempfile import TemporaryDirectory
import unittest
from unittest.mock import patch

from investigation_process import WatchdogError, run


class WatchdogTests(unittest.TestCase):
    def invoke(self, code, directory, seconds):
        return run([sys.executable, "-B", "-c", code], cwd=directory, env=os.environ.copy(),
                   payload=b"test input", audit_path=Path(directory) / "audit.jsonl", idle_seconds=seconds)

    def test_codex_events_keep_a_longer_investigation_running(self):
        with TemporaryDirectory() as directory:
            self.assertEqual(self.invoke(
                "import sys,time\n"
                "assert sys.stdin.read() == 'test input'\n"
                "for _ in range(9):\n"
                " print('{\"type\":\"item.updated\"}', flush=True); time.sleep(0.15)\n",
                directory, 0.8,
            ), 0)

    def test_source_audit_is_progress_without_stdout(self):
        with TemporaryDirectory() as directory:
            self.assertEqual(self.invoke(
                "import time\n"
                "with open('audit.jsonl','a') as audit:\n"
                " for _ in range(9):\n"
                "  audit.write('{}\\n'); audit.flush(); time.sleep(0.15)\n",
                directory, 0.8,
            ), 0)

    def assert_windows_child_stopped(self, directory):
        from ctypes import wintypes

        kernel = ctypes.WinDLL("kernel32", use_last_error=True)
        kernel.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
        kernel.OpenProcess.restype = wintypes.HANDLE
        kernel.WaitForSingleObject.argtypes = [wintypes.HANDLE, wintypes.DWORD]
        kernel.WaitForSingleObject.restype = wintypes.DWORD
        kernel.CloseHandle.argtypes = [wintypes.HANDLE]
        pid = int((Path(directory) / "child.pid").read_text())
        handle = kernel.OpenProcess(0x100000, False, pid)  # SYNCHRONIZE
        if handle:
            try:
                stopped = kernel.WaitForSingleObject(handle, 2000) == 0
            finally:
                kernel.CloseHandle(handle)
            if not stopped:
                subprocess.run([str(Path(os.environ["SYSTEMROOT"]) / "System32" / "taskkill.exe"),
                                "/PID", str(pid), "/T", "/F"], capture_output=True, timeout=10)
            self.assertTrue(stopped)
        else:
            self.assertEqual(ctypes.get_last_error(), 87)  # ERROR_INVALID_PARAMETER, exited PID

    @unittest.skipUnless(os.name == "nt", "Windows process-tree cleanup")
    def test_error_noise_does_not_keep_owned_process_tree_alive(self):
        with TemporaryDirectory() as directory:
            with self.assertRaisesRegex(WatchdogError, "no progress"):
                self.invoke(
                    "from pathlib import Path\n"
                    "import subprocess,sys,time\n"
                    "child=subprocess.Popen([sys.executable,'-c','import time; time.sleep(30)'])\n"
                    "Path('child.pid').write_text(str(child.pid))\n"
                    "for _ in range(100):\n"
                    " print('{\"type\":\"error\"}', flush=True); time.sleep(0.10)\n",
                    directory, 1.2,
                )
            self.assert_windows_child_stopped(directory)

    @unittest.skipUnless(os.name == "nt", "Windows process-tree cleanup")
    def test_leader_exit_also_stops_descendant_that_inherits_stdout(self):
        with TemporaryDirectory() as directory:
            self.assertEqual(self.invoke(
                "from pathlib import Path\n"
                "import subprocess,sys\n"
                "child=subprocess.Popen([sys.executable,'-c','import time; time.sleep(30)'])\n"
                "Path('child.pid').write_text(str(child.pid))\n",
                directory, 1.2,
            ), 0)
            self.assert_windows_child_stopped(directory)

    @unittest.skipUnless(os.name == "nt", "Windows process-tree cleanup")
    def test_job_assignment_failure_prevents_target_execution(self):
        with TemporaryDirectory() as directory:
            with patch("investigation_process.WindowsJob.attach", side_effect=OSError):
                with self.assertRaisesRegex(WatchdogError, "Could not attach") as failure:
                    self.invoke("from pathlib import Path; Path('started').touch()", directory, 1.2)
            self.assertFalse(failure.exception.retryable)
            self.assertFalse((Path(directory) / "started").exists())
