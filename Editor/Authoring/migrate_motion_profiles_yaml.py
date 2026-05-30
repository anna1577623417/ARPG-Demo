#!/usr/bin/env python3
"""One-shot: inject AxisCurves + YPolicy into MotionProfile .asset YAML from DisplacementCurve/BaseDistance."""
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2] / "4_Data" / "3.Motion"


def extract_block(text: str, name: str) -> str | None:
    m = re.search(rf"^  {name}:\n((?:    .*\n)*)", text, re.M)
    return m.group(0) if m else None


def main():
    count = 0
    for path in ROOT.rglob("*MotionProfile*.asset"):
        text = path.read_text(encoding="utf-8")
        if "AxisCurves:" in text:
            continue
        if "DisplacementCurve:" not in text:
            continue

        base_m = re.search(r"^  BaseDistance: ([\d.]+)", text, re.M)
        peak_m = re.search(r"^  PeakSpeedMultiplier: ([\d.]+)", text, re.M)
        grav_m = re.search(r"^  GravityBehavior: (\d+)", text, re.M)
        base = float(base_m.group(1)) if base_m else 4.0
        peak = float(peak_m.group(1)) if peak_m else 1.0
        z_scale = base * max(0.01, peak)
        y_policy = 1 if grav_m and grav_m.group(1) == "1" else 0

        disp_block = extract_block(text, "DisplacementCurve")
        if not disp_block:
            continue

        inner = disp_block.replace("  DisplacementCurve:", "", 1)
        inner = re.sub(r"\n    ", "\n      ", inner)
        z_curve = "    ZCurve:" + inner

        insert = (
            "  AxisCurves:\n"
            "    XCurve:\n"
            "      serializedVersion: 2\n"
            "      m_Curve: []\n"
            "      m_PreInfinity: 2\n"
            "      m_PostInfinity: 2\n"
            "      m_RotationOrder: 4\n"
            "    YCurve:\n"
            "      serializedVersion: 2\n"
            "      m_Curve: []\n"
            "      m_PreInfinity: 2\n"
            "      m_PostInfinity: 2\n"
            "      m_RotationOrder: 4\n"
            f"{z_curve}"
            f"    XScale: 0\n"
            f"    YScale: 0\n"
            f"    ZScale: {z_scale}\n"
            f"  YPolicy: {y_policy}\n"
        )

        anchor = "  m_EditorClassIdentifier: \n"
        if anchor not in text:
            continue
        text = text.replace(anchor, anchor + insert, 1)
        path.write_text(text, encoding="utf-8")
        count += 1
        print(f"patched {path.name} ZScale={z_scale}")

    print(f"done: {count} assets")


if __name__ == "__main__":
    main()
