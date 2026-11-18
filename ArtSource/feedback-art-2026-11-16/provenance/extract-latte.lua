local root="C:/Users/user/orca/workspaces/CocktailCoffeeTea/coffee-brewing/ArtSource/feedback-art-2026-11-16/"
local spr=app.open(root.."pixellab-native-raw.aseprite")
local img=Image(spr.width,spr.height)
img:drawSprite(spr,#spr.frames)
local one=Sprite(spr.width,spr.height,ColorMode.RGB)
one:newCel(one.layers[1],1,img,Point(0,0))
one:saveAs(root.."raw/dl_coffee_latte_glass.aseprite")
one:saveCopyAs(root.."raw/dl_coffee_latte_glass.png")
