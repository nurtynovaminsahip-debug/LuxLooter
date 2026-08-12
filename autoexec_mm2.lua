--[[
  autoexec_mm2.lua — кладётся в папку autoexec экзекьютора (Potassium).
  Один скрипт на все клиенты; роль по нику локального игрока:
    - главный (ник из _main.txt) -> приёмка; любой другой -> сдача лута главному.

  Ремоуты трейда MM2 (из ReplicatedStorage.Modules.TradeModule):
    Trade.SendRequest:InvokeServer(playerObj)          -- отправить запрос
    Trade.AcceptRequest:FireServer()                   -- принять входящий запрос
    Trade.OfferItem:FireServer(itemName, category)     -- выложить предмет
    Trade.AcceptTrade:FireServer(PlaceId*3, LastOffer) -- подтвердить трейд

  Почему глушим GUI-обработчики игры:
    Мы не открываем окно трейда, поэтому клиентский TradeModule видит «битое»
    состояние и сам шлёт DeclineTrade ("Trade failed - nil") -> трейд отменяется.
    getconnections отключает его StartTrade/UpdateTrade-обработчики, и трейд
    живёт чисто на сервере + наших ремоутах.
]]

------------------------------------------------------------------
-- КОНФИГ
------------------------------------------------------------------
-- рабочая папка координации. Для многопотока оркестратор кладёт route_<ник>.txt в базовую
-- mm2_done -> клиент переключается в свою папку потока (mm2_done_s1 и т.п.). Нет файла = базовая (1 поток).
local BASE_DIR        = "mm2_done"
local _lpname = ""
for _ = 1, 50 do
  local ok, nm = pcall(function() local p = game:GetService("Players").LocalPlayer; return p and p.Name end)
  if ok and nm and nm ~= "" then _lpname = nm; break end
  task.wait(0.1)
end
local function _readRoute()
  local f = BASE_DIR .. "/route_" .. _lpname .. ".txt"
  if isfile and isfile(f) then
    local ok, v = pcall(readfile, f)
    if ok and type(v) == "string" then v = (v:gsub("%s+$", "")); if v ~= "" then return v end end
  end
  return BASE_DIR
end
local DONE_DIR        = _readRoute()  -- writefile пишет в workspace экзекьютора
local WEAPON_CATEGORY = "Weapons"    -- все НЕ-петы; петов скип
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

-- Фильтр трейда по редкости (из GUI -> оркестратор пишет _weapon_rarities.txt/_pet_rarities.txt).
-- Содержимое: "ALL" = всё, "NONE"/"" = ничего, иначе список через запятую (Godly,Ancient,Chroma,…).
local function _rarset(name, emptyMode)
  local raw = (_readset(name, "") or ""):gsub("%s+", "")
  if raw == "" then return {}, emptyMode end
  local up = raw:upper()
  if up == "NONE" then return {}, "none" end
  if up == "ALL" then return {}, "all" end
  local set = {}
  for tok in raw:gmatch("[^,]+") do set[tok] = true end
  return set, "set"
end
local WEAPON_SET, WEAPON_MODE = _rarset("_weapon_rarities.txt", "all")   -- по умолчанию всё оружие
local PET_SET, PET_MODE       = _rarset("_pet_rarities.txt", "none")     -- по умолчанию без питомцев

local SILENCE_GAME_TRADE_UI = true   -- глушить ли DeclineTrade-обработчики игры
local MAX_PER_TRADE = 4              -- лимит РАЗНЫХ предметов (слотов) в трейде; стопка = 1 слот
local OFFER_DELAY  = 0.4             -- пауза между выкладкой предметов, с
local COOLDOWN_WAIT = 3              -- пауза после выкладки; accept-цикл сам добьёт серверный кулдаун
local ACCEPT_TRIES = 12             -- сколько раз спамить подтверждение

------------------------------------------------------------------
local Players = game:GetService("Players")
local ReplicatedStorage = game:GetService("ReplicatedStorage")
local HttpService = game:GetService("HttpService")
local lp = Players.LocalPlayer

