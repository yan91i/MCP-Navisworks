// Visibility, appearance, transforms, viewpoints, camera, sectioning, screenshots.
// C# 5 syntax only. Version-sensitive APIs are called via reflection so the DLL builds on 2024-2027.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using Autodesk.Navisworks.Api;
using NwApp = Autodesk.Navisworks.Api.Application;
using NwColor = Autodesk.Navisworks.Api.Color;

namespace NavisMcp
{
    public static partial class McpTools
    {
        static void RegisterView()
        {
            // ------------------------------------------------------------ Visibility & appearance
            _cat = "Visibility & Appearance";
            Add("set_visibility",
                "Hide / unhide / isolate (hide everything else) target items, or unhide_all.",
                ST(R("action"), P("action", "string", "hide | unhide | isolate | unhide_all")), SetVisibility);

            Add("color_items",
                "Override color (and optional transparency) of target items.",
                ST(R("color"),
                   P("color", "string", "#RRGGBB or red/green/blue/yellow/orange/purple/cyan/magenta/white/grey/black"),
                   P("transparency", "number", "0.0 (opaque) - 1.0 (invisible), optional")),
                ColorItems);

            Add("set_transparency",
                "Override transparency of target items only.",
                ST(R("transparency"), P("transparency", "number", "0.0 - 1.0")), SetTransparency);

            Add("color_by_property",
                "Color items automatically by distinct values of a property (like Appearance Profiler). Returns the color legend.",
                S(R("property"),
                  P("property", "string", "'Category|Property', e.g. 'Element|Category' or 'Element|CAT Code'"),
                  P("scope", "string", "all (default) | selection"),
                  P("empty_color", "string", "Color for items with empty value (default grey)"),
                  P("max_values", "integer", "Max distinct values colored (default 30)")),
                ColorByProperty);

            Add("reset_appearance",
                "Reset color/transparency overrides of target items, or of the whole model if no target. Optionally unhide all.",
                ST(null, P("unhide_all", "boolean", "Also unhide everything (default false)")), ResetAppearance);

            Add("get_hidden_items", "Count / list hidden items.", S(null, P("limit", "integer", "Max items listed (default 50)")), GetHiddenItems);

            // ------------------------------------------------------------ Transform
            _cat = "Transform";
            Add("move_items",
                "Move (translate) target items by a vector in document units (permanent override, like Item Tools > Move).",
                ST(R("offset"), P("offset", "array", "[dx, dy, dz]")), MoveItems);

            Add("reset_transform", "Reset transform overrides of target items (or all items if no target).", ST(null), ResetTransform);

            // ------------------------------------------------------------ View / camera
            _cat = "View";
            Add("zoom_to_selection", "Zoom the view to target items (or current selection).", ST(null), ZoomToSelection);

            Add("zoom_extents", "Zoom the view to the whole model (Zoom All).", S(null), ZoomExtents);

            Add("get_camera",
                "Current camera: position, rotation, look direction, up vector, projection, render style, lighting, field of view.",
                S(null), GetCamera);

            Add("set_camera",
                "Set camera position and/or look-at target (document units).",
                S(null,
                  P("position", "array", "[x, y, z]"),
                  P("target", "array", "[x, y, z] point to look at")),
                SetCamera);

            Add("look_from",
                "Standard view of the whole model or target items: top | bottom | front | back | left | right | iso (SE iso).",
                ST(R("direction"), P("direction", "string", "top | bottom | front | back | left | right | iso")), LookFrom);

            Add("set_render_style",
                "Set render style: FullRender | Shaded | Wireframe | HiddenLine.",
                S(R("style"), P("style", "string", "FullRender | Shaded | Wireframe | HiddenLine")), SetRenderStyle);

            Add("set_projection",
                "Set camera projection: Perspective | Orthographic.",
                S(R("projection"), P("projection", "string", "Perspective | Orthographic")), SetProjection);

            Add("set_lighting",
                "Set lighting style: None | Headlight | SceneLights | FullLights.",
                S(R("lighting"), P("lighting", "string", "None | Headlight | SceneLights | FullLights")), SetLighting);

            Add("screenshot",
                "Capture the current view as PNG; saves to file and returns the image to the AI (max_size scales the returned copy).",
                S(null,
                  P("path", "string", "Optional PNG path (default Documents\\NavisMcp\\shot_<time>.png)"),
                  P("width", "integer", "Render width (default current view width)"),
                  P("height", "integer", "Render height (default current view height)"),
                  P("max_size", "integer", "Max pixel size of image returned to AI (default 1024)"),
                  P("return_image", "boolean", "Return image content to the AI (default true)")),
                Screenshot);

            // ------------------------------------------------------------ Viewpoints
            _cat = "Viewpoints";
            Add("list_viewpoints", "List saved viewpoints (with folder path).", S(null), ListViewpoints);

            Add("goto_viewpoint", "Apply a saved viewpoint by name (exact, then contains).",
                S(R("name"), P("name", "string", "Viewpoint name or folder/name")), GotoViewpoint);

            Add("save_viewpoint", "Save the current view as a new saved viewpoint.",
                S(R("name"), P("name", "string", "Viewpoint name")), SaveViewpoint);

            Add("delete_viewpoint", "Delete a saved viewpoint by name.",
                S(R("name"), P("name", "string", "Viewpoint name")), DeleteViewpoint);

            Add("export_viewpoint_images",
                "Render every saved viewpoint (or those whose name contains 'filter') to PNG files in a folder.",
                S(R("folder"),
                  P("folder", "string", "Output folder"),
                  P("filter", "string", "Name contains (optional)"),
                  P("width", "integer", "Width (default 1600)"),
                  P("height", "integer", "Height (default 900)")),
                ExportViewpointImages);

            // ------------------------------------------------------------ Sectioning
            _cat = "Sectioning";
            Add("section_box_to_items",
                "Enable a section box around target items (with optional margin).",
                ST(null, P("margin", "number", "Extra margin in document units (default 0)")), SectionBoxToItems);

            Add("section_plane",
                "Enable a single horizontal (Z) section plane at a given elevation (e.g. level plan cut).",
                S(R("elevation"), P("elevation", "number", "Cut elevation in document units"),
                  P("keep", "string", "below (default, plan view) | above")),
                SectionPlane);

            Add("clear_section", "Disable sectioning in the current view.", S(null), ClearSection);

            Add("get_section_json", "Get the raw clipping-plane JSON of the current view.", S(null), GetSectionJson);

            Add("set_section_json", "Set clipping planes from raw JSON (as returned by get_section_json).",
                S(R("json"), P("json", "string", "Clipping plane JSON")), SetSectionJson);
        }

