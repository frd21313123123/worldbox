#!/usr/bin/env python3
"""Static packaging checks; does NOT compile C# or test the game."""
from pathlib import Path
from xml.etree import ElementTree
import re
import sys
try:
    import yaml
except ImportError:
    yaml = None
root = Path(__file__).resolve().parents[1]
cs = list(root.rglob('*.cs'))
assert cs, 'No C# sources'
xmls = list(root.rglob('*.csproj'))
for path in xmls:
    ElementTree.parse(path)
    text = path.read_text(encoding='utf8')
    for target in re.findall(r'<Compile Include="([^"]+)', text):
        assert (path.parent / target).is_file(), f'Missing Compile Include: {path}: {target}'
print(f'PASS XML project syntax and linked source paths: {len(xmls)} projects')
if yaml:
    data = yaml.safe_load((root / '.github/workflows/ci.yml').read_text())
    assert 'protocol-tests' in data['jobs'] and 'game-dll' in data['jobs']
    assert len(data['jobs']['protocol-tests']['steps']) >= 5
    print('PASS YAML syntax and CI jobs')
else:
    print('SKIP YAML check: PyYAML not installed')
for path in cs:
    src = path.read_text(encoding='utf8')
    # Strip comments, regular strings and char literals. This is only a delimiter sanity check.
    clean = re.sub(r'(?s)/\*.*?\*/|//[^\n]*|@"(?:[^"]|"")*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'', ' ', src)
    stack = []
    match = {')':'(', ']':'[', '}':'{'}
    for i, ch in enumerate(clean):
        if ch in '({[': stack.append((ch,i))
        if ch in ')}]':
            assert stack and stack[-1][0] == match[ch], f'Mismatched C# delimiter: {path}:{i}'
            stack.pop()
    assert not stack, f'Unclosed C# delimiter: {path}'
print(f'PASS C# delimiter checks: {len(cs)} files')
wire = (root / 'src/Wire.cs').read_text()
session = (root / 'src/SteamSession.cs').read_text()
assert 'internal const int Version = 3' in wire
assert 'SnapshotAssembler' in session and 'Wire.Deny' in session
assert 'WorldfallInfo.Detect' in session
assert 'GameWorldSnapshot' in session
assert 'RequestWorldSync' in (root / 'src/Plugin.cs').read_text()
print('PASS source integration markers')
print('WARNING C# compilation, Steamworks runtime and WorldBox game have NOT been tested here')
