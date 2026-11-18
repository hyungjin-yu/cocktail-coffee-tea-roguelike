-- Aseprite native automation. Uses the installed official PixelLab model and its official archive.
-- No credential access, no custom network client, no source patch, no full-trust request.
local specs={{id="ct_bookshelf_source",width=128,height=192,model="pro",description="A real cafe recipe BOOKSHELF made of dark walnut: a simple rectangular floor-standing cabinet, exactly3 open shelves with cream and muted sage recipe books, a few small coffee jars, short solid base; all objects resting on shelves. No lamps, no decorative arch, no stool. Warm dark walnut and muted amber-ivory cafe pixel art; same dark201D29 outlines, small pixel clusters, restrained highlights, upper-left dominant warm light. Complete object centered, straight-on FRONT elevation, no floor shadow, no text, letters, numbers, logo, neon, characters, floating parts or isometric projection. Realistic furniture proportions."},{id="ct_coat_cabinet_source",width=128,height=192,model="pro",description="A narrow real cafe employee COAT CABINET of dark walnut, straight open rectangular frame, a horizontal brass rail at upper third with one muted brown apron and one cream apron hanging from wooden hangers, closed low drawer, no shelves of books and no person. Warm dark walnut and muted amber-ivory cafe pixel art; same dark201D29 outlines, small pixel clusters, restrained highlights, upper-left dominant warm light. Complete object centered, straight-on FRONT elevation, no floor shadow, no text, letters, numbers, logo, neon, characters, floating parts or isometric projection. Realistic furniture proportions."},{id="ct_radio_source",width=128,height=64,model="pro",description="One compact real vintage cafe tabletop RADIO, walnut rounded rectangular case, cloth speaker grille on left, two small brass rotary control knobs and narrow ivory tuning dial on right. Device itself only, no table, no antenna taller than case, no text. Warm dark walnut and muted amber-ivory cafe pixel art; same dark201D29 outlines, small pixel clusters, restrained highlights, upper-left dominant warm light. Complete object centered, straight-on FRONT elevation, no floor shadow, no text, letters, numbers, logo, neon, characters, floating parts or isometric projection. Realistic furniture proportions."},{id="ct_ledger_source",width=128,height=64,model="pro",description="An OPEN cafe business LEDGER on a flat tabletop, cream paper pages with sparse short brown ruled bookkeeping marks and no legible writing, dark walnut leather binding, a slim pencil lying alongside; natural shallow top view so both page surfaces visible. No table itself, no floating papers. Warm dark walnut and muted amber-ivory cafe pixel art; same dark201D29 outlines, small pixel clusters, restrained highlights, upper-left dominant warm light. Complete object centered, straight-on FRONT elevation, no floor shadow, no text, letters, numbers, logo, neon, characters, floating parts or isometric projection. Realistic furniture proportions."},{id="ct_espresso_source",width=128,height=96,model="pro",description="One compact realistic cafe ESPRESSO MACHINE, dark brushed metal and warm brass, two group heads, short drip tray, one small white espresso cup resting on tray under a spout, no steam clouds, no huge cup, no person. Isolated whole appliance, no countertop. Warm dark walnut and muted amber-ivory cafe pixel art; same dark201D29 outlines, small pixel clusters, restrained highlights, upper-left dominant warm light. Complete object centered, straight-on FRONT elevation, no floor shadow, no text, letters, numbers, logo, neon, characters, floating parts or isometric projection. Realistic furniture proportions."},{id="ct_facade_material_source",width=256,height=128,model="pro",description="A realistic small cafe exterior wall material study in clean game pixel art: warm dark walnut doorframe molding beside muted brown plaster/brick wall and a small brass wall plate. Straight flat FRONT view of wall surfaces, no perspective room, no actual door, no readable signs, no text, no plants or people. Warm amber indoor light at left, subdued exterior daylight, dark brown201D29 and walnut tan palette, subtle pixel grain and restrained tiny highlights."}}
assert(app.isUIAvailable,"GUI runtime required")
assert(type(generate_no_style)=="table","Installed official PixelLab native model missing")
closeAllDialogs()
_play_sound_on_complete=false
local index=0
local started=0
local jobId=nil
local activeSprite=nil
local finished=false
local function nextJob()
 index=index+1
 if index>#specs then
  finished=true
  artNativeBatchTimer:stop()
  app.alert("CT18 six source generations completed; official archives retained. Final QA required.")
  return
 end
 closeAllDialogs()
 local s=specs[index]
 activeSprite=Sprite(s.width,s.height,ColorMode.RGB)
 activeSprite.layers[1].name=s.id
 local m=generate_image_new
 if s.model=="edit" then
 local colors={app.pixelColor.rgba(0,0,0,0),app.pixelColor.rgba(40,30,28,255),app.pixelColor.rgba(255,255,255,255),app.pixelColor.rgba(238,228,208,255),app.pixelColor.rgba(206,192,168,255)}
 local px={1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,1,1,1,1,1,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,1,1,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,1,1,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,1,1,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,2,2,2,2,2,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,4,4,4,2,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,4,2,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,4,2,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,4,2,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,4,2,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,4,2,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,4,2,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,4,2,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,4,2,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,4,2,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,4,2,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,4,2,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,4,4,4,2,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,2,2,2,2,2,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,1,1,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,1,1,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,1,1,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,1,1,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,1,1,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,1,1,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,1,1,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,1,1,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,1,1,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,1,1,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,1,1,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,1,1,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,1,1,1,1,1,1,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,1,1,1,1,1,1,1,1}
 local source=Image(36,36,ColorMode.RGB)
 for y=0,35 do for x=0,35 do source:drawPixel(x,y,colors[px[y*36+x+1]]) end end
 activeSprite:newCel(activeSprite.layers[1],1,source,Point(0,0))
 end
 for k,v in pairs(m.default_json) do m.current_json[k]=v end
 m.current_json.style_image_display=Image{fromFile="C:/Users/user/orca/workspaces/CocktailCoffeeTea/coffee-brewing/ArtSource/cafe-title-2026-11-18/reference/cafe_wall_effect_right_00.png"}
 m.current_json.reference_images_display={}
 m.current_json.description=s.description
 m.current_json.negative_description="text, letters, numbers, logo, floor, glow, smooth, blur, antialiasing, top down, isometric, cropped object"
 m.current_json.view="side"
 m.current_json.direction="none"
 m.current_json.no_background=true
 m.current_json.reference_image="No"
 m.current_json.init_image="No"
 m.current_json.color_image="No"
 m.current_json.image_size={width=s.width,height=s.height}
 m.current_json.outline="single color black outline"
 m.current_json.seed=0
 m.dialog_json.title="CT18 - "..s.id
 jobId=generation_queue.next_id
 started=os.time()
 m.generate(m)
end
artNativeBatchTimer=Timer{interval=1,ontick=function()
 if finished or not jobId then return end
 local job=nil
 for _,h in ipairs(generation_queue.history) do if h.id==jobId then job=h end end
 if job and (job.status=="completed" or job.status=="completed_with_warning") then
  if dlgLoading then dlgLoading:close() end
  nextJob()
 elseif job and (job.status=="failed" or job.status=="cancelled") then
  finished=true
  artNativeBatchTimer:stop()
  app.alert("Native PixelLab job failed: "..specs[index].id.."; inspect plugin. No ready claim.")
 elseif os.time()-started>600 then
  finished=true
  artNativeBatchTimer:stop()
  app.alert("Native PixelLab timeout: "..specs[index].id.."; inspect plugin. No ready claim.")
 end
end}
artNativeBatchTimer:start()
nextJob()
