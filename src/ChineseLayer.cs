using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Forms;

internal static class Win {
    [DllImport("user32")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32")] internal static extern uint GetWindowThreadProcessId(IntPtr handle,out int pid);
    [DllImport("user32")] internal static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32")] internal static extern bool PrintWindow(IntPtr handle,IntPtr dc,uint flags);
    [DllImport("user32")] internal static extern bool GetWindowRect(IntPtr handle,out Extent rect);
    internal delegate void EventCallback(IntPtr hook,uint evt,IntPtr window,int obj,int child,uint thread,uint time);
    [DllImport("user32")] internal static extern IntPtr SetWinEventHook(uint from,uint to,IntPtr module,EventCallback callback,uint pid,uint thread,uint flags);
    [DllImport("user32")] internal static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32")] internal static extern bool RegisterHotKey(IntPtr h,int id,uint mod,uint key);
    [DllImport("user32")] internal static extern bool UnregisterHotKey(IntPtr h,int id);
    [StructLayout(LayoutKind.Sequential)] internal struct Extent {public int Left,Top,Right,Bottom;}
}
internal sealed class Mark {
    internal string English, Chinese;
    internal Rectangle Rect;
    internal bool Button, Center;
    internal int FontPixels;
    internal Color Background, Ink;
}
internal sealed class Surface {
    internal Rectangle Bounds;
    internal List<Mark> Marks=new List<Mark>();
    static readonly Dictionary<string,string> terms=ReadTerms();
    static Dictionary<string,string> ReadTerms(){
        var result=new Dictionary<string,string>(StringComparer.Ordinal);
        using(var reader=new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("zh.tsv"))){
            string s;while((s=reader.ReadLine())!=null){var fields=s.Split('\t');if(fields.Length==2)result[fields[0]]=fields[1];}
        }
        return result;
    }
    static readonly HashSet<string> chrome=new HashSet<string>("Workspace|View|Window|New Conversation|Automations|Customizations|Projects|Settings".Split('|'));
    static readonly HashSet<string> catalog=new HashSet<string>("Marketplace|Installed|Plugins|Built In|Skills & Rules|Create|Sign in required|Featured|Custom Agents|Rules|Skills|MCP Servers|Global|Add|Details|More Plugins|Developer Tools|Business|Design, Creative & Media|Productivity & Project Management|Cloud Hosting & Deployment|Overview|Developer|Links|Agents|Version|Commands".Split('|'));
    static Rectangle Pixels(System.Windows.Rect r){return r.IsEmpty?Rectangle.Empty:Rectangle.FromLTRB((int)Math.Floor(r.Left),(int)Math.Floor(r.Top),(int)Math.Ceiling(r.Right),(int)Math.Ceiling(r.Bottom));}
    internal static bool Target(IntPtr h){
        int pid;if(h==IntPtr.Zero||Win.GetWindowThreadProcessId(h,out pid)==0)return false;
        try{using(var p=Process.GetProcessById(pid))return p.ProcessName.Equals("Antigravity",StringComparison.OrdinalIgnoreCase);}catch(ArgumentException){return false;}
    }
    static string PathOf(AutomationElement e){
        object obj;if(!e.TryGetCurrentPattern(ValuePattern.Pattern,out obj))return "";
        Uri uri;return Uri.TryCreate(((ValuePattern)obj).Current.Value,UriKind.Absolute,out uri)&&uri.IsLoopback?uri.AbsolutePath:"";
    }
    internal static Surface Scan(IntPtr h){
        var root=AutomationElement.FromHandle(h);
        var dialog=root.FindFirst(TreeScope.Descendants,new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Window),new PropertyCondition(AutomationElement.NameProperty,"Settings")));
        AutomationElement area=dialog;string route="";
        if(area==null){
            foreach(AutomationElement doc in root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Document))){
                route=PathOf(doc);if(route.Length>0){area=doc;break;}
            }
        }
        if(area==null)return null;
        var surface=new Surface{Bounds=Pixels(area.Current.BoundingRectangle)};
        if(surface.Bounds.Width<30||surface.Bounds.Height<30)return null;
        bool settings=dialog!=null,market=route.StartsWith("/customizations"),home=route=="/";
        if(!settings&&!home&&!market){
            area=area.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.NameProperty,"Sidebar"));
            if(area==null)return null;
        }
        var types=new[]{ControlType.Text,ControlType.Button,ControlType.RadioButton,ControlType.Hyperlink,ControlType.TabItem,ControlType.Edit};
        var cache=new CacheRequest();
        foreach(var prop in new[]{AutomationElement.NameProperty,AutomationElement.ControlTypeProperty,AutomationElement.BoundingRectangleProperty,AutomationElement.IsOffscreenProperty})cache.Add(prop);
        AutomationElementCollection nodes;
        using(cache.Activate())nodes=area.FindAll(TreeScope.Descendants,new OrCondition(types.Select(t=>(Condition)new PropertyCondition(AutomationElement.ControlTypeProperty,t)).ToArray()));
        foreach(AutomationElement el in nodes){
            var c=el.Cached;if(c.IsOffscreen)continue;
            var r=Pixels(c.BoundingRectangle);
            if(r.Width<7||r.Height<8||r.Height>200||!surface.Bounds.Contains(r))continue;
            string name=(c.Name??"").Trim(),zh;bool button=c.ControlType!=ControlType.Text;
            if(c.ControlType==ControlType.Edit){
                if(!market&&!settings)continue;
                string hint=(name+" "+el.Current.HelpText).ToLowerInvariant();
                if(!hint.Contains("search customizations")&&!hint.Contains("search shortcuts"))continue;
                object v;if(el.Current.HasKeyboardFocus||!el.TryGetCurrentPattern(ValuePattern.Pattern,out v)||((ValuePattern)v).Current.Value.Length>0)continue;
                r.Inflate(-5,-3);surface.Marks.Add(new Mark{English=name,Chinese=market?"搜索自定义内容…":"搜索快捷键…",Rect=r,Button=false});continue;
            }
            if(name.Length==0||name.Length>1300)continue;
            // Virtualized project lists can retain an off-viewport badge with stale bounds.
            if(name=="Global"){
                var center=new System.Windows.Point(r.Left+r.Width/2,r.Top+r.Height/2);
                var actual=AutomationElement.FromPoint(center);
                if(actual==null||actual.Current.Name!="Global"||!Pixels(actual.Current.BoundingRectangle).IntersectsWith(r))continue;
            }
            if(button&&(name=="Close"||name=="System"||name=="Light"||name=="Dark"))continue;
            if(button&&name=="New Conversation"&&r.Width<55)continue;
            // A labelled switch exposes the same accessible name as the visible row title.
            if(settings&&button&&r.Width<65&&r.Height<=36&&(name.StartsWith("Enable ")||name=="Prevent Sleep"||name=="Keep In Menu Bar"||name=="Screen Reader Mode"||name=="Marketing Emails"||name=="Automatic Check for Updates"))continue;
            bool fixedNav=chrome.Contains(name)&&(button||name=="Projects"||market&&name=="Customizations");
            bool featureAction=market&&button&&r.Width>43&&r.Width<110&&(name.StartsWith("Details for ")||name.StartsWith("Add "));
            bool prompt=market&&button&&name.Length>32&&terms.ContainsKey(name);
            bool prose=!button&&name.Length>26;
            bool greeting=home&&!button&&(name.StartsWith("Good to see you")||name.StartsWith("Welcome back")||name.StartsWith("Hey "));
            if(!settings&&!fixedNav&&!featureAction&&!prompt&&!(market&&(catalog.Contains(name)||prose||name.StartsWith("See ")))&&!(home&&(greeting||name=="Local Checkout"||name.StartsWith("Ask anything")||name=="Request Review"||name.Contains("what should we build today?"))))continue;
            if(!terms.TryGetValue(name,out zh)){
                if(greeting&&name.EndsWith("What's next?"))zh="很高兴见到你，接下来做些什么？";
                else if(greeting&&name.EndsWith("Where should we pick up?"))zh="欢迎回来，从哪里继续？";
                else if(home&&Regex.IsMatch(name,@"^Hey [^,]{1,60}, what should we build today\?$"))zh="今天想做些什么？";
                else if(home&&name.TrimStart(',', ' ')=="what should we build today?")zh="今天想做些什么？";
                else if(featureAction)zh=name.StartsWith("Details for ")?"详情":"添加";
                else if(market&&Regex.IsMatch(name,@"^See [0-9]+ more$"))zh="再看 "+name.Split(' ')[1]+" 项";
                else continue;
            }
            if(market&&c.ControlType==ControlType.Hyperlink&&!fixedNav&&!featureAction&&name!="Marketplace"&&name!="Installed")continue;
            if(market&&name=="Antigravity")continue;
            surface.Marks.Add(new Mark{English=name,Chinese=zh,Rect=r,Button=button});
        }
        surface.Marks=surface.Marks.Where(a=>!a.Button||!surface.Marks.Any(b=>!b.Button&&b.Chinese==a.Chinese&&a.Rect.Contains(b.Rect))).ToList();
        Style(h,surface);
        return surface;
    }
    // Sample the original window, excluding our independent layered window.
    // Locate the actual foreground glyphs within wide UI Automation text boxes.
    static void Style(IntPtr h,Surface page){
        Win.Extent w;if(!Win.GetWindowRect(h,out w))return;
        int width=w.Right-w.Left,height=w.Bottom-w.Top;
        if(width<1||height<1||width>9000||height>6000){page.Marks.Clear();return;}
        using(var bmp=new Bitmap(width,height,PixelFormat.Format32bppRgb)){
            using(var g=Graphics.FromImage(bmp)){var dc=g.GetHdc();bool ok;try{ok=Win.PrintWindow(h,dc,2);}finally{g.ReleaseHdc(dc);}if(!ok){page.Marks.Clear();return;}}
            var data=bmp.LockBits(new Rectangle(0,0,width,height),ImageLockMode.ReadOnly,PixelFormat.Format32bppRgb);
            var px=new int[width*height];
            try{Marshal.Copy(data.Scan0,px,0,px.Length);}finally{bmp.UnlockBits(data);}
            page.Marks.RemoveAll(m=>!Fit(m,px,width,height,w.Left,w.Top));
        }
    }
    static int Contrast(int a,int b){return Math.Abs((a>>16&255)-(b>>16&255))+Math.Abs((a>>8&255)-(b>>8&255))+Math.Abs((a&255)-(b&255));}
    static bool Fit(Mark m,int[] pixels,int width,int height,int left,int top){
        var r=m.Rect;r.Offset(-left,-top);r.Intersect(new Rectangle(0,0,width,height));
        if(r.Width<6||r.Height<8)return false;
        // Larger layout cards carry both a title and a subtitle. Keep their original border and icon.
        bool card=m.Button&&r.Height>65;
        if(card){r.Inflate(-8,-3);if(r.Height>50){r.Y+=r.Height/3;r.Height-=r.Height/3;}}
        else if(m.Button&&r.Width>18&&r.Height>15)r.Inflate(-3,-2);
        var colors=new Dictionary<int,int>();
        for(int y=r.Top;y<r.Bottom;y+=3)for(int x=r.Left;x<r.Right;x+=3){
            int col=pixels[y*width+x]&0xffffff,n;colors.TryGetValue(col,out n);colors[col]=n+1;
        }
        if(colors.Count==0)return false;
        int bg=colors.Aggregate((a,b)=>a.Value>b.Value?a:b).Key, lx=r.Right,rx=r.Left,ty=r.Bottom,by=r.Top,ink=bg,peak=0,count=0;
        for(int y=r.Top;y<r.Bottom;y++)for(int x=r.Left;x<r.Right;x++){
            int color=pixels[y*width+x]&0xffffff,dist=Contrast(color,bg);
            if(dist<110)continue;
            lx=Math.Min(lx,x);rx=Math.Max(rx,x);ty=Math.Min(ty,y);by=Math.Max(by,y);count++;
            if(dist>peak){peak=dist;ink=color;}
        }
        if(count<4||rx-lx<2)return false;
        int pad=2;
        var box=Rectangle.FromLTRB(Math.Max(r.Left,lx-pad),Math.Max(r.Top,ty-pad),Math.Min(r.Right,rx+pad+1),Math.Min(r.Bottom,by+pad+1));
        if(card){
            m.Chinese=m.English=="Knowledge Work"?"知识工作　适合研究与写作":m.English=="Software Engineering"?"软件工程　显示完整开发工具":m.Chinese;
        }
        int originalMid=r.Left+r.Width/2;
        m.Center=m.Button && !card && Math.Abs((lx+rx)/2-originalMid)<Math.Max(5,r.Width/8);
        m.FontPixels=m.English.Length>66?12:13;
        if(!m.Button&&r.Height>21&&by-ty<22&&m.English.Length<35)m.FontPixels=15;
        // Reserve the native line box for Chinese ascenders and descenders.
        // Left-aligned sidebar labels retain the original text inset.
        int desired=Math.Min(r.Width,Math.Max(box.Width,Math.Min(1200,m.Chinese.Length*m.FontPixels+2)));
        int x0=m.Center?Math.Max(r.Left,Math.Min(r.Right-desired,originalMid-desired/2)):box.X;
        int x1=Math.Max(box.Right,Math.Min(r.Right,x0+desired));
        box=Rectangle.FromLTRB(Math.Min(x0,box.Left),Math.Max(r.Top,Math.Min(box.Top,(ty+by-m.FontPixels-6)/2)),x1,Math.Min(r.Bottom,Math.Max(box.Bottom,(ty+by+m.FontPixels+7)/2)));
        box.Offset(left,top);m.Rect=box;m.Background=Color.FromArgb(255,Color.FromArgb(bg));m.Ink=Color.FromArgb(255,Color.FromArgb(ink));
        return true;
    }
}
internal sealed class ChineseLayer:Form {
    readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer{Interval=100};
    bool busy,on;int generation;Surface page;
    internal Surface Displayed{get{return Visible?page:null;}}
    readonly Win.EventCallback changed;
    IntPtr scrollHook,reorderHook,watched;
    void Watch(IntPtr target){
        if(watched==target)return;
        if(scrollHook!=IntPtr.Zero)Win.UnhookWinEvent(scrollHook);
        if(reorderHook!=IntPtr.Zero)Win.UnhookWinEvent(reorderHook);
        watched=target;scrollHook=reorderHook=IntPtr.Zero;
        int pid;if(target!=IntPtr.Zero&&Win.GetWindowThreadProcessId(target,out pid)!=0){
            scrollHook=Win.SetWinEventHook(0x8015,0x8015,IntPtr.Zero,changed,(uint)pid,0,2);
            reorderHook=Win.SetWinEventHook(0x8004,0x8004,IntPtr.Zero,changed,(uint)pid,0,2);
        }
    }
    internal ChineseLayer(){
        changed=delegate{if(on&&!IsDisposed){generation++;if(Visible)Hide();}};
        AutoScaleMode=AutoScaleMode.None;FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;DoubleBuffered=true;BackColor=Color.Magenta;TransparencyKey=BackColor;
        timer.Tick+=async delegate{await Poll();};
    }
    internal void Enable(bool value){generation++;on=value;timer.Enabled=on;if(!on)Watch(IntPtr.Zero);Hide();page=null;}
    internal async Task Poll(){
        if(!on)return;var h=Win.GetForegroundWindow();
        if(!Surface.Target(h)){Watch(IntPtr.Zero);if(Visible)Hide();return;}Watch(h);if(busy)return;busy=true;int id=generation;
        try{
            var p=await Task.Run(()=>Surface.Scan(h));
            if(IsDisposed||!on||id!=generation)return;
            if(Win.GetForegroundWindow()!=h||p==null||p.Marks.Count==0){Hide();return;}
            page=p;Bounds=p.Bounds;if(!Visible)Show();Invalidate();
        }catch(Exception){if(!IsDisposed)Hide();}finally{busy=false;}
    }
    protected override bool ShowWithoutActivation{get{return true;}}
    protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x080800a0;return p;}}
    protected override void WndProc(ref Message m){if(m.Msg==0x84){m.Result=new IntPtr(-1);return;}base.WndProc(ref m);}
    protected override void OnPaint(PaintEventArgs e){
        base.OnPaint(e);if(page==null)return;
        foreach(var m in page.Marks){
            var r=m.Rect;r.Offset(-page.Bounds.X,-page.Bounds.Y);
            if(r.Width<3||r.Height<4)continue;
            // Small corner radius keeps the original control silhouette visible.
            using(var border=new GraphicsPath()){
                int d=Math.Min(4,Math.Min(r.Width,r.Height));
                border.AddArc(r.Left,r.Top,d,d,180,90);border.AddArc(r.Right-d,r.Top,d,d,270,90);border.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);border.AddArc(r.Left,r.Bottom-d,d,d,90,90);border.CloseFigure();
                using(var b=new SolidBrush(m.Background))e.Graphics.FillPath(b,border);
            }
            e.Graphics.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using(var fmt=(StringFormat)StringFormat.GenericTypographic.Clone())
            using(var brush=new SolidBrush(m.Ink)){
                fmt.Alignment=m.Center?StringAlignment.Center:StringAlignment.Near;fmt.LineAlignment=StringAlignment.Center;
                fmt.Trimming=StringTrimming.EllipsisCharacter;fmt.FormatFlags&=~StringFormatFlags.LineLimit;
                if(r.Height<25)fmt.FormatFlags|=StringFormatFlags.NoWrap;
                float size=m.FontPixels;
                if(r.Height<25)size=Math.Min(size,Math.Max(10,(float)r.Width/Math.Max(1,m.Chinese.Length)));
                using(var font=new Font("Microsoft YaHei UI",size,GraphicsUnit.Pixel)){
                    var state=e.Graphics.Save();e.Graphics.SetClip(r);
                    var layout=new RectangleF(r.X,r.Y-3,r.Width,r.Height+6);
                    e.Graphics.DrawString(m.Chinese,font,brush,layout,fmt);e.Graphics.Restore(state);
                }
            }
        }
    }
    protected override void Dispose(bool disposing){if(disposing){generation++;on=false;Watch(IntPtr.Zero);timer.Dispose();}base.Dispose(disposing);}
}
