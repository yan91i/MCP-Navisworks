// Selection / Search sets and Clash Detective.
// C# 5 syntax only.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;

namespace NavisMcp
{
    public static partial class McpTools
    {
        static void RegisterSets()
        {
            _cat = "Selection Sets";
            Add("list_selection_sets",
                "List saved Selection Sets and Search Sets (folder path, type, item count).",
                S(null, P("count_items", "boolean", "Evaluate item counts (slower for search sets, default true)")), ListSelectionSets);

            Add("create_selection_set",
                "Save target items (handles/guids/current selection/another set) as a new Selection Set.",
                ST(R("name"),
                   P("name", "string", "Set name"),
                   P("replace", "boolean", "Delete an existing set with the same name first")),
                CreateSelectionSet);

            Add("create_search_set",
                "Create a dynamic Search Set from a property condition (updates automatically when the model changes).",
                S(R("name", "property"),
                  P("name", "string", "Set name"),
                  P("property", "string", "'Category|Property'; use 'Category|*' to only require the category"),
                  P("value", "string", "Value (omit for 'exists')"),
                  P("match", "string", "exists (default w/o value) | equals (default with value) | contains | wildcard"),
                  P("replace", "boolean", "Delete an existing set with the same name first")),
                CreateSearchSet);

            Add("apply_selection_set",
                "Use a saved set: select (default) | hide | isolate | zoom, optionally color it.",
                S(R("name"),
                  P("name", "string", "Set name"),
                  P("action", "string", "select (default) | hide | isolate | zoom"),
                  P("color", "string", "Optional color override for the set's items")),
                ApplySelectionSet);

            Add("delete_selection_set", "Delete a saved Selection/Search Set.",
                S(R("name"), P("name", "string", "Set name")), DeleteSelectionSet);
        }

        static void RegisterClash()
        {
            _cat = "Clash";
            Add("list_clash_tests", "List Clash Detective tests with type, tolerance, status and result counts by status.",
                S(null), ListClashTests);

            Add("get_clash_results", "Get results of a clash test (item handles, distance, center, status, assigned).",
                S(R("test_name"),
                  P("test_name", "string", "Clash test name"),
                  P("status", "string", "Optional filter: New | Active | Reviewed | Approved | Resolved"),
                  P("limit", "integer", "Max results (default 50)")),
                GetClashResults);

            Add("select_clash_result", "Select (and zoom to) the two items of a clash result; optionally color them red/green.",
                S(R("test_name", "result_name"),
                  P("test_name", "string", "Clash test name"),
                  P("result_name", "string", "Clash result name, e.g. 'Clash1'"),
                  P("zoom", "boolean", "Zoom (default true)"),
                  P("highlight", "boolean", "Color item1 red, item2 green")),
                SelectClashResult);

            Add("run_clash_test", "Run (update) one clash test, or all tests if test_name omitted.",
                S(null, P("test_name", "string", "Clash test name (omit = run all)")), RunClashTest);

            Add("create_clash_test",
                "Create a clash test between Selection A and B (from selection sets or handles). Optionally run it.",
                S(R("name"),
                  P("name", "string", "Test name"),
                  P("type", "string", "Hard (default) | HardConservative | Clearance | Duplicate"),
                  P("tolerance", "number", "Tolerance in document units (default 0)"),
                  P("set_a", "string", "Selection set name for A"),
                  P("set_b", "string", "Selection set name for B"),
                  P("handles_a", "array", "Item handles for A"),
                  P("handles_b", "array", "Item handles for B"),
                  P("run", "boolean", "Run immediately (default true)")),
                CreateClashTest);

            Add("delete_clash_test", "Delete a clash test.",
                S(R("test_name"), P("test_name", "string", "Clash test name")), DeleteClashTest);

            Add("set_clash_status",
                "Change status (and optionally assignee) of clash results: specific result names, or all results with from_status.",
                S(R("test_name", "status"),
                  P("test_name", "string", "Clash test name"),
                  P("status", "string", "New | Active | Reviewed | Approved | Resolved"),
                  P("result_names", "array", "Result names (e.g. ['Clash1','Clash5'])"),
                  P("from_status", "string", "Or: apply to all results currently in this status"),
                  P("assigned_to", "string", "Optional assignee")),
                SetClashStatus);

            Add("export_clash_report_csv",
                "Export clash results of one or all tests to CSV (test, result, status, distance, center, item names/GUIDs, extra properties).",
                S(R("path"),
                  P("path", "string", "CSV path"),
                  P("test_name", "string", "Test name (omit = all tests)"),
                  P("item_properties", "array", "Extra item properties 'Category|Property' for both items")),
                ExportClashCsv);
        }

