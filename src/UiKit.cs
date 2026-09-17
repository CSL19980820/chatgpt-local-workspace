using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// Visual language of the desktop shell. Tokens mirror src/dashboard.css (the embedded
// workbench) so the window and the page read as one product: same teal-green primary,
// same hairline borders, same mono for machine data.
static class Theme
{
    public static readonly Color Window = Color.FromArgb(246, 248, 247);
    public static readonly Color Surface = Color.White;
    public static readonly Color Border = Color.FromArgb(228, 233, 231);
    public static readonly Color BorderStrong = Color.FromArgb(205, 214, 211);
    public static readonly Color Text = Color.FromArgb(26, 40, 37);
    public static readonly Color Muted = Color.FromArgb(106, 121, 117);
    public static readonly Color Faint = Color.FromArgb(150, 163, 159);
    public static readonly Color Primary = Color.FromArgb(22, 124, 103);
    public static readonly Color PrimaryHover = Color.FromArgb(16, 103, 85);
    public static readonly Color PrimarySoft = Color.FromArgb(231, 244, 240);
    public static readonly Color Accent = Color.FromArgb(49, 168, 138);
    public static readonly Color Danger = Color.FromArgb(190, 62, 55);
    public static readonly Color DangerSoft = Color.FromArgb(250, 237, 236);
    public static readonly Color Warn = Color.FromArgb(168, 116, 18);
    public static readonly Color WarnSoft = Color.FromArgb(250, 243, 228);
    public static readonly Color NeutralSoft = Color.FromArgb(238, 241, 240);
    public static readonly Color RowHover = Color.FromArgb(244, 248, 246);
    public static readonly Color RowSelect = Color.FromArgb(231, 244, 240);
    public static readonly Color Console = Color.FromArgb(250, 251, 250);

    public static readonly Font Title = Ui(14.5f, FontStyle.Bold);
    public static readonly Font Body = Ui(9.5f);
    public static readonly Font BodyBold = Ui(9.5f, FontStyle.Bold);
    public static readonly Font Small = Ui(9f);
    public static readonly Font SmallBold = Ui(9f, FontStyle.Bold);
    public static readonly Font Tiny = Ui(8.5f);
    public static readonly Font Mono = MonoFont(9.5f);
    public static readonly Font MonoSmall = MonoFont(9f);

