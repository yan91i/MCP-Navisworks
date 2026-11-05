// NavisMcp v2 - Navisworks MCP Server plugin
// Target: Navisworks Manage 2024+ / .NET Framework 4.8
// C# 5 syntax only -> compiles with built-in C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe

using System;
using System.Drawing;
using System.Windows.Forms;
using Autodesk.Navisworks.Api.Plugins;

namespace NavisMcp
{
    /// <summary>Tool Add-ins button: Start / Stop the MCP server.</summary>
    [Plugin("NavisMcp", "YLBM", DisplayName = "Navis MCP", ToolTip = "Start / Stop local MCP server (Cherry Studio etc.)")]
    [AddInPlugin(AddInLocation.AddIn)]
    public class NavisMcpPlugin : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            try
            {
                if (McpHost.IsRunning)
                {
                    McpHost.Stop();
                    MessageBox.Show("Navis MCP server stopped.", "Navis MCP");
                }
                else
                {
                    McpHost.Start(McpHost.ConfiguredPort());
                    MessageBox.Show("Navis MCP server running:\n\n" + McpHost.Url +
                                    "\n\nCherry Studio -> MCP -> Streamable HTTP -> URL above" +
                                    "\nTool list: http://127.0.0.1:" + McpHost.Port + "/tools", "Navis MCP");
                }
            }
            catch (Exception ex)
            {
                McpHost.Log("Execute error: " + ex);
                MessageBox.Show("Navis MCP error:\n" + ex.Message, "Navis MCP");
            }
            return 0;
        }
    }

    /// <summary>Tool Add-ins button: show all MCP tools and their descriptions.</summary>
    [Plugin("NavisMcpTools", "YLBM", DisplayName = "Navis MCP Tools", ToolTip = "List all MCP tools and descriptions")]
    [AddInPlugin(AddInLocation.AddIn)]
    public class NavisMcpToolsPlugin : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            try { new ToolsForm().Show(); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Navis MCP"); }
            return 0;
        }
    }

    public class ToolsForm : Form
    {
        public ToolsForm()
        {
            Text = "Navis MCP - Tools (" + McpTools.Count + ")  " + (McpHost.IsRunning ? McpHost.Url : "[server stopped]");
            Width = 1100; Height = 700;
            StartPosition = FormStartPosition.CenterScreen;

            ListView lv = new ListView();
            lv.Dock = DockStyle.Fill;
            lv.View = View.Details;
            lv.FullRowSelect = true;
            lv.GridLines = true;
            lv.Columns.Add("Category", 150);
            lv.Columns.Add("Tool", 190);
            lv.Columns.Add("Description", 520);
            lv.Columns.Add("Parameters", 400);
            foreach (ToolDef t in McpTools.All)
            {
                ListViewItem it = new ListViewItem(t.Category);
                it.SubItems.Add(t.Name);
                it.SubItems.Add(t.Description);
                it.SubItems.Add(McpTools.ParamSummary(t));
                it.ToolTipText = t.Description;
                lv.Items.Add(it);
            }
            lv.ShowItemToolTips = true;

            FlowLayoutPanel bar = new FlowLayoutPanel();
            bar.Dock = DockStyle.Bottom; bar.Height = 38; bar.FlowDirection = FlowDirection.RightToLeft;
            Button close = new Button(); close.Text = "Close"; close.Width = 90;
            close.Click += delegate { Close(); };
            Button copy = new Button(); copy.Text = "Copy as Markdown"; copy.Width = 140;
            copy.Click += delegate { Clipboard.SetText(McpTools.ToolsMarkdown()); MessageBox.Show("Copied.", "Navis MCP"); };
            bar.Controls.Add(close); bar.Controls.Add(copy);

            Controls.Add(lv);
            Controls.Add(bar);
        }
    }

    /// <summary>Auto-start on Navisworks load. Disable with env NAVIS_MCP_AUTOSTART=0</summary>
    [Plugin("NavisMcpAutoStart", "YLBM", DisplayName = "Navis MCP AutoStart")]
    public class NavisMcpAutoStart : EventWatcherPlugin
    {
        public override void OnLoaded()
        {
            try
            {
                if (Environment.GetEnvironmentVariable("NAVIS_MCP_AUTOSTART") == "0") return;
                McpHost.Start(McpHost.ConfiguredPort());
            }
            catch (Exception ex) { McpHost.Log("AutoStart failed: " + ex.Message); }
        }

        public override void OnUnloading()
        {
            try { McpHost.Stop(); } catch { }
        }
    }
}
