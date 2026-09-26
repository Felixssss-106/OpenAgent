#!/usr/bin/env python3
"""Prove the MSI installs exactly the tree that was screenshotted.

Build scripts have previously claimed this from a file *count*, which passes while a
renamed or stale payload hides inside the same total. This extracts the package with
`msiexec /a` and compares every file's SHA-256 against the publish directory.

    python scripts/verify-installer-payload.py [msi] [publish-dir]

Exit 0 only when the two trees are byte-for-byte identical.
"""
import hashlib
import pathlib
import shutil
import subprocess
import sys
import tempfile

ROOT = pathlib.Path(__file__).resolve().parent.parent
DEFAULT_MSI = ROOT / "artifacts/installer/OpenAgent-1.0.0-x64.msi"
DEFAULT_PUBLISH = ROOT / "artifacts/windows/win-x64"


def digests(root: pathlib.Path):
    out = {}
    for path in root.rglob("*"):
        if path.is_file():
            out[path.relative_to(root).as_posix().lower()] = hashlib.sha256(path.read_bytes()).hexdigest()
    return out


def extract(msi: pathlib.Path, target: pathlib.Path):
    subprocess.run(
        [
            "msiexec.exe",
            "/a", str(msi),
            "/qn",
            f"TARGETDIR={target}",
        ],
        check=True,
    )


def main():
    msi = pathlib.Path(sys.argv[1]) if len(sys.argv) > 1 else DEFAULT_MSI
    publish = pathlib.Path(sys.argv[2]) if len(sys.argv) > 2 else DEFAULT_PUBLISH
    if not msi.exists():
        print(f"no such installer: {msi}")
        return 2
    if not (publish / "OpenAgent.exe").exists():
        print(f"not a publish directory: {publish}")
        return 2

    work = pathlib.Path(tempfile.mkdtemp(prefix="oa-payload-"))
    try:
        extract(msi, work)
        installed = next((p for p in work.rglob("OpenAgent.exe")), None)
        if installed is None:
            print("administrative image contains no OpenAgent.exe")
            return 1
        a = digests(publish)
        b = digests(installed.parent)
        missing = sorted(set(a) - set(b))
        extra = sorted(set(b) - set(a))
        differ = sorted(k for k in set(a) & set(b) if a[k] != b[k])
        print(f"publish {len(a)} files   installed {len(b)} files")
        for label, keys in (("missing from package", missing), ("extra in package", extra), ("differing bytes", differ)):
            if keys:
                print(f"  {label}: {len(keys)}")
                for key in keys[:10]:
                    print(f"    ! {key}")
        ok = not (missing or extra or differ)
        print("IDENTICAL" if ok else "MISMATCH")
        return 0 if ok else 1
    finally:
        shutil.rmtree(work, ignore_errors=True)


if __name__ == "__main__":
    sys.exit(main())
