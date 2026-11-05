# Self-test without Cherry Studio (PowerShell, no admin)
$u = "http://127.0.0.1:3900/mcp"
function Rpc($m, $p, $id) {
  $b = @{ jsonrpc = "2.0"; id = $id; method = $m; params = $p } | ConvertTo-Json -Depth 10
  Invoke-RestMethod -Uri $u -Method Post -ContentType "application/json" -Body ([Text.Encoding]::UTF8.GetBytes($b))
}
Rpc "initialize" @{ protocolVersion = "2025-03-26"; capabilities = @{}; clientInfo = @{ name = "ps"; version = "1" } } 1 | Out-Null
"Tools: " + (Rpc "tools/list" @{} 2).result.tools.Count
(Rpc "tools/call" @{ name = "list_tools"; arguments = @{ format = "markdown" } } 3).result.content[0].text
(Rpc "tools/call" @{ name = "get_model_info"; arguments = @{} } 4).result.content[0].text
