using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class ProxyLaunch
{
    internal static string FindApp()
    {
        var folders = new[] {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        };
        foreach (string folder in folders)
        {
            string path = Path.Combine(folder, "Antigravity", "Antigravity.exe");
            if (File.Exists(path)) return path;
        }
        return "";
    }

    internal static bool IsRunning()
    {
        var processes = Process.GetProcessesByName("Antigravity");
        foreach (var process in processes) process.Dispose();
        return processes.Length > 0;
    }

    internal static async Task<bool> PortOpen(int port)
    {
        using (var client = new TcpClient())
        {
            try
            {
                var connection = client.ConnectAsync(IPAddress.Loopback, port);
                if (await Task.WhenAny(connection, Task.Delay(1800)) != connection) return false;
                await connection;
                return true;
            }
            catch (SocketException) { return false; }
        }
    }

    internal static ProcessStartInfo StartInfo(string file, int port)
    {
        var info = new ProcessStartInfo(file) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(file) };
        string url = "http://127.0.0.1:" + port;
        info.EnvironmentVariables["HTTP_PROXY"] = url;
        info.EnvironmentVariables["HTTPS_PROXY"] = url;
        return info;
    }
}

internal sealed class OrbitBanner : Panel
{
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var bg = new LinearGradientBrush(ClientRectangle, Color.FromArgb(21,34,64), Color.FromArgb(17,69,82), 13f))
            g.FillRectangle(bg, ClientRectangle);
        var saved = g.Save();
        g.TranslateTransform(Width * .79f, Height * .52f);
        g.ScaleTransform(Height / 150f, Height / 150f);
        g.RotateTransform(-19);
        using (var line = new Pen(Color.FromArgb(125,130,228,216), 1.3f))
        using (var planet = new LinearGradientBrush(new Rectangle(-38,-38,76,76), Color.FromArgb(141,247,216), Color.FromArgb(63,105,182), 67f))
        {
            g.DrawEllipse(line,-105,-43,210,86);
            g.FillEllipse(planet,-38,-38,76,76);
            g.DrawArc(line,-76,-17,152,34,0,180);
            g.FillEllipse(Brushes.White,96,8,6,6);
        }
        g.Restore(saved);
        using (var title = new Font("Microsoft YaHei UI", 25f, FontStyle.Bold))
        using (var sub = new Font("Microsoft YaHei UI", 10f))
        {
            g.DrawString("星轨启动器", title, Brushes.White, Width*.05f, Height*.19f);
            g.DrawString("Antigravity  /  本地代理启动", sub, Brushes.PaleTurquoise, Width*.053f, Height*.64f);
        }
    }
}

internal sealed class Launcher : Form
{
    readonly TextBox path = new TextBox { ReadOnly = true, Dock = DockStyle.Fill };
    readonly NumericUpDown port = new NumericUpDown { Minimum=1, Maximum=65535, Value=6478, Width=90 };
    readonly Label status = new Label { Dock=DockStyle.Fill, TextAlign=ContentAlignment.MiddleLeft, Text="准备就绪" };
    readonly Button check = MakeButton("检测端口", false);
    readonly Button launch = MakeButton("启动反重力", true);
    readonly Button browse = MakeButton("选择程序", false);
    readonly Button help = MakeButton("中文设置速查", false);

    static Button MakeButton(string text, bool primary)
    {
        var b = new Button { Text=text, FlatStyle=FlatStyle.Flat, AutoSize=true, Height=40, MinimumSize=new Size(94,38),
            BackColor=primary?Color.FromArgb(129,236,210):Color.FromArgb(39,53,78),
            ForeColor=primary?Color.FromArgb(14,38,43):Color.White, Cursor=Cursors.Hand, Padding=new Padding(7,2,7,2) };
        b.FlatAppearance.BorderSize=0;
        return b;
    }