    public static Font Ui(float size, FontStyle style = FontStyle.Regular) { return new Font("Microsoft YaHei UI", size, style); }
    static string monoFamily;
    public static Font MonoFont(float size, FontStyle style = FontStyle.Regular)
    {
        if (monoFamily == null)
        {
            monoFamily = "Consolas";
            try { using (var f = new FontFamily("Cascadia Mono")) monoFamily = "Cascadia Mono"; } catch (ArgumentException) { }
        }
        return new Font(monoFamily, size, style);
    }
    public static GraphicsPath Round(Rectangle r, int radius)
    {
        var p = new GraphicsPath(); int d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d - 1, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d - 1, r.Bottom - d - 1, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d - 1, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
    public static void Smooth(Graphics g) { g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit; }
}

// Flat rounded button with primary / secondary / ghost variants and real hover states.
class UiButton : Control
{
    public enum Variant { Primary, Secondary, Ghost }
    Variant variant = Variant.Secondary;
    bool hover, pressed;
    public UiButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent; Height = 32; Cursor = Cursors.Hand; TabStop = true;
    }
    public Variant Kind { get { return variant; } set { variant = value; Invalidate(); } }
    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); using (var g = CreateGraphics()) Width = TextRenderer.MeasureText(g, Text, Theme.SmallBold).Width + 30; }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; pressed = false; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); pressed = true; Invalidate(); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); pressed = false; Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; Theme.Smooth(g);
        var r = new Rectangle(0, 0, Width, Height);
        Color fill = Color.Transparent, border = Color.Transparent, fore = Theme.Text;
        if (variant == Variant.Primary)
        {
            fill = Enabled ? (pressed ? Theme.PrimaryHover : hover ? Theme.PrimaryHover : Theme.Primary) : Color.FromArgb(158, 196, 186);
            fore = Color.White;
        }
        else if (variant == Variant.Secondary)
        {
            fill = Enabled ? (pressed ? Theme.NeutralSoft : hover ? Theme.RowHover : Theme.Surface) : Theme.NeutralSoft;
            border = Enabled ? (hover ? Theme.BorderStrong : Theme.Border) : Theme.Border;
            fore = Enabled ? Theme.Text : Theme.Faint;
        }
        else
        {
            fill = pressed ? Theme.NeutralSoft : hover ? Theme.RowHover : Color.Transparent;
            fore = Enabled ? Theme.Muted : Theme.Faint;
        }
        using (var path = Theme.Round(r, 8))
        {
            using (var b = new SolidBrush(fill)) g.FillPath(b, path);
            if (border != Color.Transparent) using (var pen = new Pen(border, 1)) g.DrawPath(pen, path);
            if (Focused && Enabled) using (var pen = new Pen(Color.FromArgb(90, Theme.Accent), 2)) { var f = new Rectangle(2, 2, Width - 4, Height - 4); using (var fp = Theme.Round(f, 6)) g.DrawPath(pen, fp); }
        }
        TextRenderer.DrawText(g, Text, variant == Variant.Ghost ? Theme.Small : Theme.SmallBold, new Rectangle(0, 0, Width, Height), fore, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

// White rounded card with a hairline border; children sit on top of the painted surface.
class UiCard : Panel
{
    public UiCard()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor | ControlStyles.ContainerControl, true);
        BackColor = Color.Transparent;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; Theme.Smooth(g);
        var r = new Rectangle(0, 0, Width, Height);
        using (var path = Theme.Round(r, 12))
        {
            using (var b = new SolidBrush(Theme.Surface)) g.FillPath(b, path);
            using (var pen = new Pen(Theme.Border, 1)) g.DrawPath(pen, path);
        }
    }
}

// Borderless TextBox inside a painted rounded frame that carries the focus ring.
class UiInput : Control
{
    readonly TextBox box = new TextBox { BorderStyle = BorderStyle.None, BackColor = Theme.Surface };
    bool focused;
    public UiInput()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent; Height = 34;
        box.Font = Theme.Body; box.ForeColor = Theme.Text;
        box.GotFocus += (s, e) => { focused = true; Invalidate(); };
        box.LostFocus += (s, e) => { focused = false; Invalidate(); };
        Controls.Add(box);
    }
    public TextBox Box { get { return box; } }
    public new string Text { get { return box.Text; } set { box.Text = value; } }
    public bool Error { get { return error; } set { if (error != value) { error = value; Invalidate(); } } }
    bool error;
    public bool ReadOnly { get { return box.ReadOnly; } set { box.ReadOnly = value; box.BackColor = value ? Theme.Window : Theme.Surface; Invalidate(); } }
    public bool Masked { get { return box.UseSystemPasswordChar; } set { box.UseSystemPasswordChar = value; } }
    protected override void OnResize(EventArgs e) { base.OnResize(e); box.Bounds = new Rectangle(11, (Height - 20) / 2, Math.Max(10, Width - 22), 20); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; Theme.Smooth(g);
        var r = new Rectangle(0, 0, Width, Height);
        using (var path = Theme.Round(r, 8))
        {
            using (var b = new SolidBrush(box.BackColor)) g.FillPath(b, path);
            using (var pen = new Pen(error ? Theme.Danger : focused ? Theme.Accent : Theme.BorderStrong, error || focused ? 1.6f : 1)) g.DrawPath(pen, path);
        }
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keys) { return box.Focused ? false : base.ProcessCmdKey(ref msg, keys); }
}