local DISCORD_VERBOSE = false  -- true = слать ВСЕ логи в Discord (диагностика); false = только важное

-- Отправка в Discord (у юзера нет консоли экзекьютора).
local httpreq = request or http_request or (syn and syn.request) or (http and http.request)
local function dsend(msg)
  if not httpreq or DISCORD_WEBHOOK == "" then return end
  task.spawn(function()
    pcall(function()
      httpreq({
        Url = DISCORD_WEBHOOK, Method = "POST",
        Headers = { ["Content-Type"] = "application/json" },
        Body = HttpService:JSONEncode({ content = ("`[%s]` %s"):format(lp.Name, tostring(msg)) }),
      })
    end)
  end)
end
local function log(msg)
  warn(("[MM2][%s] %s"):format(lp.Name, tostring(msg)))
  if DISCORD_VERBOSE then dsend(msg) end
end
local function logd(msg) warn(("[MM2][%s] %s"):format(lp.Name, tostring(msg))); dsend(msg) end

log("СТАРТ скрипта (place=" .. tostring(game.PlaceId) .. ")")

------------------------------------------------------------------
-- РЕДКОСТЬ предметов (из ReplicatedStorage.Database.Sync — источник игры)
-- Chroma -> отдельная категория "Chroma"; Evo (нельзя трейдить) -> исключаем; Classic -> "Vintage".
------------------------------------------------------------------
local W_RARITY, P_RARITY, EVO = {}, {}, {}
local RARITY_DB_OK = false   -- удалось ли загрузить базу редкостей (нет -> берём ВСЁ)
-- require модуля ИГРЫ безопасно. На экзекьюторах с identity > 2 (как Xeno) require game-модуля
-- ПАДАЕТ И «отравляет» кэш -> ломаются скрипты игры (Animate/управление/звук), персонаж летает,
-- вылезают тач-кнопки. Поэтому на таких НЕ трогаем require вообще (работаем без фильтра).
-- На Potassium (identity <= 2) require проходит штатно -> фильтр редкости работает.
local function gameRequire(mod)
  local getid = getthreadidentity or getidentity or (syn and syn.get_thread_identity)
  local id = getid and getid()
  if id ~= nil and id > 2 then return false, "skip: identity " .. tostring(id) end
  return pcall(require, mod)
end
do
  local ok, Sync = gameRequire(ReplicatedStorage.Database.Sync)
  if ok and type(Sync) == "table" then
    if type(Sync.Weapons) == "table" then
      for nm, d in pairs(Sync.Weapons) do
        if type(d) == "table" then
          if d.Evo ~= nil or d.EvoBaseID ~= nil or d.EvoIndex ~= nil then
            EVO[nm] = true                       -- эволюции не трейдятся
          elseif d.Chroma == true then
            W_RARITY[nm] = "Chroma"
          else
            local r = d.Rarity
            if r == "Classic" then r = "Vintage" end
            W_RARITY[nm] = r
          end
        end
      end
    end
    if type(Sync.Pets) == "table" then
      for nm, d in pairs(Sync.Pets) do
        if type(d) == "table" then
          if d.Chroma == true then P_RARITY[nm] = "Chroma" else P_RARITY[nm] = d.Rarity end
        end
      end
    end
    RARITY_DB_OK = true
    log(("редкости загружены (оружие=%s, питомцы=%s)"):format(tostring(WEAPON_MODE), tostring(PET_MODE)))
  end
  -- база недоступна (Xeno и т.п.) -> молча берём всё (RARITY_DB_OK=false), без лога в консоль
end

local function weaponAllowed(name)
  if EVO[name] then return false end                 -- эволюции никогда
  if WEAPON_MODE == "none" then return false end
  if not RARITY_DB_OK then return true end           -- база недоступна -> берём всё оружие
  if WEAPON_MODE == "all" then return true end
  local cat = W_RARITY[name]
  return cat ~= nil and WEAPON_SET[cat] == true
