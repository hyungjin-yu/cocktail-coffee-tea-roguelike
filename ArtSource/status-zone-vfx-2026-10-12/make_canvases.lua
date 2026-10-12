-- 2026-10-12 42/43: PixelLab 생성용 빈 캔버스(투명, RGB). aseprite -b -script 로 실행.
local root = app.params["root"]
local specs = {
  { "status_burn", 64, 64 },
  { "status_poison", 64, 64 },
  { "zone_coffee_pool_v2", 128, 128 },
  { "zone_olive_fire_v2", 128, 128 },
  { "zone_sugar_pool_v2", 128, 128 },
  { "zone_warning_v2", 128, 128 },
}
for _, s in ipairs(specs) do
  local spr = Sprite(s[2], s[3], ColorMode.RGB)
  spr:saveAs(root .. "/" .. s[1] .. "/" .. s[1] .. ".aseprite")
  spr:close()
end
