-- Calls the installed official PixelLab model inside Aseprite; never reads credentials.
local out = "C:/Users/user/orca/workspaces/CocktailCoffeeTea/coffee-brewing/ArtSource/feedback-art-2026-11-16/raw/"
assert(app.isUIAvailable, "Native GUI required")
closeAllDialogs()
_play_sound_on_complete = false
local spr = Sprite(32,26,ColorMode.RGB)
local model = generate_no_style
for k,v in pairs(model.default_json) do model.current_json[k]=v end
model.current_json.description = "One empty low wide ceramic flat white coffee cup, squat body, broad oval opening, compact angular handle on the right, no saucer, ivory ceramic with muted warm taupe shadow, dark brown chunky pixel outline, three shading tones, simple cafe game inventory sprite, transparent background, no liquid, no text, no steam, no floor or cast shadow, crisp pixels, side view."
model.current_json.negative_description = "text, letters, logo, floor, smooth, antialiasing, glow, steam, filled cup, top down, isometric"
model.current_json.view="side"
model.current_json.direction="none"
model.current_json.no_background=true
model.current_json.reference_image="No"
model.current_json.init_image="No"
model.current_json.color_image="No"
model.current_json.image_size={width=32,height=26}
model.current_json.outline="single color black outline"
model.current_json.seed="0"
local jobId=generation_queue.next_id
model.generate(model)
local start=os.time()
local timer
timer=Timer{interval=1,ontick=function()
 local job=nil
 for _,h in ipairs(generation_queue.history) do if h.id==jobId then job=h end end
 if job and (job.status=="completed" or job.status=="completed_with_warning") then
  timer:stop()
  if dlgLoading then dlgLoading:close() end
  spr:saveAs(out.."dl_coffee_flatwhite_cup.pixellab.aseprite")
  local img=Image(spr.width,spr.height)
  img:drawSprite(spr,#spr.frames)
  local one=Sprite(spr.width,spr.height,ColorMode.RGB)
  one:newCel(one.layers[1],1,img,Point(0,0))
  one:saveAs(out.."dl_coffee_flatwhite_cup.aseprite")
  one:saveCopyAs(out.."dl_coffee_flatwhite_cup.png")
  app.refresh()
 elseif job and (job.status=="failed" or job.status=="cancelled") then
  timer:stop()
  app.alert("Native PixelLab generation failed; inspect plugin error. No ready output.")
 elseif os.time()-start>240 then
  timer:stop()
  app.alert("Native PixelLab timeout; no ready output.")
 end
end}
timer:start()
