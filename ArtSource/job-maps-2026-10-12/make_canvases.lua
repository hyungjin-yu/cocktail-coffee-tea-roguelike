-- 2026-10-12 47 직업 맵: PixelLab 생성용 빈 캔버스. aseprite -b --script-param root=<dir> --script make_canvases.lua
local root = app.params["root"]
local specs = {
  { "bg_bar_wall", 256, 192 }, { "bg_bar_floor", 256, 128 }, { "bg_bar_counter", 256, 128 }, { "bg_bar_lamp", 64, 128 },
  { "bg_tea_wall", 256, 192 }, { "bg_tea_floor", 256, 128 }, { "bg_tea_counter", 256, 128 }, { "bg_tea_lamp", 64, 128 },
}
for _, s in ipairs(specs) do
  local spr = Sprite(s[2], s[3], ColorMode.RGB)
  spr:saveAs(root .. "/" .. s[1] .. "/" .. s[1] .. ".aseprite")
  spr:close()
end
