// TimeLiner (via reflection - no compile-time reference needed), plugins, generic API reflection.
// C# 5 syntax only.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Autodesk.Navisworks.Api;
using NwApp = Autodesk.Navisworks.Api.Application;

namespace NavisMcp
{
    public static partial class McpTools
    {
        static void RegisterAdvanced()
        {
            _cat = "TimeLiner";
            Add("list_timeliner_tasks",
                "List TimeLiner tasks (hierarchy path, planned/actual dates, task type, attached item count).",
                S(null, P("limit", "integer", "Max tasks (default 200)")), ListTimelinerTasks);

            Add("create_timeliner_task",
                "Create a TimeLiner task with dates and task type, attached to target items.",
                ST(R("name"),
                   P("name", "string", "Task name"),
                   P("planned_start", "string", "yyyy-MM-dd"),
                   P("planned_end", "string", "yyyy-MM-dd"),
                   P("task_type", "string", "Construct (default) | Demolish | Temporary")),
                CreateTimelinerTask);

            Add("select_timeliner_task_items", "Select the items attached to a TimeLiner task.",
                S(R("name"), P("name", "string", "Task name")), SelectTimelinerTaskItems);

            _cat = "Plugins";
            Add("list_plugins", "List loaded Navisworks plugins (id, name, type) - ids can be used with run_plugin.",
                S(null, P("filter", "string", "Name/id contains")), ListPlugins);

            Add("run_plugin", "Execute an AddIn plugin by id (e.g. other installed add-ins / your own tools).",
                S(R("plugin_id"),
                  P("plugin_id", "string", "Plugin id, e.g. 'NavisMcpTools.YLBM'"),
                  P("args", "array", "String parameters")),
                RunPlugin);

            _cat = "Advanced API";
            Add("api_inspect",
                "Inspect any Navisworks .NET API object via reflection: path from 'doc', 'app' or an item handle, e.g. 'doc.Models', " +
                "'doc.CurrentViewpoint.Value', 'h12.PropertyCategories'. Returns type, value, properties and methods. Use to reach features without a dedicated tool.",
                S(R("path"),
                  P("path", "string", "e.g. 'doc.Units', 'app.Plugins', 'h5.Parent', 'doc.Models[0]'"),
                  P("max_members", "integer", "Max properties listed (default 60)")),
                ApiInspect);

            Add("api_invoke",
                "Invoke a public method on an API object by reflection (advanced; may modify the model). " +
                "Example: path='doc', method='SaveFile', args=['C:\\\\temp\\\\a.nwd']. Item handles in args are converted to ModelItem / ModelItemCollection.",
                S(R("path", "method"),
                  P("path", "string", "Target object path (as in api_inspect)"),
                  P("method", "string", "Method name"),
                  P("args", "array", "Arguments as strings (converted to parameter types: string/number/bool/enum/handle)")),
                ApiInvoke);
        }

        // =============================================================== TimeLiner
        static Assembly TimelinerAssembly()
        {
            Assembly asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(x => x.GetName().Name == "Autodesk.Navisworks.Timeliner");
            if (asm == null)
            {
                string p = Path.Combine(Path.GetDirectoryName(typeof(Document).Assembly.Location), "Autodesk.Navisworks.Timeliner.dll");
                if (!File.Exists(p)) throw new McpToolException("TimeLiner API not found (Autodesk.Navisworks.Timeliner.dll).");
                asm = Assembly.LoadFrom(p);
            }
            return asm;
        }

        static object Timeliner(Document doc)
        {
            Assembly asm = TimelinerAssembly();
            foreach (Type t in asm.GetExportedTypes())
            {
                MethodInfo m = t.GetMethod("GetTimeliner", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(Document) }, null);
                if (m != null)
                {
                    object tl = m.Invoke(null, new object[] { doc });
                    if (tl == null) throw new McpToolException("TimeLiner not available for this document.");
                    return tl;
                }
            }
            throw new McpToolException("GetTimeliner() not found in TimeLiner API.");
        }

        static void CollectTasks(object coll, string prefix, List<KeyValuePair<string, object>> outList)
        {
            IEnumerable e = coll as IEnumerable;
            if (e == null) return;
            foreach (object o in e)
            {
                if (o == null) continue;
                string n = Convert.ToString(Try(delegate { return GetMember(o, "DisplayName"); }));
                outList.Add(new KeyValuePair<string, object>(prefix + n, o));
                object kids = Try(delegate { return GetMember(o, "Children"); });
                if (kids != null) CollectTasks(kids, prefix + n + " / ", outList);
            }
        }

