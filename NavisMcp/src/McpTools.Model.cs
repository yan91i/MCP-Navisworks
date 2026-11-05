// Document, model tree, search, selection, properties, data check, user-defined properties.
// C# 5 syntax only.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Autodesk.Navisworks.Api;
using NwApp = Autodesk.Navisworks.Api.Application;
using NwColor = Autodesk.Navisworks.Api.Color;
using ComApi = Autodesk.Navisworks.Api.Interop.ComApi;
using ComBridge = Autodesk.Navisworks.Api.ComApi.ComApiBridge;

namespace NavisMcp
{
    public static partial class McpTools
    {
        static void RegisterModel()
        {
            // ------------------------------------------------------------ Document
            _cat = "Document";
            Add("get_model_info",
                "Active document info: file, title, units, appended models (with root handles), overall bounding box, selection count.",
                S(null), GetModelInfo);

            Add("open_file",
                "Open a model file (nwd/nwf/nwc/rvt/ifc/dwg/...) in Navisworks, replacing the current document.",
                S(R("path"), P("path", "string", "Full file path")), OpenFile);

            Add("append_files",
                "Append one or more model files into the current document.",
                S(R("paths"), P("paths", "array", "Full file paths")), AppendFiles);

            Add("save_file",
                "Save the current document as NWD/NWF (Save As if path given).",
                S(R("path"), P("path", "string", "Target path ending with .nwd or .nwf")), SaveFile);

            Add("new_document", "Close / clear the current document (File > New). Unsaved changes are lost.",
                S(null), NewDocument);

            // ------------------------------------------------------------ Model tree
            _cat = "Model Tree";
            Add("get_tree",
                "Browse the selection tree. Without handle returns model roots; with handle returns its children down to 'depth'.",
                S(null,
                  P("handle", "string", "Start node handle (optional)"),
                  P("depth", "integer", "Levels to expand (default 1, max 6)"),
                  P("max_nodes", "integer", "Max nodes in reply (default 200)")),
                GetTree);

            Add("get_item_details",
                "Item details: name, class, GUID, path, parent, child count, has geometry, hidden state, bounding box (min/max/center/size).",
                ST(null, P("limit", "integer", "Max items (default 20)")), GetItemDetails);

            Add("get_bounding_box",
                "Combined bounding box of target items (min, max, center, size) in document units.",
                ST(null), GetBoundingBoxTool);

            Add("measure_distance",
                "Measure between two items: center-to-center distance, XYZ deltas and bounding-box clearance (0 if boxes overlap).",
                S(R("handle_a", "handle_b"),
                  P("handle_a", "string", "First item handle"),
                  P("handle_b", "string", "Second item handle")),
                MeasureDistance);

            // ------------------------------------------------------------ Search
            _cat = "Search";
            Add("search_items",
                "Search model items by property (Category|Property + value/match), property category, item name and/or class. " +
                "Optionally select results and/or export CSV.",
                S(null,
                  P("category", "string", "Property category display name, e.g. 'Element', 'Item', 'Revit Type'"),
                  P("property", "string", "Property display name, e.g. 'Category', 'CAT Code', 'Level'"),
                  P("value", "string", "Value to compare"),
                  P("match", "string", "exists (default) | equals | not_equals | contains | starts_with | ends_with | regex | empty | not_empty | gt | lt"),
                  P("name_contains", "string", "Item display name contains (case-insensitive)"),
                  P("class_contains", "string", "Item class name contains, e.g. 'Wall', 'Duct'"),
                  P("geometry_only", "boolean", "Only return items that have geometry"),
                  P("scope", "string", "all (default) | selection"),
                  P("limit", "integer", "Max items in reply (default 100); count/select/export use all matches"),
                  P("select", "boolean", "Select all matches in Navisworks"),
                  P("return_properties", "array", "Extra properties per item, 'Category|Property'"),
                  P("export_csv_path", "string", "Optional CSV path (env vars allowed)")),
                SearchItems);

            // ------------------------------------------------------------ Selection
            _cat = "Selection";
            Add("get_selection",
                "List currently selected items (returns handles).",
                S(null,
                  P("limit", "integer", "Max items (default 50)"),
                  P("return_properties", "array", "Extra properties 'Category|Property'")),
                GetSelection);

            Add("select_items",
                "Set the selection: replace / add / remove / clear.",
                ST(null, P("mode", "string", "replace (default) | add | remove | clear")), SelectItems);

            Add("select_all", "Select all model roots (everything).", S(null), SelectAll);

            Add("invert_selection", "Invert the current selection (among geometry items).", S(null), InvertSelection);

            Add("select_same_value",
                "Select all items whose property value equals the value of a reference item (reference = handle or first selected item).",
                S(R("property"),
                  P("property", "string", "'Category|Property', e.g. 'Element|Type'"),
                  P("handle", "string", "Reference item handle (default: first selected item)")),
                SelectSameValue);

            Add("select_relatives",
                "Change selection to relatives of target items: parent | children | descendants | geometry_descendants | model_root.",
                ST(R("relation"), P("relation", "string", "parent | children | descendants | geometry_descendants | model_root")),
                SelectRelatives);

            // ------------------------------------------------------------ Properties
            _cat = "Properties";
            Add("get_properties",
                "Get all property categories and values of target items.",
                ST(null,
                  P("category", "string", "Only this property category"),
                  P("limit", "integer", "Max items (default 5)")),
                GetProperties);

            Add("list_property_names",
                "Discover which property categories/properties exist in the model (with item counts). Scans up to max_items items.",
                S(null,
                  P("scope", "string", "all (default) | selection"),
                  P("category", "string", "Only this category"),
                  P("max_items", "integer", "Max items to scan (default 5000)")),
                ListPropertyNames);

            Add("property_value_stats",
                "Distinct values of a property with item counts (e.g. count elements per Category, Level, CAT Code).",
                S(R("property"),
                  P("property", "string", "'Category|Property', e.g. 'Element|Category'"),
                  P("scope", "string", "all (default) | selection"),
                  P("top", "integer", "Max distinct values returned (default 100)")),
                PropertyValueStats);

            Add("export_properties_csv",
                "Export properties of target items (or of all items having a category) to CSV. Without 'properties' exports all properties found.",
                ST(R("path"),
                  P("path", "string", "CSV path"),
                  P("properties", "array", "Columns 'Category|Property' (optional)"),
                  P("having_category", "string", "If no targets given: export all items having this category (default 'Element')"),
                  P("max_items", "integer", "Max items (default 100000)")),
                ExportPropertiesCsv);

            Add("set_user_properties",
                "Write custom (user-defined) properties to items in a custom tab (COM API). Existing values in the tab are kept unless overwritten.",
                ST(R("tab", "values"),
                  P("tab", "string", "Custom tab name, e.g. 'QA Check'"),
                  P("values", "object", "Property name -> value, e.g. {\"Status\":\"OK\",\"Checker\":\"YL\"}"),
                  P("replace_tab", "boolean", "Replace all existing properties in the tab (default false)")),
                SetUserProperties);

            Add("remove_user_property_tab",
                "Remove a user-defined property tab from items.",
                ST(R("tab"), P("tab", "string", "Custom tab name")),
                RemoveUserTab);

            // ------------------------------------------------------------ Data check
            _cat = "Data Check";
            Add("check_properties",
                "Data completeness check (like Revit Interoperability Tools checkset): for items having target_category, check each property " +
                "exists and is not empty (optional regex / allowed values). Returns completion % per property, failing items; can color/select/export CSV.",
                S(R("properties"),
                  P("properties", "array", "'Category|Property' or 'Property' (uses property_category), e.g. ['CAT Code','TS_ASS','TS_LOC']"),
                  P("target_category", "string", "Only items having this category are checked (default 'Element')"),
                  P("property_category", "string", "Default category for bare names (default 'Element')"),
                  P("class_contains", "string", "Only items whose class contains this"),
                  P("filter_property", "string", "Optional pre-filter 'Category|Property' (e.g. 'Element|Category')"),
                  P("filter_value", "string", "Pre-filter value (equals, case-insensitive)"),
                  P("pattern", "string", "Optional regex every non-empty value must match"),
                  P("allowed_values", "array", "Optional list of allowed values"),
                  P("scope", "string", "all (default) | selection"),
                  P("limit", "integer", "Max failing items returned (default 50)"),
                  P("color_failures", "boolean", "Color failing red, passing green"),
                  P("select_failures", "boolean", "Select failing items"),
                  P("export_csv_path", "string", "CSV of all checked items with per-property status")),
                CheckProperties);

            Add("find_duplicates",
                "Find items sharing the same value of a property (e.g. duplicate Element|Id, Mark, GUID).",
                S(R("property"),
                  P("property", "string", "'Category|Property'"),
                  P("scope", "string", "all (default) | selection"),
                  P("limit", "integer", "Max duplicate groups (default 50)"),
                  P("select", "boolean", "Select all duplicate items")),
                FindDuplicates);
        }

