// MCP "Streamable HTTP" transport on a raw TcpListener bound to 127.0.0.1
// (no HttpListener -> no URL ACL / admin rights; no Node/Python bridge).
// All Navisworks API calls are marshalled to the Navisworks UI thread.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace NavisMcp
{
    public static class McpHost
    {
        public const int DefaultPort = 3900;
        public const string ServerName = "navisworks-mcp";
        public const string ServerVersion = "2.0.0";

        static TcpListener _listener;
        static Thread _acceptThread;
        static volatile bool _running;
        static Control _ui;
        static int _port;
        static readonly object _logLock = new object();

        public static bool IsRunning { get { return _running; } }
        public static int Port { get { return _port == 0 ? ConfiguredPort() : _port; } }
        public static string Url { get { return "http://127.0.0.1:" + Port + "/mcp"; } }

        public static int ConfiguredPort()
        {
            string s = Environment.GetEnvironmentVariable("NAVIS_MCP_PORT");
            int p;
            if (!string.IsNullOrEmpty(s) && int.TryParse(s, out p) && p > 0 && p < 65536) return p;
            return DefaultPort;
        }

        /// <summary>Must be called on the Navisworks UI thread.</summary>
        public static void Start(int port)
        {
            if (_running) return;
            if (_ui == null)
            {
                _ui = new Control();
                IntPtr h = _ui.Handle; // create handle on UI thread
            }
            _listener = new TcpListener(IPAddress.Loopback, port);
            _listener.Start();
            _port = port;
            _running = true;
            _acceptThread = new Thread(AcceptLoop);
            _acceptThread.IsBackground = true;
            _acceptThread.Name = "NavisMcp-Accept";
            _acceptThread.Start();
            Log("MCP server started at " + Url + " (" + McpTools.Count + " tools)");
        }

        public static void Stop()
        {
            if (!_running) return;
            _running = false;
            try { _listener.Stop(); } catch { }
            Log("MCP server stopped");
        }

        public static object RunOnUi(Func<object> func)
        {
            if (_ui == null || !_ui.InvokeRequired) return func();
            object result = null;
            Exception error = null;
            _ui.Invoke((MethodInvoker)delegate
            {
                try { result = func(); }
                catch (Exception ex) { error = ex; }
            });
            if (error != null) throw error;
            return result;
        }

        public static void Log(string msg)
        {
            try
            {
                lock (_logLock)
                {
                    string path = Path.Combine(Path.GetTempPath(), "NavisMcp.log");
                    File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch { }
        }

        // ---------------------------------------------------------------- HTTP
        static void AcceptLoop()
        {
            while (_running)
            {
                try
                {
                    TcpClient c = _listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(HandleClient, c);
                }
                catch (Exception ex)
                {
                    if (_running) Log("Accept error: " + ex.Message);
                    else break;
                }
            }
        }

        static int FindHeaderEnd(byte[] b, int len)
        {
            for (int i = 0; i + 3 < len; i++)
                if (b[i] == 13 && b[i + 1] == 10 && b[i + 2] == 13 && b[i + 3] == 10) return i;
            return -1;
        }

        static void HandleClient(object state)
        {
            TcpClient client = (TcpClient)state;
            try
            {
                client.ReceiveTimeout = 120000;
                client.SendTimeout = 120000;
                NetworkStream ns = client.GetStream();

                MemoryStream buf = new MemoryStream();
                byte[] tmp = new byte[16384];
                int headerEnd = -1;
                while (headerEnd < 0)
                {
                    int n = ns.Read(tmp, 0, tmp.Length);
                    if (n <= 0) return;
                    buf.Write(tmp, 0, n);
                    headerEnd = FindHeaderEnd(buf.GetBuffer(), (int)buf.Length);
                    if (buf.Length > 8 * 1024 * 1024) return;
                }

                byte[] all = buf.GetBuffer();
                string headerText = Encoding.ASCII.GetString(all, 0, headerEnd);
                string[] lines = headerText.Split(new string[] { "\r\n" }, StringSplitOptions.None);
                string[] reqLine = lines[0].Split(' ');
                string method = reqLine.Length > 0 ? reqLine[0].ToUpperInvariant() : "";
                string path = reqLine.Length > 1 ? reqLine[1] : "/";
                int q = path.IndexOf('?');
                if (q >= 0) path = path.Substring(0, q);

                Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 1; i < lines.Length; i++)
                {
                    int c = lines[i].IndexOf(':');
                    if (c > 0) headers[lines[i].Substring(0, c).Trim()] = lines[i].Substring(c + 1).Trim();
                }

                int contentLength = 0;
                string cl;
                if (headers.TryGetValue("Content-Length", out cl)) int.TryParse(cl, out contentLength);

                int bodyStart = headerEnd + 4;
                int have = (int)buf.Length - bodyStart;
                byte[] body = new byte[contentLength];
                int read = Math.Min(Math.Max(have, 0), contentLength);
                if (read > 0) Array.Copy(all, bodyStart, body, 0, read);
                while (read < contentLength)
                {
                    int n = ns.Read(body, read, contentLength - read);
                    if (n <= 0) break;
                    read += n;
                }
                Route(ns, method, path, Encoding.UTF8.GetString(body, 0, read));
            }
            catch (Exception ex) { Log("Client error: " + ex.Message); }
            finally { try { client.Close(); } catch { } }
        }

        static void Route(NetworkStream ns, string method, string path, string body)
        {
            if (method == "OPTIONS") { Send(ns, 204, "No Content", null, null, null); return; }

            if (method == "GET" && (path == "/" || path == "/health"))
            {
                Send(ns, 200, "OK", "application/json; charset=utf-8",
                     "{\"status\":\"ok\",\"server\":\"" + ServerName + "\",\"version\":\"" + ServerVersion +
                     "\",\"tools\":" + McpTools.Count + ",\"mcp\":\"" + Url + "\",\"tool_list\":\"http://127.0.0.1:" + Port + "/tools\"}", null);
                return;
            }
            if (method == "GET" && path == "/tools")
            {
                Send(ns, 200, "OK", "text/html; charset=utf-8", McpTools.ToolsHtml(), null);
                return;
            }
            if (method == "GET" && path == "/tools.json")
            {
                Send(ns, 200, "OK", "application/json; charset=utf-8", McpProtocol.Serialize(McpTools.ListTools()), null);
                return;
            }
            if (method == "GET" && path == "/tools.md")
            {
                Send(ns, 200, "OK", "text/markdown; charset=utf-8", McpTools.ToolsMarkdown(), null);
                return;
            }

            if (path != "/mcp" && path != "/mcp/") { Send(ns, 404, "Not Found", "text/plain", "Not Found. Use " + Url, null); return; }
            if (method == "GET") { Send(ns, 405, "Method Not Allowed", "text/plain", "SSE stream not supported", null); return; }
            if (method == "DELETE") { Send(ns, 200, "OK", null, null, null); return; }
            if (method != "POST") { Send(ns, 405, "Method Not Allowed", "text/plain", "Use POST", null); return; }

            string sessionId = null;
            string response = McpProtocol.HandleBody(body, ref sessionId);
            if (response == null) Send(ns, 202, "Accepted", null, null, sessionId);
            else Send(ns, 200, "OK", "application/json; charset=utf-8", response, sessionId);
        }

        static void Send(NetworkStream ns, int code, string reason, string contentType, string body, string sessionId)
        {
            byte[] b = body == null ? new byte[0] : Encoding.UTF8.GetBytes(body);
            StringBuilder sb = new StringBuilder();
            sb.Append("HTTP/1.1 ").Append(code).Append(' ').Append(reason).Append("\r\n");
            if (contentType != null) sb.Append("Content-Type: ").Append(contentType).Append("\r\n");
            sb.Append("Content-Length: ").Append(b.Length).Append("\r\n");
            sb.Append("Access-Control-Allow-Origin: *\r\n");
            sb.Append("Access-Control-Allow-Headers: *\r\n");
            sb.Append("Access-Control-Allow-Methods: GET, POST, DELETE, OPTIONS\r\n");
            sb.Append("Access-Control-Expose-Headers: Mcp-Session-Id\r\n");
            if (sessionId != null) sb.Append("Mcp-Session-Id: ").Append(sessionId).Append("\r\n");
            sb.Append("Connection: close\r\n\r\n");
            byte[] h = Encoding.ASCII.GetBytes(sb.ToString());
            ns.Write(h, 0, h.Length);
            if (b.Length > 0) ns.Write(b, 0, b.Length);
            ns.Flush();
        }
    }

    /// <summary>Tool result carrying an image (MCP image content block).</summary>
    public class McpImageResult
    {
        public string Base64;
        public string MimeType = "image/png";
        public object Info;
    }

    // ==================================================================== JSON-RPC / MCP
    public static class McpProtocol
    {
        static JavaScriptSerializer NewSerializer()
        {
            JavaScriptSerializer s = new JavaScriptSerializer();
            s.MaxJsonLength = int.MaxValue;
            s.RecursionLimit = 256;
            return s;
        }

        public static string Serialize(object o) { return NewSerializer().Serialize(o); }
        public static object Deserialize(string s) { return NewSerializer().DeserializeObject(s); }

        public static string HandleBody(string body, ref string sessionId)
        {
            object parsed;
            try { parsed = Deserialize(body); }
            catch (Exception ex) { return Serialize(Error(null, -32700, "Parse error: " + ex.Message)); }

            object[] batch = parsed as object[];
            if (batch != null)
            {
                List<object> responses = new List<object>();
                foreach (object m in batch)
                {
                    object r = HandleMessage(m as Dictionary<string, object>, ref sessionId);
                    if (r != null) responses.Add(r);
                }
                return responses.Count == 0 ? null : Serialize(responses);
            }
            object single = HandleMessage(parsed as Dictionary<string, object>, ref sessionId);
            return single == null ? null : Serialize(single);
        }

        static Dictionary<string, object> HandleMessage(Dictionary<string, object> msg, ref string sessionId)
        {
            if (msg == null) return Error(null, -32600, "Invalid Request");
            object id = msg.ContainsKey("id") ? msg["id"] : null;
            bool isNotification = !msg.ContainsKey("id");
            string method = msg.ContainsKey("method") ? msg["method"] as string : null;
            if (method == null) return null;
            Dictionary<string, object> p = msg.ContainsKey("params") ? msg["params"] as Dictionary<string, object> : null;
            if (p == null) p = new Dictionary<string, object>();
            if (isNotification) return null;

            try
            {
                switch (method)
                {
                    case "initialize":
                        {
                            string ver = p.ContainsKey("protocolVersion") ? p["protocolVersion"] as string : null;
                            if (string.IsNullOrEmpty(ver)) ver = "2025-03-26";
                            sessionId = Guid.NewGuid().ToString("N");
                            Dictionary<string, object> toolsCap = new Dictionary<string, object>();
                            toolsCap["listChanged"] = false;
                            Dictionary<string, object> caps = new Dictionary<string, object>();
                            caps["tools"] = toolsCap;
                            Dictionary<string, object> info = new Dictionary<string, object>();
                            info["name"] = McpHost.ServerName;
                            info["version"] = McpHost.ServerVersion;
                            Dictionary<string, object> res = new Dictionary<string, object>();
                            res["protocolVersion"] = ver;
                            res["capabilities"] = caps;
                            res["serverInfo"] = info;
                            res["instructions"] =
                                "Controls the running Autodesk Navisworks session. Call list_tools for a categorized overview. " +
                                "Typical flow: get_model_info -> search_items / get_selection / list_selection_sets -> get_properties -> " +
                                "select / color / hide / zoom / section / clash / timeliner tools. " +
                                "Items are referenced by 'handle' strings returned by tools (valid for the open document), by GUID, " +
                                "by selection_set name, or use_selection=true. Properties use 'Category|Property' display names, e.g. 'Element|CAT Code'. " +
                                "Lengths are in document units (see get_model_info).";
                            return Result(id, res);
                        }
                    case "ping":
                        return Result(id, new Dictionary<string, object>());
                    case "tools/list":
                        {
                            Dictionary<string, object> res = new Dictionary<string, object>();
                            res["tools"] = McpTools.ListTools();
                            return Result(id, res);
                        }
                    case "tools/call":
                        {
                            string name = p.ContainsKey("name") ? p["name"] as string : null;
                            Dictionary<string, object> args = p.ContainsKey("arguments") ? p["arguments"] as Dictionary<string, object> : null;
                            if (args == null) args = new Dictionary<string, object>();
                            return Result(id, CallTool(name, args));
                        }
                    case "resources/list":
                        {
                            Dictionary<string, object> res = new Dictionary<string, object>();
                            res["resources"] = new object[0];
                            return Result(id, res);
                        }
                    case "prompts/list":
                        {
                            Dictionary<string, object> res = new Dictionary<string, object>();
                            res["prompts"] = new object[0];
                            return Result(id, res);
                        }
                    default:
                        return Error(id, -32601, "Method not found: " + method);
                }
            }
            catch (Exception ex)
            {
                McpHost.Log("RPC error: " + ex);
                return Error(id, -32603, ex.Message);
            }
        }

        static Dictionary<string, object> CallTool(string name, Dictionary<string, object> args)
        {
            ToolDef tool = McpTools.Find(name);
            List<object> blocks = new List<object>();
            bool isError = false;
            if (tool == null)
            {
                blocks.Add(TextBlock("Unknown tool: " + name + ". Call list_tools to see available tools."));
                isError = true;
            }
            else
            {
                try
                {
                    object r = McpHost.RunOnUi(delegate { return tool.Handler(args); });
                    McpImageResult img = r as McpImageResult;
                    if (img != null)
                    {
                        blocks.Add(TextBlock(Serialize(img.Info)));
                        if (img.Base64 != null)
                        {
                            Dictionary<string, object> im = new Dictionary<string, object>();
                            im["type"] = "image";
                            im["data"] = img.Base64;
                            im["mimeType"] = img.MimeType;
                            blocks.Add(im);
                        }
                    }
                    else blocks.Add(TextBlock(Serialize(r)));
                }
                catch (Exception ex)
                {
                    Exception e = ex;
                    while ((e is TargetInvocationException || e is AggregateException) && e.InnerException != null) e = e.InnerException;
                    blocks.Add(TextBlock("Error: " + e.Message));
                    isError = true;
                    McpHost.Log("Tool " + name + " failed: " + e);
                }
            }
            Dictionary<string, object> res = new Dictionary<string, object>();
            res["content"] = blocks;
            res["isError"] = isError;
            return res;
        }

        static Dictionary<string, object> TextBlock(string text)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["type"] = "text";
            d["text"] = text;
            return d;
        }

        static Dictionary<string, object> Result(object id, object result)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["jsonrpc"] = "2.0"; d["id"] = id; d["result"] = result;
            return d;
        }

        static Dictionary<string, object> Error(object id, int code, string message)
        {
            Dictionary<string, object> e = new Dictionary<string, object>();
            e["code"] = code; e["message"] = message;
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["jsonrpc"] = "2.0"; d["id"] = id; d["error"] = e;
            return d;
        }
    }
}