// Underline tabs in the workbench's section-head style; labels may carry a muted count.
class UiTabs : Control
{
    readonly List<string> labels = new List<string>();
    readonly List<string> counts = new List<string>();
    int selected, hover = -1;
    int[] bounds;
    public event Action SelectedChanged;
    public UiTabs()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent; Height = 38; Cursor = Cursors.Hand;
    }
    public int Selected { get { return selected; } }
    public void AddTab(string label) { labels.Add(label); counts.Add(""); Invalidate(); }
    public void SetCount(int index, string count) { if (counts[index] != count) { counts[index] = count; Invalidate(); } }
    void MeasureTabs(out int[] ends)
    {
        ends = new int[labels.Count]; int x = 14;
        using (var g = CreateGraphics())
            for (int i = 0; i < labels.Count; i++) { x += TextRenderer.MeasureText(g, labels[i], Theme.SmallBold).Width + (counts[i].Length > 0 ? TextRenderer.MeasureText(g, " " + counts[i], Theme.Tiny).Width + 4 : 0); ends[i] = x; x += 26; }
    }
    int Hit(int x) { var ends = bounds; if (ends == null) return -1; for (int i = 0; i < ends.Length; i++) if (x <= ends[i]) return i; return -1; }
    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); var h = Hit(e.X); if (h != hover) { hover = h; Invalidate(); } }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = -1; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); var h = Hit(e.X);
        if (h >= 0 && h != selected) { selected = h; Invalidate(); if (SelectedChanged != null) SelectedChanged(); }
    }
    public void Select(int index) { if (index != selected) { selected = index; Invalidate(); if (SelectedChanged != null) SelectedChanged(); } }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; Theme.Smooth(g);
        using (var pen = new Pen(Theme.Border, 1)) g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
        int[] ends; MeasureTabs(out ends); bounds = ends;
        int x = 14;
        for (int i = 0; i < labels.Count; i++)
        {
            bool sel = i == selected;
            var fore = sel ? Theme.Text : hover == i ? Theme.Text : Theme.Muted;
            TextRenderer.DrawText(g, labels[i], sel ? Theme.SmallBold : Theme.Small, new Rectangle(x, 0, ends[i] - x, Height - 2), fore, TextFormatFlags.VerticalCenter);
            int lx = x + TextRenderer.MeasureText(g, labels[i], Theme.SmallBold).Width + 4;
            if (counts[i].Length > 0) TextRenderer.DrawText(g, counts[i], Theme.Tiny, new Rectangle(lx, 0, ends[i] - lx, Height - 2), sel ? Theme.Primary : Theme.Faint, TextFormatFlags.VerticalCenter);
            if (sel) using (var pen = new Pen(Theme.Primary, 2)) g.DrawLine(pen, x, Height - 2, ends[i], Height - 2);
            x = ends[i] + 26;
        }
    }
}

// Colored status dot + label: connected / connecting / stopped / error.
class UiStatusPill : Control
{
    public enum State { Stopped, Connecting, Live, Error }
    State state = State.Stopped;
    public UiStatusPill()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent; Height = 26; Width = 90;
    }
    public State Value { get { return state; } set { state = value; Invalidate(); } }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; Theme.Smooth(g);
        Color dot = Theme.Faint, fore = Theme.Muted; string text = "已停止";
        if (state == State.Connecting) { dot = Theme.Warn; fore = Theme.Warn; text = "正在连接"; }
        else if (state == State.Live) { dot = Theme.Accent; fore = Theme.Primary; text = "已连接"; }
        else if (state == State.Error) { dot = Theme.Danger; fore = Theme.Danger; text = "连接异常"; }
        string label = "● " + text;
        Width = TextRenderer.MeasureText(g, label, Theme.SmallBold).Width + 20;
        using (var b = new SolidBrush(Theme.Surface)) { var r = new Rectangle(0, 0, Width, Height); using (var path = Theme.Round(r, 13)) { g.FillPath(b, path); using (var pen = new Pen(Theme.Border, 1)) g.DrawPath(pen, path); } }
        TextRenderer.DrawText(g, "●", Theme.SmallBold, new Rectangle(9, 0, 14, Height), dot, TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(g, text, Theme.SmallBold, new Rectangle(22, 0, Width - 24, Height), fore, TextFormatFlags.VerticalCenter);
    }
}

public sealed class ActivityRow { public string Time, ThreadTag, ThreadLabel, Kind, Text; }