        // =============================================================== Document
        static Dictionary<string, object> BoxInfo(BoundingBox3D b)
        {
            if (b == null) return null;
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["min"] = Pt(b.Min);
            d["max"] = Pt(b.Max);
            d["center"] = new double[] { R4((b.Min.X + b.Max.X) / 2), R4((b.Min.Y + b.Max.Y) / 2), R4((b.Min.Z + b.Max.Z) / 2) };
            d["size"] = new double[] { R4(b.Max.X - b.Min.X), R4(b.Max.Y - b.Min.Y), R4(b.Max.Z - b.Min.Z) };
            return d;
        }

        static BoundingBox3D UnionBox(IEnumerable<ModelItem> items)
        {
            double x0 = double.MaxValue, y0 = double.MaxValue, z0 = double.MaxValue;
            double x1 = double.MinValue, y1 = double.MinValue, z1 = double.MinValue;
            bool any = false;
            foreach (ModelItem mi in items)
            {
                BoundingBox3D b = null;
                try { b = mi.BoundingBox(); } catch { }
                if (b == null || b.IsEmpty) continue;
                any = true;
                x0 = Math.Min(x0, b.Min.X); y0 = Math.Min(y0, b.Min.Y); z0 = Math.Min(z0, b.Min.Z);
                x1 = Math.Max(x1, b.Max.X); y1 = Math.Max(y1, b.Max.Y); z1 = Math.Max(z1, b.Max.Z);
            }
            if (!any) return null;
            return new BoundingBox3D(new Point3D(x0, y0, z0), new Point3D(x1, y1, z1));
        }

        static List<ModelItem> Roots(Document doc)
        {
            List<ModelItem> r = new List<ModelItem>();
            foreach (Model m in doc.Models) if (m.RootItem != null) r.Add(m.RootItem);
            return r;
        }

