from pathlib import Path
import json,re
out=Path('ArtSource/feedback-art-2026-11-16')
spec={'id':'dl_coffee_cappuccino_cup_v2','width':32,'height':26,'description':'A LARGE EMPTY ROUND DEEP cappuccino cup on a THIN SEPARATE SAUCER. The CUP is the main object, a deep hemispherical ivory ceramic bowl about eighteen pixels tall, with a small curved loop handle on the right. The thin flat saucer is only three pixels high at the very bottom. Clearly visible tall round cup body and open oval rim above it, NOT a shallow soup plate. Transparent empty mouth, no coffee, no milk, no foam. Crisp chunky cafe game inventory pixel art, dark brown one-pixel outline 201D29, ivory cream body with warm taupe shadow, three tones per material, side eye-level view, full complete object centered, transparent background, no letters, no text, no floor, no cast shadow, no antialiasing.'}
(out/'provenance/cappuccino-revision-spec.json').write_text(json.dumps(spec,indent=2),encoding='utf-8')
src=(out/'provenance/generate-native-batch.lua').read_text(encoding='utf-8')
row='{id='+json.dumps(spec['id'])+',width=32,height=26,description='+json.dumps(spec['description'])+'}'
src=re.sub(r'local specs=\{.*?\}\nassert',lambda m:'local specs={'+row+'}\nassert',src,count=1,flags=re.S).replace('All 31 native PixelLab generation jobs completed','Cappuccino revision native PixelLab generation completed')
(out/'provenance/generate-native-cappuccino-v2.lua').write_text(src,encoding='utf-8')
