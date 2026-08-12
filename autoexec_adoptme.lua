--[[
  autoexec_adoptme.lua — кладётся в папку autoexec Potassium (через лаунчер).
  Один скрипт на все клиенты Adopt Me; роль по нику:
    - главный (ник из _main.txt) -> приёмка; любой другой -> сдача лута.

  Adopt Me специфика:
    - ремоуты: ReplicatedStorage.API["TradeAPI/<Method>"] (имя содержит слэш!)
    - трейд: SendTradeRequest(player) -> AcceptOrDeclineTradeRequest(player,true)
             -> AddItemToOffer(unique) -> AcceptNegotiation() -> ConfirmTrade()
    - инвентарь: из памяти (getgc), трейдбл = properties.tradeable_timestamp в прошлом/нет
    - разлочка: BackpackAPI/CommitBackpackItemSet("backpack_locks", {[unique]=false})
    - старт-меню и попапы закрываются VIM-кликами
]]

------------------------------------------------------------------
-- КОНФИГ
------------------------------------------------------------------
-- рабочая папка координации. Многопоток: оркестратор кладёт route_<ник>.txt в базовую adoptme_done
-- -> клиент переключается в свою папку потока. Нет файла = базовая (1 поток).
local BASE_DIR      = "adoptme_done"
local _lpname = ""
for _ = 1, 50 do
  local ok, nm = pcall(function() local p = game:GetService("Players").LocalPlayer; return p and p.Name end)
  if ok and nm and nm ~= "" then _lpname = nm; break end
  task.wait(0.1)
end
local DONE_DIR      = BASE_DIR
do
  local f = BASE_DIR .. "/route_" .. _lpname .. ".txt"
  if isfile and isfile(f) then
    local ok, v = pcall(readfile, f)
    if ok and type(v) == "string" then v = (v:gsub("%s+$", "")); if v ~= "" then DONE_DIR = v end end
  end
end
-- главного и вебхук НЕ хардкодим — оркестратор пишет их в _main.txt/_webhook.txt (из полей GUI/конфига)
local function _readset(name, default)
  if isfile and isfile(DONE_DIR .. "/" .. name) then
    local ok, v = pcall(readfile, DONE_DIR .. "/" .. name)
    if ok and type(v) == "string" then v = (v:gsub("%s+$", "")); if v ~= "" then return v end end
  end
  return default
end
local MAIN_USERNAME   = _readset("_main.txt", "")
local DISCORD_WEBHOOK = _readset("_webhook.txt", "")
local ADOPTME_PLACE = 920587237

local MAX_PER_TRADE  = 18
local OFFER_DELAY    = 0.2    -- пауза между AddItemToOffer (проверенное: 0.06 сервер дропал)
local STEP_TIMEOUT   = 20
local BETWEEN_TRADES = 0.6    -- пауза между трейдами (если трейды начнут срываться — подними)
local SETTLE         = 2      -- пауза после открытия торга (было 3; ускорено)

-- категории, которые НЕ сдаём (мусор). Добавляй сюда нужное.
local SKIP_CATEGORIES = { stickers = true, food = true }

-- Антрейд ловим ПО ФАКТУ (не встал в оффер / не ушёл в состоявшемся трейде -> пропуск), без фильтра
-- по id: трейдабельность per-instance, один и тот же id у разных акков может быть трейд/антрейд.

------------------------------------------------------------------
local Players = game:GetService("Players")
local RS      = game:GetService("ReplicatedStorage")
local Http    = game:GetService("HttpService")
local VIM     = game:GetService("VirtualInputManager")
local GuiSvc  = game:GetService("GuiService")
local lp      = Players.LocalPlayer

local DISCORD_VERBOSE = false  -- log() -> только консоль; logd() -> консоль+Discord (вехи)

local httpreq = request or http_request or (syn and syn.request) or (http and http.request)
local function dsend(msg)
  if not httpreq or DISCORD_WEBHOOK == "" then return end
  task.spawn(function() pcall(function()
    httpreq({ Url = DISCORD_WEBHOOK, Method = "POST",
      Headers = { ["Content-Type"] = "application/json" },
      Body = Http:JSONEncode({ content = ("`[%s]` %s"):format(lp.Name, tostring(msg)) }) })
  end) end)
end
local function log(m)  warn(("[AM][%s] %s"):format(lp.Name, tostring(m))); if DISCORD_VERBOSE then dsend(m) end end
local function logd(m) warn(("[AM][%s] %s"):format(lp.Name, tostring(m))); dsend(m) end

log("СТАРТ (place=" .. tostring(game.PlaceId) .. ")")

------------------------------------------------------------------
-- ФЛАГИ для оркестратора
------------------------------------------------------------------
local function flagFor(name, ext, body)
  pcall(function()
    if makefolder then pcall(makefolder, DONE_DIR) end
    writefile(DONE_DIR .. "/" .. name .. ext, body or "1")
  end)
end
local function flag(ext, body) flagFor(lp.Name, ext, body) end
flag(".run")   -- атачнулся (Lua запустился) -> оркестратор: нет .run за ~12с = битый

