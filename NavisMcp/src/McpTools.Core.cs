// Tool registry, shared helpers, meta tools (list_tools / server_info).
// C# 5 syntax only.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Autodesk.Navisworks.Api;
using NwApp = Autodesk.Navisworks.Api.Application;
using NwColor = Autodesk.Navisworks.Api.Color;

namespace NavisMcp
{
    public delegate object ToolHandler(Dictionary<string, object> args);

    public class ToolDef
    {
        public string Category;
        public string Name;
        public string Description;
        public Dictionary<string, object> Schema;
        public ToolHandler Handler;
    }

    public class McpToolException : Exception
    {
        public McpToolException(string msg) : base(msg) { }
    }

    public static partial class McpTools
    {
        static readonly List<ToolDef> _tools = new List<ToolDef>();
        static readonly Dictionary<string, ModelItem> _byHandle = new Dictionary<string, ModelItem>();
        static readonly Dictionary<ModelItem, string> _toHandle = new Dictionary<ModelItem, string>();
        static int _counter;
        static string _docKey;
        static string _cat = "General";

        // common "target items" parameters
        static readonly object[] TH = P("handles", "array", "Item handles returned by other tools, e.g. [\"h12\",\"h13\"]");
        static readonly object[] TG = P("guids", "array", "Item InstanceGuid values");
        static readonly object[] TSEL = P("use_selection", "boolean", "Use the current Navisworks selection as target");
        static readonly object[] TSET = P("selection_set", "string", "Name (or folder/name) of a saved Selection/Search Set as target");

        public static IEnumerable<ToolDef> All { get { return _tools; } }
        public static int Count { get { return _tools.Count; } }

        static McpTools()
        {
            RegisterMeta();
            RegisterModel();
            RegisterSets();
            RegisterView();
            RegisterClash();
            RegisterAdvanced();
        }

        // =============================================================== registry
        static void Add(string name, string desc, Dictionary<string, object> schema, ToolHandler h)
        {
            ToolDef t = new ToolDef();
            t.Category = _cat; t.Name = name; t.Description = desc; t.Schema = schema; t.Handler = h;
            _tools.Add(t);
        }

        public static List<object> ListTools()
        {
            List<object> list = new List<object>();
            foreach (ToolDef t in _tools)
            {
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["name"] = t.Name;
                d["description"] = "[" + t.Category + "] " + t.Description;
                d["inputSchema"] = t.Schema;
                list.Add(d);
            }
            return list;
        }

        public static ToolDef Find(string name)
        {
            foreach (ToolDef t in _tools) if (t.Name == name) return t;
            return null;
        }

        static object[] P(string name, string type, string desc) { return new object[] { name, type, desc }; }

        static Dictionary<string, object> S(string[] required, params object[][] props)
        {
            Dictionary<string, object> ps = new Dictionary<string, object>();
            foreach (object[] p in props)
            {
                Dictionary<string, object> d = new Dictionary<string, object>();
                string t = (string)p[1];
                d["type"] = t;
                d["description"] = p[2];
                if (t == "array")
                {
                    Dictionary<string, object> it = new Dictionary<string, object>();
                    it["type"] = "string";
                    d["items"] = it;
                }
                ps[(string)p[0]] = d;
            }
            Dictionary<string, object> s = new Dictionary<string, object>();
            s["type"] = "object";
            s["properties"] = ps;
            if (required != null && required.Length > 0) s["required"] = required;
            return s;
        }

        /// <summary>Schema with the standard target parameters (handles/guids/use_selection/selection_set) first.</summary>
        static Dictionary<string, object> ST(string[] required, params object[][] props)
        {
            List<object[]> all = new List<object[]>();
            all.Add(TH); all.Add(TG); all.Add(TSEL); all.Add(TSET);
            all.AddRange(props);
            return S(required, all.ToArray());
        }

        static string[] R(params string[] names) { return names; }

        public static string ParamSummary(ToolDef t)
        {
            Dictionary<string, object> props = t.Schema["properties"] as Dictionary<string, object>;
            if (props == null || props.Count == 0) return "(none)";
            List<string> req = new List<string>();
            if (t.Schema.ContainsKey("required")) req.AddRange((string[])t.Schema["required"]);
            List<string> parts = new List<string>();
            foreach (string k in props.Keys) parts.Add(req.Contains(k) ? k + "*" : k);
            return string.Join(", ", parts.ToArray());
        }

