// PortPilot: Windows 10/11, .NET Framework 4.8, C# 5. No NuGet dependencies.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PortPilot {
    public sealed class PortEntry {
        public string Protocol { get; set; }
        public int Port { get; set; }
        public string Address { get; set; }
        public int Pid { get; set; }
        public string Technology { get; set; }
        public string ProcessName { get; set; }
        public string Path { get; set; }
        public string Command { get; set; }
        public string Access { get; set; }
        public long Created;
        public int Session;
        public DateTime? DisappearedUtc;
        public bool IsActive { get { return !DisappearedUtc.HasValue; } }
        public bool CanStop { get { return IsActive && Access == "Disponible"; } }
        public string StatusText { get { return !IsActive ? "● Inactivo" : Access != null && Access.StartsWith("Protegido:") ? "● Activo · protegido" : Access != "Disponible" ? "● Activo · sin acceso" : "● Activo"; } }
        public string ActionText { get { return !IsActive ? "Registro reciente" : CanStop ? "Se puede detener" : Access; } }
        public string Identity { get { return Key + "|" + Created; } }
        public string Key { get { return Protocol + "|" + Address + "|" + Port + "|" + Pid; } }
    }

    // Inactive means an observed endpoint disappeared, never that a port is guaranteed free.
    public sealed class PortHistory {
        List<PortEntry> previous = new List<PortEntry>();
        public List<PortEntry> Merge(List<PortEntry> live, DateTime now) {
            var identities = new HashSet<string>(live.Select(r => r.Identity));
            var recent = new List<PortEntry>();
            foreach (var old in previous) {
                if (identities.Contains(old.Identity)) continue;
                if (!old.DisappearedUtc.HasValue) old.DisappearedUtc = now;
                if (now - old.DisappearedUtc.Value < TimeSpan.FromMinutes(5)) recent.Add(old);
            }
            previous = live.Concat(recent.OrderByDescending(r => r.DisappearedUtc).Take(500)).OrderBy(r => !r.IsActive).ThenBy(r => r.Port).ThenBy(r => r.Protocol).ThenBy(r => r.Address).ToList();
            return previous;
        }
    }

    public static class Engine {
        [DllImport("iphlpapi.dll")] static extern uint GetExtendedTcpTable(IntPtr buffer, ref int size, bool order, int family, int tableClass, uint reserved);
        [DllImport("iphlpapi.dll")] static extern uint GetExtendedUdpTable(IntPtr buffer, ref int size, bool order, int family, int tableClass, uint reserved);
        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool GetProcessTimes(IntPtr handle, out long created, out long exited, out long kernel, out long user);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool IsProcessCritical(IntPtr handle, out bool critical);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool TerminateProcess(IntPtr handle, uint code);
        [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr handle, uint timeout);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
        static readonly HashSet<string> Protected = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "system", "idle", "registry", "smss", "csrss", "wininit", "winlogon", "services", "lsass", "svchost", "dwm", "fontdrvhost", "secure system", "memory compression"
        };
        public static string Classify(string name) {
            string n = System.IO.Path.GetFileNameWithoutExtension(name ?? "").ToLowerInvariant();
            if (n == "node" || n == "nodejs") return "Node.js";
            if (n == "python" || n == "pythonw" || System.Text.RegularExpressions.Regex.IsMatch(n, @"^python\d+(?:\.\d+)?[wt]?$")) return "Python";
            if (n == "php" || n == "php-cgi" || n == "php-fpm") return "PHP";
            if (n == "nginx") return "Nginx";
            if (n == "httpd" || n == "apache" || n == "apache2") return "Apache";
            if (n == "dotnet" || n == "iisexpress" || n == "w3wp") return ".NET / IIS";
            if (n == "java" || n == "javaw") return "Java";
            if (n == "mysqld" || n == "mariadbd") return "MySQL / MariaDB";
            if (n == "postgres") return "PostgreSQL";
            if (n == "redis-server") return "Redis";
            if (n == "mongod") return "MongoDB";
            if (n == "docker-proxy" || n == "com.docker.backend" || n == "docker desktop") return "Docker";
            if (n == "wslhost" || n == "wslrelay" || n == "wslservice") return "WSL";
            if (n == "deno") return "Deno";
            if (n == "bun") return "Bun";
            if (Protected.Contains(n)) return "Windows";
            return "Otro";
        }
        static void ReadTable(List<PortEntry> rows, bool tcp, bool ipv6) {
            int size = 0, family = ipv6 ? 23 : 2;
            uint code = tcp ? GetExtendedTcpTable(IntPtr.Zero, ref size, false, family, 3, 0) : GetExtendedUdpTable(IntPtr.Zero, ref size, false, family, 1, 0);
            if (code != 122 && code != 0) throw new Win32Exception((int)code);
            for (int attempt = 0; attempt < 5; attempt++) {
                int allocated = Math.Max(size, 4);
                IntPtr memory = Marshal.AllocHGlobal(allocated);
                try {
                    code = tcp ? GetExtendedTcpTable(memory, ref size, false, family, 3, 0) : GetExtendedUdpTable(memory, ref size, false, family, 1, 0);
                    if (code == 122) continue;
                    if (code != 0) throw new Win32Exception((int)code);
                    int count = Marshal.ReadInt32(memory);
                    int stride = tcp ? (ipv6 ? 56 : 24) : (ipv6 ? 28 : 12);
                    if (count < 0 || count > (allocated - 4) / stride) throw new InvalidDataException("Tabla de puertos inválida.");
                    for (int i = 0; i < count; i++) {
                        IntPtr row = IntPtr.Add(memory, 4 + i * stride);
                        int addrOffset = tcp && !ipv6 ? 4 : 0;
                        byte[] bytes = new byte[ipv6 ? 16 : 4];
                        Marshal.Copy(IntPtr.Add(row, addrOffset), bytes, 0, bytes.Length);
                        int portOffset = ipv6 ? 20 : (tcp ? 8 : 4);
                        int port = (Marshal.ReadByte(row, portOffset) << 8) | Marshal.ReadByte(row, portOffset + 1);
                        long scope = ipv6 ? (uint)IPAddress.NetworkToHostOrder(Marshal.ReadInt32(row, 16)) : 0;
                        string address = ipv6 ? new IPAddress(bytes, scope).ToString() : new IPAddress(bytes).ToString();
                        rows.Add(new PortEntry { Protocol = tcp ? "TCP" : "UDP", Address = address, Port = port, Pid = Marshal.ReadInt32(row, stride - 4) });
                    }
                    return;
                } finally { Marshal.FreeHGlobal(memory); }
            }
            throw new IOException("La tabla cambió durante la lectura. Pulsa Actualizar.");
        }
        public static List<PortEntry> ReadPorts() {
            var rows = new List<PortEntry>();
            ReadTable(rows, true, false); ReadTable(rows, true, true);
            ReadTable(rows, false, false); ReadTable(rows, false, true);
            return rows;
        }
        public static List<PortEntry> Snapshot() {
            var rows = ReadPorts();
            var metadata = new Dictionary<int, string[]>();
            try {
                var options = new EnumerationOptions { Timeout = TimeSpan.FromSeconds(5), ReturnImmediately = false };
                using (var search = new ManagementObjectSearcher("root\\CIMV2", "SELECT ProcessId, ExecutablePath, CommandLine, CreationDate FROM Win32_Process", options))
                using (var collection = search.Get()) {
                    foreach (ManagementObject obj in collection) using (obj) {
                        metadata[Convert.ToInt32(obj["ProcessId"])] = new[] { Convert.ToString(obj["ExecutablePath"]), Convert.ToString(obj["CommandLine"]), Convert.ToString(obj["CreationDate"]) };
                    }
                }
            } catch (ManagementException) { } catch (UnauthorizedAccessException) { } catch (System.Runtime.InteropServices.COMException) { }
            foreach (var group in rows.GroupBy(r => r.Pid)) {
                string name = "No disponible", path = "", command = "";
                long created = 0; int session = -1;
                try {
                    using (var p = Process.GetProcessById(group.Key)) {
                        name = p.ProcessName + ".exe";
                        session = p.SessionId;
                        created = p.StartTime.ToUniversalTime().ToFileTimeUtc();
                        string[] meta;
                        if (metadata.TryGetValue(group.Key, out meta) && !String.IsNullOrEmpty(meta[2]) &&
                            Math.Abs(ManagementDateTimeConverter.ToDateTime(meta[2]).ToUniversalTime().ToFileTimeUtc() - created) < 10000000) {
                            path = meta[0]; command = meta[1];
                        }
                        if (String.IsNullOrEmpty(path)) { try { path = p.MainModule.FileName; } catch (Win32Exception) { } }
                    }
                } catch (ArgumentException) { } catch (Win32Exception) { } catch (InvalidOperationException) { }
                string blocked = BlockReason(group.Key, name, created, session);
                foreach (var row in group) {
                    row.ProcessName = name; row.Path = path; row.Command = command;
                    row.Created = created; row.Session = session; row.Technology = Classify(name);
                    row.Access = blocked ?? "Disponible";
                }
            }
            return rows.OrderBy(r => r.Port).ThenBy(r => r.Protocol).ThenBy(r => r.Address).ToList();
        }
        public static string BlockReason(int pid, string name, long created, int session) {
            if (pid <= 4 || Protected.Contains(System.IO.Path.GetFileNameWithoutExtension(name ?? ""))) return "Protegido: Windows";
            if (pid == Process.GetCurrentProcess().Id) return "Protegido: esta app";
            if (session == 0) return "Protegido: servicio";
            if (created == 0 || session < 0) return "Sin acceso / finalizado";
            return null;
        }
        public static void Stop(PortEntry selected) {
            if (!selected.IsActive) throw new InvalidOperationException("Este registro está inactivo. Actualiza para ver quién ocupa el puerto ahora.");
            string blocked = BlockReason(selected.Pid, selected.ProcessName, selected.Created, selected.Session);
            if (blocked != null) throw new InvalidOperationException(blocked);
            // Bind identity and termination to the SAME handle, guarding against PID reuse.
            IntPtr handle = OpenProcess(0x00101001, false, selected.Pid); // synchronize, query limited, terminate
            if (handle == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "No se pudo abrir el proceso. Puede requerir permisos de administrador.");
            try {
                long created, exited, kernel, user;
                if (!GetProcessTimes(handle, out created, out exited, out kernel, out user)) throw new Win32Exception(Marshal.GetLastWin32Error());
                if (created != selected.Created) throw new InvalidOperationException("El proceso cambió. Actualiza la lista antes de continuar.");
                bool critical;
                if (!IsProcessCritical(handle, out critical)) throw new Win32Exception(Marshal.GetLastWin32Error());
                if (critical) throw new InvalidOperationException("Windows identifica este proceso como crítico. Se bloqueó su cierre.");
                if (!ReadPorts().Any(r => r.Key == selected.Key)) throw new InvalidOperationException("El proceso ya no ocupa ese puerto. Actualiza la lista.");
                if (!TerminateProcess(handle, 1)) throw new Win32Exception(Marshal.GetLastWin32Error());
                if (WaitForSingleObject(handle, 4000) != 0) throw new IOException("Se solicitó el cierre, pero el proceso aún no terminó. Actualiza la lista.");
            } finally { CloseHandle(handle); }
        }
        public static string Csv(string value) {
            string text = value ?? "";
            if (text.TrimStart().Length > 0 && "=+-@".IndexOf(text.TrimStart()[0]) >= 0) text = "'" + text;
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }
    }

    public sealed class MainForm : Form {
        readonly Color blue = Color.FromArgb(0, 113, 227), ink = Color.FromArgb(30, 32, 38), muted = Color.FromArgb(114, 119, 130);
        readonly TextBox search = new TextBox(), details = new TextBox();
        readonly SelectField technology = new SelectField(), protocol = new SelectField(), stateFilter = new SelectField();
        readonly DataGridView grid = new DataGridView();
        readonly MetricCard activeCount = new MetricCard(), protectedCount = new MetricCard(), inactiveCount = new MetricCard(), unknownCount = new MetricCard();
        readonly Label status = new Label(), guidance = new Label(), empty = new Label(), selectionTitle = new Label(), resultCount = new Label();
        readonly SoftButton refresh = new SoftButton(), stop = new SoftButton(), export = new SoftButton();
        readonly CheckBox automatic = new CheckBox();
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        readonly ToolTip tips = new ToolTip { AutoPopDelay = 12000, InitialDelay = 400, ReshowDelay = 200 };
        readonly PortHistory history = new PortHistory();
        List<PortEntry> all = new List<PortEntry>();
        bool busy;
        public MainForm() {
            Text = "PortPilot · Monitor de puertos"; StartPosition = FormStartPosition.CenterScreen;
            AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 10); ForeColor = ink; BackColor = Color.FromArgb(245, 245, 247);
            Size = new Size(1360, 940); MinimumSize = new Size(1120, 780);
            var content = new TableLayoutPanel { ColumnCount = 1, RowCount = 7, BackColor = BackColor, Margin = new Padding(0) };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 108));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 114));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 180));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); Controls.Add(content);
            Action placeContent = delegate {
                int scale = DeviceDpi; int gap = 28 * scale / 96;
                int width = Math.Min(ClientSize.Width - 2 * gap, 1460 * scale / 96);
                content.SetBounds((ClientSize.Width - width) / 2, 18 * scale / 96, width, Math.Max(300, ClientSize.Height - 36 * scale / 96));
            };
            Resize += delegate { placeContent(); }; DpiChanged += delegate { placeContent(); }; placeContent();
            var header = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            header.Controls.Add(new Label { Text = "PortPilot", Font = new Font("Segoe UI", 25, FontStyle.Bold), ForeColor = ink, AutoSize = true, Location = new Point(0, 0) });
            header.Controls.Add(new Label { Text = "Tus puertos, bajo control.", ForeColor = muted, Font = new Font("Segoe UI", 10), AutoSize = true, Location = new Point(3, 48) });
            var help = new SoftButton(); SetupButton(help, "Guía rápida", false); help.Width = 124;
            SetupButton(refresh, "↻  Actualizar", true); refresh.Width = 144;
            var headerActions = new FlowLayoutPanel { Size = new Size(280, 44), Anchor = AnchorStyles.Top | AnchorStyles.Right, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = BackColor };
            headerActions.Controls.Add(help); headerActions.Controls.Add(refresh); header.Controls.Add(headerActions);
            header.Resize += delegate { headerActions.Location = new Point(header.Width - headerActions.Width, 12 * DeviceDpi / 96); }; content.Controls.Add(header, 0, 0);
            var cards = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Margin = new Padding(0, 0, 0, 12) };
            for (int i = 0; i < 4; i++) cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            SetupCard(activeCount, "Activos", "Puertos en uso", Color.FromArgb(40, 144, 90));
            SetupCard(protectedCount, "Protegidos", "Incluidos en los activos", Color.FromArgb(184, 125, 25));
            SetupCard(inactiveCount, "Inactivos", "Últimos 5 minutos", Color.FromArgb(202, 70, 79));
            SetupCard(unknownCount, "Sin acceso", "Incluidos en los activos", Color.FromArgb(130, 137, 150)); unknownCount.Margin = new Padding(0);
            cards.Controls.Add(activeCount, 0, 0); cards.Controls.Add(protectedCount, 1, 0); cards.Controls.Add(inactiveCount, 2, 0); cards.Controls.Add(unknownCount, 3, 0); content.Controls.Add(cards, 0, 1);
            var filterCard = new SoftPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 14), Padding = new Padding(16, 13, 16, 13) };
            var filters = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 2, Margin = new Padding(0), BackColor = Color.White };
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 192)); filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 136)); filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 168));
            filters.RowStyles.Add(new RowStyle(SizeType.Absolute, 25)); filters.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            string[] captions = { "Buscar", "Tecnología", "Protocolo", "Estado" };
            for (int i = 0; i < captions.Length; i++) filters.Controls.Add(new Label { Text = captions[i], AutoSize = true, ForeColor = muted, Font = new Font("Segoe UI", 9), Margin = new Padding(1, 0, 0, 0) }, i, 0);
            var searchFrame = new SoftPanel { Dock = DockStyle.Fill, Radius = 9, Margin = new Padding(0, 0, 14, 0), Outline = Color.FromArgb(215, 218, 224) };
            search.BorderStyle = BorderStyle.None; search.Font = new Font("Segoe UI", 10); search.BackColor = Color.White; search.AccessibleName = "Buscar puerto, proceso, PID o ruta";
            searchFrame.Controls.Add(search); searchFrame.Resize += delegate { int s = DeviceDpi; int h = search.PreferredHeight; search.SetBounds(14 * s / 96, (searchFrame.Height - h) / 2, searchFrame.Width - 28 * s / 96, h); };
            search.GotFocus += delegate { searchFrame.Outline = blue; searchFrame.Invalidate(); }; search.LostFocus += delegate { searchFrame.Outline = Color.FromArgb(215, 218, 224); searchFrame.Invalidate(); };
            filters.Controls.Add(searchFrame, 0, 1);
            technology.Dock = DockStyle.Fill; technology.Margin = new Padding(0, 0, 14, 0); technology.AccessibleName = "Tecnología";
            technology.Items.AddRange(new object[] { "Todas", "Node.js", "Python", "PHP", "Nginx", "Apache", ".NET / IIS", "Java", "MySQL / MariaDB", "PostgreSQL", "Redis", "MongoDB", "Docker", "WSL", "Deno", "Bun", "Windows", "Otro" }); technology.SelectedIndex = 0; filters.Controls.Add(technology, 1, 1);
            protocol.Dock = DockStyle.Fill; protocol.Margin = new Padding(0, 0, 14, 0); protocol.AccessibleName = "Protocolo"; protocol.Items.AddRange(new object[] { "Todos", "TCP", "UDP" }); protocol.SelectedIndex = 0; filters.Controls.Add(protocol, 2, 1);
            stateFilter.Dock = DockStyle.Fill; stateFilter.Margin = new Padding(0); stateFilter.AccessibleName = "Estado"; stateFilter.Items.AddRange(new object[] { "Todos", "Activos", "Inactivos", "Protegidos", "Sin acceso" }); stateFilter.SelectedIndex = 0; filters.Controls.Add(stateFilter, 3, 1);
            filterCard.Controls.Add(filters); content.Controls.Add(filterCard, 0, 2);
            var tableCard = new SoftPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 14), Padding = new Padding(12, 7, 12, 10) };
            var tableLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Margin = new Padding(0), BackColor = Color.White };
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 53)); tableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var tableHeader = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(12, 2, 12, 0) };
            resultCount.Text = "Puertos detectados"; resultCount.Font = new Font("Segoe UI", 12, FontStyle.Bold); resultCount.AutoSize = true; resultCount.Location = new Point(7, 12); tableHeader.Controls.Add(resultCount);
            var legend = new FlowLayoutPanel { AutoSize = false, Size = new Size(409, 30), FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Color.White, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            AddLegend(legend, "● Activo", Color.FromArgb(40, 129, 80)); AddLegend(legend, "● Inactivo", Color.FromArgb(188, 61, 70)); AddLegend(legend, "● Protegido", Color.FromArgb(155, 106, 19)); AddLegend(legend, "● Sin acceso", muted);
            tableHeader.Controls.Add(legend); tableHeader.Resize += delegate { legend.Location = new Point(tableHeader.Width - legend.Width, 13 * DeviceDpi / 96); }; tableLayout.Controls.Add(tableHeader, 0, 0);
            grid.Dock = DockStyle.Fill; grid.Margin = new Padding(0); grid.ReadOnly = true; grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false;
            grid.MultiSelect = false; grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; grid.RowHeadersVisible = false; grid.AutoGenerateColumns = false;
            grid.BackgroundColor = Color.White; grid.BorderStyle = BorderStyle.None; grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.GridColor = Color.FromArgb(240, 241, 244); grid.EnableHeadersVisualStyles = false; grid.ColumnHeadersHeight = 38; grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(250, 250, 252), ForeColor = muted, Font = new Font("Segoe UI", 9), Padding = new Padding(12, 0, 0, 0), SelectionBackColor = Color.FromArgb(250, 250, 252) };
            grid.DefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.White, ForeColor = ink, SelectionBackColor = Color.FromArgb(239, 246, 255), SelectionForeColor = ink, Padding = new Padding(12, 0, 4, 0), Font = new Font("Segoe UI", 9.5f) };
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.White; grid.RowTemplate.Height = 42;
            AddColumn("StatusText", "Estado", 184); AddColumn("Port", "Puerto", 80); AddColumn("Protocol", "Protocolo", 96); AddColumn("Technology", "Tecnología", 166);
            AddColumn("ProcessName", "Proceso", 208); AddColumn("Pid", "PID", 80); AddColumn("Address", "Dirección local", 166);
            grid.Columns["Port"].DefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            grid.Columns["ProcessName"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; grid.Columns["ProcessName"].MinimumWidth = 150;
            grid.Columns["Address"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; grid.Columns["Address"].MinimumWidth = 135;
            grid.CellPainting += PaintStatus; grid.CellFormatting += FormatCell;
            grid.CellToolTipTextNeeded += delegate(object sender, DataGridViewCellToolTipTextNeededEventArgs e) { if (e.RowIndex >= 0 && e.RowIndex < grid.Rows.Count) e.ToolTipText = Explain((PortEntry)grid.Rows[e.RowIndex].DataBoundItem); };
            grid.SelectionChanged += delegate { Selection(); };
            var tablePanel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0), BackColor = Color.White }; tablePanel.Controls.Add(grid);
            empty.Text = "No hay coincidencias\nPrueba otra búsqueda o cambia los filtros."; empty.Dock = DockStyle.Fill; empty.TextAlign = ContentAlignment.MiddleCenter; empty.BackColor = Color.White; empty.ForeColor = muted; empty.Visible = false; tablePanel.Controls.Add(empty);
            tableLayout.Controls.Add(tablePanel, 0, 1); tableCard.Controls.Add(tableLayout); content.Controls.Add(tableCard, 0, 3);
            var detailCard = new SoftPanel { Dock = DockStyle.Fill, Margin = new Padding(0), Padding = new Padding(19, 12, 19, 10) };
            var detailLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = new Padding(0), BackColor = Color.White };
            detailLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); detailLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 37)); detailLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var detailHeader = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0), BackColor = Color.White };
            selectionTitle.Text = "Proceso seleccionado"; selectionTitle.Font = new Font("Segoe UI", 11, FontStyle.Bold); selectionTitle.AutoEllipsis = true; selectionTitle.Dock = DockStyle.Fill; selectionTitle.TextAlign = ContentAlignment.MiddleLeft;
            SetupButton(stop, "Liberar puerto…", true); stop.Width = 164; stop.Dock = DockStyle.Right; stop.Enabled = false;
            detailHeader.Controls.Add(selectionTitle); detailHeader.Controls.Add(stop); detailLayout.Controls.Add(detailHeader, 0, 0);
            guidance.Text = "Selecciona una fila para ver los detalles."; guidance.Dock = DockStyle.Fill; guidance.Font = new Font("Segoe UI", 9); guidance.TextAlign = ContentAlignment.MiddleLeft; guidance.BackColor = Color.White; guidance.Margin = new Padding(0); detailLayout.Controls.Add(guidance, 0, 1);
            details.Dock = DockStyle.Fill; details.Multiline = true; details.ReadOnly = true; details.BorderStyle = BorderStyle.None; details.ScrollBars = ScrollBars.Vertical; details.BackColor = Color.White; details.ForeColor = muted; details.Font = new Font("Segoe UI", 9); details.AccessibleName = "Detalles del proceso"; details.Margin = new Padding(0, 3, 0, 0); detailLayout.Controls.Add(details, 0, 2);
            detailCard.Controls.Add(detailLayout); content.Controls.Add(detailCard, 0, 4);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(0, 12, 0, 0), Margin = new Padding(0), BackColor = BackColor };
            SetupButton(export, "Exportar CSV", false); export.Width = 138; actions.Controls.Add(export);
            var admin = new SoftButton(); SetupButton(admin, "Abrir como administrador", false); admin.Width = 218; actions.Controls.Add(admin);
            automatic.Text = "Actualizar cada 5 s"; automatic.AutoSize = true; automatic.ForeColor = muted; automatic.Margin = new Padding(10, 10, 0, 0); actions.Controls.Add(automatic); content.Controls.Add(actions, 0, 5);
            bool elevated = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator); admin.Enabled = !elevated;
            status.Dock = DockStyle.Fill; status.TextAlign = ContentAlignment.MiddleLeft; status.Font = new Font("Segoe UI", 8.5f); status.ForeColor = muted; status.AutoEllipsis = true; content.Controls.Add(status, 0, 6);
            tips.SetToolTip(search, "Busca un puerto (3000), un proceso (python), un PID o parte de su ruta. Ctrl+F para buscar.");
            tips.SetToolTip(stateFilter, "Activos incluye protegidos y sin acceso. Inactivos muestra registros desaparecidos en los últimos cinco minutos.");
            tips.SetToolTip(stop, "Termina este proceso y cierra todos sus puertos. Se pedirá confirmación."); tips.SetToolTip(refresh, "Actualizar los puertos y procesos · F5");
            help.Click += delegate { ShowGuide(); };
            search.TextChanged += delegate { ApplyFilter(); }; technology.SelectedIndexChanged += delegate { ApplyFilter(); }; protocol.SelectedIndexChanged += delegate { ApplyFilter(); }; stateFilter.SelectedIndexChanged += delegate { ApplyFilter(); };
            refresh.Click += async delegate { await Reload(); }; stop.Click += async delegate { await StopSelected(); }; export.Click += delegate { Export(); };
            admin.Click += delegate { try { Process.Start(new ProcessStartInfo(Application.ExecutablePath) { UseShellExecute = true, Verb = "runas" }); Close(); } catch (Win32Exception ex) { if (ex.NativeErrorCode != 1223) MessageBox.Show(this, ex.Message, "No se pudo abrir", MessageBoxButtons.OK, MessageBoxIcon.Information); } };
            timer.Interval = 5000; timer.Tick += async delegate { if (automatic.Checked && !busy) await Reload(); }; timer.Start();
            FormClosed += delegate { timer.Stop(); timer.Dispose(); tips.Dispose(); };
            Shown += async delegate { placeContent(); await Reload(); };
            KeyPreview = true; KeyDown += async delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.F5) { e.Handled = true; await Reload(); } if (e.Control && e.KeyCode == Keys.F) { e.Handled = true; search.Focus(); search.SelectAll(); } if (e.KeyCode == Keys.F1) { e.Handled = true; ShowGuide(); } };
        }
        void ShowGuide() {
            MessageBox.Show(this, "Busca un puerto (3000, 8000…), un proceso o una tecnología y selecciona una fila." + Environment.NewLine + Environment.NewLine +
                "VERDE · Activo: puerto en uso.\nROJO · Inactivo: dejó de aparecer.\nÁMBAR · Protegido: activo, con cierre bloqueado.\nGRIS · Sin acceso: faltan datos o permisos." + Environment.NewLine + Environment.NewLine +
                "Liberar puerto termina el proceso y cierra todos sus puertos. Revisa su ruta antes de confirmar." + Environment.NewLine + Environment.NewLine +
                "PID es el identificador del proceso. TCP muestra conexiones en escucha; UDP muestra puertos vinculados. Activo no garantiza que el servicio responda correctamente." + Environment.NewLine + Environment.NewLine +
                "Los registros inactivos se conservan cinco minutos en esta sesión (máximo 500). Otro proceso puede ocupar el mismo puerto. Los contadores muestran todos los registros antes de filtrar." + Environment.NewLine + Environment.NewLine +
                "F5 actualiza · Ctrl+F busca · F1 abre esta guía. Los filtros admiten teclado: Tab para enfocar y flechas para elegir.", "Guía rápida de PortPilot", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        void SetupButton(SoftButton button, string text, bool primary) { button.Text = text; button.Height = 40; button.Primary = primary; button.Margin = new Padding(0, 0, 12, 0); button.Font = new Font("Segoe UI", 10); }
        void SetupCard(MetricCard card, string caption, string description, Color accent) { card.Dock = DockStyle.Fill; card.Margin = new Padding(0, 0, 12, 0); card.Caption = caption; card.Description = description; card.Accent = accent; }
        void AddLegend(FlowLayoutPanel panel, string text, Color color) { panel.Controls.Add(new Label { AutoSize = true, Text = text, ForeColor = color, Margin = new Padding(0, 4, 16, 0), Font = new Font("Segoe UI", 8.5f) }); }
        void PaintStatus(object sender, DataGridViewCellPaintingEventArgs e) {
            if (e.RowIndex < 0 || grid.Columns[e.ColumnIndex].Name != "StatusText") return;
            var r = grid.Rows[e.RowIndex].DataBoundItem as PortEntry; if (r == null) return;
            e.PaintBackground(e.CellBounds, true); e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            float s = DeviceDpi / 96f;
            RectangleF badge = new RectangleF(e.CellBounds.X + 11 * s, e.CellBounds.Y + 8 * s, e.CellBounds.Width - 24 * s, e.CellBounds.Height - 16 * s);
            using (var path = Surface.Round(badge, 7 * s)) using (var brush = new SolidBrush(StateBackground(r))) { e.Graphics.FillPath(brush, path); }
            using (var font = new Font("Segoe UI", 8.5f, FontStyle.Bold)) { TextRenderer.DrawText(e.Graphics, r.StatusText, font, Rectangle.Round(badge), StateColor(r), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis); }
            e.Paint(e.CellBounds, DataGridViewPaintParts.Border); e.Handled = true;
        }
        static Color StateColor(PortEntry r) {
            return !r.IsActive ? Color.FromArgb(164, 39, 49) : r.Access != null && r.Access.StartsWith("Protegido:") ? Color.FromArgb(128, 78, 0) : r.Access != "Disponible" ? Color.FromArgb(75, 89, 109) : Color.FromArgb(20, 108, 62);
        }
        static Color StateBackground(PortEntry r) {
            return !r.IsActive ? Color.FromArgb(253, 232, 233) : r.Access != null && r.Access.StartsWith("Protegido:") ? Color.FromArgb(255, 243, 214) : r.Access != "Disponible" ? Color.FromArgb(233, 237, 243) : Color.FromArgb(224, 245, 233);
        }
        static string Explain(PortEntry r) {
            if (!r.IsActive) return "Inactivo: este registro dejó de aparecer a las " + r.DisappearedUtc.Value.ToLocalTime().ToString("HH:mm:ss") + ". Otro proceso podría ocupar el mismo puerto. Actualiza para comprobarlo.";
            if (r.Access != null && r.Access.StartsWith("Protegido:")) return "Activo y protegido: el puerto está en uso. " + r.Access + ". Para servicios, utiliza el administrador de servicios de Windows.";
            if (!r.CanStop) return "Activo, sin acceso: el puerto aparece en Windows, pero faltan datos o permisos del proceso. Actualiza o abre como administrador.";
            return "Activo: el puerto está en uso. Puedes liberarlo deteniendo este proceso; se cerrarán todos sus puertos.";
        }
        void FormatCell(object sender, DataGridViewCellFormattingEventArgs e) {
            if (e.RowIndex < 0 || e.RowIndex >= grid.Rows.Count) return;
            var r = grid.Rows[e.RowIndex].DataBoundItem as PortEntry; if (r == null) return;
            if (!r.IsActive) { e.CellStyle.ForeColor = Color.FromArgb(132, 118, 123); }
        }
        void AddColumn(string field, string title, int width) { grid.Columns.Add(new DataGridViewTextBoxColumn { Name = field, DataPropertyName = field, HeaderText = title, Width = width, SortMode = DataGridViewColumnSortMode.NotSortable }); }
        PortEntry Selected { get { return grid.CurrentRow == null ? null : grid.CurrentRow.DataBoundItem as PortEntry; } }
        void Selection() {
            var r = Selected; stop.Enabled = !busy && r != null && r.CanStop;
            if (r == null) { details.Text = "Aquí verás la ruta, el comando y el identificador (PID) del proceso seleccionado."; guidance.Text = "Selecciona una fila para saber qué puedes hacer. ¿Primera vez? Abre Guía rápida."; guidance.BackColor = Color.White; guidance.ForeColor = muted; selectionTitle.Text = "Proceso seleccionado"; return; }
            guidance.Text = Explain(r); guidance.BackColor = Color.White; guidance.ForeColor = StateColor(r);
            selectionTitle.Text = r.ProcessName + "  ·  PID " + r.Pid + "  ·  " + r.Technology;
            details.Text = "Ruta: " + (String.IsNullOrEmpty(r.Path) ? "No disponible con los permisos actuales" : r.Path) + Environment.NewLine +
                "Comando: " + (String.IsNullOrEmpty(r.Command) ? "No disponible con los permisos actuales" : r.Command) + Environment.NewLine +
                "Puertos activos del mismo proceso: " + String.Join(", ", all.Where(x => x.IsActive && x.Pid == r.Pid && x.Created == r.Created).Select(x => x.Protocol + ":" + x.Port).Distinct()) + "   |   " + r.ActionText;
        }
        void ApplyFilter() {
            string key = Selected == null ? null : Selected.Identity;
            string term = search.Text.Trim(); string tech = Convert.ToString(technology.SelectedItem), proto = Convert.ToString(protocol.SelectedItem), state = Convert.ToString(stateFilter.SelectedItem);
            var visible = all.Where(r => (tech == "Todas" || r.Technology == tech) && (proto == "Todos" || r.Protocol == proto) &&
                (state == "Todos" || state == "Activos" && r.IsActive || state == "Inactivos" && !r.IsActive || state == "Protegidos" && r.IsActive && r.Access.StartsWith("Protegido:") || state == "Sin acceso" && r.IsActive && !r.CanStop && !r.Access.StartsWith("Protegido:")) &&
                (term.Length == 0 || (r.Port + " " + r.Protocol + " " + r.Pid + " " + r.Technology + " " + r.ProcessName + " " + r.Address + " " + r.Path).IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
            grid.DataSource = visible;
            if (key != null) foreach (DataGridViewRow row in grid.Rows) if (((PortEntry)row.DataBoundItem).Identity == key) { grid.CurrentCell = row.Cells[0]; break; }
            activeCount.Count = all.Count(r => r.IsActive);
            protectedCount.Count = all.Count(r => r.IsActive && r.Access.StartsWith("Protegido:"));
            inactiveCount.Count = all.Count(r => !r.IsActive);
            unknownCount.Count = all.Count(r => r.IsActive && !r.CanStop && !r.Access.StartsWith("Protegido:"));
            resultCount.Text = "Puertos detectados · " + visible.Count;
            empty.Visible = visible.Count == 0; if (empty.Visible) empty.BringToFront();
            Selection();
        }
        async Task Reload() {
            if (busy) return; busy = true; refresh.Enabled = false; stop.Enabled = false;
            status.ForeColor = blue; status.Text = "Consultando puertos y procesos…";
            try { var data = await Task.Run(() => Engine.Snapshot()); if (IsDisposed) return; all = history.Merge(data, DateTime.UtcNow); ApplyFilter(); status.ForeColor = ink; status.Text = "Actualizado " + DateTime.Now.ToString("HH:mm:ss") + " · TCP en escucha / UDP vinculado · Los contadores incluyen todos los registros"; }
            catch (Exception ex) { if (!IsDisposed) { status.ForeColor = Color.FromArgb(164, 39, 49); status.Text = "No se pudo actualizar; se conservan los datos anteriores: " + ex.Message; } }
            finally { busy = false; if (!IsDisposed) { refresh.Enabled = true; Selection(); } }
        }
        async Task StopSelected() {
            var selected = Selected; if (selected == null || busy || !selected.CanStop) return;
            busy = true; stop.Enabled = false; refresh.Enabled = false;
            try {
                string ports = String.Join(", ", all.Where(x => x.IsActive && x.Pid == selected.Pid && x.Created == selected.Created).Select(x => x.Protocol + ":" + x.Port).Distinct());
                string question = "Se terminará de forma forzada " + selected.ProcessName + " (PID " + selected.Pid + ")." + Environment.NewLine + Environment.NewLine +
                    "Puertos del proceso: " + ports + Environment.NewLine + "Ejecutable: " + (String.IsNullOrEmpty(selected.Path) ? "No disponible" : selected.Path) + Environment.NewLine + Environment.NewLine +
                    "Se cerrarán todos sus puertos y se perderá el trabajo no guardado de ese proceso. Si un supervisor lo reinicia, el puerto puede volver a ocuparse." + Environment.NewLine + Environment.NewLine + "¿Terminar este proceso?";
                if (MessageBox.Show(this, question, "Confirmar liberación de puerto", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
                await Task.Run(() => Engine.Stop(selected));
                var data = await Task.Run(() => Engine.Snapshot()); if (IsDisposed) return; all = history.Merge(data, DateTime.UtcNow); ApplyFilter();
                bool occupied = all.Any(x => x.IsActive && x.Protocol == selected.Protocol && x.Port == selected.Port && x.Address == selected.Address);
                status.ForeColor = occupied ? Color.FromArgb(128, 78, 0) : Color.FromArgb(20, 108, 62);
                status.Text = occupied ? "Proceso terminado. El endpoint sigue ocupado; revisa el proceso actual." : "Proceso terminado. El endpoint seleccionado ya no aparece ocupado.";
            } catch (Exception ex) { if (!IsDisposed) MessageBox.Show(this, ex.Message, "No se pudo liberar", MessageBoxButtons.OK, MessageBoxIcon.Information); }
            finally { busy = false; if (!IsDisposed) { refresh.Enabled = true; Selection(); } }
        }
        void Export() {
            using (var dialog = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "puertos-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".csv", OverwritePrompt = true }) {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try {
                    var data = grid.DataSource as List<PortEntry> ?? new List<PortEntry>();
                    var lines = new List<string> { "Estado,Puerto,Protocolo,Tecnología,Proceso,PID,Dirección,Ruta,Acción,InactivoDesde" };
                    lines.AddRange(data.Select(r => String.Join(",", new[] { r.StatusText, r.Port.ToString(), r.Protocol, r.Technology, r.ProcessName, r.Pid.ToString(), r.Address, r.Path, r.ActionText, r.DisappearedUtc.HasValue ? r.DisappearedUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : "" }.Select(Engine.Csv))));
                    File.WriteAllLines(dialog.FileName, lines, new UTF8Encoding(true)); status.Text = "CSV guardado con " + data.Count + " endpoints visibles.";
                } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Error al exportar", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }
    }

    public static class Program {
        [STAThread] public static int Main(string[] args) {
            if (args.Contains("--test-host")) return TestHost();
            if (args.Contains("--self-test")) return SelfTest();
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e) { MessageBox.Show(e.Exception.Message, "PortPilot", MessageBoxButtons.OK, MessageBoxIcon.Error); };
            Application.Run(new MainForm()); return 0;
        }
        static int TestHost() {
            var tcp4 = new TcpListener(IPAddress.Loopback, 0); tcp4.Start();
            var tcp6 = new TcpListener(IPAddress.IPv6Loopback, 0); tcp6.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, true); tcp6.Start();
            using (var udp4 = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
            using (var udp6 = new UdpClient(new IPEndPoint(IPAddress.IPv6Loopback, 0))) { Thread.Sleep(45000); }
            tcp4.Stop(); tcp6.Stop(); return 0;
        }
        static void Check(bool ok, string message) { if (!ok) throw new Exception("FAIL: " + message); Console.WriteLine("PASS: " + message); }
        static PortEntry Sample(int pid) { return new PortEntry { Pid = pid, Created = 1, Session = 1, Port = 8000, Address = "127.0.0.1", Protocol = "TCP", Access = "Disponible", ProcessName = "example.exe" }; }
        static void TestHistory() {
            var history = new PortHistory(); var now = DateTime.UtcNow;
            var first = history.Merge(new List<PortEntry> { Sample(101) }, now);
            Check(first.Count == 1 && first[0].StatusText == "● Activo" && first[0].CanStop, "Registro observado activo y accionable");
            var missing = history.Merge(new List<PortEntry>(), now.AddSeconds(1));
            Check(missing.Count == 1 && !missing[0].CanStop && missing[0].StatusText == "● Inactivo", "Registro desaparecido inactivo y sin acción de cierre");
            bool refused = false; try { Engine.Stop(missing[0]); } catch (InvalidOperationException) { refused = true; }
            Check(refused, "El motor rechaza registros inactivos antes de abrir procesos");
            var reused = history.Merge(new List<PortEntry> { Sample(102) }, now.AddSeconds(2));
            Check(reused.Count == 2 && reused.Count(r => r.IsActive) == 1 && reused.Single(r => r.IsActive).Pid == 102, "Puerto ocupado por otro PID conserva estados separados");
            var returned = history.Merge(new List<PortEntry> { Sample(101) }, now.AddSeconds(3));
            Check(returned.Count(r => r.Pid == 101) == 1 && returned.Single(r => r.Pid == 101).IsActive, "Reaparición del mismo endpoint sin duplicar el registro");
            Check(history.Merge(new List<PortEntry>(), now.AddMinutes(6)).All(r => r.Pid == 101), "Caducidad del historial anterior a cinco minutos");
            Check(history.Merge(new List<PortEntry>(), now.AddMinutes(12)).Count == 0, "El historial caduca y no inventa puertos libres");
        }
        static int SelfTest() {
            Process child = null;
            try {
                TestHistory();
                Check(Engine.Classify("node.exe") == "Node.js" && Engine.Classify("python3.15.exe") == "Python" && Engine.Classify("php-cgi.exe") == "PHP" && Engine.Classify("nginx.exe") == "Nginx", "Clasificación Node/Python/PHP/Nginx");
                Check(Engine.Classify("unrelated.exe") == "Otro" && Engine.Classify("notnode.exe") == "Otro", "No inferir tecnología por nombre parcial o puerto");
                Check(Engine.Csv("=1+1").StartsWith("\"'=") && Engine.Csv("a\"b") == "\"a\"\"b\"", "CSV: comillas y fórmulas");
                Check(Engine.BlockReason(4, "System", 1, 1) != null && Engine.BlockReason(100, "svchost.exe", 1, 1) != null && Engine.BlockReason(100, "node.exe", 1, 0) != null, "Protección de Windows y servicios");
                child = Process.Start(new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName, "--test-host") { UseShellExecute = false, CreateNoWindow = true });
                List<PortEntry> ports = null;
                for (int i = 0; i < 80; i++) { ports = Engine.ReadPorts().Where(r => r.Pid == child.Id).ToList(); if (ports.Count >= 4) break; Thread.Sleep(100); }
                Check(ports.Any(r => r.Protocol == "TCP" && r.Address == "127.0.0.1") && ports.Any(r => r.Protocol == "TCP" && r.Address == "::1"), "Detección TCP IPv4 e IPv6 del proceso de prueba");
                Check(ports.Any(r => r.Protocol == "UDP" && r.Address == "127.0.0.1") && ports.Any(r => r.Protocol == "UDP" && r.Address == "::1"), "Detección UDP IPv4 e IPv6 del proceso de prueba");
                var selected = Engine.Snapshot().First(r => r.Pid == child.Id);
                Check(selected.Created > 0 && selected.ProcessName != "No disponible", "Identidad real del proceso");
                long original = selected.Created; selected.Created++;
                bool rejected = false; try { Engine.Stop(selected); } catch (InvalidOperationException) { rejected = true; }
                selected.Created = original;
                Check(rejected && !child.HasExited, "Rechazar identidad caducada sin terminar el proceso");
                if (selected.Session == 0) { Console.WriteLine("SKIP: cierre de prueba bloqueado en sesión de servicios"); return 2; }
                string address = selected.Address; selected.Address = "192.0.2.123";
                rejected = false; try { Engine.Stop(selected); } catch (InvalidOperationException) { rejected = true; }
                selected.Address = address;
                Check(rejected && !child.HasExited, "Rechazar endpoint que ya no pertenece al proceso");
                Engine.Stop(selected);
                Check(child.WaitForExit(4000) && !Engine.ReadPorts().Any(r => r.Pid == child.Id), "Liberar todos los puertos solo del proceso de prueba");
                Console.WriteLine("17 comprobaciones correctas. No se modificaron procesos ajenos a la prueba."); return 0;
            } catch (Exception ex) { Console.WriteLine(ex.ToString()); return 1; }
            finally { if (child != null) { try { if (!child.HasExited) { child.Kill(); child.WaitForExit(3000); } } finally { child.Dispose(); } } }
        }
    }
}
