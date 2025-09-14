using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Timers;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
class CB_Tweak_App : Form
{
    const int WM_CLIPBOARDUPDATE = 0x31D;

    [DllImport("user32.dll", SetLastError = true)]
    private extern static void AddClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll", SetLastError = true)]
    private extern static void RemoveClipboardFormatListener(IntPtr hwnd);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CreateProcess(
        string lpApplicationName,
        string lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);
    [DllImport("kernel32.dll")]
    public static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GetStdHandle(int nStdHandle);
    public delegate bool ConsoleCtrlDelegate(uint ctrlType);

    [DllImport("kernel32.dll")]
    public static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate HandlerRoutine, bool Add);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);

    public static readonly int STD_INPUT_HANDLE = -10;
    public static readonly int STD_OUTPUT_HANDLE = -11;
    public static readonly int STD_ERROR_HANDLE = -12;
    [StructLayout(LayoutKind.Sequential)]
    public struct STARTUPINFO
    {
        public int cb;
        public IntPtr lpReserved;
        public IntPtr lpDesktop;
        public IntPtr lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    private class Handler
    {
        private uint pid;
        public Handler(uint pid)
        {
            this.pid = pid;
        }
        public bool HandlerRoutine(uint dwCtrlType)
        {
            if (dwCtrlType == 0 || dwCtrlType == 1)
            {
                GenerateConsoleCtrlEvent(1, pid);
                return true;
            }
            return false;
        }
    }

    public static int ExecuteCommand_cui(string command)
    {
        // Prepare the STARTUPINFO structure
        STARTUPINFO si = new STARTUPINFO();
        si.cb = Marshal.SizeOf(si);
        si.dwFlags = 0x00000100; // STARTF_USESTDHANDLES
        si.hStdInput = GetStdHandle(STD_INPUT_HANDLE);
        si.hStdOutput = GetStdHandle(STD_OUTPUT_HANDLE);
        si.hStdError = GetStdHandle(STD_ERROR_HANDLE);

        PROCESS_INFORMATION pi;

        if (!CreateProcess(null, command, IntPtr.Zero, IntPtr.Zero, true, 0x00000200, // CREATE_NEW_PROCESS_GROUP
                                    IntPtr.Zero, null, ref si, out pi))
        {
            throw new System.ComponentModel.Win32Exception();
        }

        SetConsoleCtrlHandler(new ConsoleCtrlDelegate(new Handler((uint)pi.dwProcessId).HandlerRoutine), true);
        uint r = WaitForSingleObject(pi.hProcess, uint.MaxValue); // INFINITE
        if (r != 0)
        {// WAIT_OBJECT_0
            Console.WriteLine("Wait failed; exit code {0}", r); return -1;
        }

        uint exitCode;
        // Get the exit code
        if (!GetExitCodeProcess(pi.hProcess, out exitCode))
        {
            Console.WriteLine("GetExitCodeProcess failed"); return -1;
        }
        // Close the handles
        CloseHandle(pi.hProcess);
        CloseHandle(pi.hThread);

        return (int)exitCode;
    }
    public static int ExecuteCommand_gui(string command)
    {
        STARTUPINFO si = new STARTUPINFO();
        si.cb = Marshal.SizeOf(si);
        PROCESS_INFORMATION pi = new PROCESS_INFORMATION();

        if (!CreateProcess(null, command, IntPtr.Zero, IntPtr.Zero, true, 0, IntPtr.Zero, null, ref si, out pi))
            return -1;

        uint r = WaitForSingleObject(pi.hProcess, uint.MaxValue); // INFINITE
        if (r != 0) {
            Console.WriteLine("Wait failed; exit code {0}", r); return -1;
        }
        
        // Get the exit code
        uint exitCode;
        if (!GetExitCodeProcess(pi.hProcess, out exitCode)){
            Console.WriteLine("GetExitCodeProcess failed"); return -1;
        }

        CloseHandle(pi.hProcess);
        CloseHandle(pi.hThread);

        return (int)exitCode;
    }

    public static string SeparateExecPath(string s)
    {
        if (s.StartsWith("\""))
        {
            s = s.Substring(s.IndexOf("\"", 1) + 1);
        }
        else
        {
            int spaceIndex = s.IndexOf(" ");
            int tabIndex = s.IndexOf("\t");
            if (spaceIndex == -1 && tabIndex == -1) return "";
            int firstWhitespaceIndex = (spaceIndex == -1) ? tabIndex
                                      : (tabIndex == -1) ? spaceIndex
                                      : (spaceIndex < tabIndex ? spaceIndex : tabIndex);
            s = s.Substring(firstWhitespaceIndex);
        }
        return s.TrimStart(new[] { ' ', '\t' });
    }
    private const int CBSTT_NOOP = 0;
    private const int CBSTT_GETTEXT = 1;
    private const int CBSTT_RUNCOMMAND = 2;
    private const int CBSTT_RESTORE_LAST = 3;
    private const int CBSTT_RUNCOMMAND_GUI = 4;

    private int clip_detect = CBSTT_NOOP;
    private System.Windows.Forms.IDataObject last_cb = new DataObject();
    private string arg0 = "";
    //use as multiset (bool is used like "unit" type)
    private static readonly Dictionary<string, bool> unsupported_formats = new Dictionary<string, bool>(){
        {"Object Descriptor", true},//Required in .NET Framework 4.x (tested 4.6.2)
        {"EnhancedMetafile", true},//Required in .NET Framework 3.x (tested 3.5) & 4.x (tested 4.6.2)
    };
    private bool restore_done = false; 
    [STAThread]
    public static void Main(string[] args)
    {
        using (var form = new CB_Tweak_App())
        {
            form.RunCommand();
        }
    }
    public CB_Tweak_App()
    {
        this.Visible = false;
        this.WindowState = FormWindowState.Minimized;
        this.ShowInTaskbar = false;
        this.TopMost = true;
        this.Size = new Size(800, 800);
        this.CenterToScreen();
    }
    private void RunCommand()
    {
        arg0 = SeparateExecPath(Environment.CommandLine);
        if (arg0.Length == 0){
            clip_detect = CBSTT_GETTEXT;
        }else if (arg0[0] == 'g') {
            clip_detect = CBSTT_RUNCOMMAND_GUI;
        } else if (arg0[0] == 'c') {
            clip_detect = CBSTT_RUNCOMMAND;
        } else {
            clip_detect = CBSTT_GETTEXT;
        }
        BackupCBto(ref last_cb);
        AddClipboardFormatListener(this.Handle);
        SendKeys.SendWait("^c");
        SendKeys.Flush();
        var timer = new System.Windows.Forms.Timer();
        timer.Interval = 5000;
        timer.Tick += (s, e) =>
        {
            timer.Stop();
            Console.WriteLine("Error: no clipboard update (is there anything selected?).");
            Application.Exit();
        };
        timer.Start();
        Application.Run(new ApplicationContext(this));//begin event loop
    }
    protected override bool ShowWithoutActivation
    {
        get { return true; }
    }
    protected override CreateParams CreateParams
    {
        get {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
            return cp;
        }
    }
    static private void BackupCBto(ref System.Windows.Forms.IDataObject cb)
    {
        IDataObject cb_raw;
        while (true)
        {
            try
            {
                cb_raw = Clipboard.GetDataObject(); break;
            }
            catch (Exception)
            {
                Thread.Sleep(200);
            }
        }
        //last_cb = cb_raw; return;
        cb = new DataObject();
        foreach (string fmt in cb_raw.GetFormats(false))
        {
            if (!unsupported_formats.ContainsKey(fmt)) { cb.SetData(fmt, cb_raw.GetData(fmt)); }
        }
    }
    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        switch (message.Msg)
        {
            case WM_CLIPBOARDUPDATE:
                if (restore_done) return;
                switch (clip_detect)
                {
                    case CBSTT_RUNCOMMAND_GUI:
                        ExecuteCommand_gui(arg0.Substring(2));
                        goto case CBSTT_RESTORE_LAST;
                    case CBSTT_RUNCOMMAND:
                        ExecuteCommand_cui(arg0.Substring(2));
                        goto case CBSTT_RESTORE_LAST;
                    case CBSTT_GETTEXT:
                        string text = Clipboard.GetText(TextDataFormat.UnicodeText);
                        if (text != null) Console.Write(text);
                        goto case CBSTT_RESTORE_LAST;
                    case CBSTT_RESTORE_LAST:
                        restore_done = true;
                        Clipboard.SetDataObject(last_cb, true);
                        Application.Exit();
                        break;
                    default:
                        break;
                }
                break;
        }
    }
}