        // =============================================================== Selection sets
        static void CollectSets(object coll, string prefix, List<KeyValuePair<string, SelectionSet>> outList)
        {
            IEnumerable e = coll as IEnumerable;
            if (e == null) return;
            foreach (object o in e)
            {
                SelectionSet ss = o as SelectionSet;
                if (ss != null) { outList.Add(new KeyValuePair<string, SelectionSet>(prefix + ss.DisplayName, ss)); continue; }
                GroupItem g = o as GroupItem;
                if (g != null) CollectSets(g.Children, prefix + g.DisplayName + " / ", outList);
            }
        }

        static List<KeyValuePair<string, SelectionSet>> AllSets(Document doc)
        {
            List<KeyValuePair<string, SelectionSet>> l = new List<KeyValuePair<string, SelectionSet>>();
            CollectSets(doc.SelectionSets.Value, "", l);
            return l;
        }

        static SelectionSet FindSet(Document doc, string name)
        {
            KeyValuePair<string, SelectionSet>? kv = TryFindSet(doc, name);
            if (kv == null) throw new McpToolException("Selection set not found: " + name);
            return kv.Value.Value;
        }

        static KeyValuePair<string, SelectionSet>? TryFindSet(Document doc, string name)
        {
            List<KeyValuePair<string, SelectionSet>> sets = AllSets(doc);
            foreach (KeyValuePair<string, SelectionSet> kv in sets)
                if (string.Equals(kv.Value.DisplayName, name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase)) return kv;
            foreach (KeyValuePair<string, SelectionSet> kv in sets)
                if (Contains(kv.Key, name)) return kv;
            return null;
        }

        static ModelItemCollection SetItems(Document doc, SelectionSet ss)
        {
            ModelItemCollection c = new ModelItemCollection();
            if (ss.HasExplicitModelItems) c.AddRange(ss.ExplicitModelItems);
            else if (ss.HasSearch) c.AddRange(ss.Search.FindAll(doc, false));
            return c;
        }

        static object ListSelectionSets(Dictionary<string, object> a)
        {
            Document doc = Doc();
            bool count = Bool(a, "count_items", true);
            List<object> list = new List<object>();
            foreach (KeyValuePair<string, SelectionSet> kv in AllSets(doc))
            {
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["name"] = kv.Key;
                d["type"] = kv.Value.HasSearch ? "search" : "selection";
                if (count) d["items"] = Try(delegate { return SetItems(doc, kv.Value).Count; });
                list.Add(d);
            }
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["count"] = list.Count;
            r["sets"] = list;
            return r;
        }

        static void DeleteIfExists(Document doc, string name)
        {
            foreach (KeyValuePair<string, SelectionSet> kv in AllSets(doc))
                if (string.Equals(kv.Value.DisplayName, name, StringComparison.OrdinalIgnoreCase))
                {
                    RemoveSavedItem(doc.SelectionSets, kv.Value);
                    return;
                }
        }

        static object CreateSelectionSet(Dictionary<string, object> a)
        {
            Document doc = Doc();
            string name = Str(a, "name");
            ModelItemCollection items = ResolveItems(a, doc, false);
            if (Bool(a, "replace", false)) DeleteIfExists(doc, name);
            SelectionSet ss = new SelectionSet(items);
            ss.DisplayName = name;
            doc.SelectionSets.AddCopy(ss);
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["created"] = name;
            r["items"] = items.Count;
            return r;
        }

