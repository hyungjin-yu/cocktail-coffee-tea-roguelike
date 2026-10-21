-- 커피 제조 아트 Aseprite 정리·출력(배치): aseprite -b --script-param src=<raw> --script-param ase=<원본> --script-param out=<Resources> --script coffee_art_aseprite.lua
-- raw PNG마다: 열기 → 알파 정리(반투명 유리 외에는 0/255로, 거의 투명한 잡티 제거) → .aseprite 원본 저장 → PNG 재출력.
local src = app.params["src"]
local ase = app.params["ase"]
local out = app.params["out"]
assert(src and ase and out, "src/ase/out params required")

local count = 0
for _, name in ipairs(app.fs.listFiles(src)) do
  if name:match("%.png$") then
    local spr = app.open(app.fs.joinPath(src, name))
    local glassLike = name:match("carafe") ~= nil or name:match("espresso") ~= nil
    for _, cel in ipairs(spr.cels) do
      local img = cel.image:clone()
      for it in img:pixels() do
        local c = it()
        local a = app.pixelColor.rgbaA(c)
        if a > 0 and a < 24 then
          it(app.pixelColor.rgba(0, 0, 0, 0))
        elseif a < 255 and a >= 24 and not glassLike and not name:match("_mask") then
          it(app.pixelColor.rgba(app.pixelColor.rgbaR(c), app.pixelColor.rgbaG(c), app.pixelColor.rgbaB(c), 255))
        end
      end
      cel.image = img
    end
    local base = name:gsub("%.png$", "")
    spr:saveAs(app.fs.joinPath(ase, base .. ".aseprite"))
    spr:saveCopyAs(app.fs.joinPath(out, name))
    spr:close()
    count = count + 1
  end
end
print("coffee art exported: " .. count)
