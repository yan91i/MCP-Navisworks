The Navisworks MCP plugin has 73 tools, and has a feature that lists every tool with its description.
The MCP is initially called with **Cherry**

# Deployment
1. **Build**: Double-click `build.bat`. It uses the `csc.exe` that ships with Windows, and the code is written in C# 5 syntax, so Visual Studio is not required. The Navisworks path is set to `%ProgramFiles%\Autodesk\Navisworks Manage 2024` by default.
2. **Install**: Close Navisworks first, then run `install.bat`. The plugin is copied to `%APPDATA%\Autodesk Navisworks Manage 2024\Plugins\NavisMcp\` as a per-user install.
3. **Start**: The plugin starts automatically when you open Navisworks. You can also turn it on or off manually from **Tool Add-ins → Navis MCP**.
4. **Verify**: Open `http://127.0.0.1:3900/health` in a browser. If you see `ok`, the server is running.
5. **Connect in Cherry**: Go to **Settings → MCP Servers → Add**.
   - Type: **Streamable HTTP**
   - URL: `http://127.0.0.1:3900/mcp`
   - Alternatively, import the `cherry-mcp.json` included in the package.

   > **Note:** When chatting, tick **Navisworks** under the MCP icon below the input box. The selected model must support tool calling.

If build.bat reports errors, please compile or run manually with correct dependency path

# Listing all tools
here are four ways to see every tool and what it does:

1. **In chat:**   call list_tools. You can filter by category or keyword, or ask for Markdown output.
2. **In Navisworks:** go to Tool Add-ins → Navis MCP Tools. A table window opens, and you can copy it as Markdown with one click.
3. **In a browser:** open http://127.0.0.1:3900/tools. The same list is also available at /tools.json and /tools.md.
4. **In Offline:**  read TOOLS.md in the folder.

# Tools
| Category | Main functions |
|---|---|
| Document | Open, append and save NWD/NWF; new document; model info |
| Model Tree | Browse the selection tree, view item details and bounding boxes, measure the distance between two items |
| Search / Selection | Find items by property, name or class (supports gt/lt/regex); select all, invert, select same value, select parent or children |
| Properties | Read properties, list which properties exist and count their values, export to CSV; **write or delete custom property tabs** (through the COM API) |
| Data Check | Check parameter completeness (supports regex and allowed-value lists, colors failures red, exports CSV); find duplicate values |
| Selection Sets | List and create selection or search sets; apply them (select, hide, isolate, zoom); delete them |
| Visibility / Appearance | Hide, isolate, color, set transparency, **color by property value and return a legend**, reset appearance |
| Transform | Move items, reset transforms |
| View / Viewpoints | Read and set the camera, standard views, render style, projection, lighting; **take a screenshot and return the image directly to the AI**; save or delete viewpoints; batch-export viewpoint images |
| Sectioning | Section box around the selected items, section at an elevation, clear sectioning, read/write the raw JSON |
| Clash | Create or delete clash tests, run tests, change status and assignee, export a clash report CSV |
| TimeLiner | List tasks, create a task linked to items, select the items linked to a task |
| Plugins | List and run other loaded plugins |
| Advanced API | `api_inspect` and `api_invoke`: use reflection to reach any Navisworks API feature that doesn't have its own tool |

Every tool that targets items accepts them four ways: handles, guids, selection_set (the set's name) or use_selection (the current selection).

# Compatibility

Screenshots, sectioning, TimeLiner, clash editing, and file open/save work differently across Navisworks versions. Those calls are made at runtime, so the build doesn't depend on them. If your version doesn't support one, only that tool returns a clear error; the rest of the plugin still compiles and works.