        static object CreateSearchSet(Dictionary<string, object> a)
        {
            Document doc = Doc();
            string name = Str(a, "name");
            string cat, prop;
            SplitSpec(Str(a, "property") ?? "", "Element", out cat, out prop);
            string val = Str(a, "value");
            string match = (Str(a, "match") ?? (val == null ? "exists" : "equals")).ToLowerInvariant();

            SearchCondition cond;
            if (prop == "*" || prop.Length == 0) cond = SearchCondition.HasCategoryByDisplayName(cat);
            else
            {
                cond = SearchCondition.HasPropertyByDisplayName(cat, prop);
                if (match == "equals") cond = cond.EqualValue(VariantData.FromDisplayString(val ?? ""));
                else if (match == "contains") cond = cond.DisplayStringContains(val ?? "");
                else if (match == "wildcard") cond = cond.DisplayStringWildcard(val ?? "*");
                else if (match != "exists") throw new McpToolException("match must be exists | equals | contains | wildcard");
            }
            Search s = new Search();
            s.Selection.SelectAll();
            s.Locations = SearchLocations.DescendantsAndSelf;
            s.SearchConditions.Add(cond);

            if (Bool(a, "replace", false)) DeleteIfExists(doc, name);
            SelectionSet ss = new SelectionSet(s);
            ss.DisplayName = name;
            doc.SelectionSets.AddCopy(ss);

            Dictionary<string, object> r = new Dictionary<string, object>();
            r["created"] = name;
            r["condition"] = cat + "|" + prop + " " + match + (val != null ? " '" + val + "'" : "");
            r["current_matches"] = s.FindAll(doc, false).Count;
            if (match == "equals" && (int)r["current_matches"] == 0)
                r["hint"] = "0 matches: value type may not be text; try match='contains' or 'wildcard'.";
            return r;
        }

        static object ApplySelectionSet(Dictionary<string, object> a)
        {
            Document doc = Doc();
            SelectionSet ss = FindSet(doc, Str(a, "name") ?? "");
            ModelItemCollection items = SetItems(doc, ss);
            string action = (Str(a, "action") ?? "select").ToLowerInvariant();
            Dictionary<string, object> args = new Dictionary<string, object>();
            args["selection_set"] = ss.DisplayName;
            switch (action)
            {
                case "select": doc.CurrentSelection.CopyFrom(items); break;
                case "hide": doc.Models.SetHidden(items, true); break;
                case "isolate": args["action"] = "isolate"; SetVisibility(args); break;
                case "zoom": doc.CurrentSelection.CopyFrom(items); ZoomCurrentSelection(); break;
                default: throw new McpToolException("action must be select | hide | isolate | zoom");
            }
            string color = Str(a, "color");
            if (color != null) doc.Models.OverridePermanentColor(items, ParseColor(color));
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["set"] = ss.DisplayName;
            r["action"] = action;
            r["items"] = items.Count;
            return r;
        }

        static object DeleteSelectionSet(Dictionary<string, object> a)
        {
            Document doc = Doc();
            SelectionSet ss = FindSet(doc, Str(a, "name") ?? "");
            string n = ss.DisplayName;
            RemoveSavedItem(doc.SelectionSets, ss);
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["deleted"] = n;
            return r;
        }

        // =============================================================== Clash
        static DocumentClash ClashDoc(Document doc)
        {
            DocumentClash dc = doc.GetClash();
            if (dc == null) throw new McpToolException("Clash Detective not available.");
            return dc;
        }

        static List<ClashTest> ClashTests(Document doc)
        {
            List<ClashTest> list = new List<ClashTest>();
            foreach (object o in (IEnumerable)ClashDoc(doc).TestsData.Tests)
            {
                ClashTest t = o as ClashTest;
                if (t != null) list.Add(t);
            }
            return list;
        }

