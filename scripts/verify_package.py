#!/usr/bin/env python3
"""Check a repository source manifest or the DLL whitelist in a local release ZIP.
Git metadata, submodules and generated output are excluded from source manifests.
"""
from pathlib import Path
import argparse
import hashlib
import os
import sys
import zipfile

EXCLUDED = {'.git', '.vs', 'bin', 'obj', 'artifacts', 'dependencies', '__pycache__', 'work', 'outputs'}
ASSEMBLIES = ('SecretFlasherManaka.ForEveryThing', 'BooBoopControl', 'SecretFlasherManaka.BooBoopBridge')

def source_manifest(root):
    files = {}
    for directory, directories, filenames in os.walk(root):
        directories[:] = sorted(d for d in directories if d not in EXCLUDED)
        for name in sorted(filenames):
            if name in ('.git', 'SOURCE_MANIFEST.sha256'): continue
            path = Path(directory) / name
            if path.suffix.lower() in ('.dll', '.exe', '.pdb', '.cfg') or name == 'product_list.json' or name == '.env' or name.startswith('.env.'):
                raise ValueError('Private or binary file in source repository: ' + str(path.relative_to(root)))
            files[path.relative_to(root).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()
    return dict(sorted(files.items()))

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument('--update-manifest', action='store_true')
    parser.add_argument('--archive', type=Path)
    args = parser.parse_args()
    if args.archive:
        if args.update_manifest: raise ValueError('Choose archive verification or manifest update.')
        expected = {'INSTALL.md', 'LICENSE', 'THIRD_PARTY_NOTICES.md'} | {f'BepInEx/plugins/SecretFlasherManakaBooBoop/{name}.dll' for name in ASSEMBLIES}
        with zipfile.ZipFile(args.archive) as archive:
            entries = [i.filename.replace('\\', '/') for i in archive.infolist() if not i.is_dir()]
            if len(entries) != len(set(entries)) or set(entries) != expected:
                raise ValueError('Archive must contain installation/license/notices and exactly the three expected plugin DLLs.')
            if archive.testzip() is not None: raise ValueError('ZIP CRC validation failed.')
            for info in archive.infolist():
                if info.filename.lower().endswith('.dll') and not archive.read(info).startswith(b'MZ'):
                    raise ValueError('DLL does not have a PE header.')
        print('PASS: release ZIP whitelist, duplicate entries, PE headers and CRC.')
        return
    root = args.source_root.resolve()
    if not root.is_dir(): raise ValueError('Source repository directory does not exist.')
    manifest = root / 'SOURCE_MANIFEST.sha256'
    expected = source_manifest(root)
    if args.update_manifest:
        manifest.write_text(''.join(f'{digest}  {name}\n' for name, digest in expected.items()), encoding='utf-8')
        print(f'Updated source manifest: {len(expected)} files in {root.name}.')
        return
    actual = {}
    for line in manifest.read_text(encoding='utf-8').splitlines():
        digest, name = line.split('  ', 1)
        if name in actual: raise ValueError('Duplicate manifest entry: ' + name)
        actual[name] = digest
    if actual != expected: raise ValueError('Manifest differs from repository files; inspect changes before updating.')
    print(f'PASS: {len(expected)} source file hashes; Git and dependencies excluded.')

if __name__ == '__main__':
    try: main()
    except (OSError, ValueError, zipfile.BadZipFile) as error:
        print('FAIL:', error, file=sys.stderr)
        sys.exit(1)