        // =============================================================== Visibility
        static object SetVisibility(Dictionary<string, object> a)
        {
            Document doc = Doc();
            string action = (Str(a, "action") ?? "").ToLowerInvariant();
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["action"] = action;
            if (action == "unhide_all") { doc.Models.ResetAllHidden(); return r; }

            ModelItemCollection items = ResolveItems(a, doc, false);
            if (action == "hide") doc.Models.SetHidden(items, true);
            else if (action == "unhide")
            {
                ModelItemCollection show = new ModelItemCollection();
                foreach (ModelItem mi in items) show.AddRange(mi.AncestorsAndSelf);
                doc.Models.SetHidden(show, false);
            }
            else if (action == "isolate")
            {
                HashSet<ModelItem> keep = new HashSet<ModelItem>();
                foreach (ModelItem mi in items) foreach (ModelItem an in mi.AncestorsAndSelf) keep.Add(an);
                ModelItemCollection hide = new ModelItemCollection();
                HashSet<ModelItem> visited = new HashSet<ModelItem>();
                foreach (ModelItem k in keep)
                {
                    ModelItem parent = k.Parent;
                    if (parent == null || !visited.Add(parent)) continue;
                    foreach (ModelItem sib in parent.Children) if (!keep.Contains(sib)) hide.Add(sib);
                }
                foreach (Model m in doc.Models)
                    if (m.RootItem != null && !keep.Contains(m.RootItem)) hide.Add(m.RootItem);
                doc.Models.ResetAllHidden();
                doc.Models.SetHidden(hide, true);
                r["hidden_branches"] = hide.Count;
            }
            else throw new McpToolException("action must be hide | unhide | isolate | unhide_all");
            r["items"] = items.Count;
            return r;
        }