        public static string ToolsMarkdown()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Navisworks MCP tools (" + _tools.Count + ")");
            sb.AppendLine();
            sb.AppendLine("`*` = required parameter");
            foreach (IGrouping<string, ToolDef> g in _tools.GroupBy(x => x.Category))
            {
                sb.AppendLine();
                sb.AppendLine("## " + g.Key);
                sb.AppendLine();
                sb.AppendLine("| Tool | Description | Parameters |");
                sb.AppendLine("|---|---|---|");
                foreach (ToolDef t in g)
                    sb.AppendLine("| `" + t.Name + "` | " + t.Description.Replace("|", "/") + " | " + ParamSummary(t) + " |");
            }
            return sb.ToString();
        }

        public static string ToolsHtml()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html><head><meta charset='utf-8'><title>Navisworks MCP tools</title><style>");
            sb.Append("body{font-family:Segoe UI,Arial;margin:24px}table{border-collapse:collapse;width:100%;margin-bottom:24px}");
            sb.Append("td,th{border:1px solid #ccc;padding:6px;vertical-align:top;font-size:13px}th{background:#f0f0f0}code{color:#0b5394}");
            sb.Append("</style></head><body>");
            sb.Append("<h1>Navisworks MCP tools (" + _tools.Count + ")</h1><p>MCP endpoint: <code>" + Esc(McpHost.Url) + "</code> &nbsp; * = required</p>");
            foreach (IGrouping<string, ToolDef> g in _tools.GroupBy(x => x.Category))
            {
                sb.Append("<h2>" + Esc(g.Key) + "</h2><table><tr><th style='width:200px'>Tool</th><th>Description</th><th style='width:30%'>Parameters</th></tr>");
                foreach (ToolDef t in g)
                    sb.Append("<tr><td><code>" + Esc(t.Name) + "</code></td><td>" + Esc(t.Description) + "</td><td>" + Esc(ParamSummary(t)) + "</td></tr>");
                sb.Append("</table>");
            }
            sb.Append("</body></html>");
            return sb.ToString();
        }

        static string Esc(string s)
        {
            return (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        // =============================================================== meta tools
        static void RegisterMeta()
        {
            _cat = "Meta";
            Add("list_tools",
                "List all available Navisworks MCP tools with category, description and parameters. Use this first to discover capabilities.",
                S(null,
                  P("category", "string", "Optional category filter (contains), e.g. 'Clash', 'View'"),
                  P("keyword", "string", "Optional keyword filter on name/description"),
                  P("format", "string", "json (default) | markdown")),
                ListToolsTool);

            Add("server_info",
                "MCP server status: version, URL, tool count, Navisworks version and loaded document.",
                S(null), ServerInfo);
        }

        static object ListToolsTool(Dictionary<string, object> a)
        {
            string cat = Str(a, "category");
            string kw = Str(a, "keyword");
            IEnumerable<ToolDef> q = _tools;
            if (cat != null) q = q.Where(t => Contains(t.Category, cat));
            if (kw != null) q = q.Where(t => Contains(t.Name, kw) || Contains(t.Description, kw));
            List<ToolDef> list = q.ToList();

            if ((Str(a, "format") ?? "").ToLowerInvariant() == "markdown")
            {
                StringBuilder sb = new StringBuilder();
                foreach (IGrouping<string, ToolDef> g in list.GroupBy(x => x.Category))
                {
                    sb.AppendLine("## " + g.Key);
                    foreach (ToolDef t in g) sb.AppendLine("- `" + t.Name + "`: " + t.Description + "  (params: " + ParamSummary(t) + ")");
                }
                return sb.ToString();
            }

            List<object> cats = new List<object>();
            foreach (IGrouping<string, ToolDef> g in list.GroupBy(x => x.Category))
            {
                List<object> tools = new List<object>();
                foreach (ToolDef t in g)
                {
                    Dictionary<string, object> d = new Dictionary<string, object>();
                    d["name"] = t.Name;
                    d["description"] = t.Description;
                    d["parameters"] = ParamSummary(t);
                    tools.Add(d);
                }
                Dictionary<string, object> c = new Dictionary<string, object>();
                c["category"] = g.Key;
                c["count"] = tools.Count;
                c["tools"] = tools;
                cats.Add(c);
            }
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["total_tools"] = list.Count;
            r["note"] = "'*' marks required parameters. Full HTML list: http://127.0.0.1:" + McpHost.Port + "/tools";
            r["categories"] = cats;
            return r;
        }

        static object ServerInfo(Dictionary<string, object> a)
        {
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["server"] = McpHost.ServerName;
            r["version"] = McpHost.ServerVersion;
            r["url"] = McpHost.Url;
            r["tools"] = _tools.Count;
            r["api_assembly"] = typeof(Document).Assembly.GetName().Version.ToString();
            try { r["navisworks_exe"] = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName; } catch { }
            Document d = NwApp.ActiveDocument;
            r["document"] = (d == null || d.IsClear) ? null : d.FileName;
            return r;
        }

        // =============================================================== arg helpers
        static string Str(Dictionary<string, object> a, string k)
        {
            object v;
            if (a == null || !a.TryGetValue(k, out v) || v == null) return null;
            string s = Convert.ToString(v, CultureInfo.InvariantCulture);
            return s.Length == 0 ? null : s;
        }

        static int Int(Dictionary<string, object> a, string k, int def)
        {
            object v;
            if (a == null || !a.TryGetValue(k, out v) || v == null) return def;
            try { return Convert.ToInt32(v, CultureInfo.InvariantCulture); } catch { return def; }
        }

        static double? Dbl(Dictionary<string, object> a, string k)
        {
            object v;
            if (a == null || !a.TryGetValue(k, out v) || v == null) return null;
            try { return Convert.ToDouble(v, CultureInfo.InvariantCulture); } catch { return null; }
        }

        static bool Bool(Dictionary<string, object> a, string k, bool def)
        {
            object v;
            if (a == null || !a.TryGetValue(k, out v) || v == null) return def;
            if (v is bool) return (bool)v;
            string s = v.ToString().ToLowerInvariant();
            return s == "true" || s == "1" || s == "yes";
        }

        static List<string> StrList(Dictionary<string, object> a, string k)
        {
            List<string> r = new List<string>();
            object v;
            if (a == null || !a.TryGetValue(k, out v) || v == null) return r;
            string single = v as string;
            if (single != null)
            {
                foreach (string s in single.Split(new char[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    if (s.Trim().Length > 0) r.Add(s.Trim());
                return r;
            }
            IEnumerable e = v as IEnumerable;
            if (e != null) foreach (object o in e) if (o != null) r.Add(Convert.ToString(o, CultureInfo.InvariantCulture));
            return r;
        }

        static double[] Vec3(Dictionary<string, object> a, string k)
        {
            object v;
            if (a == null || !a.TryGetValue(k, out v) || v == null) return null;
            List<double> d = new List<double>();
            string s = v as string;
            if (s != null)
            {
                foreach (string x in s.Split(new char[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries))
                    d.Add(double.Parse(x, CultureInfo.InvariantCulture));
            }
            else
            {
                IEnumerable e = v as IEnumerable;
                if (e != null) foreach (object o in e) d.Add(Convert.ToDouble(o, CultureInfo.InvariantCulture));
            }
            if (d.Count != 3) throw new McpToolException(k + " must be [x,y,z]");
            return d.ToArray();
        }

        static Dictionary<string, string> StrMap(Dictionary<string, object> a, string k)
        {
            Dictionary<string, string> r = new Dictionary<string, string>();
            object v;
            if (a == null || !a.TryGetValue(k, out v) || v == null) return r;
            Dictionary<string, object> d = v as Dictionary<string, object>;
            if (d == null)
            {
                string s = v as string;
                if (s != null) d = McpProtocol.Deserialize(s) as Dictionary<string, object>;
            }
            if (d != null) foreach (KeyValuePair<string, object> kv in d) r[kv.Key] = kv.Value == null ? "" : Convert.ToString(kv.Value, CultureInfo.InvariantCulture);
            return r;
        }

        static object Try(Func<object> f)
        {
            try { return f(); } catch { return null; }
        }

        static double R4(double v) { return Math.Round(v, 4); }
        static double[] Pt(Point3D p) { return p == null ? null : new double[] { R4(p.X), R4(p.Y), R4(p.Z) }; }

        // =============================================================== document / handles
        static Document Doc()
        {
            Document d = NwApp.ActiveDocument;
            if (d == null || d.IsClear) throw new McpToolException("No model is open in Navisworks.");
            CheckDocKey(d);
            return d;
        }

        static void CheckDocKey(Document d)
        {
            string key = (d.FileName ?? "") + "|" + d.Models.Count;
            if (key != _docKey)
            {
                _docKey = key;
                _byHandle.Clear();
                _toHandle.Clear();
            }
        }

        static string Handle(ModelItem mi)
        {
            string h;
            if (_toHandle.TryGetValue(mi, out h)) return h;
            _counter++;
            h = "h" + _counter;
            _toHandle[mi] = h;
            _byHandle[h] = mi;
            return h;
        }

        static string PathOf(ModelItem mi)
        {
            List<string> names = new List<string>();
            foreach (ModelItem a in mi.AncestorsAndSelf)
                names.Add(string.IsNullOrEmpty(a.DisplayName) ? a.ClassDisplayName : a.DisplayName);
            names.Reverse();
            if (names.Count > 5) names = new List<string>(new string[] { "..." }.Concat(names.Skip(names.Count - 5)));
            return string.Join(" > ", names.ToArray());
        }

        static Dictionary<string, object> Summary(ModelItem mi, List<string> extraProps)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["handle"] = Handle(mi);
            d["name"] = mi.DisplayName;
            d["class"] = mi.ClassDisplayName;
            if (mi.InstanceGuid != Guid.Empty) d["guid"] = mi.InstanceGuid.ToString();
            d["path"] = PathOf(mi);
            if (extraProps != null)
                foreach (string spec in extraProps)
                {
                    string cat, prop;
                    SplitSpec(spec, "Element", out cat, out prop);
                    d[cat + "|" + prop] = GetProp(mi, cat, prop);
                }
            return d;
        }

        static void SplitSpec(string spec, string defCat, out string cat, out string prop)
        {
            int i = spec.IndexOf('|');
            if (i > 0) { cat = spec.Substring(0, i).Trim(); prop = spec.Substring(i + 1).Trim(); }
            else { cat = defCat; prop = spec.Trim(); }
        }

        static ModelItemCollection ResolveItems(Dictionary<string, object> a, Document doc, bool allowEmpty)
        {
            ModelItemCollection result = new ModelItemCollection();
            List<string> unknown = new List<string>();

            foreach (string h in StrList(a, "handles"))
            {
                ModelItem mi;
                if (_byHandle.TryGetValue(h, out mi)) result.Add(mi); else unknown.Add(h);
            }

            List<string> gs = StrList(a, "guids");
            if (gs.Count > 0)
            {
                HashSet<Guid> set = new HashSet<Guid>();
                foreach (string g in gs) { Guid x; if (Guid.TryParse(g, out x)) set.Add(x); }
                foreach (ModelItem mi in doc.Models.RootItemDescendantsAndSelf)
                    if (set.Contains(mi.InstanceGuid)) result.Add(mi);
            }

            if (Bool(a, "use_selection", false))
                result.AddRange(doc.CurrentSelection.SelectedItems);

            string setName = Str(a, "selection_set");
            if (setName != null) result.AddRange(SetItems(doc, FindSet(doc, setName)));

            if (result.Count == 0 && !allowEmpty)
            {
                string msg = "No target items. Provide handles / guids / selection_set / use_selection=true.";
                if (unknown.Count > 0) msg += " Unknown/expired handles: " + string.Join(",", unknown.ToArray()) + " (search again).";
                throw new McpToolException(msg);
            }
            return result;
        }

        static ModelItemCollection ScopeItems(Document doc, string scope)
        {
            ModelItemCollection c = new ModelItemCollection();
            if (scope == "selection")
                foreach (ModelItem mi in doc.CurrentSelection.SelectedItems) c.AddRange(mi.DescendantsAndSelf);
            else
                c.AddRange(doc.Models.RootItemDescendantsAndSelf);
            return c;
        }

        static ModelItemCollection RunSearch(Document doc, string scope, SearchCondition cond)
        {
            Search s = new Search();
            if (scope == "selection") s.Selection.CopyFrom(doc.CurrentSelection.SelectedItems);
            else s.Selection.SelectAll();
            s.Locations = SearchLocations.DescendantsAndSelf;
            s.SearchConditions.Add(cond);
            return s.FindAll(doc, false);
        }

        // =============================================================== property helpers
        static string GetProp(ModelItem mi, string cat, string prop)
        {
            DataProperty dp = mi.PropertyCategories.FindPropertyByDisplayName(cat, prop);
            if (dp == null) return null;
            return VarStr(dp.Value);
        }

        static string VarStr(VariantData v)
        {
            if (v == null) return null;
            try
            {
                switch (v.DataType)
                {
                    case VariantDataType.None: return "";
                    case VariantDataType.DisplayString: return v.ToDisplayString();
                    case VariantDataType.IdentifierString: return v.ToIdentifierString();
                    case VariantDataType.Boolean: return v.ToBoolean() ? "Yes" : "No";
                    case VariantDataType.Int32: return v.ToInt32().ToString(CultureInfo.InvariantCulture);
                    case VariantDataType.Double: return v.ToDouble().ToString("0.####", CultureInfo.InvariantCulture);
                    case VariantDataType.DoubleLength: return v.ToDoubleLength().ToString("0.####", CultureInfo.InvariantCulture);
                    case VariantDataType.DoubleArea: return v.ToDoubleArea().ToString("0.####", CultureInfo.InvariantCulture);
                    case VariantDataType.DoubleVolume: return v.ToDoubleVolume().ToString("0.####", CultureInfo.InvariantCulture);
                    case VariantDataType.DoubleAngle: return v.ToDoubleAngle().ToString("0.####", CultureInfo.InvariantCulture);
                    case VariantDataType.DateTime: return v.ToDateTime().ToString("s", CultureInfo.InvariantCulture);
                    case VariantDataType.NamedConstant: return v.ToNamedConstant().DisplayName;
                }
            }
            catch { }
            string s = v.ToString();
            int i = s.IndexOf(':');
            return i >= 0 ? s.Substring(i + 1) : s;
        }

        static bool Match(string v, string target, string mode)
        {
            switch (mode)
            {
                case "exists": return v != null;
                case "empty": return v == null || v.Trim().Length == 0;
                case "not_empty": return v != null && v.Trim().Length > 0;
            }
            if (v == null) return mode == "not_equals";
            string t = target ?? "";
            double dv, dt;
            switch (mode)
            {
                case "equals": return string.Equals(v.Trim(), t.Trim(), StringComparison.OrdinalIgnoreCase);
                case "not_equals": return !string.Equals(v.Trim(), t.Trim(), StringComparison.OrdinalIgnoreCase);
                case "contains": return v.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0;
                case "starts_with": return v.StartsWith(t, StringComparison.OrdinalIgnoreCase);
                case "ends_with": return v.EndsWith(t, StringComparison.OrdinalIgnoreCase);
                case "regex": return Regex.IsMatch(v, t, RegexOptions.IgnoreCase);
                case "gt": return double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out dv) && double.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out dt) && dv > dt;
                case "lt": return double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out dv) && double.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out dt) && dv < dt;
            }
            throw new McpToolException("Unknown match mode: " + mode);
        }

        static bool Contains(string s, string part)
        {
            return s != null && s.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // =============================================================== files / CSV
        static string OutPath(string p, string ext)
        {
            p = Environment.ExpandEnvironmentVariables(p.Trim().Trim('"'));
            if (!Path.IsPathRooted(p))
                p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), p);
            if (ext != null && !p.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) p += ext;
            string dir = Path.GetDirectoryName(p);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            return p;
        }

        static string CsvPath(string p) { return OutPath(p, ".csv"); }

        static string InPath(string p)
        {
            p = Environment.ExpandEnvironmentVariables(p.Trim().Trim('"'));
            if (!File.Exists(p)) throw new McpToolException("File not found: " + p);
            return p;
        }

        static string Csv(string s)
        {
            if (s == null) return "";
            if (s.IndexOfAny(new char[] { ',', '"', '\n', '\r' }) >= 0) return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        static void WriteCsv(string path, List<string> header, List<List<string>> rows)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(string.Join(",", header.Select(x => Csv(x)).ToArray()));
            foreach (List<string> r in rows) sb.AppendLine(string.Join(",", r.Select(x => Csv(x)).ToArray()));
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        // =============================================================== color
        static NwColor ParseColor(string s)
        {
            s = (s ?? "").Trim().ToLowerInvariant();
            switch (s)
            {
                case "red": return new NwColor(1, 0, 0);
                case "green": return new NwColor(0, 0.8, 0);
                case "blue": return new NwColor(0, 0.3, 1);
                case "yellow": return new NwColor(1, 1, 0);
                case "orange": return new NwColor(1, 0.55, 0);
                case "purple": return new NwColor(0.6, 0.2, 0.8);
                case "cyan": return new NwColor(0, 1, 1);
                case "magenta": return new NwColor(1, 0, 1);
                case "white": return new NwColor(1, 1, 1);
                case "grey":
                case "gray": return new NwColor(0.5, 0.5, 0.5);
                case "black": return new NwColor(0, 0, 0);
            }
            string hex = s.TrimStart('#');
            int v;
            if (hex.Length == 6 && int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v))
                return new NwColor(((v >> 16) & 255) / 255.0, ((v >> 8) & 255) / 255.0, (v & 255) / 255.0);
            throw new McpToolException("Invalid color: " + s + " (use #RRGGBB or a basic color name)");
        }

        static string Hex(NwColor c)
        {
            return "#" + ((int)(c.R * 255)).ToString("X2") + ((int)(c.G * 255)).ToString("X2") + ((int)(c.B * 255)).ToString("X2");
        }

        // =============================================================== reflection helpers
        static object GetMember(object target, string name)
        {
            if (target == null) throw new McpToolException("Cannot read '" + name + "' of null");
            Type t = target as Type;
            bool isStatic = t != null;
            if (t == null) t = target.GetType();
            BindingFlags f = BindingFlags.Public | BindingFlags.IgnoreCase | BindingFlags.FlattenHierarchy | (isStatic ? BindingFlags.Static : BindingFlags.Instance);
            PropertyInfo pi = t.GetProperty(name, f);
            if (pi != null && pi.GetIndexParameters().Length == 0) return pi.GetValue(isStatic ? null : target, null);
            FieldInfo fi = t.GetField(name, f);
            if (fi != null) return fi.GetValue(isStatic ? null : target);
            MethodInfo mi = t.GetMethod(name, f, null, Type.EmptyTypes, null);
            if (mi != null) return mi.Invoke(isStatic ? null : target, null);
            throw new McpToolException("Member not found: " + t.Name + "." + name);
        }

        static void SetEnumProperty(object target, string prop, string value)
        {
            PropertyInfo pi = target.GetType().GetProperty(prop);
            if (pi == null) throw new McpToolException("Property not supported in this Navisworks version: " + prop);
            object v;
            try { v = Enum.Parse(pi.PropertyType, value, true); }
            catch { throw new McpToolException("Invalid value '" + value + "'. Allowed: " + string.Join(", ", Enum.GetNames(pi.PropertyType))); }
            pi.SetValue(target, v, null);
        }

        static object CallMethod(object target, string name, params object[] args)
        {
            Type t = target.GetType();
            foreach (MethodInfo m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (m.Name != name) continue;
                ParameterInfo[] ps = m.GetParameters();
                if (ps.Length != args.Length) continue;
                bool ok = true;
                for (int i = 0; i < ps.Length; i++)
                    if (args[i] != null && !ps[i].ParameterType.IsInstanceOfType(args[i])) { ok = false; break; }
                if (ok) return m.Invoke(target, args);
            }
            throw new McpToolException("Method not available in this Navisworks version: " + t.Name + "." + name);
        }
    }
}
