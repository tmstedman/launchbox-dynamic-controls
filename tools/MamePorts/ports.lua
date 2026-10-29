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

local rows = {}
for tag, port in pairs(ioport.ports) do
	for _, field in pairs(port.fields) do
		local token = ioport:input_type_to_token(field.type, field.player)
		if token ~= "" and not skip[token] then rows[#rows + 1] = {
			tag = tag,
			mask = field.mask,
			type = token,
			name = field.name,
			default = field.default_name,
			analog = field.is_analog,
			enabled = field.enabled,
		} end
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
	f:write(string.format('  <port type="%s" tag="%s" mask="%d"', esc(r.type), esc(r.tag:gsub("^:", "")), r.mask))
	if r.name ~= r.default then f:write(string.format(' name="%s"', esc(r.name))) end
	if r.analog then f:write(' analog="yes"') end
	-- A PORT_CONDITION field (one cabinet's control on a shared board) under the default DIP settings.
	if not r.enabled then f:write(' enabled="no"') end
	f:write(" />\n")
end
f:write("</machine>\n")
f:close()
machine:exit()