        static object ColorItems(Dictionary<string, object> a)
        {
            Document doc = Doc();
            ModelItemCollection items = ResolveItems(a, doc, false);
            doc.Models.OverridePermanentColor(items, ParseColor(Str(a, "color")));
            double? t = Dbl(a, "transparency");
            if (t.HasValue) doc.Models.OverridePermanentTransparency(items, Math.Max(0, Math.Min(1, t.Value)));
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["colored"] = items.Count;
            return r;
        }

        static object SetTransparency(Dictionary<string, object> a)
        {
            Document doc = Doc();
            ModelItemCollection items = ResolveItems(a, doc, false);
            double t = Dbl(a, "transparency") ?? 0.5;
            doc.Models.OverridePermanentTransparency(items, Math.Max(0, Math.Min(1, t)));
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["items"] = items.Count;
            r["transparency"] = t;
            return r;
        }

        static readonly string[] Palette = {
            "#E6194B", "#3CB44B", "#4363D8", "#F58231", "#911EB4", "#42D4F4", "#F032E6", "#BFEF45", "#FABED4", "#469990",
            "#DCBEFF", "#9A6324", "#FFE119", "#800000", "#AAFFC3", "#808000", "#FFD8B1", "#000075", "#A9A9A9", "#00A86B" };

        static NwColor PaletteColor(int i)
        {
            if (i < Palette.Length) return ParseColor(Palette[i]);
            double h = (i * 0.618033988749895) % 1.0;
            System.Drawing.Color c = FromHsv(h * 360, 0.75, 0.9);
            return new NwColor(c.R / 255.0, c.G / 255.0, c.B / 255.0);
        }

        static System.Drawing.Color FromHsv(double h, double s, double v)
        {
            int hi = (int)Math.Floor(h / 60) % 6;
            double f = h / 60 - Math.Floor(h / 60);
            int V = (int)(v * 255), p = (int)(V * (1 - s)), q = (int)(V * (1 - f * s)), t = (int)(V * (1 - (1 - f) * s));
            switch (hi)
            {
                case 0: return System.Drawing.Color.FromArgb(V, t, p);
                case 1: return System.Drawing.Color.FromArgb(q, V, p);
                case 2: return System.Drawing.Color.FromArgb(p, V, t);
                case 3: return System.Drawing.Color.FromArgb(p, q, V);
                case 4: return System.Drawing.Color.FromArgb(t, p, V);
                default: return System.Drawing.Color.FromArgb(V, p, q);
            }
        }

        static object ColorByProperty(Dictionary<string, object> a)
        {
            Document doc = Doc();
            string cat, prop;
            SplitSpec(Str(a, "property") ?? "", "Element", out cat, out prop);
            string scope = (Str(a, "scope") ?? "all").ToLowerInvariant();
            int maxV = Int(a, "max_values", 30);
            Dictionary<string, ModelItemCollection> groups = new Dictionary<string, ModelItemCollection>();
            ModelItemCollection empties = new ModelItemCollection();
            foreach (ModelItem mi in RunSearch(doc, scope, SearchCondition.HasPropertyByDisplayName(cat, prop)))
            {
                string v = GetProp(mi, cat, prop);
                if (v == null || v.Trim().Length == 0) { empties.Add(mi); continue; }
                ModelItemCollection c;
                if (!groups.TryGetValue(v, out c)) { c = new ModelItemCollection(); groups[v] = c; }
                c.Add(mi);
            }
            List<object> legend = new List<object>();
            int i = 0;
            ModelItemCollection others = new ModelItemCollection();
            foreach (KeyValuePair<string, ModelItemCollection> g in groups.OrderByDescending(x => x.Value.Count))
            {
                if (i >= maxV) { others.AddRange(g.Value); continue; }
                NwColor col = PaletteColor(i++);
                doc.Models.OverridePermanentColor(g.Value, col);
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["value"] = g.Key; d["count"] = g.Value.Count; d["color"] = Hex(col);
                legend.Add(d);
            }
            NwColor grey = ParseColor(Str(a, "empty_color") ?? "grey");
            if (empties.Count > 0) doc.Models.OverridePermanentColor(empties, grey);
            if (others.Count > 0) doc.Models.OverridePermanentColor(others, ParseColor("white"));
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["property"] = cat + "|" + prop;
            r["distinct_values"] = groups.Count;
            r["empty_items"] = empties.Count;
            r["other_values_white"] = others.Count;
            r["legend"] = legend;
            return r;
        }