        static object GetModelInfo(Dictionary<string, object> a)
        {
            Document doc = Doc();
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["file"] = doc.FileName;
            r["title"] = doc.Title;
            r["units"] = doc.Units.ToString();
            r["selected_count"] = doc.CurrentSelection.SelectedItems.Count;
            List<object> models = new List<object>();
            foreach (Model m in doc.Models)
            {
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["file"] = m.FileName;
                d["source_file"] = m.SourceFileName;
                d["units"] = Try(delegate { return m.Units.ToString(); });
                if (m.RootItem != null)
                {
                    d["root_name"] = m.RootItem.DisplayName;
                    d["root_handle"] = Handle(m.RootItem);
                }
                models.Add(d);
            }
            r["models"] = models;
            r["bounding_box"] = BoxInfo(UnionBox(Roots(doc)));
            return r;
        }

        static object OpenFile(Dictionary<string, object> a)
        {
            string path = InPath(Str(a, "path") ?? "");
            Document doc = NwApp.ActiveDocument;
            object ok;
            try { ok = CallMethod(doc, "TryOpenFile", path); }
            catch (McpToolException) { ok = CallMethod(doc, "OpenFile", path); }
            _docKey = null;
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["opened"] = ok is bool ? (bool)ok : true;
            r["file"] = path;
            return r;
        }

        static object AppendFiles(Dictionary<string, object> a)
        {
            Document doc = NwApp.ActiveDocument;
            List<object> res = new List<object>();
            foreach (string p0 in StrList(a, "paths"))
            {
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["file"] = p0;
                try
                {
                    string p = InPath(p0);
                    object ok;
                    try { ok = CallMethod(doc, "TryAppendFile", p); }
                    catch (McpToolException) { ok = CallMethod(doc, "AppendFile", p); }
                    d["ok"] = ok is bool ? (bool)ok : true;
                }
                catch (Exception ex) { d["ok"] = false; d["error"] = ex.Message; }
                res.Add(d);
            }
            _docKey = null;
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["results"] = res;
            r["model_count"] = doc.Models.Count;
            return r;
        }

        static object SaveFile(Dictionary<string, object> a)
        {
            Document doc = Doc();
            string path = Str(a, "path");
            path = path.EndsWith(".nwf", StringComparison.OrdinalIgnoreCase) ? OutPath(path, null) : OutPath(path, ".nwd");
            CallMethod(doc, "SaveFile", path);
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["saved"] = path;
            return r;
        }

        static object NewDocument(Dictionary<string, object> a)
        {
            Document doc = NwApp.ActiveDocument;
            CallMethod(doc, "Clear");
            _docKey = null;
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["cleared"] = true;
            return r;
        }

        // =============================================================== Tree
        static ModelItem ByHandle(string h)
        {
            ModelItem mi;
            if (h == null || !_byHandle.TryGetValue(h, out mi)) throw new McpToolException("Unknown/expired handle: " + h);
            return mi;
        }

        static object GetTree(Dictionary<string, object> a)
        {
            Document doc = Doc();
            int depth = Math.Max(1, Math.Min(6, Int(a, "depth", 1)));
            int max = Int(a, "max_nodes", 200);
            string h = Str(a, "handle");
            List<ModelItem> starts = h == null ? Roots(doc) : ByHandle(h).Children.ToList();
            int count = 0;
            bool truncated = false;
            List<object> nodes = new List<object>();
            foreach (ModelItem mi in starts)
            {
                object n = TreeNode(mi, depth - 1, max, ref count, ref truncated);
                if (n == null) break;
                nodes.Add(n);
            }
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["nodes"] = nodes;
            r["node_count"] = count;
            r["truncated"] = truncated;
            return r;
        }

        static object TreeNode(ModelItem mi, int depth, int max, ref int count, ref bool truncated)
        {
            if (count >= max) { truncated = true; return null; }
            count++;
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["handle"] = Handle(mi);
            d["name"] = mi.DisplayName;
            d["class"] = mi.ClassDisplayName;
            int cc = mi.Children.Count();
            d["children"] = cc;
            if (mi.HasGeometry) d["geometry"] = true;
            if (depth > 0 && cc > 0)
            {
                List<object> kids = new List<object>();
                foreach (ModelItem c in mi.Children)
                {
                    object k = TreeNode(c, depth - 1, max, ref count, ref truncated);
                    if (k == null) break;
                    kids.Add(k);
                }
                d["items"] = kids;
            }
            return d;
        }

        static object GetItemDetails(Dictionary<string, object> a)
        {
            Document doc = Doc();
            ModelItemCollection items = ResolveItems(a, doc, false);
            int limit = Int(a, "limit", 20);
            List<object> list = new List<object>();
            foreach (ModelItem mi in items)
            {
                if (list.Count >= limit) break;
                Dictionary<string, object> d = Summary(mi, null);
                if (mi.Parent != null) { d["parent_handle"] = Handle(mi.Parent); d["parent_name"] = mi.Parent.DisplayName; }
                d["child_count"] = mi.Children.Count();
                d["has_geometry"] = mi.HasGeometry;
                d["is_hidden"] = mi.IsHidden;
                d["source_file"] = Try(delegate { return mi.Model == null ? null : mi.Model.SourceFileName; });
                d["bounding_box"] = BoxInfo(UnionBox(new ModelItem[] { mi }));
                list.Add(d);
            }
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["total"] = items.Count;
            r["items"] = list;
            return r;
        }

