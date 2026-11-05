# Navisworks MCP 工具清单 (73 tools)

`*` = 必填参数。运行时也可调用 `list_tools`，或浏览 http://127.0.0.1:3900/tools


## Meta

| Tool | Description | Parameters |
|---|---|---|
| `list_tools` | List all available Navisworks MCP tools with category, description and parameters. Use this first to discover capabilities. | category, keyword, format |
| `server_info` | MCP server status: version, URL, tool count, Navisworks version and loaded document. | (none) |

## Document

| Tool | Description | Parameters |
|---|---|---|
| `get_model_info` | Active document info: file, title, units, appended models (with root handles), overall bounding box, selection count. | (none) |
| `open_file` | Open a model file (nwd/nwf/nwc/rvt/ifc/dwg/...) in Navisworks, replacing the current document. | path* |
| `append_files` | Append one or more model files into the current document. | paths* |
| `save_file` | Save the current document as NWD/NWF (Save As if path given). | path* |
| `new_document` | Close / clear the current document (File > New). Unsaved changes are lost. | (none) |

## Model Tree

| Tool | Description | Parameters |
|---|---|---|
| `get_tree` | Browse the selection tree. Without handle returns model roots; with handle returns its children down to 'depth'. | handle, depth, max_nodes |
| `get_item_details` | Item details: name, class, GUID, path, parent, child count, has geometry, hidden state, bounding box (min/max/center/size). | handles, guids, use_selection, selection_set, limit |
| `get_bounding_box` | Combined bounding box of target items (min, max, center, size) in document units. | handles, guids, use_selection, selection_set |
| `measure_distance` | Measure between two items: center-to-center distance, XYZ deltas and bounding-box clearance (0 if boxes overlap). | handle_a*, handle_b* |

## Search

| Tool | Description | Parameters |
|---|---|---|
| `search_items` | Search model items by property (Category/Property + value/match), property category, item name and/or class. Optionally select results and/or export CSV. | category, property, value, match, name_contains, class_contains, geometry_only, scope, limit, select, return_properties, export_csv_path |

## Selection

| Tool | Description | Parameters |
|---|---|---|
| `get_selection` | List currently selected items (returns handles). | limit, return_properties |
| `select_items` | Set the selection: replace / add / remove / clear. | handles, guids, use_selection, selection_set, mode |
| `select_all` | Select all model roots (everything). | (none) |
| `invert_selection` | Invert the current selection (among geometry items). | (none) |
| `select_same_value` | Select all items whose property value equals the value of a reference item (reference = handle or first selected item). | property*, handle |
| `select_relatives` | Change selection to relatives of target items: parent / children / descendants / geometry_descendants / model_root. | handles, guids, use_selection, selection_set, relation* |

## Properties

| Tool | Description | Parameters |
|---|---|---|
| `get_properties` | Get all property categories and values of target items. | handles, guids, use_selection, selection_set, category, limit |
| `list_property_names` | Discover which property categories/properties exist in the model (with item counts). Scans up to max_items items. | scope, category, max_items |
| `property_value_stats` | Distinct values of a property with item counts (e.g. count elements per Category, Level, CAT Code). | property*, scope, top |
| `export_properties_csv` | Export properties of target items (or of all items having a category) to CSV. Without 'properties' exports all properties found. | handles, guids, use_selection, selection_set, path*, properties, having_category, max_items |
| `set_user_properties` | Write custom (user-defined) properties to items in a custom tab (COM API). Existing values in the tab are kept unless overwritten. | handles, guids, use_selection, selection_set, tab*, values*, replace_tab |
| `remove_user_property_tab` | Remove a user-defined property tab from items. | handles, guids, use_selection, selection_set, tab* |

## Data Check

| Tool | Description | Parameters |
|---|---|---|
| `check_properties` | Data completeness check (like Revit Interoperability Tools checkset): for items having target_category, check each property exists and is not empty (optional regex / allowed values). Returns completion % per property, failing items; can color/select/export CSV. | properties*, target_category, property_category, class_contains, filter_property, filter_value, pattern, allowed_values, scope, limit, color_failures, select_failures, export_csv_path |
| `find_duplicates` | Find items sharing the same value of a property (e.g. duplicate Element/Id, Mark, GUID). | property*, scope, limit, select |

## Selection Sets

| Tool | Description | Parameters |
|---|---|---|
| `list_selection_sets` | List saved Selection Sets and Search Sets (folder path, type, item count). | count_items |
| `create_selection_set` | Save target items (handles/guids/current selection/another set) as a new Selection Set. | handles, guids, use_selection, selection_set, name*, replace |
| `create_search_set` | Create a dynamic Search Set from a property condition (updates automatically when the model changes). | name*, property*, value, match, replace |
| `apply_selection_set` | Use a saved set: select (default) / hide / isolate / zoom, optionally color it. | name*, action, color |
| `delete_selection_set` | Delete a saved Selection/Search Set. | name* |

## Visibility & Appearance

| Tool | Description | Parameters |
|---|---|---|
| `set_visibility` | Hide / unhide / isolate (hide everything else) target items, or unhide_all. | handles, guids, use_selection, selection_set, action* |
| `color_items` | Override color (and optional transparency) of target items. | handles, guids, use_selection, selection_set, color*, transparency |
| `set_transparency` | Override transparency of target items only. | handles, guids, use_selection, selection_set, transparency* |
| `color_by_property` | Color items automatically by distinct values of a property (like Appearance Profiler). Returns the color legend. | property*, scope, empty_color, max_values |
| `reset_appearance` | Reset color/transparency overrides of target items, or of the whole model if no target. Optionally unhide all. | handles, guids, use_selection, selection_set, unhide_all |
| `get_hidden_items` | Count / list hidden items. | limit |

