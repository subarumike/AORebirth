"""Watch Codex investigation activity and stop its process tree if it stalls."""

import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import tempfile
from threading import Event, Thread
import time


MAX_EVENT_BYTES = 1024 * 1024
PROGRESS_EVENTS = {"thread.started", "turn.started", "turn.completed",
                   "item.started", "item.updated", "item.completed"}


class WatchdogError(RuntimeError):
    def __init__(self, message, *, retryable=True):
        super().__init__(message)
        self.retryable = retryable


def watch_events(stream, activity):
    oversized = False
    try:
        with stream:
            while line := stream.readline(MAX_EVENT_BYTES + 1):
                if not line.endswith(b"\n"):
                    oversized = True
                    continue
                if not oversized and len(line) <= MAX_EVENT_BYTES:
                    try:
                        event = json.loads(line)
                    except ValueError:
                        pass
                    else:
                        kind = event.get("type") if isinstance(event, dict) else None
                        if isinstance(kind, str) and kind in PROGRESS_EVENTS:
                            activity.set()
                oversized = False
    except OSError:
        return  # Closed pipes cannot provide another progress signal.


def audit_size(path):
    try:
        return path.stat().st_size
    except FileNotFoundError:
        return 0


class WindowsJob:
    def __init__(self):
        import ctypes
        from ctypes import wintypes

        class BasicLimits(ctypes.Structure):
            _fields_ = [
                ("per_process_time", ctypes.c_longlong), ("per_job_time", ctypes.c_longlong),
                ("flags", wintypes.DWORD), ("minimum_working_set", ctypes.c_size_t),
                ("maximum_working_set", ctypes.c_size_t), ("active_processes", wintypes.DWORD),
                ("affinity", ctypes.c_size_t), ("priority", wintypes.DWORD), ("scheduling", wintypes.DWORD),
            ]

        class ExtendedLimits(ctypes.Structure):
            _fields_ = [
                ("basic", BasicLimits), ("io_counters", ctypes.c_ulonglong * 6),
                ("process_memory", ctypes.c_size_t), ("job_memory", ctypes.c_size_t),
                ("peak_process_memory", ctypes.c_size_t), ("peak_job_memory", ctypes.c_size_t),
            ]

        kernel = ctypes.WinDLL("kernel32", use_last_error=True)
        kernel.CreateJobObjectW.argtypes = [ctypes.c_void_p, wintypes.LPCWSTR]
        kernel.CreateJobObjectW.restype = wintypes.HANDLE
        kernel.SetInformationJobObject.argtypes = [
            wintypes.HANDLE, ctypes.c_int, ctypes.c_void_p, wintypes.DWORD,
        ]
        kernel.SetInformationJobObject.restype = wintypes.BOOL
        kernel.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
        kernel.OpenProcess.restype = wintypes.HANDLE
        kernel.AssignProcessToJobObject.argtypes = [wintypes.HANDLE, wintypes.HANDLE]
        kernel.AssignProcessToJobObject.restype = wintypes.BOOL
        kernel.CloseHandle.argtypes = [wintypes.HANDLE]
        kernel.CloseHandle.restype = wintypes.BOOL
        self.kernel = kernel
        self.handle = kernel.CreateJobObjectW(None, None)
        self.last_error = lambda: ctypes.WinError(ctypes.get_last_error())
        if not self.handle:
            raise self.last_error()
        limits = ExtendedLimits()
        limits.basic.flags = 0x2000  # JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
        if not kernel.SetInformationJobObject(self.handle, 9, ctypes.byref(limits), ctypes.sizeof(limits)):
            error = self.last_error()
            self.close()
            raise error

    def attach(self, pid):
        process = self.kernel.OpenProcess(0x0101, False, pid)  # PROCESS_SET_QUOTA | PROCESS_TERMINATE
        if not process:
            raise self.last_error()
        try:
            if not self.kernel.AssignProcessToJobObject(self.handle, process):
                raise self.last_error()
        finally:
            self.kernel.CloseHandle(process)

    def close(self):
        if not self.kernel.CloseHandle(self.handle):
            raise self.last_error()


def start_process(command, cwd, env, input_path):
    if os.name != "nt":
        with input_path.open("rb") as stream:
            return subprocess.Popen(command, cwd=cwd, env=env, stdin=stream,
                                    stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
                                    start_new_session=True), None
    try:
        job = WindowsJob()
    except OSError:
        raise WatchdogError("Could not create a Windows job for Codex.", retryable=False) from None
    process = None
    try:
        # The launcher waits for one byte, so Codex cannot start outside the job.
        launcher = [sys.executable, "-I", "-B", str(Path(__file__).resolve()), str(input_path), *command]
        process = subprocess.Popen(launcher, cwd=cwd, env=env, stdin=subprocess.PIPE,
                                   stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
                                   creationflags=subprocess.CREATE_NEW_PROCESS_GROUP)
        job.attach(process.pid)
        with process.stdin:
            process.stdin.write(b"1")
        return process, job
    except BaseException as error:
        job.close()
        if process is not None:
            if process.poll() is None:
                process.kill()
            process.wait(timeout=5)
            process.stdin.close()
            process.stdout.close()
        if isinstance(error, OSError):
            raise WatchdogError("Could not attach the Codex launcher to its Windows job.", retryable=False) from None
        raise


def stop_tree(process, job):
    try:
        if job is not None:
            job.close()
        else:
            try:
                os.killpg(process.pid, signal.SIGKILL)
            except ProcessLookupError:
                pass
        process.wait(timeout=5)
    except (OSError, subprocess.SubprocessError):
        raise WatchdogError("Could not confirm Codex process-tree cleanup.", retryable=False) from None


def run(command, *, cwd, env, payload, audit_path, idle_seconds, cancelled=None):
    with tempfile.NamedTemporaryFile(dir=cwd, delete=False) as input_stream:
        input_stream.write(payload)
        input_path = Path(input_stream.name)
    try:
        process, job = start_process(command, cwd, env, input_path)
        activity = Event()
        reader = Thread(target=watch_events, args=(process.stdout, activity), daemon=True)
        try:
            reader.start()
            last_progress = time.monotonic()
            size = 0
            while (status := process.poll()) is None:
                if cancelled is not None and cancelled.is_set():
                    raise WatchdogError("Codex investigation cancelled.")
                current_size = audit_size(audit_path)
                if activity.is_set() or current_size > size:
                    activity.clear()
                    last_progress = time.monotonic()
                size = current_size
                remaining = idle_seconds - (time.monotonic() - last_progress)
                if remaining <= 0:
                    raise WatchdogError(f"Codex produced no progress for {idle_seconds} seconds.")
                activity.wait(min(0.2, remaining))
        finally:
            try:
                stop_tree(process, job)
            finally:
                if reader.ident is not None:
                    reader.join(timeout=5)
                else:
                    process.stdout.close()
            if reader.is_alive():
                raise WatchdogError("Codex output did not close.", retryable=False)
        return status
    finally:
        input_path.unlink(missing_ok=True)


if __name__ == "__main__":
    if sys.stdin.buffer.read(1) != b"1":
        raise SystemExit(1)
    with Path(sys.argv[1]).open("rb") as stream:
        raise SystemExit(subprocess.call(sys.argv[2:], stdin=stream))