        static object GetBoundingBoxTool(Dictionary<string, object> a)
        {
            Document doc = Doc();
            ModelItemCollection items = ResolveItems(a, doc, true);
            IEnumerable<ModelItem> src = items.Count > 0 ? (IEnumerable<ModelItem>)items : Roots(doc);
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["items"] = items.Count == 0 ? "whole model" : (object)items.Count;
            r["units"] = doc.Units.ToString();
            r["bounding_box"] = BoxInfo(UnionBox(src));
            return r;
        }

        static object MeasureDistance(Dictionary<string, object> a)
        {
            Doc();
            ModelItem ia = ByHandle(Str(a, "handle_a"));
            ModelItem ib = ByHandle(Str(a, "handle_b"));
            BoundingBox3D ba = UnionBox(new ModelItem[] { ia });
            BoundingBox3D bb = UnionBox(new ModelItem[] { ib });
            if (ba == null || bb == null) throw new McpToolException("Both items need geometry.");
            double cax = (ba.Min.X + ba.Max.X) / 2, cay = (ba.Min.Y + ba.Max.Y) / 2, caz = (ba.Min.Z + ba.Max.Z) / 2;
            double cbx = (bb.Min.X + bb.Max.X) / 2, cby = (bb.Min.Y + bb.Max.Y) / 2, cbz = (bb.Min.Z + bb.Max.Z) / 2;
            double gx = Math.Max(0, Math.Max(ba.Min.X - bb.Max.X, bb.Min.X - ba.Max.X));
            double gy = Math.Max(0, Math.Max(ba.Min.Y - bb.Max.Y, bb.Min.Y - ba.Max.Y));
            double gz = Math.Max(0, Math.Max(ba.Min.Z - bb.Max.Z, bb.Min.Z - ba.Max.Z));
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["units"] = NwApp.ActiveDocument.Units.ToString();
            r["center_distance"] = R4(Math.Sqrt((cax - cbx) * (cax - cbx) + (cay - cby) * (cay - cby) + (caz - cbz) * (caz - cbz)));
            r["delta_xyz"] = new double[] { R4(cbx - cax), R4(cby - cay), R4(cbz - caz) };
            r["bbox_clearance"] = R4(Math.Sqrt(gx * gx + gy * gy + gz * gz));
            r["note"] = "bbox_clearance is an axis-aligned bounding-box approximation; use Clash Detective for exact clearance.";
            return r;
        }

        // =============================================================== Search
        static object SearchItems(Dictionary<string, object> a)
        {
            Document doc = Doc();
            string cat = Str(a, "category");
            string prop = Str(a, "property");
            string val = Str(a, "value");
            string match = (Str(a, "match") ?? (val != null ? "equals" : "exists")).ToLowerInvariant();
            string nameC = Str(a, "name_contains");
            string cls = Str(a, "class_contains");
            bool geo = Bool(a, "geometry_only", false);
            string scope = (Str(a, "scope") ?? "all").ToLowerInvariant();
            int limit = Int(a, "limit", 100);
            List<string> extra = StrList(a, "return_properties");
            string csv = Str(a, "export_csv_path");

            if (prop != null && cat == null) cat = "Element";

            ModelItemCollection candidates;
            if (cat != null && prop != null) candidates = RunSearch(doc, scope, SearchCondition.HasPropertyByDisplayName(cat, prop));
            else if (cat != null) candidates = RunSearch(doc, scope, SearchCondition.HasCategoryByDisplayName(cat));
            else if (nameC != null || cls != null || geo) candidates = ScopeItems(doc, scope);
            else throw new McpToolException("Provide at least one filter: category/property, name_contains, class_contains or geometry_only.");

            ModelItemCollection matched = new ModelItemCollection();
            foreach (ModelItem mi in candidates)
            {
                if (nameC != null && !Contains(mi.DisplayName, nameC)) continue;
                if (cls != null && !Contains(mi.ClassDisplayName, cls)) continue;
                if (geo && !mi.HasGeometry) continue;
                if (prop != null && match != "exists" && !Match(GetProp(mi, cat, prop), val, match)) continue;
                matched.Add(mi);
            }

            List<object> items = new List<object>();
            foreach (ModelItem mi in matched)
            {
                if (items.Count >= limit) break;
                Dictionary<string, object> s = Summary(mi, extra);
                if (prop != null && !s.ContainsKey(cat + "|" + prop)) s[cat + "|" + prop] = GetProp(mi, cat, prop);
                items.Add(s);
            }

            bool sel = Bool(a, "select", false);
            if (sel) doc.CurrentSelection.CopyFrom(matched);

            Dictionary<string, object> r = new Dictionary<string, object>();
            r["total_matches"] = matched.Count;
            r["returned"] = items.Count;
            r["selected"] = sel;

            if (csv != null)
            {
                string path = CsvPath(csv);
                List<string> header = new List<string>(new string[] { "Handle", "Name", "Class", "GUID", "Path" });
                List<string> cols = new List<string>();
                if (prop != null) cols.Add(cat + "|" + prop);
                foreach (string e in extra) if (!cols.Contains(e)) cols.Add(e);
                header.AddRange(cols);
                List<List<string>> rows = new List<List<string>>();
                foreach (ModelItem mi in matched)
                {
                    List<string> row = new List<string>();
                    row.Add(Handle(mi)); row.Add(mi.DisplayName); row.Add(mi.ClassDisplayName);
                    row.Add(mi.InstanceGuid == Guid.Empty ? "" : mi.InstanceGuid.ToString());
                    row.Add(PathOf(mi));
                    foreach (string c in cols) { string cc, pp; SplitSpec(c, "Element", out cc, out pp); row.Add(GetProp(mi, cc, pp)); }
                    rows.Add(row);
                }
                WriteCsv(path, header, rows);
                r["csv"] = path;
            }
            r["items"] = items;
            return r;
        }

