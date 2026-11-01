#!/usr/bin/env python3
"""Thin wrapper — prefer tools/generate-icon.ps1 on Windows.

  python tools/generate-icon.py
  python tools/generate-icon.py --svg outputs/sticky_note_logo.svg
"""
from __future__ import annotations

import argparse
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--svg", type=Path, default=None)
    parser.add_argument("--out", type=Path, default=None)
    args = parser.parse_args()
    cmd = [
        "powershell",
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        str(ROOT / "tools" / "generate-icon.ps1"),
    ]
    if args.svg:
        cmd += ["-Svg", str(args.svg)]
    if args.out:
        cmd += ["-OutIco", str(args.out)]
    return subprocess.call(cmd)


if __name__ == "__main__":
    sys.exit(main())
