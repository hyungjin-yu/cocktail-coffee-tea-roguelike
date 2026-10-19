import pathlib
r=pathlib.Path(__file__).resolve().parent
p=r/'build-manifest.py';s=p.read_text(encoding='utf-8-sig');s=s.replace(" if base=='staff_card':a.update", " if base=='spring_notebook':a['scaling']='uniform nearest; preserve discrete spiral rings; borders describe paper safe insets only, do not stretch binding'\n if base=='staff_card':a.update")
p.write_text(s,encoding='utf8')
p=r/'verify.py';s=p.read_text(encoding='utf-8-sig');s=s.replace("  checks.append({'check':aid+':preview_loop_metadata','passed':True,'detail':gif.info.get('loop','absent')})", "  check(aid+':preview_play_once','loop' not in gif.info,gif.info.get('loop','absent'))")
p.write_text(s,encoding='utf8')
