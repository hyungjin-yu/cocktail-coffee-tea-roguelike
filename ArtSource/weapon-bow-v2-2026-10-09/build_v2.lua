-- Builds weapon_bow_v2 native source: frame1 = v1 reference, frame2 = raw PixelLab "Edit image (Pro)" result,
-- frame3 = final (raw shifted +4px x so the limb grip matches the v1 grip anchor (76,128)); exports the final PNG.
local dir = app.params["dir"]
local ref = Image{ fromFile = dir .. "/reference/weapon_bow_effect_right_00.png" }
local raw = Image{ fromFile = dir .. "/raw/pixellab_edit_pro_0.png" }
local spr = Sprite(256, 256, ColorMode.RGB)
local layer = spr.layers[1]
spr:newEmptyFrame(); spr:newEmptyFrame()
spr:newCel(layer, 1, ref, Point(0, 0))
spr:newCel(layer, 2, raw, Point(0, 0))
local final = Image(256, 256, ColorMode.RGB)
final:drawImage(raw, Point(4, 0))
spr:newCel(layer, 3, final, Point(0, 0))
spr:saveAs(dir .. "/weapon_bow_v2.aseprite")
final:saveAs(dir .. "/frames/weapon_bow_v2_right_00.png")
