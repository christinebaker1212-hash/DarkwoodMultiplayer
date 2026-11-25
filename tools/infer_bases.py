#!/usr/bin/env python3
# Iteratively infers game-class base types from "Cannot convert type 'X' to 'Y'" (CS0030) errors.
import re, subprocess, sys
BT = 'tools/StubGen/base-types.txt'
UNITY_GENERIC = {'UnityEngine.Object', 'UnityEngine.Component', 'UnityEngine.Behaviour', 'UnityEngine.MonoBehaviour'}
for it in range(30):
    out = subprocess.run(['./build.sh'], capture_output=True, text=True).stdout
    errs = set(re.findall(r"error CS0030: Cannot convert type '([^']+)' to '([^']+)'", out))
    errs |= set(re.findall(r"error CS0311: The type '([^']+)' cannot be used as type parameter .*? to '([^']+)'", out))
    lines = open(BT).read().splitlines()
    have = {l.split('=')[0].strip(): l for l in lines if '=' in l and not l.startswith('#')}
    added = 0
    for x, y in sorted(errs):
        if x.startswith(('UnityEngine.', 'System.')): continue
        base = 'UnityEngine.MonoBehaviour' if y in UNITY_GENERIC else y
        cur = have.get(x)
        if cur is None:
            lines.append(f'{x} = {base}'); have[x] = base; added += 1
        elif base != 'UnityEngine.MonoBehaviour' and cur.split('=')[1].strip() == 'UnityEngine.MonoBehaviour':
            lines = [l for l in lines if l != cur] + [f'{x} = {base}']; have[x] = f'{x} = {base}'; added += 1
    open(BT, 'w').write('\n'.join(lines) + '\n')
    nerr = len(set(re.findall(r'error CS\d+.*', out)))
    print(f'iter {it}: {nerr} errors, {added} bases added', flush=True)
    if added == 0: break