## Transform

| Tool | Description | Parameters |
|---|---|---|
| `move_items` | Move (translate) target items by a vector in document units (permanent override, like Item Tools > Move). | handles, guids, use_selection, selection_set, offset* |
| `reset_transform` | Reset transform overrides of target items (or all items if no target). | handles, guids, use_selection, selection_set |

## View

| Tool | Description | Parameters |
|---|---|---|
| `zoom_to_selection` | Zoom the view to target items (or current selection). | handles, guids, use_selection, selection_set |
| `zoom_extents` | Zoom the view to the whole model (Zoom All). | (none) |
| `get_camera` | Current camera: position, rotation, look direction, up vector, projection, render style, lighting, field of view. | (none) |
| `set_camera` | Set camera position and/or look-at target (document units). | position, target |
| `look_from` | Standard view of the whole model or target items: top / bottom / front / back / left / right / iso (SE iso). | handles, guids, use_selection, selection_set, direction* |
| `set_render_style` | Set render style: FullRender / Shaded / Wireframe / HiddenLine. | style* |
| `set_projection` | Set camera projection: Perspective / Orthographic. | projection* |
| `set_lighting` | Set lighting style: None / Headlight / SceneLights / FullLights. | lighting* |
| `screenshot` | Capture the current view as PNG; saves to file and returns the image to the AI (max_size scales the returned copy). | path, width, height, max_size, return_image |

## Viewpoints

| Tool | Description | Parameters |
|---|---|---|
| `list_viewpoints` | List saved viewpoints (with folder path). | (none) |
| `goto_viewpoint` | Apply a saved viewpoint by name (exact, then contains). | name* |
| `save_viewpoint` | Save the current view as a new saved viewpoint. | name* |
| `delete_viewpoint` | Delete a saved viewpoint by name. | name* |
| `export_viewpoint_images` | Render every saved viewpoint (or those whose name contains 'filter') to PNG files in a folder. | folder*, filter, width, height |

## Sectioning

| Tool | Description | Parameters |
|---|---|---|
| `section_box_to_items` | Enable a section box around target items (with optional margin). | handles, guids, use_selection, selection_set, margin |
| `section_plane` | Enable a single horizontal (Z) section plane at a given elevation (e.g. level plan cut). | elevation*, keep |
| `clear_section` | Disable sectioning in the current view. | (none) |
| `get_section_json` | Get the raw clipping-plane JSON of the current view. | (none) |
| `set_section_json` | Set clipping planes from raw JSON (as returned by get_section_json). | json* |

## Clash

| Tool | Description | Parameters |
|---|---|---|
| `list_clash_tests` | List Clash Detective tests with type, tolerance, status and result counts by status. | (none) |
| `get_clash_results` | Get results of a clash test (item handles, distance, center, status, assigned). | test_name*, status, limit |
| `select_clash_result` | Select (and zoom to) the two items of a clash result; optionally color them red/green. | test_name*, result_name*, zoom, highlight |
| `run_clash_test` | Run (update) one clash test, or all tests if test_name omitted. | test_name |
| `create_clash_test` | Create a clash test between Selection A and B (from selection sets or handles). Optionally run it. | name*, type, tolerance, set_a, set_b, handles_a, handles_b, run |
| `delete_clash_test` | Delete a clash test. | test_name* |
| `set_clash_status` | Change status (and optionally assignee) of clash results: specific result names, or all results with from_status. | test_name*, status*, result_names, from_status, assigned_to |
| `export_clash_report_csv` | Export clash results of one or all tests to CSV (test, result, status, distance, center, item names/GUIDs, extra properties). | path*, test_name, item_properties |

## TimeLiner

| Tool | Description | Parameters |
|---|---|---|
| `list_timeliner_tasks` | List TimeLiner tasks (hierarchy path, planned/actual dates, task type, attached item count). | limit |
| `create_timeliner_task` | Create a TimeLiner task with dates and task type, attached to target items. | handles, guids, use_selection, selection_set, name*, planned_start, planned_end, task_type |
| `select_timeliner_task_items` | Select the items attached to a TimeLiner task. | name* |

## Plugins

| Tool | Description | Parameters |
|---|---|---|
| `list_plugins` | List loaded Navisworks plugins (id, name, type) - ids can be used with run_plugin. | filter |
| `run_plugin` | Execute an AddIn plugin by id (e.g. other installed add-ins / your own tools). | plugin_id*, args |

## Advanced API

| Tool | Description | Parameters |
|---|---|---|
| `api_inspect` | Inspect any Navisworks .NET API object via reflection: path from 'doc', 'app' or an item handle, e.g. 'doc.Models', 'doc.CurrentViewpoint.Value', 'h12.PropertyCategories'. Returns type, value, properties and methods. Use to reach features without a dedicated tool. | path*, max_members |
| `api_invoke` | Invoke a public method on an API object by reflection (advanced; may modify the model). Example: path='doc', method='SaveFile', args=['C:\\temp\\a.nwd']. Item handles in args are converted to ModelItem / ModelItemCollection. | path*, method*, args |
