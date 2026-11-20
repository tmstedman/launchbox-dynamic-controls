-- Dump every input port a MAME machine defines, as MAME's cfg names them.
-- Run once per machine:
--   mame <rom> -autoboot_script ports.lua -video none -sound none -nothrottle -skip_gameinfo -cfg_directory /tmp
-- Writes $MAME_PORTS_OUT (default ports/<rom>.xml) and exits.

local machine = manager.machine
local ioport = machine.ioport
local name = machine.system.name
local out = os.getenv("MAME_PORTS_OUT") or ("ports/" .. name .. ".xml")

local function esc(s)
	return (tostring(s):gsub("&", "&amp;"):gsub("<", "&lt;"):gsub(">", "&gt;"):gsub('"', "&quot;"))
end

-- Settings, not controls: nothing a player presses.
local skip = { DIPSWITCH = true, CONFIG = true, ADJUSTER = true, UNUSED = true, UNKNOWN = true, SPECIAL = true }
-- A type MAME has no name for comes back as TYPE_OTHER(n,m), which no cfg can bind.
local function unnamed(token) return token:sub(1, 11) == "TYPE_OTHER(" end

local rows = {}
for tag, port in pairs(ioport.ports) do
	local seen = {}
	local function add(field)
		local token = ioport:input_type_to_token(field.type, field.player)
		local key = token .. ":" .. field.mask
		if token ~= "" and not skip[token] and not unnamed(token) and not seen[key] then
			seen[key] = true
			rows[#rows + 1] = {
				tag = tag,
				mask = field.mask,
				type = token,
				name = field.name,
				default = field.default_name,
				analog = field.is_analog,
				enabled = field.enabled,
				defvalue = field.defvalue,
			}
		end
	end
	-- Neither way of listing a port's fields finds them all. port.fields is keyed by field name, so
	-- fields sharing one (both players' "Spot") collapse to a single entry; port:field(mask) returns
	-- one field per bit, so fields sharing bits (a PORT_CONDITION alternative) hide each other.
	-- Take both, keeping one row per type and mask.
	for _, field in pairs(port.fields) do add(field) end
	for bit = 0, 31 do
		local field = port:field(1 << bit)
		if field then add(field) end
	end
end
-- Numbers inside a name compare as numbers, so P1_BUTTON2 sorts before P1_BUTTON10 and IN2 before IN10.
local function natural(s)
	return (s:gsub("%d+", function(d) return string.format("%010d", tonumber(d)) end))
end
table.sort(rows, function(a, b)
	if a.type ~= b.type then return natural(a.type) < natural(b.type) end
	if a.tag ~= b.tag then return natural(a.tag) < natural(b.tag) end
	return a.mask < b.mask
end)

local f = assert(io.open(out, "w"))
f:write(string.format('<machine name="%s" parent="%s">\n', esc(name), esc(machine.system.parent == "0" and "" or machine.system.parent)))
for _, r in ipairs(rows) do
	f:write(string.format('  <port type="%s" tag="%s" mask="%d" defvalue="%d"', esc(r.type), esc(r.tag:gsub("^:", "")), r.mask, r.defvalue))
	if r.name ~= r.default then f:write(string.format(' name="%s"', esc(r.name))) end
	if r.analog then f:write(' analog="yes"') end
	-- A PORT_CONDITION field (one cabinet's control on a shared board) under the default DIP settings.
	-- A cfg entry for a field that is off is dropped when MAME loads it. The flag can differ from run to run.
	if not r.enabled then f:write(' enabled="no"') end
	f:write(" />\n")
end
f:write("</machine>\n")
f:close()
machine:exit()