        // =============================================================== Selection
        static object SelCount(Document doc)
        {
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["selected_count"] = doc.CurrentSelection.SelectedItems.Count;
            return r;
        }

        static object GetSelection(Dictionary<string, object> a)
        {
            Document doc = Doc();
            int limit = Int(a, "limit", 50);
            List<string> extra = StrList(a, "return_properties");
            ModelItemCollection sel = doc.CurrentSelection.SelectedItems;
            List<object> items = new List<object>();
            foreach (ModelItem mi in sel)
            {
                if (items.Count >= limit) break;
                items.Add(Summary(mi, extra));
            }
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["total"] = sel.Count;
            r["returned"] = items.Count;
            r["items"] = items;
            return r;
        }

        static object SelectItems(Dictionary<string, object> a)
        {
            Document doc = Doc();
            string mode = (Str(a, "mode") ?? "replace").ToLowerInvariant();
            if (mode == "clear") doc.CurrentSelection.Clear();
            else
            {
                ModelItemCollection items = ResolveItems(a, doc, false);
                if (mode == "add") doc.CurrentSelection.AddRange(items);
                else if (mode == "remove")
                {
                    HashSet<ModelItem> rm = new HashSet<ModelItem>(items);
                    ModelItemCollection keep = new ModelItemCollection();
                    foreach (ModelItem mi in doc.CurrentSelection.SelectedItems) if (!rm.Contains(mi)) keep.Add(mi);
                    doc.CurrentSelection.CopyFrom(keep);
                }
                else doc.CurrentSelection.CopyFrom(items);
            }
            return SelCount(doc);
        }

        static object SelectAll(Dictionary<string, object> a)
        {
            Document doc = Doc();
            doc.CurrentSelection.SelectAll();
            return SelCount(doc);
        }

        static object InvertSelection(Dictionary<string, object> a)
        {
            Document doc = Doc();
            HashSet<ModelItem> sel = new HashSet<ModelItem>();
            foreach (ModelItem mi in doc.CurrentSelection.SelectedItems)
                foreach (ModelItem d in mi.DescendantsAndSelf) sel.Add(d);
            ModelItemCollection inv = new ModelItemCollection();
            foreach (ModelItem mi in doc.Models.RootItemDescendantsAndSelf)
                if (mi.HasGeometry && !sel.Contains(mi)) inv.Add(mi);
            doc.CurrentSelection.CopyFrom(inv);
            return SelCount(doc);
        }

        static object SelectSameValue(Dictionary<string, object> a)
        {
            Document doc = Doc();
            string cat, prop;
            SplitSpec(Str(a, "property") ?? "", "Element", out cat, out prop);
            ModelItem refItem = Str(a, "handle") != null ? ByHandle(Str(a, "handle")) : doc.CurrentSelection.SelectedItems.FirstOrDefault();
            if (refItem == null) throw new McpToolException("Select a reference item or pass handle.");
            string v = GetProp(refItem, cat, prop);
            if (v == null) throw new McpToolException("Reference item has no property " + cat + "|" + prop);
            ModelItemCollection hits = new ModelItemCollection();
            foreach (ModelItem mi in RunSearch(doc, "all", SearchCondition.HasPropertyByDisplayName(cat, prop)))
                if (string.Equals(GetProp(mi, cat, prop), v, StringComparison.OrdinalIgnoreCase)) hits.Add(mi);
            doc.CurrentSelection.CopyFrom(hits);
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["property"] = cat + "|" + prop;
            r["value"] = v;
            r["selected_count"] = hits.Count;
            return r;
        }

        static object SelectRelatives(Dictionary<string, object> a)
        {
            Document doc = Doc();
            ModelItemCollection items = ResolveItems(a, doc, true);
            if (items.Count == 0) items = doc.CurrentSelection.SelectedItems;
            string rel = (Str(a, "relation") ?? "").ToLowerInvariant();
            HashSet<ModelItem> res = new HashSet<ModelItem>();
            foreach (ModelItem mi in items)
            {
                switch (rel)
                {
                    case "parent": if (mi.Parent != null) res.Add(mi.Parent); break;
                    case "children": foreach (ModelItem c in mi.Children) res.Add(c); break;
                    case "descendants": foreach (ModelItem c in mi.Descendants) res.Add(c); break;
                    case "geometry_descendants": foreach (ModelItem c in mi.DescendantsAndSelf) if (c.HasGeometry) res.Add(c); break;
                    case "model_root": { ModelItem r0 = mi; while (r0.Parent != null) r0 = r0.Parent; res.Add(r0); } break;
                    default: throw new McpToolException("relation must be parent | children | descendants | geometry_descendants | model_root");
                }
            }
            ModelItemCollection c2 = new ModelItemCollection();
            c2.AddRange(res);
            doc.CurrentSelection.CopyFrom(c2);
            return SelCount(doc);
        }

