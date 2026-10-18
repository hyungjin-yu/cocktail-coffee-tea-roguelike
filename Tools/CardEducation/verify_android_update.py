"""Verify local upload artifact without handling signing credentials."""
from pathlib import Path
import hashlib, json, re, ssl, struct, subprocess, zipfile

root = Path(__file__).resolve().parents[2]
out = root / 'Delivery/Android-2026-10-16'
aab = out / 'CocktailCoffeeTea-1.0.2-vc3-upload-signed.aab'
android = Path('C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Data/PlaybackEngines/AndroidPlayer')
java = android / 'OpenJDK/bin/java.exe'
bundletool = android / 'Tools/bundletool-all-1.17.2.jar'

def run(name, args):
    p = subprocess.run([str(x) for x in args], capture_output=True)
    text = p.stdout.decode('utf-8', errors='replace') + p.stderr.decode('utf-8', errors='replace')
    (out / name).write_text(text, encoding='utf-8')
    if p.returncode:
        raise RuntimeError(f'{name}: exit {p.returncode}; see saved output')
    return text

run('bundletool-validate.txt', [java, '-jar', bundletool, 'validate', f'--bundle={aab}'])
manifest = run('aab-manifest.xml', [java, '-jar', bundletool, 'dump', 'manifest', f'--bundle={aab}', '--module=base'])
config = run('bundletool-config.json', [java, '-jar', bundletool, 'dump', 'config', f'--bundle={aab}'])
assert 'com.hyungjinyu.cocktailcoffeetea' in manifest
assert 'versionCode="3"' in manifest and 'versionName="1.0.2"' in manifest
assert 'AD_ID' not in manifest
assert 'PAGE_ALIGNMENT_16K' in config
run('signature-verify.txt', [android / 'OpenJDK/bin/jarsigner.exe', '-verify', aab])
cert = run('public-signing-certificate.txt', [android / 'OpenJDK/bin/keytool.exe', '-printcert', '-jarfile', aab, '-rfc'])
pem = re.search(r'-----BEGIN CERTIFICATE-----.*?-----END CERTIFICATE-----', cert, re.S).group()
fingerprint = hashlib.sha256(ssl.PEM_cert_to_DER_cert(pem)).hexdigest()
assert fingerprint == 'a42020957cae6fa00d66d00531788beb46695f2b6ca858b4efa083cb7d7da7bf'
libs = []
with zipfile.ZipFile(aab) as z:
    assert z.testzip() is None
    for name in z.namelist():
        if not (name.startswith('base/lib/') and name.endswith('.so')):
            continue
        assert name.startswith('base/lib/arm64-v8a/'), name
        data = z.read(name)
        assert data[:6] == b'\x7fELF\x02\x01'
        offset = struct.unpack_from('<Q', data, 32)[0]
        size, count = struct.unpack_from('<HH', data, 54)
        aligns = [struct.unpack_from('<Q', data, offset + i * size + 48)[0]
                  for i in range(count) if struct.unpack_from('<I', data, offset + i * size)[0] == 1]
        assert aligns and min(aligns) >= 0x4000, name
        libs.append({'file': name, 'minimumLoadAlignment': min(aligns)})
assert len(libs) == 7
proof = {'version': '1.0.2', 'versionCode': 3, 'package': 'com.hyungjinyu.cocktailcoffeetea',
         'bytes': aab.stat().st_size, 'sha256': hashlib.sha256(aab.read_bytes()).hexdigest(),
         'signingCertificateSha256': fingerprint, 'libraries': libs,
         'bundleValidated': True, 'signatureVerified': True, 'adIdPermission': False,
         'uploaded': False, 'consoleAuthenticationRequired': True}
(out / 'verification.json').write_text(json.dumps(proof, indent=2), encoding='utf-8')
print(json.dumps(proof, indent=2))