        static object ResetAppearance(Dictionary<string, object> a)
        {
            Document doc = Doc();
            ModelItemCollection items = ResolveItems(a, doc, true);
            if (items.Count > 0) doc.Models.ResetPermanentMaterials(items);
            else doc.Models.ResetAllPermanentMaterials();
            if (Bool(a, "unhide_all", false)) doc.Models.ResetAllHidden();
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["reset"] = items.Count > 0 ? (object)items.Count : "all";
            return r;
        }

        static object GetHiddenItems(Dictionary<string, object> a)
        {
            Document doc = Doc();
            int limit = Int(a, "limit", 50);
            List<object> list = new List<object>();
            int count = 0;
            foreach (ModelItem mi in doc.Models.RootItemDescendantsAndSelf)
            {
                if (!mi.IsHidden) continue;
                if (mi.Parent != null && mi.Parent.IsHidden) continue; // report top-most hidden only
                count++;
                if (list.Count < limit) list.Add(Summary(mi, null));
            }
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["hidden_branches"] = count;
            r["items"] = list;
            return r;
        }

        // =============================================================== Transform
        static object MoveItems(Dictionary<string, object> a)
        {
            Document doc = Doc();
            ModelItemCollection items = ResolveItems(a, doc, false);
            double[] v = Vec3(a, "offset");
            Transform3D t = Transform3D.CreateTranslation(new Vector3D(v[0], v[1], v[2]));
            doc.Models.OverridePermanentTransform(items, t, true);
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["moved"] = items.Count;
            r["offset"] = v;
            return r;
        }

        static object ResetTransform(Dictionary<string, object> a)
        {
            Document doc = Doc();
            ModelItemCollection items = ResolveItems(a, doc, true);
            if (items.Count > 0) doc.Models.ResetPermanentTransform(items);
            else doc.Models.ResetAllPermanentTransforms();
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["reset"] = items.Count > 0 ? (object)items.Count : "all";
            return r;
        }

        // =============================================================== View / camera
        static void ZoomCurrentSelection()
        {
            Autodesk.Navisworks.Api.ComApi.ComApiBridge.State.ZoomInCurViewOnCurSel();
        }

        static void ZoomToItems(Document doc, IEnumerable<ModelItem> items)
        {
            BoundingBox3D box = UnionBox(items);
            if (box == null) throw new McpToolException("Items have no geometry.");
            Viewpoint v = doc.CurrentViewpoint.CreateCopy();
            try
            {
                CallMethod(v, "ZoomBox", box);
                doc.CurrentViewpoint.CopyFrom(v);
            }
            catch
            {
                ModelItemCollection keep = new ModelItemCollection();
                keep.CopyFrom(doc.CurrentSelection.SelectedItems);
                ModelItemCollection c = new ModelItemCollection();
                c.AddRange(items);
                doc.CurrentSelection.CopyFrom(c);
                ZoomCurrentSelection();
                doc.CurrentSelection.CopyFrom(keep);
            }
        }

        static object ZoomToSelection(Dictionary<string, object> a)
        {
            Document doc = Doc();
            ModelItemCollection items = ResolveItems(a, doc, true);
            if (items.Count > 0) doc.CurrentSelection.CopyFrom(items);
            if (doc.CurrentSelection.SelectedItems.Count == 0) throw new McpToolException("Nothing selected to zoom to.");
            ZoomCurrentSelection();
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["zoomed_to"] = doc.CurrentSelection.SelectedItems.Count;
            return r;
        }