        // =============================================================== Properties
        static object GetProperties(Dictionary<string, object> a)
        {
            Document doc = Doc();
            ModelItemCollection items = ResolveItems(a, doc, false);
            string catFilter = Str(a, "category");
            int limit = Int(a, "limit", 5);
            List<object> list = new List<object>();
            foreach (ModelItem mi in items)
            {
                if (list.Count >= limit) break;
                Dictionary<string, object> s = Summary(mi, null);
                Dictionary<string, object> cats = new Dictionary<string, object>();
                foreach (PropertyCategory pc in mi.PropertyCategories)
                {
                    if (catFilter != null && !string.Equals(pc.DisplayName, catFilter, StringComparison.OrdinalIgnoreCase)) continue;
                    Dictionary<string, object> props = new Dictionary<string, object>();
                    foreach (DataProperty dp in pc.Properties)
                    {
                        string k = dp.DisplayName ?? dp.Name;
                        while (props.ContainsKey(k)) k += "_";
                        props[k] = VarStr(dp.Value);
                    }
                    string ck = pc.DisplayName ?? pc.Name;
                    while (cats.ContainsKey(ck)) ck += "_";
                    cats[ck] = props;
                }
                s["properties"] = cats;
                list.Add(s);
            }
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["total_targets"] = items.Count;
            r["items"] = list;
            return r;
        }

        static object ListPropertyNames(Dictionary<string, object> a)
        {
            Document doc = Doc();
            string scope = (Str(a, "scope") ?? "all").ToLowerInvariant();
            string catF = Str(a, "category");
            int max = Int(a, "max_items", 5000);
            SortedDictionary<string, SortedDictionary<string, int>> map = new SortedDictionary<string, SortedDictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
            int scanned = 0;
            foreach (ModelItem mi in ScopeItems(doc, scope))
            {
                if (scanned >= max) break;
                scanned++;
                foreach (PropertyCategory pc in mi.PropertyCategories)
                {
                    string cn = pc.DisplayName ?? pc.Name;
                    if (catF != null && !string.Equals(cn, catF, StringComparison.OrdinalIgnoreCase)) continue;
                    SortedDictionary<string, int> pm;
                    if (!map.TryGetValue(cn, out pm)) { pm = new SortedDictionary<string, int>(StringComparer.OrdinalIgnoreCase); map[cn] = pm; }
                    foreach (DataProperty dp in pc.Properties)
                    {
                        string pn = dp.DisplayName ?? dp.Name;
                        int c; pm.TryGetValue(pn, out c); pm[pn] = c + 1;
                    }
                }
            }
            Dictionary<string, object> cats = new Dictionary<string, object>();
            foreach (KeyValuePair<string, SortedDictionary<string, int>> kv in map)
            {
                Dictionary<string, object> pd = new Dictionary<string, object>();
                foreach (KeyValuePair<string, int> p in kv.Value) pd[p.Key] = p.Value;
                cats[kv.Key] = pd;
            }
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["scanned_items"] = scanned;
            r["categories"] = cats;
            r["note"] = "Values are item counts having the property.";
            return r;
        }

        static object PropertyValueStats(Dictionary<string, object> a)
        {
            Document doc = Doc();
            string cat, prop;
            SplitSpec(Str(a, "property") ?? "", "Element", out cat, out prop);
            string scope = (Str(a, "scope") ?? "all").ToLowerInvariant();
            int top = Int(a, "top", 100);
            Dictionary<string, int> counts = new Dictionary<string, int>();
            int total = 0;
            foreach (ModelItem mi in RunSearch(doc, scope, SearchCondition.HasPropertyByDisplayName(cat, prop)))
            {
                string v = GetProp(mi, cat, prop) ?? "";
                if (v.Trim().Length == 0) v = "(empty)";
                int c; counts.TryGetValue(v, out c); counts[v] = c + 1;
                total++;
            }
            List<object> vals = new List<object>();
            foreach (KeyValuePair<string, int> kv in counts.OrderByDescending(x => x.Value).Take(top))
            {
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["value"] = kv.Key; d["count"] = kv.Value;
                vals.Add(d);
            }
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["property"] = cat + "|" + prop;
            r["items_with_property"] = total;
            r["distinct_values"] = counts.Count;
            r["values"] = vals;
            return r;
        }

        static object ExportPropertiesCsv(Dictionary<string, object> a)
        {
            Document doc = Doc();
            string path = CsvPath(Str(a, "path"));
            int max = Int(a, "max_items", 100000);
            ModelItemCollection items = ResolveItems(a, doc, true);
            if (items.Count == 0)
                items = RunSearch(doc, "all", SearchCondition.HasCategoryByDisplayName(Str(a, "having_category") ?? "Element"));

            List<string> cols = StrList(a, "properties");
            List<ModelItem> list = items.Take(max).ToList();
            if (cols.Count == 0)
            {
                List<string> found = new List<string>();
                HashSet<string> seen = new HashSet<string>();
                foreach (ModelItem mi in list)
                    foreach (PropertyCategory pc in mi.PropertyCategories)
                        foreach (DataProperty dp in pc.Properties)
                        {
                            string k = (pc.DisplayName ?? pc.Name) + "|" + (dp.DisplayName ?? dp.Name);
                            if (seen.Add(k)) found.Add(k);
                        }
                cols = found;
            }
            List<string> header = new List<string>(new string[] { "Handle", "Name", "Class", "GUID", "Path" });
            header.AddRange(cols);
            List<List<string>> rows = new List<List<string>>();
            foreach (ModelItem mi in list)
            {
                List<string> row = new List<string>();
                row.Add(Handle(mi)); row.Add(mi.DisplayName); row.Add(mi.ClassDisplayName);
                row.Add(mi.InstanceGuid == Guid.Empty ? "" : mi.InstanceGuid.ToString());
                row.Add(PathOf(mi));
                foreach (string c in cols) { string cc, pp; SplitSpec(c, "Element", out cc, out pp); row.Add(GetProp(mi, cc, pp)); }
                rows.Add(row);
            }
            WriteCsv(path, header, rows);
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["csv"] = path;
            r["rows"] = rows.Count;
            r["columns"] = header.Count;
            return r;
        }

