import pathlib

base = pathlib.Path(__file__).resolve().parent
cs_path = base / "PlayerInputSystem.cs"
js_path = base / "PlayerInputSystem.inputactions"
cs = cs_path.read_text(encoding="utf-8")
js = js_path.read_text(encoding="utf-8")
escaped = js.replace('"', '""')
start = cs.find('asset = InputActionAsset.FromJson(@"')
if start == -1:
    raise SystemExit("start not found")
end = cs.find('");', start)
if end == -1:
    raise SystemExit("end not found")
end += 3
new_cs = cs[:start] + 'asset = InputActionAsset.FromJson(@"' + escaped + '");' + cs[end:]
cs_path.write_text(new_cs, encoding="utf-8")
print("Synced embedded JSON from PlayerInputSystem.inputactions")
