import pathlib
R=pathlib.Path(__file__).resolve().parent;ROOT=R
old=(R.parent/'ProgressionUI-2026-10-19/produce.py').read_text(encoding='utf-8-sig')
body=old[old.index('def generate(spec):'):]
a=body.index("req=dict(description=desc+");b=body.index("image_size=dict",a)
body=body[:a]+"req=dict(description=desc+' One original adult cafe research employee HEAD AND SHOULDERS portrait only. Face and neck visible, cropped at shoulder line, no arms or hands. Calm professional expression, frontal or very slight three-quarter face. Crisp chunky cafe-game pixel art matching the supplied style reference ONLY in pixel clusters and three-tone shading, do not copy its face, haircut or bartender uniform. Dark clean outlines #201D29, upper-left lighting, restrained palette. Single complete portrait with two pixel transparent margin around head and shoulders. Genuine transparent background, no background panel, no text, numbers, lettering, badge text, props, drinks, weapons, logos, watermarks, photorealism, blur or gradients. No child, no full body, no hand gestures or sipping.',"+body[b:]
prefix='''import base64,io,json,os,pathlib,sys,time,websocket
from PIL import Image
ROOT=pathlib.Path(__file__).resolve().parent
EXT=pathlib.Path(os.environ['APPDATA'])/'Aseprite/extensions/pixellab'
PROJECT=ROOT.parents[2]
REF=PROJECT/'Assets/_Project/Art/v1/bartender/frames/bartender_idle_right_00.png'
SPECS=[
('cocktail_aroma',64,80,'Distinct mature woman aged about 55, light warm skin, neat chin-length SILVER BOB haircut with straight silhouette, narrow oval face, thin rectangular navy eyeglasses, plum high-neck tailored jacket over ivory blouse. Slender upright shoulders. Cocktail aroma analyst, stirring specialist.'),
('cocktail_blend',64,80,'Distinct adult man aged about 35, medium brown skin, SHAVED BALD HEAD, broad square jaw, neatly trimmed short dark full beard, broad shoulders, burnt orange utility jacket over charcoal crewneck shirt, dark apron strap on one shoulder. Cocktail blending technician, shaking specialist.'),
('tea_aroma',64,80,'Distinct adult woman aged about 40, light olive skin, dark black hair gathered in a TALL ROUND HIGH BUN, soft elongated face, no glasses, moss green mandarin-collar blouse with an ivory shawl forming triangular shoulders. Tea aroma evaluator, clear infusion specialist.'),
('tea_blend',64,80,'Distinct mature man aged about 60, deep brown skin, SHORT WAVY IVORY HAIR, rounded face, small round gold-rim eyeglasses, no beard, deep indigo work smock with a sand beige neck scarf, gently rounded shoulders. Tea blending researcher, warm infusion specialist.')]

'''
(ROOT/'produce.py').write_text(prefix+body,encoding='utf8')
(ROOT/'references').mkdir(exist_ok=True)
import shutil
shutil.copy2(ROOT.parents[2]/'Assets/_Project/Art/v1/bartender/frames/bartender_idle_right_00.png',ROOT/'references/bartender-style.png')

