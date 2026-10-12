local sprites={}
generation_queue.set_background_enabled(true)
description_mode=true
do
local spr=Sprite(128,128,ColorMode.RGB);app.activeSprite=spr
local tool=dofile('./generate-animate-character-object.lua')
tool.current_json={description="side view crisp pixel art enemy for a 2D tea house action game, facing right, centered full body, fixed scale, dark pixel outline, limited warm palette, transparent background, no text, an imposing terracotta tea kettle elder spirit with a long curved spout, jade lid crown, bamboo feet, billowing white steam beard and two vapor arms, warm earthenware and tea green accents, no metal coffee machine", action="slow seamless idle breathing loop, stationary bamboo feet, steam beard curls gently, lid rises slightly, four distinct frames, no morphing, no camera movement", seed="149002", no_background=true, output_method="New frame", model_name="generate_animate_character_object"}
sprites[#sprites+1]={sprite=spr,id="steam_kettle_elder"}
tool.generate(tool,"Advanced")
end
do
local spr=Sprite(128,128,ColorMode.RGB);app.activeSprite=spr
local tool=dofile('./generate-animate-character-object.lua')
tool.current_json={description="side view crisp pixel art enemy for a 2D tea house action game, facing right, centered full body, fixed scale, dark pixel outline, limited warm palette, transparent background, no text, a powerful tea charcoal furnace dragon, glazed bronze and jade ceramic brazier belly with small glowing charcoal embers, leafy mane, curled tea steam wings, four sturdy ceramic feet, large readable silhouette, no espresso pump or coffee machine", action="slow seamless breathing loop while planted, leafy mane sways gently, steam wings curl, consistent body and size, four distinct frames, no morphing, no camera movement", seed="149003", no_background=true, output_method="New frame", model_name="generate_animate_character_object"}
sprites[#sprites+1]={sprite=spr,id="tea_kiln_dragon"}
tool.generate(tool,"Advanced")
end
do
local spr=Sprite(128,128,ColorMode.RGB);app.activeSprite=spr
local tool=dofile('./generate-animate-character-object.lua')
tool.current_json={description="side view crisp pixel art enemy for a 2D tea house action game, facing right, centered full body, fixed scale, dark pixel outline, limited warm palette, transparent background, no text, a small mischievous young tea leaf bud sprite, humanoid layered fresh green leaves, two small plant legs, gentle expressive face, folded leaf arms, not a teabag and not a lime wedge", action="slow seamless walking in place facing right, alternate plant legs with clearly distinct steps, grounded feet, no translation, four distinct frames, no morphing, no camera movement", seed="149004", no_background=true, output_method="New frame", model_name="generate_animate_character_object"}
sprites[#sprites+1]={sprite=spr,id="spring_leaf_sprite"}
tool.generate(tool,"Advanced")
end
do
local spr=Sprite(128,128,ColorMode.RGB);app.activeSprite=spr
local tool=dofile('./generate-animate-character-object.lua')
tool.current_json={description="side view crisp pixel art enemy for a 2D tea house action game, facing right, centered full body, fixed scale, dark pixel outline, limited warm palette, transparent background, no text, a small animated bamboo tea scoop guardian, bamboo scoop body and curved scoop beak, jade leaf quiver, two short wooden legs, expressive face, holding a loose tea leaf ready to throw, not a kettle or bottle", action="slow seamless walk in place facing right, alternate wooden legs, hold tea leaf ready, steady upper body, no translation, four distinct frames, no morphing, no camera movement", seed="149005", no_background=true, output_method="New frame", model_name="generate_animate_character_object"}
sprites[#sprites+1]={sprite=spr,id="bamboo_scoop_archer"}
tool.generate(tool,"Advanced")
end
teaBatchTimer=Timer{interval=1.0,ontick=function()
if generation_queue.is_busy() then return end
teaBatchTimer:stop()
for _,entry in ipairs(sprites) do
local spr=entry.sprite
local dest="C:/Users/user/Documents/Projects/CocktailCoffeeTea/ArtSource/tea-encounters-2026-10-12/"..entry.id
spr:saveAs(dest.."/"..entry.id..".aseprite")
for i=1,#spr.frames do local img=Image(spr.width,spr.height);img:drawSprite(spr,i);img:saveAs(dest..string.format("/pixellab_%02d.png",i-1)) end
end
end}
teaBatchTimer:start()