end
local function petAllowed(name)
  if PET_MODE == "none" then return false end
  if not RARITY_DB_OK then return true end           -- база недоступна -> берём всех питомцев
  local cat = P_RARITY[name]
  if PET_MODE == "all" then return cat ~= nil end
  return cat ~= nil and PET_SET[cat] == true
end

-- список питомцев к сдаче (из данных инвентаря; в трейде категория "Pets")
local function getPetItems()
  local items = {}
  if PET_MODE == "none" then return items end
  local rem = ReplicatedStorage:FindFirstChild("Remotes")
  local extras = rem and rem:FindFirstChild("Extras")
  local gfi = extras and extras:FindFirstChild("GetFullInventory")
  if not gfi then return items end
  local ok, res = pcall(function() return gfi:InvokeServer(lp) end)
  if not ok or type(res) ~= "table" or type(res.Pets) ~= "table" or type(res.Pets.Owned) ~= "table" then
    return items
  end
  for nm, cnt in pairs(res.Pets.Owned) do
    cnt = tonumber(cnt) or 0
    if cnt > 0 and petAllowed(nm) then
      items[#items + 1] = { name = nm, category = "Pets", amount = cnt }
    end
  end
  return items
end

-- ПОЛНЫЙ инвентарь главного (всё, без фильтра редкости) -> <ник>.fullitems для оценки $ в отчёте
local function dumpFullInventory()
  local rem = ReplicatedStorage:FindFirstChild("Remotes")
  local extras = rem and rem:FindFirstChild("Extras")
  local gfi = extras and extras:FindFirstChild("GetFullInventory")
  if not gfi then return end
  local ok, res = pcall(function() return gfi:InvokeServer(lp) end)
  if not ok or type(res) ~= "table" then return end
  local lines = {}
  if type(res.Weapons) == "table" and type(res.Weapons.Owned) == "table" then
    for nm, cnt in pairs(res.Weapons.Owned) do
      cnt = tonumber(cnt) or 0
      if cnt > 0 then lines[#lines + 1] = tostring(nm) .. "\t" .. cnt .. "\t" .. (W_RARITY[nm] or "?") end
    end
  end
  if type(res.Pets) == "table" and type(res.Pets.Owned) == "table" then
    for nm, cnt in pairs(res.Pets.Owned) do
      cnt = tonumber(cnt) or 0
      if cnt > 0 then lines[#lines + 1] = tostring(nm) .. "\t" .. cnt .. "\tПитомцы" end
    end
  end
  pcall(function()
    if makefolder then pcall(makefolder, DONE_DIR) end
    writefile(DONE_DIR .. "/" .. lp.Name .. ".fullitems", table.concat(lines, "\n"))
  end)
  log("полный инвентарь записан (.fullitems): " .. #lines .. " видов")
end

-- флаг "атачнулся" (Lua реально запустился) — пишем СРАЗУ, до загрузки игры.
-- Оркестратор: нет .run за ~12с = Потассиум не атачнулся = акк не в игре (битый) -> скип.
pcall(function()
  if makefolder then pcall(makefolder, DONE_DIR) end
  writefile(DONE_DIR .. "/" .. lp.Name .. ".run", "1")
end)

-- ГЛАВНЫЙ публикует свой JobId -> оркестратор загоняет альтов ЖЁСТКО в его сервер (надёжнее follow)
if lp.Name == MAIN_USERNAME then
  task.spawn(function()
    if not game:IsLoaded() then game.Loaded:Wait() end
    while true do
      local jid = game.JobId
      if jid and jid ~= "" then
        pcall(function()
          if makefolder then pcall(makefolder, DONE_DIR) end
          writefile(DONE_DIR .. "/_mainjob.txt", jid)
        end)
      end
      task.wait(8)
    end
  end)

  -- по сигналу оркестратора (_dumpfull.txt) — записать полную стоимость инвентаря главного
  task.spawn(function()
    while true do
      if isfile and isfile(DONE_DIR .. "/_dumpfull.txt") then pcall(dumpFullInventory) end
      task.wait(2)
    end
  end)
end

-- ДЕТЕКТ "Join Error" (битые акки). Отдельный поток ДО подключения, т.к. дальше
-- скрипт ждёт Trade (на битых акках он не реплицируется -> завис без логов).
-- Пишем код в Discord и в файл <ник>.err — оркестратор решит: 543 (родит. блок) -> скип,
-- прочие (524 и т.п.) -> в публичку.
-- Работает ВЕСЬ прогон (не только 45с): ловит и кик В ИГРЕ (273 «зашли с другого устройства» и т.п.),
-- иначе альт висит до active_timeout. До спавна = не зашёл (.err); после спавна = дисконнект (.fail).
task.spawn(function()
  local roots = { game:GetService("CoreGui") }
  pcall(function() roots[#roots + 1] = lp:WaitForChild("PlayerGui", 10) end)
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
              or txt:find("parent to unlock") or txt:find("not authorized") then
            local ext = spawned and ".fail" or ".err"   -- в игре -> дисконнект (перечек); до игры -> не зашёл
            logd((spawned and "ДИСКОННЕКТ в игре" or "JOIN ERROR") .. ": " .. txt .. " (код " .. tostring(code) .. ")")
            pcall(function()
              if makefolder then pcall(makefolder, DONE_DIR) end
              writefile(DONE_DIR .. "/" .. lp.Name .. ext, spawned and "1" or (code or "join_error"))
            end)
            return
          end
        end
      end
    end
    task.wait(tick() - t0 < 45 and 0.5 or 1.5)
  end
end)

-- АНТИ-АФК: Roblox кикает за ~20 мин бездействия, а главный висит в сервере долго.
pcall(function()
  local VirtualUser = game:GetService("VirtualUser")
  lp.Idled:Connect(function()
    VirtualUser:CaptureController()
    VirtualUser:ClickButton2(Vector2.new())
  end)
  log("анти-афк активен")
end)

-- Ждём подключения к серверу. На битых акках Trade не появится -> выходим
-- (детект ошибки выше уже отработал в отдельном потоке).
local Trade = ReplicatedStorage:WaitForChild("Trade", 60)
if not Trade then log("Trade не реплицировался за 60с (битый акк) — выход"); return end

local ACCEPT_ARG1 = game.PlaceId * 3   -- верификационная константа AcceptTrade

-- обёртка вызова ремоута с логом ошибки
local function fire(desc, fn)
  local ok, err = pcall(fn)
  if not ok then log(desc .. " -> ОШИБКА: " .. tostring(err)) end
  return ok
end

-- Сигнал «я реально зашёл в MM2» (Trade есть = подключились). Битые сюда не доходят.
if game.PlaceId == 142823291 then
  pcall(function()
    if makefolder then pcall(makefolder, DONE_DIR) end
    writefile(DONE_DIR .. "/" .. lp.Name .. ".join", "1")
  end)
  log("зашёл в игру (join-флаг записан)")
end

-- Экран "Joining a friend?" (PlayerGui.Join.Friends.Play) появляется через ~3-4с
-- после захода и блокирует игру. Кликаем по нему НАСТОЯЩИМ курсором (VIM) каждую
-- секунду первые ~10с, чтобы поймать момент появления.
task.spawn(function()
  local pg = lp:WaitForChild("PlayerGui", 20)
  if not pg then return end
  local VIM = game:GetService("VirtualInputManager")
  local GuiService = game:GetService("GuiService")
  for _ = 1, 7 do
    local join = pg:FindFirstChild("Join")
    local friends = join and join:FindFirstChild("Friends")
    local play = friends and friends:FindFirstChild("Play")
    if play and play.Visible and join.Enabled ~= false then
      pcall(function()
        local inset = GuiService:GetGuiInset()
        local p = play.AbsolutePosition + play.AbsoluteSize / 2 + inset
        VIM:SendMouseMoveEvent(p.X, p.Y, game)
        task.wait(0.05)
        VIM:SendMouseButtonEvent(p.X, p.Y, 0, true, game, 0)
        task.wait(0.05)
        VIM:SendMouseButtonEvent(p.X, p.Y, 0, false, game, 0)
      end)
      log("клик по Play (экран Join)")
    end
    task.wait(1)
  end
end)

------------------------------------------------------------------
-- 1) Глушим штатные обработчики трейда игры (до подключения своих!)
------------------------------------------------------------------
local function silenceGameTradeUI()
  if not SILENCE_GAME_TRADE_UI then return end
  if type(getconnections) ~= "function" then
    log("ВНИМАНИЕ: getconnections недоступен — игра может отменять трейд")
    return
  end
  for attempt = 1, 6 do
    local n = 0
    for _, sig in ipairs({ Trade.StartTrade.OnClientEvent, Trade.UpdateTrade.OnClientEvent }) do
      for _, c in ipairs(getconnections(sig)) do
        pcall(function() if c.Disable then c:Disable() else c:Disconnect() end end)
        n = n + 1
      end
    end
    if n > 0 then log(("заглушено GUI-обработчиков игры: %d (попытка %d)"):format(n, attempt)); return end
    task.wait(0.5)
  end
  log("обработчики игры не найдены (0) — возможно ещё не подгрузились")
end

silenceGameTradeUI()

------------------------------------------------------------------
-- 2) Свои обработчики: ловим LastOffer + факт трейда
------------------------------------------------------------------
local inTrade = false
local lastOffer = nil

Trade.StartTrade.OnClientEvent:Connect(function(state, partner)
  inTrade = true
  lastOffer = state and state.LastOffer or nil
  log(("СТАРТ трейда (partner=%s, lastOffer=%s)"):format(tostring(partner), tostring(lastOffer)))
end)
Trade.UpdateTrade.OnClientEvent:Connect(function(state)
  if state and state.LastOffer ~= nil then lastOffer = state.LastOffer end
  log("UPDATE трейда, lastOffer=" .. tostring(lastOffer))
end)
Trade.DeclineTrade.OnClientEvent:Connect(function()
  log("!! DECLINE — трейд отменён")
  inTrade = false; lastOffer = nil
end)
Trade.AcceptTrade.OnClientEvent:Connect(function(done)
  log("ACCEPT-событие, done=" .. tostring(done))
  if done then inTrade = false; lastOffer = nil end
end)

local function tryAccept()
  if inTrade and lastOffer ~= nil then
    fire("AcceptTrade", function() Trade.AcceptTrade:FireServer(ACCEPT_ARG1, lastOffer) end)
  end
end

-- Оркестратор пишет в _active.txt ник альта, которому сейчас можно трейдить.
local ACTIVE_FILE = DONE_DIR .. "/_active.txt"
local function readActive()
  if not (isfile and isfile(ACTIVE_FILE)) then return nil end
  local ok, c = pcall(readfile, ACTIVE_FILE)
  if ok and type(c) == "string" then return (c:gsub("%s+$", "")) end
  return nil
end
local function isMyTurn() return readActive() == lp.Name end

-- ВАЙТЛИСТ главного (ТОЛЬКО событие RequestSent, без перехвата ремоутов игры):
-- трейд от активного альта принимаем, от остальных (рандомы) — отклоняем.
Trade.RequestSent.OnClientEvent:Connect(function(sender)
  if lp.Name ~= MAIN_USERNAME then return end
  -- В VIP рандомов нет -> принимаем ЛЮБОЙ входящий трейд МГНОВЕННО (без вайтлиста).
  if game.PrivateServerId ~= "" then
    fire("AcceptRequest", function() Trade.AcceptRequest:FireServer() end)
    return
  end
  -- Публичка: только активного альта, остальных отклоняем.
  local who = sender and sender.Name
  if not who then return end
  if who == readActive() then
    fire("AcceptRequest", function() Trade.AcceptRequest:FireServer() end)
  else
    fire("DeclineRequest", function() Trade.DeclineRequest:FireServer() end)
    log("отклонил трейд от постороннего: " .. who)
  end
end)

------------------------------------------------------------------
local function writeDoneFlag()
  pcall(function()
    if makefolder then pcall(makefolder, DONE_DIR) end
    writefile(DONE_DIR .. "/" .. lp.Name .. ".txt", "done")
  end)
  log("done-флаг записан")
end

-- сколько предметов передал этот акк (для итоговой статы оркестратора)
local function writeCount(n)
  pcall(function()
    if makefolder then pcall(makefolder, DONE_DIR) end
    writefile(DONE_DIR .. "/" .. lp.Name .. ".count", tostring(n or 0))
  end)
end

-- флаг СБОЯ (частичный дамп / краш) — НЕ путать с done
local function writeFail()
  pcall(function()
    if makefolder then pcall(makefolder, DONE_DIR) end
    writefile(DONE_DIR .. "/" .. lp.Name .. ".fail", "1")
  end)
  log("fail-флаг записан")
end

-- флаг РЕДЖОИНА: альт зашёл, но главного нет в его сервере (промах follow в публичке).
-- Оркестратор перезапустит = новый follow = новый шанс попасть к главному (не считается «битым»).
local function writeRejoin()
  pcall(function()
    if makefolder then pcall(makefolder, DONE_DIR) end
    writefile(DONE_DIR .. "/" .. lp.Name .. ".rejoin", "1")
  end)
  log("rejoin-флаг записан (главного нет в сервере)")
end

------------------------------------------------------------------
-- ПРИЁМНИК (главный)
------------------------------------------------------------------
local function runMain()
  local inVIP = game.PrivateServerId ~= ""
  log("режим ПРИЁМКИ (" .. (inVIP and "VIP: принимаю любого" or "публичка: вайтлист") .. ")")
  local idle = 0
  while true do
    if inTrade then
      tryAccept(); idle = 0                -- подтверждаем быстро (каждые 0.5с)
    elseif inVIP then
      -- VIP: рандомов нет -> жмём приём каждую итерацию (любой входящий принят ≤0.5с)
      fire("AcceptRequest", function() Trade.AcceptRequest:FireServer() end)
    else
      -- публичка: фолбэк, если RequestSent не сработал и активный альт уже в сервере
      idle = idle + 1
      local act = readActive()
      if idle >= 6 and act and Players:FindFirstChild(act) then
        fire("AcceptRequest", function() Trade.AcceptRequest:FireServer() end)
        idle = 0
      end
    end
    task.wait(0.5)
  end
end

------------------------------------------------------------------
-- ОТДАЮЩИЙ (альт)
------------------------------------------------------------------
local function waitForMain(timeout)
  local t0 = tick()
  while tick() - t0 < timeout do
    local m = Players:FindFirstChild(MAIN_USERNAME)
    if m then return m end
    task.wait(0.5)
  end
  return nil
end

local function waitMyTurn(timeout)
  local deadline = tick() + timeout
  while tick() < deadline do
    if isMyTurn() then return true end
    task.wait(1)
  end
  return false
end

local function inventoryContainer()
  local node = lp:WaitForChild("PlayerGui", 15)
  for _, name in ipairs({ "MainGUI", "Game", "Crafting", "Inventory", "Salvage", "ScrollFrame", "Container" }) do
    if not node then return nil end
    node = node:WaitForChild(name, 10)
  end
  return node
end

-- читаем количество в стопке. Из декомпила трейда: frame.Container.Amount.Text = "x5"
local function readAmount(frame)
  local cont = frame:FindFirstChild("Container")
  local lbl = cont and cont:FindFirstChild("Amount")
  if lbl and (lbl:IsA("TextLabel") or lbl:IsA("TextButton")) then
    local n = tonumber((tostring(lbl.Text):gsub("[^%d]", "")))
    if n and n > 0 then return n end
  end
  return 1
end

local loggedStructure = false

local function getInventoryItems()
  local items = {}
  local container = inventoryContainer()
  if not container then log("инвентарь не найден (Salvage.ScrollFrame.Container)"); return items end
  for _, frame in ipairs(container:GetChildren()) do
    if frame:IsA("GuiObject") and frame.Name ~= "Title" then
      -- разовый дамп структуры первого предмета — чтобы найти, где лежит счётчик стака
      if not loggedStructure then
        loggedStructure = true
        local parts = {}
        for _, c in ipairs(frame:GetDescendants()) do
          local extra = ""
          if c:IsA("TextLabel") or c:IsA("TextButton") then extra = "='" .. tostring(c.Text) .. "'" end
          parts[#parts + 1] = c.Name .. "(" .. c.ClassName .. ")" .. extra
        end
        log("СТРУКТУРА [" .. frame.Name .. "]: " .. table.concat(parts, ", "))
      end
      if weaponAllowed(frame.Name) then
        table.insert(items, { name = frame.Name, category = WEAPON_CATEGORY, amount = readAmount(frame) })
      end
    end
  end
  return items
end

-- встать в очередь: повторять запрос, пока главный не примет именно нас
local function queueForTrade(main)
  local deadline = tick() + 150
  while not inTrade and tick() < deadline do
    fire("SendRequest", function() Trade.SendRequest:InvokeServer(main) end)
    local w = tick() + 4
    while not inTrade and tick() < w do task.wait(0.3) end
  end
  return inTrade
end

-- выложить чанк (до MAX_PER_TRADE РАЗНЫХ предметов); каждый вид — amount раз
-- (копии складываются в один слот). true если трейд завершился.
local function tradeBatch(chunk)
  local total = 0
  for _, it in ipairs(chunk) do total = total + it.amount end
  log(("выкладываю видов: %d (%d шт. всего)"):format(#chunk, total))
  for _, item in ipairs(chunk) do
    for _ = 1, item.amount do
      fire("OfferItem(" .. item.name .. ")", function() Trade.OfferItem:FireServer(item.name, item.category) end)
      task.wait(OFFER_DELAY)
    end
  end
  task.wait(COOLDOWN_WAIT)
  for _ = 1, ACCEPT_TRIES do
    if not inTrade then return true end
    tryAccept()
    task.wait(1)
  end
  return not inTrade
end

local function runAlt()
  log("режим СДАЧИ (ACCEPT_ARG1=" .. tostring(ACCEPT_ARG1) .. ")")
  -- сперва дожидаемся СВОЕГО спавна (мы реально в игре), потом коротко ищем главного.
  -- Если мы в сервере главного — он уже в Players за пару секунд; нет за 12с = ДРУГОЙ сервер -> реджоин.
  local t0 = tick()
  while not (lp.Character and lp.Character:FindFirstChild("HumanoidRootPart")) and tick() - t0 < 45 do
    task.wait(0.5)
  end

  -- ЧЕК УРОВНЯ: в MM2 ниже 10 lvl аккаунт НЕ может трейдить (ограничение игры) -> такой акк не сдаст
  -- ни одного предмета, незачем висеть на нём. Скипаем сразу (пишем 0 + done, оркестратор едет дальше).
  -- Уровень — в атрибуте LocalPlayer.Level (ждём прогруз до 5с; если так и не пришёл — НЕ скипаем).
  local MIN_TRADE_LEVEL = 10
  local lvl = nil
  for _ = 1, 20 do lvl = lp:GetAttribute("Level"); if lvl ~= nil then break end; task.wait(0.25) end
  if type(lvl) == "number" and lvl < MIN_TRADE_LEVEL then
    logd(("уровень %d < %d — аккаунт не может трейдить в MM2, СКИП"):format(lvl, MIN_TRADE_LEVEL))
    writeCount(0); writeDoneFlag(); return
  end

  local main = waitForMain(12)
  if not main then logd("главного нет в этом сервере — прошу реджоин"); writeRejoin(); return end

  -- читаем инвентарь. Если пусто — перечитываем до 8с (вдруг фреймы чуть позже),
  -- потом считаем акк реально пустым и быстро закрываем. GUI после трейда не
  -- обновляется — читаем ОДИН раз (как наберётся) и сдаём чанками, не перечитывая.
  local items = getInventoryItems()
  local t0 = tick()
  while #items == 0 and WEAPON_MODE ~= "none" and tick() - t0 < 8 do
    task.wait(0.5)
    items = getInventoryItems()
  end
  -- питомцы (отдельная категория, из данных инвентаря) — добавляем к списку оружия
  local pets = getPetItems()
  for _, p in ipairs(pets) do items[#items + 1] = p end
  if #pets > 0 then log(("питомцев к сдаче: %d"):format(#pets)) end
  -- список предметов (имя\tкол-во) -> оркестратор посчитает стоимость в $ по StarPets
  pcall(function()
    local lines = {}
    for _, it in ipairs(items) do
      local bucket
      if it.category == "Pets" then bucket = "Питомцы" else bucket = W_RARITY[it.name] or "?" end
      lines[#lines + 1] = tostring(it.name) .. "\t" .. tostring(it.amount or 1) .. "\t" .. tostring(bucket)
    end
    if makefolder then pcall(makefolder, DONE_DIR) end
    writefile(DONE_DIR .. "/" .. lp.Name .. ".items", table.concat(lines, "\n"))
  end)
  logd(("разных предметов к сдаче: %d (~%d трейдов)"):format(#items, math.ceil(#items / MAX_PER_TRADE)))
  if #items == 0 then logd("инвентарь пуст — пропускаю акк (быстро)"); writeCount(0); writeDoneFlag(); return end

  -- есть лут -> ждём свою очередь (трейд строго по очереди) и сдаём
  log("жду своей очереди (active token)")
  if not waitMyTurn(900) then logd("не дождался очереди за 900с — выход"); writeFail(); return end
  log("моя очередь — начинаю дамп")

  local idx, fails, transferred = 1, 0, 0
  while idx <= #items do
    local chunk = {}
    for i = idx, math.min(idx + MAX_PER_TRADE - 1, #items) do chunk[#chunk + 1] = items[i] end

    -- каждый чанк = отдельный трейд (лимит = 4 РАЗНЫХ предмета), поэтому открываем новый
    log(("открываю трейд (сдано %d/%d видов) -> %s"):format(idx - 1, #items, main.Name))
    if not queueForTrade(main) then log("главный не принял запрос за 150с — выход"); break end

    if tradeBatch(chunk) then
      idx = idx + #chunk; fails = 0
      for _, it in ipairs(chunk) do transferred = transferred + (it.amount or 1) end  -- для .count
      log(("сдано %d/%d видов"):format(idx - 1, #items))
    else
      fails = fails + 1; log("трейд не подтвердился (неудача " .. fails .. ")")
      if fails >= 3 then log("3 неудачи подряд — выход"); break end
      -- idx не двигаем — повторим тот же чанк
    end
    task.wait(0.5)  -- пауза перед следующим трейдом
  end

  writeCount(transferred)   -- сколько предметов реально передано (для итоговой статы)
  if idx > #items then
    log("весь снапшот сдан (" .. transferred .. " шт.)")
    writeDoneFlag()
  else
    logd("сдал НЕ ВСЁ (прервано) — ставлю fail, оркестратор перезапустит")
    writeFail()
  end
end

------------------------------------------------------------------
if lp.Name == MAIN_USERNAME then
  local ok, err = pcall(runMain)
  if not ok then logd("runMain ОШИБКА: " .. tostring(err)) end
else
  local ok, err = pcall(runAlt)
  if not ok then logd("runAlt ОШИБКА: " .. tostring(err)); writeFail() end
end
