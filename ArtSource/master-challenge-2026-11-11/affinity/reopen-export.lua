local o='C:/Users/user/orca/workspaces/CocktailCoffeeTea/coffee-brewing/ArtSource/master-challenge-2026-11-11/affinity/'
local f=io.open(o..'native-frames.csv','w');f:write('clip,index,duration_ms,width,height,layers,tags\n')
for _,n in ipairs({'cocktail_foam','coffee_bean_steam','tea_leaf_wind'}) do
 for _,suffix in ipairs({'','_low'}) do local name=n..suffix;local s=app.open(o..name..'.aseprite');for i,v in ipairs(s.frames) do local im=Image(s.spec);im:drawSprite(s,i);im:saveAs(o..'roundtrip/'..name..'_'..string.format('%02d',i-1)..'.png');f:write(name..','..(i-1)..','..math.floor(v.duration*1000+.5)..','..s.width..','..s.height..','..#s.layers..','..#s.tags..'\n') end;s:close() end
end;f:close()