        static List<KeyValuePair<string, object>> AllTasks(Document doc)
        {
            object tl = Timeliner(doc);
            object root = Try(delegate { return GetMember(tl, "Tasks"); });
            if (root == null) root = Try(delegate { return GetMember(GetMember(tl, "TasksRoot"), "Children"); });
            List<KeyValuePair<string, object>> l = new List<KeyValuePair<string, object>>();
            CollectTasks(root, "", l);
            return l;
        }

        static ModelItemCollection TaskItems(Document doc, object task)
        {
            ModelItemCollection c = new ModelItemCollection();
            object sel = Try(delegate { return GetMember(task, "Selection"); });
            if (sel == null) return c;
            object hasExp = Try(delegate { return GetMember(sel, "HasExplicitSelection"); });
            if (hasExp is bool && (bool)hasExp)
            {
                IEnumerable ex = Try(delegate { return GetMember(sel, "ExplicitSelection"); }) as IEnumerable;
                if (ex != null) foreach (object o in ex) { ModelItem mi = o as ModelItem; if (mi != null) c.Add(mi); }
                return c;
            }
            object res = Try(delegate { return CallMethod(sel, "GetSelectedItems", doc); });
            if (res == null) res = Try(delegate { return CallMethod(sel, "GetSelectedItems"); });
            IEnumerable en = res as IEnumerable;
            if (en != null) foreach (object o in en) { ModelItem mi = o as ModelItem; if (mi != null) c.Add(mi); }
            return c;
        }

        static object ListTimelinerTasks(Dictionary<string, object> a)
        {
            Document doc = Doc();
            int limit = Int(a, "limit", 200);
            List<KeyValuePair<string, object>> tasks = AllTasks(doc);
            List<object> list = new List<object>();
            foreach (KeyValuePair<string, object> kv in tasks.Take(limit))
            {
                object t = kv.Value;
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["path"] = kv.Key;
                d["planned_start"] = Convert.ToString(Try(delegate { return GetMember(t, "PlannedStartDate"); }));
                d["planned_end"] = Convert.ToString(Try(delegate { return GetMember(t, "PlannedEndDate"); }));
                d["actual_start"] = Convert.ToString(Try(delegate { return GetMember(t, "ActualStartDate"); }));
                d["actual_end"] = Convert.ToString(Try(delegate { return GetMember(t, "ActualEndDate"); }));
                d["task_type"] = Try(delegate { return GetMember(t, "SimulationTaskTypeName"); });
                d["items"] = Try(delegate { return TaskItems(doc, t).Count; });
                list.Add(d);
            }
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["total"] = tasks.Count;
            r["tasks"] = list;
            return r;
        }

        static void SetProp(object target, string name, object value)
        {
            PropertyInfo pi = target.GetType().GetProperty(name);
            if (pi == null || !pi.CanWrite) throw new McpToolException("Cannot set " + name);
            pi.SetValue(target, value, null);
        }

        static object CreateTimelinerTask(Dictionary<string, object> a)
        {
            Document doc = Doc();
            object tl = Timeliner(doc);
            Assembly asm = TimelinerAssembly();
            Type taskType = asm.GetType("Autodesk.Navisworks.Api.Timeliner.TimelinerTask");
            if (taskType == null) throw new McpToolException("TimelinerTask type not found.");
            object task = Activator.CreateInstance(taskType);
            SetProp(task, "DisplayName", Str(a, "name"));
            string ps = Str(a, "planned_start"), pe = Str(a, "planned_end");
            if (ps != null) SetProp(task, "PlannedStartDate", DateTime.Parse(ps, CultureInfo.InvariantCulture));
            if (pe != null) SetProp(task, "PlannedEndDate", DateTime.Parse(pe, CultureInfo.InvariantCulture));
            try { SetProp(task, "SimulationTaskTypeName", Str(a, "task_type") ?? "Construct"); } catch { }

            ModelItemCollection items = ResolveItems(a, doc, true);
            List<string> warn = new List<string>();
            if (items.Count > 0)
            {
                object sel = GetMember(task, "Selection");
                try { CallMethod(sel, "CopyFrom", items); }
                catch (Exception ex) { warn.Add("attach items: " + ex.Message); }
            }

