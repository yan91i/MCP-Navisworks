The Navisworks MCP plugin has 73 tools, and has a feature that lists every tool with its description.

Deployment: run build.bat, then install.bat. 
在 Cherry 里连接：设置 → MCP 服务器 → 添加。
类型：Streamable HTTP
URL：http://127.0.0.1:3900/mcp
也可以直接导入包里的 cherry-mcp.json。

If build.bat reports errors, please compile or run manually with correct dependency path

#Listing all tools
here are four ways to see every tool and what it does:

##In chat: 
call list_tools. You can filter by category or keyword, or ask for Markdown output.
##In Navisworks: 
go to Tool Add-ins → Navis MCP Tools. A table window opens, and you can copy it as Markdown with one click.
##In a browser: 
open http://127.0.0.1:3900/tools. The same list is also available at /tools.json and /tools.md.
##Offline: 
read TOOLS.md in the folder.

#Tools


