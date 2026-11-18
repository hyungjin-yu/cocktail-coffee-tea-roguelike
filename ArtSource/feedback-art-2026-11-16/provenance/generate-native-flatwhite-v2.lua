-- Aseprite native automation. Uses the installed official PixelLab model and its official archive.
-- No credential access, no custom network client, no source patch, no full-trust request.
local specs={{id="dl_coffee_flatwhite_cup_v2",width=32,height=26,description="A VERY LOW VERY WIDE empty ivory ceramic flat-white cup seen from the side. Horizontal shallow cup, broad oval mouth, main cup body only eight pixels tall and twenty-four pixels wide, a small rectangular loop handle on the RIGHT. NO SAUCER, NO PLATE, no spoon. Broad low straight-walled bowl with a short flat base. The cup is about twice as wide as tall, low rim ellipse, not a tall mug, not a round cappuccino bowl. Transparent background and empty mouth, no liquid, no coffee, no foam. Simple chunky cafe inventory pixel sprite, dark brown one-pixel outline 201D29, ivory body, muted taupe shadow, three tones per material, upper left light, crisp pixels, complete object centered, no letters, no text, no cast shadow, no floor, no antialiasing."}}
assert(app.isUIAvailable,"GUI runtime required")
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
  app.alert("Flatwhite revision native PixelLab generation completed; official image archives retained. Final QA still required.")
  return
 end
 closeAllDialogs()
 local s=specs[index]
 activeSprite=Sprite(s.width,s.height,ColorMode.RGB)
 activeSprite.layers[1].name=s.id
 local m=generate_no_style
 for k,v in pairs(m.default_json) do m.current_json[k]=v end
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
 m.current_json.seed="0"
 m.dialog_json.title="Feedback art - "..s.id
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
 elseif os.time()-started>300 then
  finished=true
  artNativeBatchTimer:stop()
  app.alert("Native PixelLab timeout: "..specs[index].id.."; inspect plugin. No ready claim.")
 end
end}
artNativeBatchTimer:start()
nextJob()
