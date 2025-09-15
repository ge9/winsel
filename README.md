# Winsel: do something with selection in Windows 

`winsel` can be used to do something with some content (text, image, etc.) which is currently selected (= can be stored into clipboard with Ctrl+C).  It saves current clipboard, sends Ctrl+C, does something (print text or run some external command), and restores the saved clipboard.

# Usage

```
winsel [arg ...]
```
`winsel` firstly saves current clipboard, and sends Ctrl+C (to the active window). Then it executes the commandline as is (without any parsing).
Thus the excuted program can obtain currently selected contents through clipboard.
When the program finished, `winsel` restores the saved clipboard.
If `winsel` is run **without any arguments**, it prints selected text in stdout. When non-text content (e.g. image) is selected, it outputs nothing.

# Versions
For versions, `winsel`, `winsel-net-framework`, `winsel-gui`, and `winsel-gui-net-framework`, are released. 
`winsel-gui*` are compiled as GUI application (no console window and stdout).
`*-net-framework` are compiled with older .NET Framework 4.6.2, but I recommend them as of now because they don't need any runtime installation.
