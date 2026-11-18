-- Aseprite native automation. Uses the installed official PixelLab model and its official archive.
-- No credential access, no custom network client, no source patch, no full-trust request.
local specs={{id="ct_door_material_probe",width=96,height=128,model="create",description="One realistic warm dark walnut cafe entrance door, straight-on FRONT VIEW. A single rectangular wood frame, large clear upper glass pane, solid walnut bottom panel, brass lever handle on RIGHT and two hinges on LEFT. True human door proportions one meter wide by two meters tall. Complete object isolated transparent background, grounded crisp cafe-game PIXEL ART matching dark walnut brown, warm amber ivory light, small clean handcrafted pixel clusters, no text, no letters, no sign, no people, no isometric distortion, no double door, no rounded arches, no cartoon oversize handle."}}
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
  app.alert("CT18 access probe completed; official archives retained. Final QA required.")
  return
 end
 closeAllDialogs()
 local s=specs[index]
 activeSprite=Sprite(s.width,s.height,ColorMode.RGB)
 activeSprite.layers[1].name=s.id
 local m=s.model=="edit" and generate_edit or generate_no_style
 if s.model=="edit" then
 local colors={app.pixelColor.rgba(0,0,0,0),app.pixelColor.rgba(40,30,28,255),app.pixelColor.rgba(255,255,255,255),app.pixelColor.rgba(238,228,208,255),app.pixelColor.rgba(206,192,168,255)}
 local px={1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,1,1,1,1,1,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,1,1,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,1,1,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,1,1,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,2,2,2,2,2,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,4,4,4,2,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,4,2,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,4,2,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,4,2,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,4,2,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,4,2,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,4,2,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,4,2,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,4,2,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,4,2,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,4,2,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,4,2,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,4,4,4,2,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,2,2,2,2,2,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,1,1,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,1,1,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,1,1,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,1,1,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,1,1,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,1,1,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,1,1,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,1,1,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,1,1,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,1,1,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,1,1,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,1,1,1,1,1,2,3,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,5,2,1,1,1,1,1,1,1,1,1,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,1,1,1,1,1,1,1,1}
 local source=Image(36,36,ColorMode.RGB)
 for y=0,35 do for x=0,35 do source:drawPixel(x,y,colors[px[y*36+x+1]]) end end
 activeSprite:newCel(activeSprite.layers[1],1,source,Point(0,0))
 end
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
 elseif os.time()-started>300 then
  finished=true
  artNativeBatchTimer:stop()
  app.alert("Native PixelLab timeout: "..specs[index].id.."; inspect plugin. No ready claim.")
 end
end}
artNativeBatchTimer:start()
nextJob()