        static object ZoomExtents(Dictionary<string, object> a)
        {
            Document doc = Doc();
            ZoomToItems(doc, Roots(doc));
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["zoomed"] = "all";
            return r;
        }

        static object GetCamera(Dictionary<string, object> a)
        {
            Document doc = Doc();
            Viewpoint v = doc.CurrentViewpoint.Value;
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["position"] = Pt(v.Position);
            Rotation3D q = v.Rotation;
            r["rotation_quaternion"] = new double[] { R4(q.A), R4(q.B), R4(q.C), R4(q.D) };
            Vector3D dir = Rotate(q, new Vector3D(0, 0, -1));
            Vector3D up = Rotate(q, new Vector3D(0, 1, 0));
            r["look_direction"] = new double[] { R4(dir.X), R4(dir.Y), R4(dir.Z) };
            r["up_vector"] = new double[] { R4(up.X), R4(up.Y), R4(up.Z) };
            r["projection"] = Try(delegate { return GetMember(v, "Projection").ToString(); });
            r["render_style"] = Try(delegate { return GetMember(v, "RenderStyle").ToString(); });
            r["lighting"] = Try(delegate { return GetMember(v, "Lighting").ToString(); });
            r["height_field"] = Try(delegate { return GetMember(v, "HeightField"); });
            r["focal_distance"] = Try(delegate { return GetMember(v, "FocalDistance"); });
            return r;
        }

        /// <summary>Rotate vector by quaternion (A,B,C = x,y,z ; D = w).</summary>
        static Vector3D Rotate(Rotation3D q, Vector3D v)
        {
            double x = q.A, y = q.B, z = q.C, w = q.D;
            double tx = 2 * (y * v.Z - z * v.Y), ty = 2 * (z * v.X - x * v.Z), tz = 2 * (x * v.Y - y * v.X);
            return new Vector3D(v.X + w * tx + (y * tz - z * ty), v.Y + w * ty + (z * tx - x * tz), v.Z + w * tz + (x * ty - y * tx));
        }

        static void ApplyCamera(Document doc, double[] pos, double[] target)
        {
            Viewpoint v = doc.CurrentViewpoint.CreateCopy();
            if (pos != null) v.Position = new Point3D(pos[0], pos[1], pos[2]);
            if (target != null) v.PointAt(new Point3D(target[0], target[1], target[2]));
            doc.CurrentViewpoint.CopyFrom(v);
        }

        static object SetCamera(Dictionary<string, object> a)
        {
            Document doc = Doc();
            double[] pos = a.ContainsKey("position") ? Vec3(a, "position") : null;
            double[] tgt = a.ContainsKey("target") ? Vec3(a, "target") : null;
            if (pos == null && tgt == null) throw new McpToolException("Provide position and/or target.");
            ApplyCamera(doc, pos, tgt);
            return GetCamera(a);
        }

        static object LookFrom(Dictionary<string, object> a)
        {
            Document doc = Doc();
            ModelItemCollection items = ResolveItems(a, doc, true);
            BoundingBox3D b = UnionBox(items.Count > 0 ? (IEnumerable<ModelItem>)items : Roots(doc));
            if (b == null) throw new McpToolException("No geometry.");
            double cx = (b.Min.X + b.Max.X) / 2, cy = (b.Min.Y + b.Max.Y) / 2, cz = (b.Min.Z + b.Max.Z) / 2;
            double dx = b.Max.X - b.Min.X, dy = b.Max.Y - b.Min.Y, dz = b.Max.Z - b.Min.Z;
            double d = Math.Max(1e-3, Math.Sqrt(dx * dx + dy * dy + dz * dz)) * 1.2;
            double[] dir;
            switch ((Str(a, "direction") ?? "").ToLowerInvariant())
            {
                case "top": dir = new double[] { 0, -0.001, 1 }; break;
                case "bottom": dir = new double[] { 0, -0.001, -1 }; break;
                case "front": dir = new double[] { 0, -1, 0 }; break;
                case "back": dir = new double[] { 0, 1, 0 }; break;
                case "left": dir = new double[] { -1, 0, 0 }; break;
                case "right": dir = new double[] { 1, 0, 0 }; break;
                case "iso": dir = new double[] { 1, -1, 1 }; break;
                default: throw new McpToolException("direction must be top | bottom | front | back | left | right | iso");
            }
            double l = Math.Sqrt(dir[0] * dir[0] + dir[1] * dir[1] + dir[2] * dir[2]);
            ApplyCamera(doc, new double[] { cx + dir[0] / l * d, cy + dir[1] / l * d, cz + dir[2] / l * d }, new double[] { cx, cy, cz });
            try { ZoomToItems(doc, items.Count > 0 ? (IEnumerable<ModelItem>)items : Roots(doc)); } catch { }
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["direction"] = Str(a, "direction");
            return r;
        }

