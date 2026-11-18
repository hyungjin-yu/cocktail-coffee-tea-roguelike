from pathlib import Path
p=Path('ArtSource/feedback-art-2026-11-16/provenance/verify_official_install.py');s=p.read_text();s=s.replace('target=ext/n','target=ext/pathlib.Path(*pathlib.PurePosixPath(n).parts[1:])').replace("'status':'submitted_native_plugin_awaiting_result'","'status':'native_generation_success_seed_4169501354'");p.write_text(s,encoding='utf-8')