            object root = Try(delegate { return GetMember(tl, "TasksRoot"); });
            bool added = false;
            if (root != null)
                try { CallMethod(tl, "TaskAddCopy", root, task); added = true; } catch { }
            if (!added) CallMethod(tl, "TaskAddCopy", task);

            Dictionary<string, object> r = new Dictionary<string, object>();
            r["created"] = Str(a, "name");
            r["items"] = items.Count;
            if (warn.Count > 0) r["warnings"] = warn;
            return r;
        }

        static object SelectTimelinerTaskItems(Dictionary<string, object> a)
        {
            Document doc = Doc();
            string name = Str(a, "name") ?? "";
            List<KeyValuePair<string, object>> tasks = AllTasks(doc);
            object hit = null;
            foreach (KeyValuePair<string, object> kv in tasks)
                if (kv.Key.EndsWith(name, StringComparison.OrdinalIgnoreCase)) { hit = kv.Value; break; }
            if (hit == null)
                foreach (KeyValuePair<string, object> kv in tasks)
                    if (Contains(kv.Key, name)) { hit = kv.Value; break; }
            if (hit == null) throw new McpToolException("Task not found: " + name);
            ModelItemCollection items = TaskItems(doc, hit);
            doc.CurrentSelection.CopyFrom(items);
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["task"] = GetMember(hit, "DisplayName");
            r["selected"] = items.Count;
            return r;
        }

        // =============================================================== Plugins
        static object ListPlugins(Dictionary<string, object> a)
        {
            string f = Str(a, "filter");
            List<object> list = new List<object>();
            foreach (object rec in (IEnumerable)NwApp.Plugins.PluginRecords)
            {
                string id = Convert.ToString(Try(delegate { return GetMember(rec, "Id"); }));
                string dn = Convert.ToString(Try(delegate { return GetMember(rec, "DisplayName"); }));
                if (f != null && !Contains(id, f) && !Contains(dn, f)) continue;
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["id"] = id;
                d["name"] = dn;
                d["type"] = rec.GetType().Name;
                d["loaded"] = Try(delegate { return GetMember(rec, "IsLoaded"); });
                list.Add(d);
            }
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["count"] = list.Count;
            r["plugins"] = list;
            return r;
        }

        static object RunPlugin(Dictionary<string, object> a)
        {
            string id = Str(a, "plugin_id");
            if (id == "NavisMcp.YLBM") throw new McpToolException("Refusing to toggle the MCP server itself.");
            object rc = CallMethod(NwApp.Plugins, "ExecuteAddInPlugin", id, StrList(a, "args").ToArray());
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["plugin"] = id;
            r["return_code"] = rc;
            return r;
        }

        // =============================================================== Reflection
        static object ResolvePath(string path)
        {
            string[] parts = path.Split('.');
            object cur;
            string root = parts[0].Trim();
            string idx = null;
            int b = root.IndexOf('[');
            if (b > 0) { idx = root.Substring(b); root = root.Substring(0, b); }

            if (root.Equals("doc", StringComparison.OrdinalIgnoreCase) || root.Equals("document", StringComparison.OrdinalIgnoreCase)) cur = Doc();
            else if (root.Equals("app", StringComparison.OrdinalIgnoreCase) || root.Equals("application", StringComparison.OrdinalIgnoreCase)) cur = typeof(NwApp);
            else if (_byHandle.ContainsKey(root)) cur = _byHandle[root];
            else throw new McpToolException("Path must start with 'doc', 'app' or an item handle.");
            if (idx != null) cur = Index(cur, idx);

            for (int i = 1; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                idx = null;
                b = p.IndexOf('[');
                if (b > 0) { idx = p.Substring(b); p = p.Substring(0, b); }
                cur = GetMember(cur, p);
                if (idx != null) cur = Index(cur, idx);
            }
            return cur;
        }

        static object Index(object o, string idx)
        {
            int n = int.Parse(idx.Trim('[', ']', ' '), CultureInfo.InvariantCulture);
            IEnumerable e = o as IEnumerable;
            if (e == null) throw new McpToolException("Not indexable: " + o.GetType().Name);
            int i = 0;
            foreach (object x in e) { if (i == n) return x; i++; }
            throw new McpToolException("Index out of range: " + n);
        }

