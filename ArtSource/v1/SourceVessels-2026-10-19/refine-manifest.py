import pathlib
r=pathlib.Path(__file__).resolve().parent;p=r/'verify-manifest.py';s=p.read_text(encoding='utf-8-sig')
s=s.replace(" if typ!='aseprite':\n", " if typ not in ['empty','aseprite']:\n   ck(aid+':'+typ+':nativeLayerRoundtrip',Image.open(R/f).convert('RGBA').tobytes()==Image.open(R/f'qa/{aid}_{typ}_roundtrip.png').convert('RGBA').tobytes())\n  if typ!='aseprite':\n")
s=s.replace("'pourLipRight':lip,", "'pourLip':lip,'pourLipRight':lip,")
s=s.replace("'assetId':aid,'status':'ready'", "'assetId':aid,'maskAssetId':aid+'_mask','status':'ready'")
p.write_text(s,encoding='utf8')
