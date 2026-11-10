local o='C:/Users/user/orca/workspaces/CocktailCoffeeTea/coffee-brewing/ArtSource/master-challenge-2026-11-11/'
local names={'quality_normal','quality_excellent','quality_master','quality_normal_low','quality_excellent_low','quality_master_low','banner_install','npc_porter_walk_in','npc_porter_install','npc_porter_idle','npc_porter_walk_out','npc_rigger_walk_in','npc_rigger_install','npc_rigger_idle','npc_rigger_walk_out'}
local meta=io.open(o..'native-frames.csv','w');meta:write('clip,index,duration_ms,width,height,layers,tags\n')
for _,n in ipairs(names) do
 local s=app.open(o..n..'.aseprite')
 for i,f in ipairs(s.frames) do local im=Image(s.spec);im:drawSprite(s,i);im:saveAs(o..'roundtrip/'..n..'_'..string.format('%02d',i-1)..'.png');meta:write(n..','..(i-1)..','..math.floor(f.duration*1000+.5)..','..s.width..','..s.height..','..#s.layers..','..#s.tags..'\n') end;s:close()
end
meta:close()