        static object Simple(object v)
        {
            if (v == null) return null;
            if (v is string || v is bool || v is int || v is double || v is long || v is float || v is decimal) return v;
            if (v is Enum || v is DateTime || v is Guid) return v.ToString();
            ModelItem mi = v as ModelItem;
            if (mi != null) return Summary(mi, null);
            VariantData vd = v as VariantData;
            if (vd != null) return VarStr(vd);
            string s = v.ToString();
            return s.Length > 300 ? s.Substring(0, 300) + "..." : s;
        }

        static object ApiInspect(Dictionary<string, object> a)
        {
            object o = ResolvePath(Str(a, "path") ?? "doc");
            int max = Int(a, "max_members", 60);
            Type t = o as Type;
            bool isStatic = t != null;
            if (t == null) t = o.GetType();
            BindingFlags f = BindingFlags.Public | (isStatic ? BindingFlags.Static : BindingFlags.Instance);

            Dictionary<string, object> r = new Dictionary<string, object>();
            r["type"] = t.FullName;
            if (!isStatic) r["value"] = Simple(o);

            Dictionary<string, object> props = new Dictionary<string, object>();
            foreach (PropertyInfo pi in t.GetProperties(f).OrderBy(x => x.Name))
            {
                if (props.Count >= max) break;
                if (pi.GetIndexParameters().Length > 0) continue;
                object v;
                try { v = Simple(pi.GetValue(isStatic ? null : o, null)); }
                catch (Exception ex) { v = "<" + (ex.InnerException ?? ex).GetType().Name + ">"; }
                props[pi.Name + " : " + pi.PropertyType.Name] = v;
            }
            r["properties"] = props;
            r["methods"] = t.GetMethods(f).Where(m => !m.IsSpecialName && m.DeclaringType != typeof(object))
                .Select(m => m.Name + "(" + string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name).ToArray()) + ")")
                .Distinct().OrderBy(x => x).Take(150).ToList();

            IEnumerable e = o as IEnumerable;
            if (e != null && !(o is string))
            {
                List<object> sample = new List<object>();
                int n = 0;
                foreach (object x in e) { if (n < 20) sample.Add(Simple(x)); n++; if (n > 100000) break; }
                r["count"] = n;
                r["first_items"] = sample;
            }
            return r;
        }

        static object ConvertArg(string s, Type t)
        {
            if (t == typeof(string) || t == typeof(object)) return s;
            if (t == typeof(int)) return int.Parse(s, CultureInfo.InvariantCulture);
            if (t == typeof(double)) return double.Parse(s, CultureInfo.InvariantCulture);
            if (t == typeof(bool)) return s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1";
            if (t.IsEnum) return Enum.Parse(t, s, true);
            if (t == typeof(ModelItem)) return ByHandle(s);
            if (typeof(IEnumerable<ModelItem>).IsAssignableFrom(t) || t == typeof(ModelItemCollection))
            {
                ModelItemCollection c = new ModelItemCollection();
                foreach (string h in s.Split(',')) c.Add(ByHandle(h.Trim()));
                return c;
            }
            return Convert.ChangeType(s, t, CultureInfo.InvariantCulture);
        }

        static object ApiInvoke(Dictionary<string, object> a)
        {
            object o = ResolvePath(Str(a, "path") ?? "doc");
            string method = Str(a, "method");
            List<string> args = StrList(a, "args");
            Type t = o as Type;
            bool isStatic = t != null;
            if (t == null) t = o.GetType();
            BindingFlags f = BindingFlags.Public | (isStatic ? BindingFlags.Static : BindingFlags.Instance);
            Exception last = null;
            foreach (MethodInfo m in t.GetMethods(f))
            {
                if (!string.Equals(m.Name, method, StringComparison.OrdinalIgnoreCase)) continue;
                ParameterInfo[] ps = m.GetParameters();
                if (ps.Length != args.Count) continue;
                object[] conv = new object[ps.Length];
                try
                {
                    for (int i = 0; i < ps.Length; i++) conv[i] = ConvertArg(args[i], ps[i].ParameterType);
                }
                catch (Exception ex) { last = ex; continue; }
                object res = m.Invoke(isStatic ? null : o, conv);
                Dictionary<string, object> r = new Dictionary<string, object>();
                r["invoked"] = t.Name + "." + m.Name;
                r["result_type"] = m.ReturnType.Name;
                r["result"] = Simple(res);
                return r;
            }
            throw new McpToolException("No matching overload " + t.Name + "." + method + "(" + args.Count + " args)" + (last != null ? ": " + last.Message : ""));
        }
    }
}