-- ГЛАВНЫЙ публикует свой JobId -> оркестратор загоняет альтов ЖЁСТКО в его сервер (надёжнее follow)
if lp.Name == MAIN_USERNAME then
  task.spawn(function()
    if not game:IsLoaded() then game.Loaded:Wait() end
    while true do
      local jid = game.JobId
      if jid and jid ~= "" then pcall(writefile, DONE_DIR .. "/_mainjob.txt", jid) end
      task.wait(8)
    end
  end)
end

-- детект ошибок захода (битые акки) И дисконнектов В ИГРЕ (273 «зашли с другого устройства» и т.п.).
-- Работает ВЕСЬ прогон, не только первые 45с — иначе кик в середине трейда не ловится и акк висит
-- до active_timeout (20 мин). Различаем: до спавна = не зашёл (.err); после = дисконнект (.fail, перечек).
task.spawn(function()
  local roots = { game:GetService("CoreGui") }
  pcall(function() roots[#roots+1] = lp:WaitForChild("PlayerGui", 10) end)
  local spawned = lp.Character ~= nil
  lp.CharacterAdded:Connect(function() spawned = true end)
  local t0 = tick()
  while true do
    for _, root in ipairs(roots) do
      for _, d in ipairs(root:GetDescendants()) do
        if d:IsA("TextLabel") then
          local txt = tostring(d.Text)
          local code = txt:match("Error Code:?%s*(%d+)")
          if code or txt:find("Join Error") or txt:find("permission to join")
              or txt:find("not authorized") then
            if spawned then
              logd("ДИСКОННЕКТ в игре (код " .. tostring(code) .. ") -> .fail, перечек")
              flag(".fail")            -- кик/дисконнект в процессе -> оркестратор сразу переходит дальше
            else
              logd("JOIN ERROR: " .. txt)
              flag(".err", code or "join_error")
            end
            return
          end
        end
      end
    end
    task.wait(tick() - t0 < 45 and 0.5 or 1.5)   -- при заходе чаще, потом реже (меньше нагрузки)
  end
end)

-- анти-афк
pcall(function()
  local VU = game:GetService("VirtualUser")
  lp.Idled:Connect(function() VU:CaptureController(); VU:ClickButton2(Vector2.new()) end)
end)

------------------------------------------------------------------
-- КЛИКИ (старт-меню + попапы)
------------------------------------------------------------------
local pg = lp:WaitForChild("PlayerGui")
local INSET = GuiSvc:GetGuiInset().Y

local function resolve(path)
  local n = pg
  for part in path:gmatch("[^%.]+") do n = n and n:FindFirstChild(part) end
  return n
end
-- "показан ли элемент": собственная .Visible + ScreenGui.Enabled + есть размер + на экране.
-- (НЕ обходим всех предков — в Adopt Me это давало ложное "невидим")
local function visible(g)
  if not g or not g:IsA("GuiObject") or not g.Visible then return false end
  local sg = g:FindFirstAncestorWhichIsA("ScreenGui")
  if sg and not sg.Enabled then return false end
  if g.AbsoluteSize.X < 2 then return false end
  local cam = workspace.CurrentCamera
  local vp = cam and cam.ViewportSize or Vector2.new(3000, 3000)
  local c = g.AbsolutePosition + g.AbsoluteSize / 2
  return c.X > -5 and c.Y > -5 and c.X < vp.X + 5 and c.Y < vp.Y + 5
end
local function clickGui(g)
  local p, s = g.AbsolutePosition, g.AbsoluteSize
  local x, y = p.X + s.X / 2, p.Y + s.Y / 2 + INSET
  VIM:SendMouseMoveEvent(x, y, game); task.wait(0.03)
  VIM:SendMouseButtonEvent(x, y, 0, true, game, 0); task.wait(0.05)
  VIM:SendMouseButtonEvent(x, y, 0, false, game, 0)
end
local function clickPath(path, tries)
  tries = tries or 1
  local short = path:match("[^%.]+$")
  local g
  for _ = 1, 30 do
    local cand = resolve(path)
    if cand and visible(cand) then g = cand break end
    task.wait(0.5)
  end
  if not g then log("  [клик] НЕ найдена/не видна: " .. short); return false end
  for _ = 1, tries do clickGui(g); if tries > 1 then task.wait(0.6) end end
  local p = g.AbsolutePosition
  log(("  [клик] %s @(%d,%d) x%d"):format(short, p.X, p.Y + INSET, tries))
  return true
end

-- фоновый вотчер случайных попапов
local POPUPS = {
  "PlaytimePayoutsApp.Frame.Container.CashOutContainer.CashOutButton",  -- CASH OUT
  "ExperimentalNewsApp.EnclosingFrame.MainFrame.Contents.PlayButton",   -- новости
  "DialogApp.Dialog.NormalDialog.Buttons.ButtonTemplate",              -- "Be careful when trading"
  "DialogApp.Dialog.ColorPickerDialog.Buttons.ButtonTemplate",         -- выбор цвета дома (недоуч. акки)
  "DialogApp.Dialog.ExitButton",                                       -- промо/реклама (крестик)
}
task.spawn(function()
  local seen = {}
  while true do
    for _, path in ipairs(POPUPS) do
      local g = resolve(path)
      if g and visible(g) then
        pcall(clickGui, g)
        if not seen[path] then log("[попап] закрыл " .. g.Name); seen[path] = true end
      else
        seen[path] = false
      end
    end
    task.wait(1)
  end
end)

-- СТАРТ-навигация: ЦИКЛ до спавна (~90с) — кликает Play/Home/промо как только появятся.
-- Устойчиво к долгой загрузке save/house (кнопки всплывают не сразу).
task.spawn(function()
  if not game:IsLoaded() then game.Loaded:Wait() end
  log("старт-навигация: клики до спавна")
  local NAV = {
    { name = "Play",  path = "ExperimentalNewsApp.EnclosingFrame.MainFrame.Contents.PlayButton" },
    { name = "Home",  path = "DialogApp.Dialog.SpawnChooserDialog.UpperCardContainer.ChoicesContent.Choices.Home" },
    { name = "Promo", path = "DialogApp.Dialog.ExitButton" },
  }
  local clicked, lastSig = {}, ""
  for i = 1, 60 do
    local parts = {}
    for _, it in ipairs(NAV) do
      local g = resolve(it.path)
      parts[#parts + 1] = it.name .. (g and "=есть" or "=нет")
      if g then                         -- жмём ПО ФАКТУ наличия (как рабочий click.lua), без visible
        clickGui(g)
        if not clicked[it.name] then logd("[клик] " .. it.name); clicked[it.name] = true end
      end
    end
    -- лог состояния меню при изменении (видно, появилась ли Home вообще)
    local sig = table.concat(parts, "  ")
    if sig ~= lastSig then logd("меню: " .. sig); lastSig = sig end
    local ch = lp.Character
    if ch and ch:FindFirstChild("HumanoidRootPart") then
      logd("v ЗАСПАВНИЛСЯ (итераций " .. i .. ")"); return
    end
    task.wait(1.5)
  end
  logd("! НЕ заспавнился за ~90с")
end)

------------------------------------------------------------------
-- API + состояние трейда
------------------------------------------------------------------
local API = RS:WaitForChild("API")
local function api(name)
  local r = API:FindFirstChild(name)
  if not r then log("нет ремоута: " .. name) end
  return r
end

local TradeApp = pg:WaitForChild("TradeApp", 60)
-- ВАЖНО: торг открыт = TradeApp.Frame.Visible == true (у под-фреймов .Visible всегда true!).
-- Фазу различаем по .Visible под-фрейма ВНУТРИ открытого торга.
local function tradeFrame() return TradeApp and TradeApp:FindFirstChild("Frame") end
local function tradeOpen()
  local f = tradeFrame()
  return f ~= nil and f.Visible == true
end
local function negotiating()
  local f = tradeFrame()
  if not (f and f.Visible) then return false end
  local n = f:FindFirstChild("NegotiationFrame")
  return n ~= nil and n.Visible == true
end
local function confirming()
  local f = tradeFrame()
  if not (f and f.Visible) then return false end
  local c = f:FindFirstChild("ConfirmationFrame")
  return c ~= nil and c.Visible == true
end

-- сколько предметов реально стоит в МОЁМ оффере (слот занят = есть ItemImageTemplate)
local function countOffer()
  local ok, slots = pcall(function() return pg.TradeApp.Frame.NegotiationFrame.Body.MyOffer.Slots end)
  if not ok or not slots then return -1 end
  local n = 0
  for _, c in ipairs(slots:GetChildren()) do
    if c:IsA("ImageButton") and c.Name:match("^Slot%d+$") and c:FindFirstChild("ItemImageTemplate") then
      n += 1
    end
  end
  return n
end
local function waitFor(fn, t)
  local t0 = os.clock()
  while os.clock() - t0 < t do if fn() then return true end task.wait(0.3) end
  return false
end

-- сигнал "зашёл в игру"
if game.PlaceId == ADOPTME_PLACE then
  task.spawn(function()
    lp.CharacterAdded:Wait()
    flag(".join")
    log("зашёл в игру (join)")
  end)
end

-- инвентарь: читаем из GUI РЮКЗАКА (getgc мёртв на Xeno — экзекьютор на identity 3 в отдельном VM,
-- игровых таблиц не видит). Сдаём ТОЛЬКО эти 5 категорий; остальное (gifts/food/stickers/event) не трогаем.
-- Варианты (neon/mega/fly/ride) бывают ТОЛЬКО у pets.
-- onlyCat = nil -> все 5; иначе одна категория (для раздачи).
-- Возвращает: items (список uniques), loaded (true = рюкзак открылся => список авторитетный, 0 = реально пусто).
-- INFO[unique] = {name=realName, cat, neon, mega, fly, ride} — для .items (оценка $ с вариантами).
local TRADE_CATS = { "pets", "pet_accessories", "strollers", "transport", "toys" }
-- фильтр категорий из приложения (аналог фильтров редкости MM2). Оркестратор пишет _categories.txt
-- (имена через запятую: pets,pet_accessories,strollers,transport,toys). Пусто/нет файла = все 5.
local CATEGORIES = (function()
  local raw = _readset("_categories.txt", "")
  if raw == "" then return TRADE_CATS end
  local allow, sel = {}, {}
  for _, c in ipairs(TRADE_CATS) do allow[c] = true end
  for c in raw:gmatch("[^,%s]+") do if allow[c] then sel[#sel + 1] = c end end
  return (#sel > 0) and sel or TRADE_CATS
end)()
local INFO, SOLD = {}, {}
local function bucketLabel(cat)
  cat = tostring(cat or "")
  local map = { pets = "Питомцы", transport = "Транспорт", vehicles = "Транспорт",
                toys = "Игрушки", strollers = "Коляски", pet_accessories = "Одежда", eggs = "Яйца" }
  return map[cat] or cat
end
-- вариант пета для оценки $: "<pumping>,<fly>,<ride>" (mega_neon > neon > default). Не-пет = "default,0,0".
local function variantOf(info)
  if not info or info.cat ~= "pets" then return "default,0,0" end
  local pump = info.mega and "mega_neon" or (info.neon and "neon" or "default")
  return pump .. "," .. (info.fly and 1 or 0) .. "," .. (info.ride and 1 or 0)
end

-- ── чтение тайлов рюкзака ──
local function bpApp() return pg:FindFirstChild("BackpackApp") end
local function bpOpen() local a = bpApp(); return (a and a.Frame and a.Frame.Visible == true) or false end
local function openBackpack()
  if bpOpen() then return true end
  local btn
  pcall(function() btn = pg.ToolApp.Frame.Hotbar.OpenBackpackContainer.OpenBackpack end)
  if not btn then pcall(function() btn = pg.FocusPetApp.Frame.Backpack.OpenBackpackContainer.OpenBackpack end) end
  if btn then pcall(clickGui, btn) end
  return waitFor(bpOpen, 6)
end
local function clickCategory(cat)
  local a = bpApp(); if not a then return false end
  local b; pcall(function() b = a.Frame.Body.Categories.Buttons:FindFirstChild(cat) end)
  if not b then return false end
  pcall(clickGui, b); task.wait(0.45); return true
end
-- прочитать ОДИН видимый тайл -> в items+INFO (дедуп по seen). unique = имя слота ЦЕЛИКОМ (= v.unique в getgc).
local function readSlot(slot, items, seen)
  local nm = slot.Name
  if not (slot:IsA("Frame") and nm:find("_", 1, true) and not nm:find("add_more", 1, true)) then return end
  if seen[nm] then return end
  local realName, cat = "?", "?"
  local ok, tags = pcall(function() return slot:GetTags() end)
  if ok then for _, t in ipairs(tags) do
    local a2, b2 = t:match("^backpack:([^:]+):(.+)$"); if a2 then cat = a2; realName = b2 end
  end end
  local neon, mega, fly, ride = false, false, false, false
  if cat == "pets" then
    local btn = slot:FindFirstChild("Button")
    local tdt = btn and btn:FindFirstChild("TagDisplayTemplate")
    local function vis(n) local x = tdt and tdt:FindFirstChild(n); local o, v = pcall(function() return x and x.Visible end); return (o and v) and true or false end
    neon, mega, fly, ride = vis("neon"), vis("mega_neon"), vis("flyable"), vis("rideable")
  end
  seen[nm] = true
  items[#items + 1] = nm
  INFO[nm] = { name = realName, cat = cat, neon = neon, mega = mega, fly = fly, ride = ride }
end

-- тайлы текущей открытой категории -> items+INFO. ScrollingFrame ВИРТУАЛИЗИРОВАН (рендерит только видимые
-- тайлы ~4 шт; у акка может быть 69), поэтому быстро проскролливаем сверху донизу через CanvasPosition
-- (без VIM) и собираем на каждом шаге. Дедуп по unique. Мелкие категории (всё влезает) не скроллим.
local function readOpenCategory(items, seen)
  local a = bpApp(); if not a then return end
  local sf; pcall(function() sf = a.Frame.Body.ScrollComplex.ScrollingFrame end)
  if not sf then return end
  local content = sf:FindFirstChild("Content"); if not content then return end
  local function readVisible()
    for _, grp in ipairs(content:GetChildren()) do
      if grp:IsA("Frame") then
        for _, row in ipairs(grp:GetChildren()) do
          if row:IsA("Frame") and row.Name:sub(1, 3) == "Row" then
            for _, slot in ipairs(row:GetChildren()) do readSlot(slot, items, seen) end
          end
        end
      end
    end
  end
  pcall(function() sf.CanvasPosition = Vector2.new(0, 0) end)   -- сверху
  task.wait(0.07)
  readVisible()
  local maxY, view = 0, 250
  pcall(function() maxY = sf.AbsoluteCanvasSize.Y - sf.AbsoluteWindowSize.Y; view = sf.AbsoluteWindowSize.Y end)
  if maxY > 4 then                                              -- есть что скроллить -> идём вниз шагами
    local step = math.max(80, view * 0.8)                      -- ~20% нахлёст, чтобы не пропустить ряды
    local y = 0
    for _ = 1, 60 do                                           -- кап на всякий (60*шаг покрывает огромный инвентарь)
      y = y + step
      pcall(function() sf.CanvasPosition = Vector2.new(0, math.min(y, maxY)) end)
      task.wait(0.07)                                          -- быстрый рендер виртуализации
      readVisible()
      if y >= maxY then break end
    end
  end
end
-- ── чтение через getgc (любой экзекьютор, ГДЕ getgc видит игровые таблицы — Potassium и т.п.) ──
-- сейв = таблица {inventory=table, money=..., БЕЗ trade_partner_inventory}. unique целиком (с префиксом).
local function getgcInv()
  if type(getgc) ~= "function" then return nil end
  local ok, g = pcall(getgc, true)
  if not ok or type(g) ~= "table" then return nil end
  local function sz(t) local n = 0 for _ in pairs(t) do n = n + 1 end return n end
  local function tot(inv) local t = 0 for _, c in pairs(inv) do if type(c) == "table" then t = t + sz(c) end end return t end
  local inv, best = nil, -1
  for _, o in ipairs(g) do if type(o) == "table" then pcall(function()
    local i = rawget(o, "inventory")
    if type(i) == "table" and rawget(o, "money") ~= nil and rawget(o, "trade_partner_inventory") == nil then
      local n = tot(i); if n > best then best = n; inv = i end
    end
  end) end end
  return inv
end
local function allowedSet(onlyCat)
  local s = {}
  if onlyCat then s[onlyCat] = true else for _, c in ipairs(CATEGORIES) do s[c] = true end end
  return s
end
-- собрать из getgc-инвентаря: uniques + INFO (варианты прямо из properties). true = getgc дал инвентарь.
local function readGetgc(onlyCat, items, seen)
  local inv = getgcInv()
  if not inv then return false end
  for cat in pairs(allowedSet(onlyCat)) do
    local catItems = inv[cat]
    if type(catItems) == "table" then
      for _, v in pairs(catItems) do
        if type(v) == "table" then
          local u = rawget(v, "unique")
          if type(u) == "string" and not seen[u] then
            seen[u] = true
            local props = rawget(v, "properties") or {}
            local rn = rawget(v, "id") or rawget(v, "kind") or "?"
            items[#items + 1] = u
            INFO[u] = { name = tostring(rn), cat = cat,
              neon = rawget(props, "neon") == true, mega = rawget(props, "mega_neon") == true,
              fly = rawget(props, "flyable") == true, ride = rawget(props, "rideable") == true }
          end
        end
      end
    end
  end
  return true
end

-- ГИБРИД (по возможностям, не по имени экзека): getgc если он видит инвентарь -> иначе GUI рюкзака.
-- Возвращает (uniques, loaded). unique в обоих путях ОДИНАКОВЫЙ (полное имя слота = v.unique).
local function getTradeable(onlyCat)
  local items, seen = {}, {}
  if readGetgc(onlyCat, items, seen) then return items, true end   -- 1) getgc — быстро, со всеми полями
  if not openBackpack() then return items, false end               -- 2) getgc не видит (Xeno) -> GUI рюкзака
  local cats = onlyCat and { onlyCat } or CATEGORIES
  for _, cat in ipairs(cats) do
    if clickCategory(cat) then readOpenCategory(items, seen) end
  end
  return items, true
end
local function invSet(onlyCat) local s = {}; for _, u in ipairs((getTradeable(onlyCat))) do s[u] = true end return s end

-- ПОЛНЫЙ инвентарь главного (5 торгуемых категорий) из GUI -> <ник>.fullitems для оценки $ в отчёте
local function dumpFullInventory()
  local items = (getTradeable(nil))   -- тот же GUI-ридер; наполняет INFO
  local agg = {}
  for _, u in ipairs(items) do
    local info = INFO[u]
    if info then
      local key = (info.name or "?") .. "\t" .. bucketLabel(info.cat) .. "\t" .. variantOf(info)
      agg[key] = (agg[key] or 0) + 1
    end
  end
  local lines = {}
  for key, cnt in pairs(agg) do
    local n, b, var = key:match("^(.-)\t(.-)\t(.*)$")
    lines[#lines + 1] = n .. "\t" .. cnt .. "\t" .. b .. "\t" .. var
  end
  if #lines > 0 then flag(".fullitems", table.concat(lines, "\n")); log("полный инвентарь записан (.fullitems): " .. #lines .. " видов") end
end

-- по сигналу оркестратора (_dumpfull.txt) главный пишет полную стоимость инвентаря
if lp.Name == MAIN_USERNAME then
  task.spawn(function()
    while true do
      if isfile and isfile(DONE_DIR .. "/_dumpfull.txt") then pcall(dumpFullInventory) end
      task.wait(2)
    end
  end)
end


-- active-token (очередь альтов в публичке). Нет файла = ручной тест -> без очереди.
local ACTIVE_FILE = DONE_DIR .. "/_active.txt"
local function activeExists() return isfile and isfile(ACTIVE_FILE) end
local function readActive()
  if not activeExists() then return nil end
  local ok, c = pcall(readfile, ACTIVE_FILE)
  if ok and type(c) == "string" then return (c:gsub("%s+$", "")) end
  return nil
end

-- режим: "collect" (сбор на главный) или "distribute" (раздача с главный по категориям)
local MODE_FILE = DONE_DIR .. "/_mode.txt"
local function readMode()
  if not (isfile and isfile(MODE_FILE)) then return "collect" end
  local ok, c = pcall(readfile, MODE_FILE)
  if ok and type(c) == "string" and c:gsub("%s+", "") == "distribute" then return "distribute" end
  return "collect"
end
-- категория активного получателя (для раздачи)
local CAT_FILE = DONE_DIR .. "/_category.txt"
local function readCategory()
  if not (isfile and isfile(CAT_FILE)) then return nil end
  local ok, c = pcall(readfile, CAT_FILE)
  if ok and type(c) == "string" then local s = (c:gsub("%s+$", "")); if s ~= "" then return s end end
  return nil
end
local function waitForPlayer(name, timeout)
  local t0 = tick()
  while tick() - t0 < timeout do
    local p = Players:FindFirstChild(name)
    if p then return p end
    task.wait(0.5)
  end
end

------------------------------------------------------------------
-- ПРИЁМНИК (главный)
------------------------------------------------------------------
-- acceptPred(from) -> bool: принимать ли запрос. По умолчанию (сбор) — активный токен/любой.
local function runReceiver(acceptPred)
  acceptPred = acceptPred or function(from)
    local act = readActive()
    return (not act) or from.Name == act
  end
  log("роль: ПРИЁМНИК — жду запросы на трейд")
  local pendingFrom = nil   -- последний запросивший (для ретрая принятия)
  local recv = api("TradeAPI/TradeRequestReceived")
  if recv then
    recv.OnClientEvent:Connect(function(...)
      local from
      for _, a in ipairs({...}) do
        if typeof(a) == "Instance" and a:IsA("Player") then from = a
        elseif type(a) == "number" then from = Players:GetPlayerByUserId(a)
        elseif type(a) == "string" then from = Players:FindFirstChild(a) end
        if from then break end
      end
      if not from then log("<- запрос на трейд, но игрок не распознан"); return end
      log("<- запрос на трейд от " .. from.Name)
      if not acceptPred(from) then
        pcall(function() api("TradeAPI/AcceptOrDeclineTradeRequest"):InvokeServer(from, false) end)
        log("  x отклонил " .. from.Name); return
      end
      pendingFrom = from
      pcall(function() api("TradeAPI/AcceptOrDeclineTradeRequest"):InvokeServer(from, true) end)
      log("  v принял запрос от " .. from.Name)
    end)
  end
  -- авто-принятие(ретрай) + готов/финал + лог смены фаз
  task.spawn(function()
    local prev = "idle"
    while true do
      local open = tradeOpen()
      if open then pendingFrom = nil end
      -- ретрай принятия запроса, если есть ожидающий и мы НЕ в трейде (кулдаун прошёл)
      if not open and pendingFrom then
        pcall(function() api("TradeAPI/AcceptOrDeclineTradeRequest"):InvokeServer(pendingFrom, true) end)
      end
      local st = negotiating() and "torg" or (confirming() and "confirm" or "idle")
      if st ~= prev then
        if st == "torg" then log("  фаза: ТОРГ — жму 'готов'")
        elseif st == "confirm" then log("  фаза: ПОДТВЕРЖДЕНИЕ — жму 'финал'")
        elseif prev ~= "idle" then log("  трейд закрыт") end
        prev = st
      end
      if st == "torg" then api("TradeAPI/AcceptNegotiation"):FireServer()
      elseif st == "confirm" then api("TradeAPI/ConfirmTrade"):FireServer() end
      task.wait(1)
    end
  end)
  log("приёмник готов (авто-принимает по вайтлисту, авто-подтверждает)")
end

------------------------------------------------------------------
-- СДАЮЩИЙ (альт)
------------------------------------------------------------------
local function waitForMain(timeout)
  local t0 = tick()
  while tick() - t0 < timeout do
    local m = Players:FindFirstChild(MAIN_USERNAME)
    if m then return m end
    task.wait(0.5)
  end
end
local function waitMyTurn(timeout)
  if not activeExists() then return true end   -- ручной тест без оркестратора
  local deadline = tick() + timeout
  while tick() < deadline do
    if readActive() == lp.Name then return true end
    task.wait(1)
  end
  return false
end

local function unlockBatch(batch)
  local set = {}
  for _, u in ipairs(batch) do set[u] = false end
  local r = api("BackpackAPI/CommitBackpackItemSet")
  if r then pcall(function() r:FireServer("backpack_locks", set) end) end
  task.wait(0.3)
end

-- открыть трейд и НАБРАТЬ оффер до 18 РЕАЛЬНО вставших предметов. untrade не встаёт в оффер
-- (сервер не даёт) -> помечаем и НЕ теряем слот: добиваем следующим из очереди. Возврат: offered
-- (список вставших в этом трейде) или nil если трейд вообще не открылся.
local _addRemote
local function fillTrade(target, queue, sent, untradable)
  if tradeOpen() then waitFor(function() return not tradeOpen() end, 8) end
  task.wait(0.4)

  -- ЗАПРОС С РЕТРАЯМИ: шлём, пока приёмник не примет (торг откроется)
  local opened = false
  for _ = 1, 6 do
    if not tradeOpen() then api("TradeAPI/SendTradeRequest"):FireServer(target) end
    if waitFor(negotiating, 5) then opened = true break end
  end
  if not opened then return nil end
  task.wait(SETTLE)

  _addRemote = _addRemote or API:FindFirstChild("TradeAPI/AddItemToOffer")
  local offered, qi = {}, 1
  -- ВАЖНО: цикл крутим по РЕАЛЬНОМУ счётчику оффера (countOffer), а НЕ по #offered. При быстрой накладке
  -- GUI лагает -> предмет встаёт в оффер с задержкой; жёсткое окно 0.45с читало «не встал» -> трейдабл
  -- ложно метился untrade И не считался -> #offered недобирал 18, а в оффере уже 18 реальных -> цикл
  -- долбил добавление в ПОЛНЫЙ оффер -> «not tradeable» каждые ~3с и зависание. Теперь: цель — реальные
  -- 18 в оффере (не переполняем), а вставку ждём С ЗАПАСОМ (1.5с) -> поздняя вставка ловится, трейдабл
  -- не метится untrade; untrade только если за 1.5с реально не встал.
  while countOffer() < MAX_PER_TRADE and qi <= #queue do
    local u = queue[qi]; qi = qi + 1
    if not sent[u] and not untradable[u] then
      local before = countOffer()
      pcall(function() _addRemote:FireServer(u) end)
      if waitFor(function() return countOffer() > before end, 1.5) then
        offered[#offered + 1] = u                 -- встал в оффер (с учётом лага) => трейдабл
      else
        untradable[u] = true                      -- за 1.5с не вырос => реально untrade, больше не пробуем
      end
    end
  end

  -- ничего не встало (вся очередь — untrade) -> отменяем быстро
  if #offered == 0 then
    pcall(function() api("TradeAPI/DeclineTrade"):FireServer() end)
    waitFor(function() return not tradeOpen() end, 5)
    return offered
  end

  -- готов -> финал. БЕЗ спама: бесконечный луп AcceptNegotiation/ConfirmTrade каждые 0.4с будит
  -- анти-спам Adopt Me и роняет СДАЮЩЕГО на 0.5–2 мин (18 AddItemToOffer + спам accept за 2-3 трейда
  -- пробивают порог; у главного такого нет — он не кладёт предметы и жмёт реже). Жмём по разу с
  -- проверкой фазы, считанные повторы — поток ремоутов в разы ниже, порог не будится.
  -- 1) «готов»: обычно хватает одного нажатия; пере-жмём только если за 8с не перешли в подтверждение.
  for _ = 1, 3 do
    if confirming() or not tradeOpen() then break end
    pcall(function() local r = api("TradeAPI/AcceptNegotiation"); if r then r:FireServer() end end)
    if waitFor(function() return confirming() or not tradeOpen() end, 8) then break end
  end
  -- 2) «финал»: жмём ConfirmTrade раз в ~2с (countdown «IS THIS TRADE FAIR» ~5с -> пара нажатий), пока не закроется.
  for _ = 1, 6 do
    if not tradeOpen() then break end
    if confirming() then pcall(function() local r = api("TradeAPI/ConfirmTrade"); if r then r:FireServer() end end) end
    if waitFor(function() return not tradeOpen() end, 2) then break end
  end
  return offered
end

-- Сдать всё на target (фильтр по категории nil=все 5). Инвентарь читаем ОДИН раз (GUI дорогой),
-- трейдим пачками 18/18 через fillTrade; untrade ловится по countOffer и пропускается без потери слота.
-- offered после состоявшегося трейда считаем переданными (приёмник авто-подтверждает). Если трейд
-- сорвётся — предметы останутся в инвентаре, подберутся следующим прогоном.
local function dumpAllTo(target, onlyCat)
  local sent, untradable = {}, {}
  local trades, stalls, transferred = 0, 0, 0
  local items, loaded = getTradeable(onlyCat)
  if not loaded then logd("рюкзак не открылся — выход"); return transferred end
  unlockBatch(items)                                -- разлочить ВСЁ один раз перед сдачей (не на каждый трейд)
  local reread = false

  while true do
    if target == nil or target.Parent == nil then return transferred end
    local queue = {}
    for _, u in ipairs(items) do if not sent[u] and not untradable[u] then queue[#queue + 1] = u end end

    if #queue == 0 then
      if reread then return transferred end        -- уже перечитывали -> реально всё
      reread = true
      items = (getTradeable(onlyCat))              -- разовая догрузка (вдруг подъехали поздние)
      unlockBatch(items)                           -- разлочить догруженные
    else
      trades = trades + 1
      logd("=== трейд #" .. trades .. " | в очереди " .. #queue .. (onlyCat and (" [" .. onlyCat .. "]") or "") .. " ===")
      local offered = fillTrade(target, queue, sent, untradable)

      if offered == nil then
        stalls = stalls + 1
        log(("трейд #%d не открылся, повтор"):format(trades))
        if stalls >= 6 then logd("трейд не идёт 6 раз подряд — стоп"); return transferred, true end
        task.wait(2)
      else
        stalls = 0
        for _, u in ipairs(offered) do
          if not sent[u] then sent[u] = true; transferred = transferred + 1; SOLD[#SOLD + 1] = INFO[u] end
        end
        log(("ИТОГ #%d: ушло %d (всего %d)"):format(trades, #offered, transferred))
        task.wait(BETWEEN_TRADES)
      end
    end
  end
end

-- .items: агрегируем проданное -> "realName\tкол-во\tкатегория\tвариант" (для оценки $ с вариантами)
local function writeItems(soldList)
  local agg = {}
  for _, info in ipairs(soldList) do
    if info then
      local key = (info.name or "?") .. "\t" .. bucketLabel(info.cat) .. "\t" .. variantOf(info)
      agg[key] = (agg[key] or 0) + 1
    end
  end
  local lines = {}
  for key, cnt in pairs(agg) do
    local name, bucket, variant = key:match("^(.-)\t(.-)\t(.*)$")
    lines[#lines + 1] = name .. "\t" .. cnt .. "\t" .. bucket .. "\t" .. variant
  end
  if #lines > 0 then flag(".items", table.concat(lines, "\n")) end
end

local function runDumper()
  log("роль: СДАЮЩИЙ")
  local main = waitForMain(60)
  if not main then logd("главный не в сервере — выход"); flag(".fail"); return end
  if not waitMyTurn(900) then logd("не дождался очереди — выход"); flag(".fail"); return end

  local transferred, failed = dumpAllTo(main, nil)
  writeItems(SOLD)
  if transferred <= 0 then logd("нет трейдбл-предметов (всё антрейд/стикеры/пусто) — СКИП акка")
  else logd("✅ ВСЁ СДАНО: передано " .. transferred .. " предметов") end
  flag(".count", tostring(transferred))
  if failed then flag(".fail") else flag(".txt", "done") end
end

------------------------------------------------------------------
-- РАСПРЕДЕЛИТЕЛЬ (главный) — раздаёт КАТЕГОРИЮ конкретному получателю
------------------------------------------------------------------
-- раздача = тот же dumpAllTo, но фильтр по категории и target = получатель
-- (учёт sent тоже по факту исчезновения из инвентаря)
local function dumpCategoryTo(target, cat) return dumpAllTo(target, cat) end

local function runDistributor()
  logd("роль: РАСПРЕДЕЛИТЕЛЬ — раздаю лут по категориям")
  -- ждём прогрузки СВОЕГО инвентаря (у главный он большой -> грузится дольше)
  local t0 = tick()
  while #getTradeable() == 0 and tick() - t0 < 90 do task.wait(2) end
  logd("инвентарь загружен: " .. #getTradeable() .. " трейдбл предметов")
  while true do
    local recv = readActive()
    local cat  = readCategory()
    if recv and cat and recv ~= "" and recv ~= MAIN_USERNAME then
      local target = waitForPlayer(recv, 30)
      if not target then
        log("получатель " .. recv .. " не в сервере — жду")
        task.wait(2)
      else
        logd(("раздаю '%s' -> %s"):format(cat, recv))
        local transferred = dumpCategoryTo(target, cat)
        flagFor(recv, ".count", tostring(transferred))
        flagFor(recv, ".txt", "done")
        logd(("✅ %s: роздано %d (%s)"):format(recv, transferred, cat))
        -- ждём, пока оркестратор переключит активного (иначе зациклимся на том же)
        local t0 = tick()
        while readActive() == recv and tick() - t0 < 60 do task.wait(1) end
      end
    else
      task.wait(1)
    end
  end
end

-- получатель в режиме раздачи: принимаю от главный, только если я активный получатель
local function runDistributeReceiver()
  log("роль: ПОЛУЧАТЕЛЬ (приём от главный)")
  if not waitForMain(60) then logd("главный не в сервере — выход"); flag(".fail"); return end
  runReceiver(function(from)
    return from.Name == MAIN_USERNAME and readActive() == lp.Name
  end)
  -- держим клиент живым; главный напишет наш .txt/.count, оркестратор закроет
  while true do task.wait(5) end
end

------------------------------------------------------------------
local MODE = readMode()
log("режим: " .. MODE)
if MODE == "distribute" then
  if lp.Name == MAIN_USERNAME then
    local ok, err = pcall(runDistributor)
    if not ok then logd("runDistributor ОШИБКА: " .. tostring(err)) end
  else
    local ok, err = pcall(runDistributeReceiver)
    if not ok then logd("runDistributeReceiver ОШИБКА: " .. tostring(err)); flag(".fail") end
  end
else
  if lp.Name == MAIN_USERNAME then
    local ok, err = pcall(runReceiver)
    if not ok then logd("runReceiver ОШИБКА: " .. tostring(err)) end
  else
    local ok, err = pcall(runDumper)
    if not ok then logd("runDumper ОШИБКА: " .. tostring(err)); flag(".fail") end
  end
end