        static object SetViewEnum(string prop, string value)
        {
            Document doc = Doc();
            Viewpoint v = doc.CurrentViewpoint.CreateCopy();
            SetEnumProperty(v, prop, value);
            doc.CurrentViewpoint.CopyFrom(v);
            Dictionary<string, object> r = new Dictionary<string, object>();
            r[prop] = GetMember(doc.CurrentViewpoint.Value, prop).ToString();
            return r;
        }

        static object SetRenderStyle(Dictionary<string, object> a) { return SetViewEnum("RenderStyle", Str(a, "style") ?? ""); }
        static object SetProjection(Dictionary<string, object> a) { return SetViewEnum("Projection", Str(a, "projection") ?? ""); }
        static object SetLighting(Dictionary<string, object> a) { return SetViewEnum("Lighting", Str(a, "lighting") ?? ""); }

        // ------------------------------------------------------------ screenshot
        static Bitmap RenderView(Document doc, int w, int h)
        {
            object view = doc.ActiveView;
            if (view == null) throw new McpToolException("No active view.");
            if (w <= 0) w = Convert.ToInt32(Try(delegate { return GetMember(view, "Width"); }) ?? 1280);
            if (h <= 0) h = Convert.ToInt32(Try(delegate { return GetMember(view, "Height"); }) ?? 720);
            Type styleType = typeof(Document).Assembly.GetType("Autodesk.Navisworks.Api.ImageGenerationStyle");
            if (styleType == null) throw new McpToolException("Image generation not supported in this Navisworks version.");
            object style;
            try { style = Enum.Parse(styleType, "ScenePlusOverlay", true); }
            catch { style = Enum.Parse(styleType, "Scene", true); }
            Bitmap bmp = CallMethod(view, "GenerateImage", style, w, h) as Bitmap;
            if (bmp == null) throw new McpToolException("GenerateImage returned nothing.");
            return bmp;
        }

        static object Screenshot(Dictionary<string, object> a)
        {
            Document doc = Doc();
            string path = Str(a, "path");
            if (path == null)
                path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "NavisMcp", "shot_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
            path = OutPath(path, ".png");
            int maxSize = Int(a, "max_size", 1024);
            McpImageResult res = new McpImageResult();
            Dictionary<string, object> info = new Dictionary<string, object>();
            using (Bitmap bmp = RenderView(doc, Int(a, "width", 0), Int(a, "height", 0)))
            {
                bmp.Save(path, ImageFormat.Png);
                info["file"] = path;
                info["size"] = new int[] { bmp.Width, bmp.Height };
                if (Bool(a, "return_image", true))
                {
                    double s = Math.Min(1.0, (double)maxSize / Math.Max(bmp.Width, bmp.Height));
                    using (Bitmap small = new Bitmap(bmp, new System.Drawing.Size(Math.Max(1, (int)(bmp.Width * s)), Math.Max(1, (int)(bmp.Height * s)))))
                    using (MemoryStream ms = new MemoryStream())
                    {
                        small.Save(ms, ImageFormat.Png);
                        res.Base64 = Convert.ToBase64String(ms.ToArray());
                    }
                }
            }
            res.Info = info;
            return res;
        }