        static ClashTest FindTest(Document doc, string name)
        {
            List<ClashTest> tests = ClashTests(doc);
            foreach (ClashTest t in tests) if (string.Equals(t.DisplayName, name, StringComparison.OrdinalIgnoreCase)) return t;
            foreach (ClashTest t in tests) if (Contains(t.DisplayName, name)) return t;
            throw new McpToolException("Clash test not found: " + name);
        }

        static void CollectResults(object coll, List<ClashResult> outList)
        {
            IEnumerable e = coll as IEnumerable;
            if (e == null) return;
            foreach (object o in e)
            {
                ClashResult r = o as ClashResult;
                if (r != null) { outList.Add(r); continue; }
                ClashResultGroup g = o as ClashResultGroup;
                if (g != null) CollectResults(g.Children, outList);
            }
        }

        static List<ClashResult> Results(ClashTest t)
        {
            List<ClashResult> l = new List<ClashResult>();
            CollectResults(t.Children, l);
            return l;
        }

        static object ListClashTests(Dictionary<string, object> a)
        {
            Document doc = Doc();
            List<object> list = new List<object>();
            foreach (ClashTest t in ClashTests(doc))
            {
                List<ClashResult> results = Results(t);
                Dictionary<string, object> byStatus = new Dictionary<string, object>();
                foreach (IGrouping<string, ClashResult> g in results.GroupBy(x => x.Status.ToString()))
                    byStatus[g.Key] = g.Count();
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["name"] = t.DisplayName;
                d["status"] = t.Status.ToString();
                d["type"] = t.TestType.ToString();
                d["tolerance"] = t.Tolerance;
                d["last_run"] = Try(delegate { return Convert.ToString(GetMember(t, "LastRun")); });
                d["results"] = results.Count;
                d["by_status"] = byStatus;
                list.Add(d);
            }
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["count"] = list.Count;
            r["tests"] = list;
            return r;
        }

        static Dictionary<string, object> ResultInfo(ClashResult cr, bool items)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["name"] = cr.DisplayName;
            d["status"] = cr.Status.ToString();
            d["distance"] = R4(cr.Distance);
            if (cr.Center != null) d["center"] = Pt(cr.Center);
            d["assigned_to"] = Try(delegate { return GetMember(cr, "AssignedTo"); });
            d["description"] = Try(delegate { return GetMember(cr, "Description"); });
            d["created"] = Try(delegate { return Convert.ToString(GetMember(cr, "CreatedTime")); });
            if (items)
            {
                if (cr.Item1 != null) d["item1"] = Summary(cr.Item1, null);
                if (cr.Item2 != null) d["item2"] = Summary(cr.Item2, null);
            }
            return d;
        }

        static object GetClashResults(Dictionary<string, object> a)
        {
            Document doc = Doc();
            ClashTest t = FindTest(doc, Str(a, "test_name") ?? "");
            string status = Str(a, "status");
            int limit = Int(a, "limit", 50);
            List<object> list = new List<object>();
            int total = 0;
            foreach (ClashResult cr in Results(t))
            {
                if (status != null && !string.Equals(cr.Status.ToString(), status, StringComparison.OrdinalIgnoreCase)) continue;
                total++;
                if (list.Count < limit) list.Add(ResultInfo(cr, true));
            }
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["test"] = t.DisplayName;
            r["total"] = total;
            r["returned"] = list.Count;
            r["results"] = list;
            return r;
        }

        static ClashResult FindResult(ClashTest t, string name)
        {
            ClashResult hit = Results(t).FirstOrDefault(x => string.Equals(x.DisplayName, name, StringComparison.OrdinalIgnoreCase));
            if (hit == null) throw new McpToolException("Clash result not found: " + name);
            return hit;
        }