    readonly ChineseLayer layer = new ChineseLayer();
    readonly CheckBox chinese = new CheckBox {Text="启用中文设置",AutoSize=true,Margin=new Padding(26,5,4,4)};
    readonly NotifyIcon tray = new NotifyIcon();
    bool exiting;
    internal static readonly System.Threading.EventWaitHandle Reveal = new System.Threading.EventWaitHandle(false,System.Threading.EventResetMode.AutoReset,"Local\\OrbitProxyShowV2");
    readonly System.Windows.Forms.Timer wake = new System.Windows.Forms.Timer {Interval=400};
    internal void OpenPanel(){Show();WindowState=FormWindowState.Normal;Activate();}
    void UpdateChinese(bool enabled){
        layer.Enable(enabled);
        try{Microsoft.Win32.Registry.SetValue(@"HKEY_CURRENT_USER\Software\OrbitAntigravityLauncher","Chinese",enabled?1:0);}catch(Exception ex){status.Text="未能保存偏好："+ex.Message;}
    }
    internal Launcher()
    {
        Text="星轨 · 反重力代理启动器";
        Font=new Font("Microsoft YaHei UI",10);
        AutoScaleMode=AutoScaleMode.Dpi;
        BackColor=Color.FromArgb(20,29,47);
        ForeColor=Color.FromArgb(227,239,245);
        ClientSize=new Size(650,440);
        FormBorderStyle=FormBorderStyle.FixedSingle;
        MaximizeBox=false;
        StartPosition=FormStartPosition.CenterScreen;
        var grid=new TableLayoutPanel { Dock=DockStyle.Fill, Padding=new Padding(24,12,24,16), ColumnCount=2, RowCount=5 };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        foreach(int h in new[]{31,48,44,62,60}) grid.RowStyles.Add(new RowStyle(SizeType.Absolute,h));
        var fileLabel=new Label {Text="反重力桌面程序", AutoSize=true, Anchor=AnchorStyles.Left};
        grid.Controls.Add(fileLabel,0,0);
        path.Anchor=AnchorStyles.Left|AnchorStyles.Right;
        grid.Controls.Add(path,0,1);
        grid.Controls.Add(browse,1,1);
        var proxy=new FlowLayoutPanel { Dock=DockStyle.Fill, WrapContents=false };
        proxy.Controls.Add(new Label {Text="本地 HTTP 代理     127.0.0.1  /  端口",AutoSize=true, Margin=new Padding(0,5,8,0)});
        proxy.Controls.Add(port);
        chinese.Checked=Convert.ToInt32(Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\OrbitAntigravityLauncher","Chinese",1))!=0;
        proxy.Controls.Add(chinese);
        chinese.CheckedChanged+=delegate{UpdateChinese(chinese.Checked);};
        layer.Enable(chinese.Checked);
        var menu=new ContextMenuStrip();
        menu.Items.Add("打开启动器",null,delegate{OpenPanel();});
        var cn=menu.Items.Add("启用中文设置",null,delegate{chinese.Checked=!chinese.Checked;}) as ToolStripMenuItem;
        menu.Opening+=delegate{cn.Checked=chinese.Checked;};
        menu.Items.Add("退出启动器",null,delegate{exiting=true;Close();});
        tray.Icon=SystemIcons.Application;tray.Text="星轨 · 反重力代理启动器";tray.ContextMenuStrip=menu;tray.Visible=true;
        tray.DoubleClick+=delegate{OpenPanel();}; Win.RegisterHotKey(Handle,1,0x4003,(uint)Keys.F8);
        wake.Tick+=async delegate{if(Reveal.WaitOne(0)){OpenPanel();if(launch.Enabled && !ProxyLaunch.IsRunning())await Run(true);}};wake.Start();
        FormClosing+=delegate(object sender,FormClosingEventArgs e){if(!exiting && e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();}};
        FormClosed+=delegate{Win.UnregisterHotKey(Handle,1);wake.Dispose();tray.Dispose();layer.Dispose();};
        Shown+=async delegate{if(!ProxyLaunch.IsRunning())await Run(true);else status.Text="反重力正在运行。可以在这里切换中文显示。";};
        grid.Controls.Add(proxy,0,2);
        grid.SetColumnSpan(proxy,2);
        grid.Controls.Add(status,0,3); grid.SetColumnSpan(status,2);
        var buttons=new FlowLayoutPanel { Dock=DockStyle.Fill };
        buttons.Controls.AddRange(new Control[]{launch,check,help});
        grid.Controls.Add(buttons,0,4); grid.SetColumnSpan(buttons,2);
        Controls.Add(grid);
        Controls.Add(new OrbitBanner { Height=150, Dock=DockStyle.Top });
        path.Text=ProxyLaunch.FindApp();
        browse.Click+=delegate {
            using(var dialog=new OpenFileDialog {Filter="反重力桌面程序|Antigravity.exe",Title="选择 Antigravity.exe"})
                if(dialog.ShowDialog(this)==DialogResult.OK && String.Equals(Path.GetFileName(dialog.FileName),"Antigravity.exe",StringComparison.OrdinalIgnoreCase)) path.Text=dialog.FileName;
        };
        check.Click+=async delegate {await Run(false);};
        launch.Click+=async delegate {await Run(true);};
        help.Click+=delegate {
            string file=Path.Combine(Path.GetTempPath(),"OrbitGuide-"+Guid.NewGuid().ToString("N")+".html");
            try {
                using(var input=typeof(Launcher).Assembly.GetManifestResourceStream("guide.html"))using(var output=File.Create(file))input.CopyTo(output);
                using(Process child=Process.Start(new ProcessStartInfo(file){UseShellExecute=true})) {}
            } catch(Exception ex){status.Text="打开说明失败："+ex.Message;}
        };
    }
    protected override void WndProc(ref Message m){if(m.Msg==0x312 && m.WParam.ToInt32()==1){chinese.Checked=!chinese.Checked;return;}base.WndProc(ref m);}
    async Task Run(bool start)
    {
        check.Enabled=launch.Enabled=browse.Enabled=port.Enabled=false;
        try
        {
            if(start && !File.Exists(path.Text)) {status.Text="请先选择已安装的 Antigravity.exe。";return;}
            int selected=(int)port.Value;
            status.Text="正在检测本地端口…";
            if(!await ProxyLaunch.PortOpen(selected)) {status.Text="端口 "+selected+" 未连通。请启动代理客户端，核对 HTTP 或混合端口。";return;}
            if(!start) {status.Text="本地端口 "+selected+" 可连接。此检测仅验证 TCP 连通性。";return;}
            if(ProxyLaunch.IsRunning()) {status.Text="反重力已在运行。请先保存工作并从托盘完全退出，再点击启动。";return;}
            using(var child=Process.Start(ProxyLaunch.StartInfo(path.Text,selected)))
                {status.Text="已发出启动请求，进程号 "+child.Id+"，代理端口 "+selected+"。";Hide();}
        }
        catch(Exception ex){status.Text="操作失败："+ex.Message;}
        finally{check.Enabled=launch.Enabled=browse.Enabled=port.Enabled=true;}
    }
    [STAThread] static void Main()
    {
        try{Win.SetProcessDpiAwarenessContext(new IntPtr(-4));}catch(EntryPointNotFoundException){}
        bool first;using(var gate=new System.Threading.Mutex(true,"Local\\OrbitProxyLauncherV2",out first)){
        if(!first){Reveal.Set();return;}
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new Launcher());}
    }
}