        // =============================================================== Viewpoints
        static void CollectViewpoints(object coll, string prefix, List<KeyValuePair<string, SavedViewpoint>> outList)
        {
            IEnumerable e = coll as IEnumerable;
            if (e == null) return;
            foreach (object o in e)
            {
                SavedViewpoint vp = o as SavedViewpoint;
                if (vp != null) { outList.Add(new KeyValuePair<string, SavedViewpoint>(prefix + vp.DisplayName, vp)); continue; }
                GroupItem g = o as GroupItem;
                if (g != null) CollectViewpoints(g.Children, prefix + g.DisplayName + " / ", outList);
            }
        }

        static List<KeyValuePair<string, SavedViewpoint>> AllViewpoints(Document doc)
        {
            List<KeyValuePair<string, SavedViewpoint>> vps = new List<KeyValuePair<string, SavedViewpoint>>();
            CollectViewpoints(doc.SavedViewpoints.Value, "", vps);
            return vps;
        }

        static KeyValuePair<string, SavedViewpoint> FindViewpoint(Document doc, string name)
        {
            List<KeyValuePair<string, SavedViewpoint>> vps = AllViewpoints(doc);
            foreach (KeyValuePair<string, SavedViewpoint> kv in vps)
                if (string.Equals(kv.Value.DisplayName, name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase)) return kv;
            foreach (KeyValuePair<string, SavedViewpoint> kv in vps)
                if (Contains(kv.Key, name)) return kv;
            throw new McpToolException("Viewpoint not found: " + name);
        }

        static object ListViewpoints(Dictionary<string, object> a)
        {
            Document doc = Doc();
            List<KeyValuePair<string, SavedViewpoint>> vps = AllViewpoints(doc);
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["count"] = vps.Count;
            r["viewpoints"] = vps.Select(kv => (object)kv.Key).ToList();
            return r;
        }

        static object GotoViewpoint(Dictionary<string, object> a)
        {
            Document doc = Doc();
            KeyValuePair<string, SavedViewpoint> hit = FindViewpoint(doc, Str(a, "name") ?? "");
            doc.SavedViewpoints.CurrentSavedViewpoint = hit.Value;
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["applied"] = hit.Key;
            return r;
        }

        static object SaveViewpoint(Dictionary<string, object> a)
        {
            Document doc = Doc();
            SavedViewpoint sv = new SavedViewpoint(doc.CurrentViewpoint.CreateCopy());
            sv.DisplayName = Str(a, "name");
            doc.SavedViewpoints.AddCopy(sv);
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["saved"] = sv.DisplayName;
            return r;
        }

        static void RemoveSavedItem(object docSaved, SavedItem item)
        {
            GroupItem parent = item.Parent;
            if (parent != null)
            {
                int idx = -1, i = 0;
                foreach (SavedItem si in parent.Children) { if (si == item || si.Equals(item)) { idx = i; break; } i++; }
                if (idx >= 0) try { CallMethod(docSaved, "Remove", parent, idx); return; } catch (Exception) { }
            }
            CallMethod(docSaved, "Remove", item);
        }

        static object DeleteViewpoint(Dictionary<string, object> a)
        {
            Document doc = Doc();
            KeyValuePair<string, SavedViewpoint> hit = FindViewpoint(doc, Str(a, "name") ?? "");
            RemoveSavedItem(doc.SavedViewpoints, hit.Value);
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["deleted"] = hit.Key;
            return r;
        }

        static object ExportViewpointImages(Dictionary<string, object> a)
        {
            Document doc = Doc();
            string folder = Environment.ExpandEnvironmentVariables(Str(a, "folder") ?? "");
            Directory.CreateDirectory(folder);
            string filter = Str(a, "filter");
            int w = Int(a, "width", 1600), h = Int(a, "height", 900);
            List<string> files = new List<string>();
            foreach (KeyValuePair<string, SavedViewpoint> kv in AllViewpoints(doc))
            {
                if (filter != null && !Contains(kv.Key, filter)) continue;
                doc.SavedViewpoints.CurrentSavedViewpoint = kv.Value;
                System.Windows.Forms.Application.DoEvents();
                string name = kv.Key;
                foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
                string p = Path.Combine(folder, name.Replace(" / ", "__") + ".png");
                using (Bitmap bmp = RenderView(doc, w, h)) bmp.Save(p, ImageFormat.Png);
                files.Add(p);
            }
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["count"] = files.Count;
            r["files"] = files;
            return r;
        }