        static object SelectClashResult(Dictionary<string, object> a)
        {
            Document doc = Doc();
            ClashTest t = FindTest(doc, Str(a, "test_name") ?? "");
            ClashResult hit = FindResult(t, Str(a, "result_name") ?? "");
            ModelItemCollection items = new ModelItemCollection();
            if (hit.Item1 != null) items.Add(hit.Item1);
            if (hit.Item2 != null) items.Add(hit.Item2);
            doc.CurrentSelection.CopyFrom(items);
            if (Bool(a, "highlight", false))
            {
                if (hit.Item1 != null) { ModelItemCollection c1 = new ModelItemCollection(); c1.Add(hit.Item1); doc.Models.OverridePermanentColor(c1, ParseColor("red")); }
                if (hit.Item2 != null) { ModelItemCollection c2 = new ModelItemCollection(); c2.Add(hit.Item2); doc.Models.OverridePermanentColor(c2, ParseColor("green")); }
            }
            if (Bool(a, "zoom", true) && items.Count > 0) ZoomCurrentSelection();
            return ResultInfo(hit, true);
        }

        static object RunClashTest(Dictionary<string, object> a)
        {
            Document doc = Doc();
            DocumentClash dc = ClashDoc(doc);
            string name = Str(a, "test_name");
            if (name == null) CallMethod(dc.TestsData, "TestsRunAllTests");
            else CallMethod(dc.TestsData, "TestsRunTest", FindTest(doc, name));
            return ListClashTests(a);
        }

        static ModelItemCollection SideItems(Document doc, Dictionary<string, object> a, string setKey, string handlesKey)
        {
            ModelItemCollection c = new ModelItemCollection();
            string set = Str(a, setKey);
            if (set != null) c.AddRange(SetItems(doc, FindSet(doc, set)));
            foreach (string h in StrList(a, handlesKey)) c.Add(ByHandle(h));
            return c;
        }

        static object CreateClashTest(Dictionary<string, object> a)
        {
            Document doc = Doc();
            DocumentClash dc = ClashDoc(doc);
            ModelItemCollection ia = SideItems(doc, a, "set_a", "handles_a");
            ModelItemCollection ib = SideItems(doc, a, "set_b", "handles_b");
            if (ia.Count == 0 || ib.Count == 0) throw new McpToolException("Selection A and B must not be empty (set_a/handles_a, set_b/handles_b).");

            ClashTest t = new ClashTest();
            t.DisplayName = Str(a, "name");
            string type = Str(a, "type") ?? "Hard";
            try { t.TestType = (ClashTestType)Enum.Parse(typeof(ClashTestType), type, true); }
            catch { throw new McpToolException("type must be one of: " + string.Join(", ", Enum.GetNames(typeof(ClashTestType)))); }
            t.Tolerance = Dbl(a, "tolerance") ?? 0;
            t.SelectionA.Selection.CopyFrom(ia);
            t.SelectionB.Selection.CopyFrom(ib);
            CallMethod(dc.TestsData, "TestsAddCopy", t);

            Dictionary<string, object> r = new Dictionary<string, object>();
            r["created"] = t.DisplayName;
            r["items_a"] = ia.Count;
            r["items_b"] = ib.Count;
            if (Bool(a, "run", true))
            {
                ClashTest added = FindTest(doc, t.DisplayName);
                CallMethod(dc.TestsData, "TestsRunTest", added);
                ClashTest after = FindTest(doc, t.DisplayName);
                r["results"] = Results(after).Count;
            }
            return r;
        }

        static object DeleteClashTest(Dictionary<string, object> a)
        {
            Document doc = Doc();
            ClashTest t = FindTest(doc, Str(a, "test_name") ?? "");
            CallMethod(ClashDoc(doc).TestsData, "TestsRemove", t);
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["deleted"] = t.DisplayName;
            return r;
        }

