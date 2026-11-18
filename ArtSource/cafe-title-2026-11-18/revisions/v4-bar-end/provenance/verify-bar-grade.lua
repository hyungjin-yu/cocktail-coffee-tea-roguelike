local root="C:/Users/user/orca/workspaces/CocktailCoffeeTea/coffee-brewing/ArtSource/cafe-title-2026-11-18/revisions/v4-bar-end/"
do local s=app.open(root.."ready/gameplay/bg_barwarm_floor/bg_barwarm_floor_00.aseprite") s:saveCopyAs(root.."verification/roundtrip/bg_barwarm_floor.png") s:close() end
do local s=app.open(root.."ready/gameplay/bg_barwarm_wall/bg_barwarm_wall_00.aseprite") s:saveCopyAs(root.."verification/roundtrip/bg_barwarm_wall.png") s:close() end
do local s=app.open(root.."ready/gameplay/bg_barwarm_counter/bg_barwarm_counter_00.aseprite") s:saveCopyAs(root.."verification/roundtrip/bg_barwarm_counter.png") s:close() end
do local s=app.open(root.."ready/gameplay/bg_barwarm_lamp/bg_barwarm_lamp_00.aseprite") s:saveCopyAs(root.."verification/roundtrip/bg_barwarm_lamp.png") s:close() end