        // =============================================================== Sectioning (View.Get/SetClippingPlanes JSON)
        static string GetClipJson(Document doc)
        {
            return CallMethod(doc.ActiveView, "GetClippingPlanes") as string;
        }

        static void SetClipJson(Document doc, string json)
        {
            CallMethod(doc.ActiveView, "SetClippingPlanes", json);
        }

        static string D(double v) { return v.ToString("R", CultureInfo.InvariantCulture); }

        static object SectionBoxToItems(Dictionary<string, object> a)
        {
            Document doc = Doc();
            ModelItemCollection items = ResolveItems(a, doc, true);
            if (items.Count == 0) items = doc.CurrentSelection.SelectedItems;
            BoundingBox3D b = UnionBox(items.Count > 0 ? (IEnumerable<ModelItem>)items : Roots(doc));
            if (b == null) throw new McpToolException("No geometry.");
            double m = Dbl(a, "margin") ?? 0;
            string json = "{\"Type\":\"ClipPlaneSet\",\"Version\":1,\"OrientedBox\":{\"Type\":\"OrientedBox3D\",\"Version\":1,\"Box\":[[" +
                D(b.Min.X - m) + "," + D(b.Min.Y - m) + "," + D(b.Min.Z - m) + "],[" +
                D(b.Max.X + m) + "," + D(b.Max.Y + m) + "," + D(b.Max.Z + m) + "]],\"Rotation\":[0,0,0]},\"Enabled\":true}";
            SetClipJson(doc, json);
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["section_box"] = BoxInfo(b);
            r["margin"] = m;
            return r;
        }

        static object SectionPlane(Dictionary<string, object> a)
        {
            Document doc = Doc();
            double z = Dbl(a, "elevation") ?? 0;
            bool above = (Str(a, "keep") ?? "below").ToLowerInvariant() == "above";
            // plane: normal . p = distance ; keeps the side opposite to normal
            string normal = above ? "[0,0,-1]" : "[0,0,1]";
            string dist = above ? D(-z) : D(z);
            string json = "{\"Type\":\"ClipPlaneSet\",\"Version\":1,\"Planes\":[{\"Type\":\"ClipPlane\",\"Version\":1,\"Normal\":" + normal +
                          ",\"Distance\":" + dist + ",\"Enabled\":true}],\"Linked\":false,\"Current\":0,\"Mode\":\"Planes\",\"Enabled\":true}";
            SetClipJson(doc, json);
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["elevation"] = z;
            r["keep"] = above ? "above" : "below";
            r["note"] = "If the wrong side is kept, call again with the other 'keep' value.";
            return r;
        }

        static object ClearSection(Dictionary<string, object> a)
        {
            Document doc = Doc();
            string json = GetClipJson(doc);
            Dictionary<string, object> d = string.IsNullOrEmpty(json) ? null : McpProtocol.Deserialize(json) as Dictionary<string, object>;
            if (d == null) d = new Dictionary<string, object>();
            d["Enabled"] = false;
            if (!d.ContainsKey("Type")) d["Type"] = "ClipPlaneSet";
            if (!d.ContainsKey("Version")) d["Version"] = 1;
            SetClipJson(doc, McpProtocol.Serialize(d));
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["section_enabled"] = false;
            return r;
        }

        static object GetSectionJson(Dictionary<string, object> a)
        {
            Document doc = Doc();
            Dictionary<string, object> r = new Dictionary<string, object>();
            r["json"] = GetClipJson(doc);
            return r;
        }

        static object SetSectionJson(Dictionary<string, object> a)
        {
            Document doc = Doc();
            SetClipJson(doc, Str(a, "json"));
            return GetSectionJson(a);
        }
    }
}
