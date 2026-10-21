from PIL import Image, ImageDraw
from pathlib import Path
import json, collections
p=Path(__file__).parent
refs=Path("C:/Users/user/orca/workspaces/CocktailCoffeeTea/coffee-brew-ui/Assets/_Project/Resources/DrinkLab")
names=["dl_coffee_extraction_grouphead","dl_coffee_extraction_cup_rim","dl_coffee_extraction_liquid"]
sizes=[(128,80),(64,48),(48,24)]
anchors=[
 {"mount_center":[64,48],"portafilter_lock":[61,27],"stream_left":[52,4],"stream_right":[70,4],"stream_center":[61,4],"handle_grip":[105,27]},
 {"liquid_center":[32,26],"rim_front":[32,5],"rim_back":[32,43],"opening_left":[11,26],"opening_right":[52,26]},
 {"surface_center":[24,12],"stream_impact":[24,12],"scale_origin":[24,12]}]
manifest={"specVersion":"1.0","assetId":"espresso-extraction-prototype","status":"ready","workflow":"Aseprite batch Lua integer pixels; PixelLab not exposed in connected tools","sourceReferences":[str(refs/(x+".png")) for x in ["dl_coffee_espresso","dl_coffee_demitasse"]],"storyboard":"Not attached in dispatched worker context; requested close-up elevated composition used. Coordinator notified.","coordinateSystem":"anchors use bottom-left canvas edge origin, x right, y up; pixel centers are n+0.5","filter":"nearest/point","alpha":"binary 0 or 255","assets":[],"integration":{"order":["cup body supplied by integrator","liquid","cup rim foreground"],"liquid":"neutral grayscale RGBA for multiplication tint; scale about surface_center; clip against cup opening if raised or enlarged","stream":"two runtime streams originate at stream_left and stream_right; no static coffee stream baked in","suggestedLiquidTint":"#DCAE72","note":"Rim is only an open ring, not a complete cup body. No Unity changes made."}}
sheet=Image.new("RGB",(1100,650),(33,30,39));d=ImageDraw.Draw(sheet)
for y in range(35,620,16):
 for x in range(12,1088,16):
  d.rectangle((x,y,x+15,y+15), fill=(65,59,67) if (x//16+y//16)%2 else (52,46,55))
positions=[(20,65),(560,65),(840,65)]
for n,size,a,pos in zip(names,sizes,anchors,positions):
 im=Image.open(p/(n+".png")).convert("RGBA")
 assert im.size==size
 assert set(im.getchannel("A").getdata())=={0,255}
 assert im.getpixel((0,0))[3]==0
 if "rim" in n:
  assert im.getpixel((32,22))[3]==0
  for yy in range(17,27):
   for xx in range(18,46): assert im.getpixel((xx,yy))[3]==0
 asset={"file":n+".png","source":n+".aseprite","frameSizePx":list(size),"anchorsPx":a,"pivotNormalized":[0.5,0.5],"opaqueBoundsTopLeft":im.getbbox(),"colorCountOpaque":len({v for v in im.getdata() if v[3]}),"checks":{"dimensions":True,"binaryAlpha":True,"transparentCanvasCorners":True}}
 manifest["assets"].append(asset)
 sheet.paste(im.resize((size[0]*4,size[1]*4),Image.Resampling.NEAREST),pos,im.resize((size[0]*4,size[1]*4),Image.Resampling.NEAREST))
 d.text((pos[0],pos[1]-22),n.replace("dl_coffee_extraction_","")+"  "+str(size)+"  4x",fill=(255,240,209))
# assembled technical composition, actual size multiplied exactly 4
stage=Image.new("RGBA",(160,140))
stage.alpha_composite(Image.open(p/(names[0]+".png")),(12,0))
rim=Image.open(p/(names[1]+".png"))
liq=Image.open(p/(names[2]+".png")).convert("RGBA")
# neutral liquid tinted to warm crema, preserving values and binary alpha
tinted=Image.new("RGBA",liq.size)
tinted.putdata([(round(220*r/255),round(174*g/255),round(114*b/255),a) for r,g,b,a in liq.getdata()])
stage.alpha_composite(tinted,(49,99))
stage.alpha_composite(rim,(41,85))
sd=ImageDraw.Draw(stage);sd.rectangle((63,76,64,103),fill=(113,71,50,255));sd.rectangle((81,76,82,103),fill=(113,71,50,255))
# preview only cup body outline demonstrates occlusion; not delivered as a fourth sprite
d.text((560,305),"Ring + tinted liquid / integration detail 4x",fill=(255,240,209))
detail=Image.new("RGBA",(100,70));detail.alpha_composite(tinted,(17,18));detail.alpha_composite(rim,(9,4))
detail4=detail.resize((400,280),Image.Resampling.NEAREST);sheet.paste(detail4,(560,330),detail4)
d.text((20,420),"Binary alpha / integer pixels / nearest-neighbor 4x",fill=(255,240,209))
d.text((20,445),"Liquid exported neutral for runtime tint.",fill=(255,240,209))
d.text((20,470),"Rim center is transparent; cup body is separate.",fill=(255,240,209))
sheet.save(p/"contact_sheet_4x.png")
stage.resize((640,560),Image.Resampling.NEAREST).save(p/"assembly_preview_4x.png")
(p/"manifest.json").write_text(json.dumps(manifest,indent=2),encoding="utf-8")
print(json.dumps(manifest["assets"],indent=2))
