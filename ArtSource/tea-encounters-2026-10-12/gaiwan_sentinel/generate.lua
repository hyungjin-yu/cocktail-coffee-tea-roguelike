local out="C:/Users/user/Documents/Projects/CocktailCoffeeTea/ArtSource/tea-encounters-2026-10-12/gaiwan_sentinel"
app.fs.makeAllDirectories(out)
local spr=Sprite(128,128,ColorMode.RGB)
app.activeSprite=spr
description_mode=true
local tool=generate_animate_character_object
local log=io.open("C:/UnityScratch/tea-encounters/generation-status.txt","w")
if not tool then log:write("missing native PixelLab tool");log:close();return end
tool.current_json={  model_name="generate_animate_character_object",
  description="side view pixel art enemy for a 2D tea house action game, an imposing anthropomorphic porcelain gaiwan tea bowl sentinel, ivory ceramic bowl with jade green tea leaf patterns, tilted lid helmet, sturdy little wooden legs, two broad tea tray arms, stern expressive face, facing right, entire body centered with feet fixed near bottom, crisp dark pixel outline, limited warm palette, no coffee grinder, no machinery, no text, transparent background",
  action="slow four frame seamless idle breathing loop, remain planted in the same position, lid tilts gently as steam rises, consistent silhouette and size, no camera movement",
  seed="149001",
  no_background=true,
  output_method="New frame"}
log:write("request prepared with native Aseprite + PixelLab extension\n");log:close()
local ok,err=pcall(function() tool.generate(tool,"Advanced") end)
if not ok then local f=io.open("C:/UnityScratch/tea-encounters/generation-status.txt","a");f:write(tostring(err));f:close();return end
teaGenerationTimer=Timer{interval=1.0,ontick=function()
 if generatingImage or #spr.cels==0 then return end
 local any=false
 for _,cel in ipairs(spr.cels) do if not cel.image:isEmpty() then any=true end end
 if not any then return end
 teaGenerationTimer:stop()
 spr:saveAs(out.."/gaiwan_sentinel.aseprite")
 for i=1,#spr.frames do local img=Image(spr.width,spr.height);img:drawSprite(spr,i);img:saveAs(out..string.format("/pixellab_%02d.png",i-1)) end
 local f=io.open("C:/UnityScratch/tea-encounters/generation-status.txt","a");f:write("complete frames="..#spr.frames);f:close()
end}
teaGenerationTimer:start()
