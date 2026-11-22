# User-input guard: low-level keyboard/mouse hooks. Any NON-injected event -> write STOP file.
import ctypes, ctypes.wintypes as W, sys, time, os
STOP = sys.argv[1]
LOG = sys.argv[2]
u32 = ctypes.WinDLL('user32', use_last_error=True)
k32 = ctypes.WinDLL('kernel32', use_last_error=True)
LRESULT = ctypes.c_ssize_t
HOOKPROC = ctypes.WINFUNCTYPE(LRESULT, ctypes.c_int, W.WPARAM, W.LPARAM)
u32.CallNextHookEx.argtypes = [W.HHOOK, ctypes.c_int, W.WPARAM, W.LPARAM]
u32.CallNextHookEx.restype = LRESULT
u32.SetWindowsHookExW.argtypes = [ctypes.c_int, HOOKPROC, W.HINSTANCE, W.DWORD]
u32.SetWindowsHookExW.restype = W.HHOOK
class KB(ctypes.Structure):
    _fields_ = [('vk', W.DWORD), ('scan', W.DWORD), ('flags', W.DWORD), ('time', W.DWORD), ('extra', ctypes.c_size_t)]
class MS(ctypes.Structure):
    _fields_ = [('x', ctypes.c_long), ('y', ctypes.c_long), ('data', W.DWORD), ('flags', W.DWORD), ('time', W.DWORD), ('extra', ctypes.c_size_t)]
def log(s):
    with open(LOG, 'a', encoding='utf-8') as f: f.write(f"{time.strftime('%H:%M:%S')} {s}\n")
def flag(kind, info):
    if not os.path.exists(STOP):
        with open(STOP, 'w') as f: f.write(f"{time.time()} {kind} {info}\n")
    log(f"USER INPUT {kind} {info}")
def kbp(n, w, l):
    if n == 0:
        k = ctypes.cast(l, ctypes.POINTER(KB)).contents
        if not (k.flags & 0x10): flag('key', f'vk={k.vk}')
    return u32.CallNextHookEx(None, n, w, l)
def msp(n, w, l):
    if n == 0:
        m = ctypes.cast(l, ctypes.POINTER(MS)).contents
        if not (m.flags & 0x01): flag('mouse', f'msg={w} {m.x},{m.y}')
    return u32.CallNextHookEx(None, n, w, l)
kp = HOOKPROC(kbp); mp = HOOKPROC(msp)
h1 = u32.SetWindowsHookExW(13, kp, None, 0)
h2 = u32.SetWindowsHookExW(14, mp, None, 0)
log(f"watch start hooks={bool(h1)},{bool(h2)}")
msg = W.MSG()
while u32.GetMessageW(ctypes.byref(msg), None, 0, 0) != 0:
    u32.TranslateMessage(ctypes.byref(msg)); u32.DispatchMessageW(ctypes.byref(msg))