        // ------------------------------------------------------------ user-defined properties (COM)
        static string SafeName(string s)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            return sb.Length == 0 ? "P" : sb.ToString();
        }

        static int FindUserTab(ComApi.InwGUIPropertyNode2 node, string tab, Dictionary<string, string> existing)
        {
            int idx = 0, i = 0;
            foreach (ComApi.InwGUIAttribute2 attr in node.GUIAttributes())
            {
                if (!attr.UserDefined) continue;
                i++;
                if (string.Equals(attr.ClassUserName, tab, StringComparison.OrdinalIgnoreCase))
                {
                    idx = i;
                    if (existing != null)
                        foreach (ComApi.InwOaProperty p in attr.Properties())
                            existing[p.UserName] = Convert.ToString(p.value, CultureInfo.InvariantCulture);
                    break;
                }
            }
            return idx;
        }

        static object SetUserProperties(Dictionary<string, object> a)
        {
            Document doc = Doc();
            ModelItemCollection items = ResolveItems(a, doc, false);
            string tab = Str(a, "tab");
            Dictionary<string, string> values = StrMap(a, "values");
            if (values.Count == 0) throw new McpToolException("values is empty.");
            bool replace = Bool(a, "replace_tab", false);

            ComApi.InwOpState10 state = ComBridge.State;
            int done = 0;
            foreach (ModelItem mi in items)
            {
                ComApi.InwOaPath path = ComBridge.ToInwOaPath(mi);
                ComApi.InwGUIPropertyNode2 node = (ComApi.InwGUIPropertyNode2)state.GetGUIPropertyNode(path, true);
                Dictionary<string, string> merged = new Dictionary<string, string>();
                int idx = FindUserTab(node, tab, replace ? null : merged);
                foreach (KeyValuePair<string, string> kv in values) merged[kv.Key] = kv.Value;

                ComApi.InwOaPropertyVec vec = (ComApi.InwOaPropertyVec)state.ObjectFactory(ComApi.nwEObjectType.eObjectType_nwOaPropertyVec, null, null);
                foreach (KeyValuePair<string, string> kv in merged)
                {
                    ComApi.InwOaProperty p = (ComApi.InwOaProperty)state.ObjectFactory(ComApi.nwEObjectType.eObjectType_nwOaProperty, null, null);
                    p.name = SafeName(kv.Key);
                    p.UserName = kv.Key;
                    p.value = kv.Value;
                    vec.Properties().Add(p);
                }
                node.SetUserDefined(idx, tab, SafeName(tab), vec);
                done++;
            }
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["tab"] = tab;
            r["items_updated"] = done;
            r["properties"] = values.Keys.ToList();
            return r;
        }

        static object RemoveUserTab(Dictionary<string, object> a)
        {
            Document doc = Doc();
            ModelItemCollection items = ResolveItems(a, doc, false);
            string tab = Str(a, "tab");
            ComApi.InwOpState10 state = ComBridge.State;
            int removed = 0;
            foreach (ModelItem mi in items)
            {
                ComApi.InwGUIPropertyNode2 node = (ComApi.InwGUIPropertyNode2)state.GetGUIPropertyNode(ComBridge.ToInwOaPath(mi), true);
                int idx = FindUserTab(node, tab, null);
                if (idx > 0) { node.RemoveUserDefined(idx); removed++; }
            }
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["tab"] = tab;
            r["items_removed"] = removed;
            return r;
        }

        // =============================================================== Data check
        static object CheckProperties(Dictionary<string, object> a)
        {
            Document doc = Doc();
            List<string> specs = StrList(a, "properties");
            if (specs.Count == 0) throw new McpToolException("properties is required.");
            string target = Str(a, "target_category") ?? "Element";
            string defCat = Str(a, "property_category") ?? "Element";
            string cls = Str(a, "class_contains");
            string scope = (Str(a, "scope") ?? "all").ToLowerInvariant();
            int limit = Int(a, "limit", 50);
            string csv = Str(a, "export_csv_path");
            string pattern = Str(a, "pattern");
            List<string> allowed = StrList(a, "allowed_values");
            string fpSpec = Str(a, "filter_property");
            string fv = Str(a, "filter_value");
            string fCat = null, fProp = null;
            if (fpSpec != null) SplitSpec(fpSpec, "Element", out fCat, out fProp);

            List<string> cats = new List<string>(), props = new List<string>(), keys = new List<string>();
            foreach (string sp in specs)
            {
                string c, p; SplitSpec(sp, defCat, out c, out p);
                cats.Add(c); props.Add(p); keys.Add(c + "|" + p);
            }

            ModelItemCollection targets = RunSearch(doc, scope, SearchCondition.HasCategoryByDisplayName(target));
            int n = keys.Count;
            int[] ok = new int[n], missing = new int[n], empty = new int[n], invalid = new int[n];
            ModelItemCollection failed = new ModelItemCollection(), passed = new ModelItemCollection();
            List<object> failList = new List<object>();
            List<List<string>> rows = csv != null ? new List<List<string>>() : null;
            int checkedCount = 0;