// Dense owner-drawn inspector grid: time, thread, kind badge, mono receipt line.
class ActivityGrid : Control
{
    readonly List<ActivityRow> rows = new List<ActivityRow>();
    readonly VScrollBar vs = new VScrollBar { Width = 10 };
    int top, hover = -1, selected = -1; bool tail = true;
    const int RowH = 30, HeadH = 28;
    public ActivityGrid()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Theme.Surface; TabStop = true;
        vs.Scroll += (s, e) => { top = e.NewValue; tail = AtBottom(); Invalidate(); };
        Controls.Add(vs);
    }
    public int Count { get { return rows.Count; } }
    public void SetRows(List<ActivityRow> next)
    {
        bool wasTail = tail;
        rows.Clear(); rows.AddRange(next);
        SyncScroll();
        if (wasTail) { tail = true; top = MaxTop(); }
        if (selected >= rows.Count) selected = -1;
        Invalidate();
    }
    public void Clear() { rows.Clear(); top = 0; selected = -1; tail = true; Invalidate(); }
    int MaxTop() { return Math.Max(0, rows.Count * RowH - (Height - HeadH)); }
    bool AtBottom() { return top >= MaxTop() - 2; }
    void SyncScroll()
    {
        int max = MaxTop();
        vs.Visible = max > 0;
        if (vs.Visible) { vs.Minimum = 0; vs.Maximum = max + vs.LargeChange - 1; vs.LargeChange = Math.Max(RowH, Height - HeadH); vs.Value = Math.Min(top, max); }
    }
    protected override void OnResize(EventArgs e) { base.OnResize(e); vs.Bounds = new Rectangle(Width - vs.Width - 2, HeadH, vs.Width, Math.Max(0, Height - HeadH - 2)); top = Math.Min(top, MaxTop()); SyncScroll(); Invalidate(); }
    protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); top = Math.Max(0, Math.Min(MaxTop(), top - e.Delta / 40 * RowH)); tail = AtBottom(); SyncScroll(); Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); Focus();
        int i = RowAt(e.Y);
        if (i >= 0) { selected = i; Invalidate(); }
    }
    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); var h = RowAt(e.Y); if (h != hover) { hover = h; Invalidate(); } }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = -1; Invalidate(); }
    protected override void OnMouseDoubleClick(MouseEventArgs e) { base.OnMouseDoubleClick(e); CopySelected(); }
    int RowAt(int y) { if (y < HeadH) return -1; int i = (y - HeadH + top) / RowH; return i >= 0 && i < rows.Count ? i : -1; }
    public void CopySelected() { if (selected < 0 || selected >= rows.Count) return; var r = rows[selected]; try { Clipboard.SetText(r.Time + "  " + r.ThreadLabel + "  " + r.Kind + "  " + r.Text); } catch (Exception) { } }
    protected override bool ProcessCmdKey(ref Message msg, Keys keys) { if (keys == (Keys.Control | Keys.C)) { CopySelected(); return true; } return base.ProcessCmdKey(ref msg, keys); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; Theme.Smooth(g);
        using (var b = new SolidBrush(Theme.Surface)) g.FillRectangle(b, 0, 0, Width, Height);
        int cTime = 66, cThread = Math.Min(190, Math.Max(90, Width / 5)), cKind = 74;
        using (var b = new SolidBrush(Theme.Console)) g.FillRectangle(b, 0, 0, Width, HeadH);
        using (var pen = new Pen(Theme.Border, 1)) g.DrawLine(pen, 0, HeadH - 1, Width, HeadH - 1);
        TextRenderer.DrawText(g, "时间", Theme.Tiny, new Rectangle(12, 0, cTime, HeadH), Theme.Muted, TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(g, "对话线程", Theme.Tiny, new Rectangle(12 + cTime, 0, cThread, HeadH), Theme.Muted, TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(g, "操作", Theme.Tiny, new Rectangle(12 + cTime + cThread, 0, cKind, HeadH), Theme.Muted, TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(g, "内容 / 状态", Theme.Tiny, new Rectangle(12 + cTime + cThread + cKind, 0, Width, HeadH), Theme.Muted, TextFormatFlags.VerticalCenter);
        if (rows.Count == 0)
        {
            TextRenderer.DrawText(g, "暂无操作记录", Theme.Small, new Rectangle(0, HeadH, Width, Math.Max(40, Height - HeadH)), Theme.Faint, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }
        g.SetClip(new Rectangle(0, HeadH, Width, Height - HeadH));
        int first = top / RowH, y = HeadH - (top % RowH);
        for (int i = first; i < rows.Count && y < Height; i++, y += RowH)
        {
            var r = rows[i];
            if (i == selected) { using (var b = new SolidBrush(Theme.RowSelect)) g.FillRectangle(b, 0, y, Width, RowH); }
            else if (i == hover) { using (var b = new SolidBrush(Theme.RowHover)) g.FillRectangle(b, 0, y, Width, RowH); }
            using (var pen = new Pen(Theme.Border, 1)) g.DrawLine(pen, 0, y + RowH - 1, Width, y + RowH - 1);
            TextRenderer.DrawText(g, r.Time, Theme.MonoSmall, new Rectangle(12, y, cTime - 6, RowH), Theme.Faint, TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, r.ThreadLabel, Theme.Small, new Rectangle(12 + cTime, y, cThread - 10, RowH), Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            DrawBadge(g, r.Kind, 12 + cTime + cThread, y + 6, cKind);
            Color tc = Theme.Text;
            if (r.Text.IndexOf("ERROR", StringComparison.OrdinalIgnoreCase) >= 0 || r.Text.IndexOf("失败", StringComparison.Ordinal) >= 0) tc = Theme.Danger;
            else if (r.Text.EndsWith("| START")) tc = Theme.Primary;
            TextRenderer.DrawText(g, r.Text, Theme.MonoSmall, new Rectangle(12 + cTime + cThread + cKind, y, Width - (12 + cTime + cThread + cKind) - 14, RowH), tc, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
        g.ResetClip();
        if (!tail)
        {
            var label = "回到最新 ↓";
            int w = TextRenderer.MeasureText(g, label, Theme.Tiny).Width + 18;
            var rect = new Rectangle(Width - w - 20, Height - 28, w, 22);
            using (var path = Theme.Round(rect, 11)) { using (var b = new SolidBrush(Theme.PrimarySoft)) g.FillPath(b, path); using (var pen = new Pen(Theme.Accent, 1)) g.DrawPath(pen, path); }
            TextRenderer.DrawText(g, label, Theme.Tiny, rect, Theme.Primary, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            jumpRect = rect;
        }
        else jumpRect = Rectangle.Empty;
    }
    Rectangle jumpRect;
    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (!tail && jumpRect.Contains(e.Location)) { tail = true; top = MaxTop(); SyncScroll(); Invalidate(); }
    }
    static void DrawBadge(Graphics g, string kind, int x, int y, int max)
    {
        bool tool = kind == "工具调用";
        Color fore = tool ? Theme.Primary : Theme.Muted;
        Color bg = tool ? Theme.PrimarySoft : Theme.NeutralSoft;
        int w = Math.Min(max, TextRenderer.MeasureText(g, kind, Theme.Tiny).Width + 14);
        var rect = new Rectangle(x, y, w, 18);
        using (var path = Theme.Round(rect, 9)) { using (var b = new SolidBrush(bg)) g.FillPath(b, path); }
        TextRenderer.DrawText(g, kind, Theme.Tiny, rect, fore, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

public sealed class LogLine { public string Time, Raw, Chip; public Color ChipFore, ChipBack, Fore; }

// Virtualized console for the raw stream: muted mono timestamps, level chips, tail follow.
class LogView : Control
{
    readonly List<LogLine> lines = new List<LogLine>();
    readonly VScrollBar vs = new VScrollBar { Width = 10 };
    readonly HScrollBar hs = new HScrollBar { Height = 10 };
    int top, left, hover = -1; bool tail = true;
    const int RowH = 21;
    readonly System.Windows.Forms.Timer tipTimer = new System.Windows.Forms.Timer { Interval = 450 };
    readonly ToolTip tip = new ToolTip { InitialDelay = 200 };
    string tipText;
    public LogView()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Theme.Console; TabStop = true;
        vs.Scroll += (s, e) => { top = e.NewValue; tail = AtBottom(); Invalidate(); };
        hs.Scroll += (s, e) => { left = e.NewValue; Invalidate(); };
        Controls.Add(vs); Controls.Add(hs);
        tipTimer.Tick += (s, e) => { tipTimer.Stop(); if (hover >= 0 && hover < lines.Count) { tipText = lines[hover].Time + "  " + lines[hover].Raw; if (tipText.Length > 4000) tipText = tipText.Substring(0, 4000) + " …"; tip.Show(tipText, this, 18, Math.Max(4, hover * RowH - top + RowH), 4000); } };
        MouseLeave += (s, e) => { tipTimer.Stop(); tip.Hide(this); };
    }
    public int Count { get { return lines.Count; } }
    public void Append(LogLine line)
    {
        lines.Add(line);
        if (lines.Count > 4000) { int removed = lines.Count - 4000; lines.RemoveRange(0, removed); top = Math.Max(0, top - removed * RowH); }
        if (tail) top = MaxTop();
        SyncScroll(); Invalidate();
    }
    public void Clear() { lines.Clear(); top = 0; left = 0; tail = true; SyncScroll(); Invalidate(); }
    public new string Text
    {
        get { var sb = new System.Text.StringBuilder(); foreach (var l in lines) sb.AppendLine(l.Time + " " + l.Raw); return sb.ToString(); }
    }
    int MaxTop() { return Math.Max(0, lines.Count * RowH - Height); }
    bool AtBottom() { return top >= MaxTop() - 2; }
    int MaxLeft() { return Math.Max(0, widest - Width + 24); }
    int widest;
    public void NoteWidth(int w) { if (w > widest) widest = w; }
    void SyncScroll()
    {
        int max = MaxTop();
        vs.Visible = max > 0;
        if (vs.Visible) { vs.Minimum = 0; vs.Maximum = max + vs.LargeChange - 1; vs.LargeChange = Math.Max(RowH, Height); vs.Value = Math.Min(top, max); }
        hs.Visible = MaxLeft() > 0;
        if (hs.Visible) { hs.Minimum = 0; hs.Maximum = MaxLeft() + hs.LargeChange - 1; hs.LargeChange = Math.Max(20, Width); hs.Value = Math.Min(left, MaxLeft()); }
    }
    protected override void OnResize(EventArgs e) { base.OnResize(e); vs.Bounds = new Rectangle(Width - vs.Width - 2, 2, vs.Width, Math.Max(0, Height - (hs.Visible ? hs.Height : 0) - 4)); hs.Bounds = new Rectangle(2, Height - hs.Height - 2, Math.Max(0, Width - (vs.Visible ? vs.Width : 0) - 6), hs.Height); top = Math.Min(top, MaxTop()); SyncScroll(); Invalidate(); }
    protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); top = Math.Max(0, Math.Min(MaxTop(), top - e.Delta / 40 * RowH)); tail = AtBottom(); SyncScroll(); Invalidate(); }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int i = (e.Y + top) / RowH;
        if (i < 0 || i >= lines.Count) i = -1;
        if (i != hover) { hover = i; Invalidate(); tipTimer.Stop(); tip.Hide(this); if (i >= 0) tipTimer.Start(); }
    }
    protected override void OnMouseDoubleClick(MouseEventArgs e) { base.OnMouseDoubleClick(e); CopyHover(); }
    public void CopyHover() { if (hover < 0 || hover >= lines.Count) return; try { Clipboard.SetText(lines[hover].Time + "  " + lines[hover].Raw); } catch (Exception) { } }
    protected override bool ProcessCmdKey(ref Message msg, Keys keys) { if (keys == (Keys.Control | Keys.C)) { CopyHover(); return true; } return base.ProcessCmdKey(ref msg, keys); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; Theme.Smooth(g);
        using (var b = new SolidBrush(Theme.Console)) g.FillRectangle(b, 0, 0, Width, Height);
        if (lines.Count == 0)
        {
            TextRenderer.DrawText(g, "暂无日志", Theme.Small, new Rectangle(0, 0, Width, Height), Theme.Faint, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }
        int first = top / RowH, y = -(top % RowH);
        for (int i = first; i < lines.Count && y < Height; i++, y += RowH)
        {
            var l = lines[i];
            if (i == hover) { using (var b = new SolidBrush(Theme.RowHover)) g.FillRectangle(b, 0, y, Width, RowH); }
            int x = 10 - left;
            TextRenderer.DrawText(g, l.Time, Theme.MonoSmall, new Rectangle(x, y, 62, RowH), Theme.Faint, TextFormatFlags.VerticalCenter);
            x += 68;
            if (l.Chip.Length > 0)
            {
                int w = TextRenderer.MeasureText(g, l.Chip, Theme.Tiny).Width + 12;
                var rect = new Rectangle(x, y + 3, w, 15);
                using (var path = Theme.Round(rect, 7)) { using (var b = new SolidBrush(l.ChipBack)) g.FillPath(b, path); }
                TextRenderer.DrawText(g, l.Chip, Theme.Tiny, rect, l.ChipFore, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                x += w + 8;
            }
            TextRenderer.DrawText(g, l.Raw, Theme.MonoSmall, new Rectangle(x, y, Width - x - 14, RowH), l.Fore, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
        if (!tail)
        {
            var label = "回到最新 ↓";
            int w = TextRenderer.MeasureText(g, label, Theme.Tiny).Width + 18;
            var rect = new Rectangle(Width - w - 20, Height - 30, w, 22);
            using (var path = Theme.Round(rect, 11)) { using (var b = new SolidBrush(Theme.PrimarySoft)) g.FillPath(b, path); using (var pen = new Pen(Theme.Accent, 1)) g.DrawPath(pen, path); }
            TextRenderer.DrawText(g, label, Theme.Tiny, rect, Theme.Primary, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            jumpRect = rect;
        }
        else jumpRect = Rectangle.Empty;
    }
    Rectangle jumpRect;
    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e); Focus();
        if (!tail && jumpRect.Contains(e.Location)) { tail = true; top = MaxTop(); SyncScroll(); Invalidate(); }
    }
}

// Painted dropdown (rounded frame + chevron) backed by a ContextMenuStrip popup.
class UiSelect : Control
{
    readonly List<string> items = new List<string>();
    int selected; bool hover, open;
    public event Action SelectedChanged;
    public UiSelect()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent; Height = 30; Cursor = Cursors.Hand; TabStop = true;
    }
    public string SelectedItem { get { return selected >= 0 && selected < items.Count ? items[selected] : null; } }
    public void AddItem(string s) { if (!items.Contains(s)) { items.Add(s); Invalidate(); } }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); Focus();
        if (open || items.Count == 0) return;
        open = true; Invalidate();
        var menu = new ContextMenuStrip { ShowImageMargin = false, Font = Theme.Small };
        for (int i = 0; i < items.Count; i++)
        {
            int index = i;
            var mi = new ToolStripMenuItem(items[i]) { Checked = i == selected };
            mi.Click += (s, ev) => { if (selected != index) { selected = index; Invalidate(); if (SelectedChanged != null) SelectedChanged(); } };
            menu.Items.Add(mi);
        }
        menu.Closed += (s, ev) => { open = false; Invalidate(); };
        menu.Show(this, new Point(0, Height - 1));
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; Theme.Smooth(g);
        var r = new Rectangle(0, 0, Width, Height);
        using (var path = Theme.Round(r, 8))
        {
            using (var b = new SolidBrush(Theme.Surface)) g.FillPath(b, path);
            using (var pen = new Pen(open || Focused ? Theme.Accent : hover ? Theme.BorderStrong : Theme.BorderStrong, open || Focused ? 1.6f : 1)) g.DrawPath(pen, path);
        }
        string text = SelectedItem ?? "";
        TextRenderer.DrawText(g, text, Theme.Small, new Rectangle(11, 0, Width - 34, Height), Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        using (var b = new SolidBrush(Theme.Muted))
        {
            var tri = new PointF[] { new PointF(Width - 19, Height / 2 - 2), new PointF(Width - 11, Height / 2 - 2), new PointF(Width - 15, Height / 2 + 3) };
            g.FillPolygon(b, tri);
        }
    }
}

// Single-line right-aligned context text; a WinForms Label would wrap, so draw with ellipsis.
class UiContext : Control
{
    public UiContext()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        TextRenderer.DrawText(e.Graphics, Text, Theme.Tiny, new Rectangle(0, 0, Width - 12, Height), Theme.Faint, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }
}

public sealed class UiMenuItem { public string Text; public Action Click; public bool Enabled = true; public bool Separator; }

// Custom dropdown surface: rounded card, hover tint, separators; replaces the system menu look.
sealed class UiMenuPanel : Control
{
    readonly List<UiMenuItem> items; int hover = -1;
    public event Action ItemClicked;
    public UiMenuPanel(List<UiMenuItem> items)
    {
        this.items = items;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Theme.Surface; Cursor = Cursors.Hand;
    }
    public Size Measure()
    {
        int w = 160;
        using (var g = CreateGraphics())
            foreach (var it in items) if (!it.Separator) w = Math.Max(w, TextRenderer.MeasureText(g, it.Text, Theme.Small).Width + 44);
        return new Size(w, HeightOf());
    }
    int HeightOf() { int h = 8; foreach (var it in items) h += it.Separator ? 9 : 30; return h + 4; }
    int RowAt(int y) { int cur = 8; for (int i = 0; i < items.Count; i++) { int rh = items[i].Separator ? 9 : 30; if (y >= cur && y < cur + rh) return i; cur += rh; } return -1; }
    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); var h = RowAt(e.Y); if (h != hover) { hover = h; Invalidate(); } }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = -1; Invalidate(); }
    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        int i = RowAt(e.Y);
        if (i < 0 || items[i].Separator || !items[i].Enabled) return;
        if (items[i].Click != null) items[i].Click();
        if (ItemClicked != null) ItemClicked();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; Theme.Smooth(g);
        var r = new Rectangle(0, 0, Width, Height);
        using (var path = Theme.Round(r, 10)) { using (var b = new SolidBrush(Theme.Surface)) g.FillPath(b, path); using (var pen = new Pen(Theme.BorderStrong, 1)) g.DrawPath(pen, path); }
        int y = 8;
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            if (it.Separator) { using (var pen = new Pen(Theme.Border, 1)) g.DrawLine(pen, 10, y + 4, Width - 10, y + 4); y += 9; continue; }
            if (i == hover && it.Enabled) { var hr = new Rectangle(6, y, Width - 12, 28); using (var path = Theme.Round(hr, 6)) { using (var b = new SolidBrush(Theme.RowHover)) g.FillPath(b, path); } }
            TextRenderer.DrawText(g, it.Text, Theme.Small, new Rectangle(16, y, Width - 32, 28), it.Enabled ? Theme.Text : Theme.Faint, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            y += 30;
        }
    }
}

sealed class UiMenuForm : Form
{
    public UiMenuForm(List<UiMenuItem> items, Point screen)
    {
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; TopMost = true;
        var p = new UiMenuPanel(items); p.ItemClicked += () => Close();
        Controls.Add(p); p.Dock = DockStyle.Fill;
        Size = p.Measure();
        var area = Screen.FromPoint(screen).WorkingArea;
        int x = Math.Max(area.X + 4, Math.Min(screen.X, area.Right - Width - 4));
        int y = screen.Y; if (y + Height > area.Bottom) y = Math.Max(area.Y + 4, screen.Y - Height);
        Location = new Point(x, y);
        Deactivate += (s, e) => Close();
        Shown += (s, e) => Region = new Region(Theme.Round(new Rectangle(0, 0, Width, Height), 10));
    }
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ClassStyle |= 0x20000; return cp; } }
}