        static object SetClashStatus(Dictionary<string, object> a)
        {
            Document doc = Doc();
            DocumentClash dc = ClashDoc(doc);
            ClashTest t = FindTest(doc, Str(a, "test_name") ?? "");
            ClashResultStatus st;
            try { st = (ClashResultStatus)Enum.Parse(typeof(ClashResultStatus), Str(a, "status") ?? "", true); }
            catch { throw new McpToolException("status must be one of: " + string.Join(", ", Enum.GetNames(typeof(ClashResultStatus)))); }
            List<string> names = StrList(a, "result_names");
            string from = Str(a, "from_status");
            string assigned = Str(a, "assigned_to");
            if (names.Count == 0 && from == null) throw new McpToolException("Provide result_names or from_status.");

            int changed = 0;
            List<string> errors = new List<string>();
            foreach (ClashResult cr in Results(t))
            {
                bool hit = names.Any(n => string.Equals(n, cr.DisplayName, StringComparison.OrdinalIgnoreCase)) ||
                           (from != null && string.Equals(cr.Status.ToString(), from, StringComparison.OrdinalIgnoreCase));
                if (!hit) continue;
                CallMethod(dc.TestsData, "TestsEditResultStatus", cr, st);
                if (assigned != null)
                {
                    try { CallMethod(dc.TestsData, "TestsEditResultAssignedTo", cr, assigned); }
                    catch (Exception ex) { if (errors.Count < 1) errors.Add("assign: " + ex.Message); }
                }
                changed++;
            }
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["test"] = t.DisplayName;
            r["changed"] = changed;
            r["status"] = st.ToString();
            if (errors.Count > 0) r["warnings"] = errors;
            return r;
        }

        static object ExportClashCsv(Dictionary<string, object> a)
        {
            Document doc = Doc();
            string path = CsvPath(Str(a, "path"));
            string tn = Str(a, "test_name");
            List<string> extra = StrList(a, "item_properties");
            List<ClashTest> tests = tn == null ? ClashTests(doc) : new List<ClashTest>(new ClashTest[] { FindTest(doc, tn) });

            List<string> header = new List<string>(new string[] {
                "Test", "Result", "Status", "Distance", "CenterX", "CenterY", "CenterZ", "AssignedTo",
                "Item1 Name", "Item1 Class", "Item1 GUID", "Item1 Path", "Item2 Name", "Item2 Class", "Item2 GUID", "Item2 Path" });
            foreach (string e in extra) { header.Add("Item1 " + e); }
            foreach (string e in extra) { header.Add("Item2 " + e); }
            List<List<string>> rows = new List<List<string>>();
            foreach (ClashTest t in tests)
                foreach (ClashResult cr in Results(t))
                {
                    List<string> row = new List<string>();
                    row.Add(t.DisplayName); row.Add(cr.DisplayName); row.Add(cr.Status.ToString());
                    row.Add(R4(cr.Distance).ToString(System.Globalization.CultureInfo.InvariantCulture));
                    double[] c = cr.Center != null ? Pt(cr.Center) : new double[] { 0, 0, 0 };
                    foreach (double v in c) row.Add(v.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    row.Add(Convert.ToString(Try(delegate { return GetMember(cr, "AssignedTo"); })));
                    foreach (ModelItem mi in new ModelItem[] { cr.Item1, cr.Item2 })
                    {
                        row.Add(mi == null ? "" : mi.DisplayName);
                        row.Add(mi == null ? "" : mi.ClassDisplayName);
                        row.Add(mi == null || mi.InstanceGuid == Guid.Empty ? "" : mi.InstanceGuid.ToString());
                        row.Add(mi == null ? "" : PathOf(mi));
                    }
                    foreach (ModelItem mi in new ModelItem[] { cr.Item1, cr.Item2 })
                        foreach (string e in extra)
                        {
                            string cc, pp; SplitSpec(e, "Element", out cc, out pp);
                            row.Add(mi == null ? "" : GetProp(mi, cc, pp));
                        }
                    rows.Add(row);
                }
            WriteCsv(path, header, rows);
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["csv"] = path;
            r["rows"] = rows.Count;
            r["tests"] = tests.Count;
            return r;
        }
    }
}