            foreach (ModelItem mi in targets)
            {
                if (cls != null && !Contains(mi.ClassDisplayName, cls)) continue;
                if (fProp != null && !Match(GetProp(mi, fCat, fProp), fv, fv == null ? "exists" : "equals")) continue;
                checkedCount++;
                List<string> bad = new List<string>();
                List<string> row = rows != null ? new List<string>() : null;
                for (int i = 0; i < n; i++)
                {
                    string v = GetProp(mi, cats[i], props[i]);
                    string st;
                    if (v == null) { missing[i]++; st = "MISSING"; }
                    else if (v.Trim().Length == 0) { empty[i]++; st = "EMPTY"; }
                    else if ((pattern != null && !System.Text.RegularExpressions.Regex.IsMatch(v, pattern)) ||
                             (allowed.Count > 0 && !allowed.Any(x => string.Equals(x, v.Trim(), StringComparison.OrdinalIgnoreCase))))
                    { invalid[i]++; st = "INVALID"; }
                    else { ok[i]++; st = "OK"; }
                    if (st != "OK") bad.Add(props[i] + ":" + st);
                    if (row != null) row.Add(st == "OK" ? v : (st == "INVALID" ? "INVALID(" + v + ")" : st));
                }
                if (bad.Count > 0)
                {
                    failed.Add(mi);
                    if (failList.Count < limit)
                    {
                        Dictionary<string, object> s = Summary(mi, null);
                        s["failed"] = bad;
                        failList.Add(s);
                    }
                }
                else passed.Add(mi);

                if (rows != null)
                {
                    List<string> full = new List<string>();
                    full.Add(Handle(mi)); full.Add(mi.DisplayName); full.Add(mi.ClassDisplayName);
                    full.Add(mi.InstanceGuid == Guid.Empty ? "" : mi.InstanceGuid.ToString());
                    full.Add(bad.Count == 0 ? "PASS" : "FAIL");
                    full.AddRange(row);
                    rows.Add(full);
                }
            }

            List<object> stats = new List<object>();
            for (int i = 0; i < n; i++)
            {
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["property"] = keys[i];
                d["ok"] = ok[i]; d["missing"] = missing[i]; d["empty"] = empty[i]; d["invalid"] = invalid[i];
                d["completion_pct"] = checkedCount == 0 ? 0.0 : Math.Round(100.0 * ok[i] / checkedCount, 2);
                stats.Add(d);
            }

            if (Bool(a, "color_failures", false))
            {
                if (passed.Count > 0) doc.Models.OverridePermanentColor(passed, new NwColor(0, 0.8, 0));
                if (failed.Count > 0) doc.Models.OverridePermanentColor(failed, new NwColor(1, 0, 0));
            }
            if (Bool(a, "select_failures", false)) doc.CurrentSelection.CopyFrom(failed);

            Dictionary<string, object> r = new Dictionary<string, object>();
            r["target_category"] = target;
            r["checked_items"] = checkedCount;
            r["passed_items"] = passed.Count;
            r["failed_items"] = failed.Count;
            r["overall_pass_pct"] = checkedCount == 0 ? 0.0 : Math.Round(100.0 * passed.Count / checkedCount, 2);
            r["per_property"] = stats;
            if (rows != null)
            {
                string path = CsvPath(csv);
                List<string> header = new List<string>(new string[] { "Handle", "Name", "Class", "GUID", "Result" });
                header.AddRange(keys);
                WriteCsv(path, header, rows);
                r["csv"] = path;
            }
            r["failed_samples"] = failList;
            return r;
        }

        static object FindDuplicates(Dictionary<string, object> a)
        {
            Document doc = Doc();
            string cat, prop;
            SplitSpec(Str(a, "property") ?? "", "Element", out cat, out prop);
            string scope = (Str(a, "scope") ?? "all").ToLowerInvariant();
            int limit = Int(a, "limit", 50);
            Dictionary<string, List<ModelItem>> groups = new Dictionary<string, List<ModelItem>>();
            foreach (ModelItem mi in RunSearch(doc, scope, SearchCondition.HasPropertyByDisplayName(cat, prop)))
            {
                string v = GetProp(mi, cat, prop);
                if (string.IsNullOrEmpty(v) || v.Trim().Length == 0) continue;
                List<ModelItem> l;
                if (!groups.TryGetValue(v, out l)) { l = new List<ModelItem>(); groups[v] = l; }
                l.Add(mi);
            }
            List<KeyValuePair<string, List<ModelItem>>> dups = groups.Where(g => g.Value.Count > 1).OrderByDescending(g => g.Value.Count).ToList();
            List<object> list = new List<object>();
            ModelItemCollection all = new ModelItemCollection();
            foreach (KeyValuePair<string, List<ModelItem>> g in dups)
            {
                all.AddRange(g.Value);
                if (list.Count >= limit) continue;
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["value"] = g.Key;
                d["count"] = g.Value.Count;
                d["handles"] = g.Value.Take(20).Select(x => Handle(x)).ToList();
                list.Add(d);
            }
            if (Bool(a, "select", false)) doc.CurrentSelection.CopyFrom(all);
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["property"] = cat + "|" + prop;
            r["duplicate_groups"] = dups.Count;
            r["duplicate_items"] = all.Count;
            r["groups"] = list;
            return r;
        }
    }
}
