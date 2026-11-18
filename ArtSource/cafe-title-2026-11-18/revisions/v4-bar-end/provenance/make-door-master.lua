local root="C:/Users/user/orca/workspaces/CocktailCoffeeTea/coffee-brewing/ArtSource/cafe-title-2026-11-18/revisions/v4-bar-end/"
local sp=Sprite(184,216,ColorMode.RGB) sp.layers[1].name="Rigid hinge projected door"
local times={.083,.083,.084,.083,.083,.084,.083,.084}
for fr=1,8 do if fr>1 then sp:newEmptyFrame() end local im=Image{fromFile=root.."ready/exterior/door_"..string.format("%02d",fr-1)..".png"} sp:newCel(sp.layers[1],fr,im) sp.frames[fr].duration=times[fr] end
local sl=sp:newSlice(Rectangle(0,0,184,216)) sl.name="door_hinge" sl.pivot=Point(43,9) sp:saveAs(root.."ready/exterior/ct_door_open_master.aseprite") sp:close()
local test=app.open(root.."ready/exterior/ct_door_open_master.aseprite") local ds={} for fr=1,8 do local im=Image(184,216,ColorMode.RGB) im:drawSprite(test,fr) local t=Sprite(184,216,ColorMode.RGB) t:newCel(t.layers[1],1,im) t:saveAs(root.."verification/roundtrip/door-master-"..fr..".png") t:close() ds[#ds+1]=test.frames[fr].duration end local f=assert(io.open(root.."verification/door-master-durations.json","w")) f:write(json.encode(ds)) f:close() test:close()
