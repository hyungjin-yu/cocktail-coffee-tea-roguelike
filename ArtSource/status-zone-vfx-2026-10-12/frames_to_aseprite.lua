-- PixelLab 생성 기록 PNG 프레임(raw)을 순서대로 한 .aseprite 문서로 저장(GUI 저장 대신 Aseprite CLI).
-- aseprite -b --script-param dir=<raw폴더> --script-param out=<파일.aseprite> --script-param count=N --script-param prefix=<이름_raw_> --script frames_to_aseprite.lua
local dir, out, count, prefix = app.params["dir"], app.params["out"], tonumber(app.params["count"]), app.params["prefix"]
local first = Image{ fromFile = dir .. "/" .. prefix .. "00.png" }
local spr = Sprite(first.width, first.height, ColorMode.RGB)
spr.cels[1].image = first
spr.frames[1].duration = 0.1
for i = 1, count - 1 do
  local img = Image{ fromFile = dir .. "/" .. string.format("%s%02d.png", prefix, i) }
  local fr = spr:newEmptyFrame()
  fr.duration = 0.1
  spr:newCel(spr.layers[1], fr, img, Point(0, 0))
end
spr.layers[1].name = "pixellab_output"
spr:saveAs(out)
spr:close()
