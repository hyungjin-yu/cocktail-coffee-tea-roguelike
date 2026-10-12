// QA 도구: Windows 실제 오디오 출력 경로 측정(외부 프로세스, 게임 코드와 무관).
// - 기본 재생 장치(eRender/eConsole)의 WASAPI 루프백을 WAV로 녹음하고 100ms RMS/피크 CSV를 쓴다(장치로 나간 시스템 전체 믹스).
// - 같은 장치의 오디오 세션 중 지정 프로세스 이름의 세션 상태·피크 미터(IAudioMeterInformation)·세션 볼륨/음소거를 50ms 간격으로 기록한다.
//   세션 미터는 해당 프로세스가 OS 믹서에 실제로 보낸 신호만 반영하므로 다른 앱(브라우저 등) 소리와 구별된다.
// 사람이 귀로 듣는 청취 검수를 대신하지 않는다.
// 사용: AudioProbe <outDir> <seconds> <processName>
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

static class Program
{
    static int Main(string[] args)
    {
        if (args.Length < 3) { Console.Error.WriteLine("usage: AudioProbe <outDir> <seconds> <processName>"); return 2; }
        string outDir = args[0];
        double seconds = double.Parse(args[1], CultureInfo.InvariantCulture);
        string procName = args[2];
        Directory.CreateDirectory(outDir);
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
        Check(enumerator.GetDefaultAudioEndpoint(0, 0, out var device), "GetDefaultAudioEndpoint");
        device.GetId(out var deviceId);
        var info = new StringBuilder();
        info.AppendLine($"utc_start={DateTime.UtcNow:o}");
        info.AppendLine($"device_id={deviceId}");
        info.AppendLine($"device_name={FriendlyName(device)}");

        var iidVol = typeof(IAudioEndpointVolume).GUID;
        Check(device.Activate(ref iidVol, 23, IntPtr.Zero, out var volObj), "Activate IAudioEndpointVolume");
        var epVol = (IAudioEndpointVolume)volObj;
        epVol.GetMasterVolumeLevelScalar(out float epScalar);
        epVol.GetMute(out bool epMute);
        info.AppendLine($"endpoint_volume_scalar={epScalar.ToString("0.000", CultureInfo.InvariantCulture)} endpoint_mute={epMute}");

        var stop = DateTime.UtcNow.AddSeconds(seconds);
        var meterThread = new Thread(() => MeterLoop(device, procName, stop, Path.Combine(outDir, "session-meter.csv")));
        meterThread.Start();
        try { Loopback(device, stop, outDir, info); }
        catch (Exception e) { info.AppendLine("loopback_error=" + e.Message); }
        meterThread.Join();
        info.AppendLine($"utc_end={DateTime.UtcNow:o}");
        File.WriteAllText(Path.Combine(outDir, "endpoint-info.txt"), info.ToString());
        Console.WriteLine(info.ToString());
        return 0;
    }

    static string FriendlyName(IMMDevice device)
    {
        try
        {
            device.OpenPropertyStore(0, out var store);
            var key = new PROPERTYKEY { fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), pid = 14 };
            store.GetValue(ref key, out var pv);
            if (pv.vt == 31) return Marshal.PtrToStringUni(pv.p);
        }
        catch { }
        return "?";
    }

    static void MeterLoop(IMMDevice device, string procName, DateTime stop, string csvPath)
    {
        var iid = typeof(IAudioSessionManager2).GUID;
        Check(device.Activate(ref iid, 23, IntPtr.Zero, out var mgrObj), "Activate IAudioSessionManager2");
        var mgr = (IAudioSessionManager2)mgrObj;
        using var w = new StreamWriter(csvPath, false, new UTF8Encoding(false));
        w.WriteLine("utc,pid,process,state,peak,session_volume,session_mute,sessions_total");
        while (DateTime.UtcNow < stop)
        {
            try
            {
                mgr.GetSessionEnumerator(out var en);
                en.GetCount(out int count);
                bool any = false;
                for (int i = 0; i < count; i++)
                {
                    en.GetSession(i, out var ctl);
                    var ctl2 = (IAudioSessionControl2)ctl;
                    ctl2.GetProcessId(out uint pid);
                    string name = "";
                    try { name = Process.GetProcessById((int)pid).ProcessName; } catch { }
                    if (!string.Equals(name, procName, StringComparison.OrdinalIgnoreCase)) continue;
                    any = true;
                    ctl2.GetState(out int state);
                    ((IAudioMeterInformation)ctl).GetPeakValue(out float peak);
                    string vol = "", mute = "";
                    if (ctl is ISimpleAudioVolume simple) // 세션 개체가 지원할 때만(일부 드라이버는 QI 실패)
                    {
                        simple.GetMasterVolume(out float v);
                        simple.GetMute(out bool m);
                        vol = v.ToString("0.000", CultureInfo.InvariantCulture);
                        mute = m.ToString();
                    }
                    w.WriteLine($"{DateTime.UtcNow:o},{pid},{name},{state},{peak.ToString("0.000000", CultureInfo.InvariantCulture)},{vol},{mute},{count}");
                }
                if (!any) w.WriteLine($"{DateTime.UtcNow:o},,,none,,,,{count}");
            }
            catch (Exception e) { w.WriteLine($"{DateTime.UtcNow:o},,,error {e.Message.Replace(',', ';')},,,,"); }
            Thread.Sleep(50);
        }
    }

    static void Loopback(IMMDevice device, DateTime stop, string outDir, StringBuilder info)
    {
        var iid = typeof(IAudioClient).GUID;
        Check(device.Activate(ref iid, 23, IntPtr.Zero, out var clientObj), "Activate IAudioClient");
        var client = (IAudioClient)clientObj;
        Check(client.GetMixFormat(out var pFormat), "GetMixFormat");
        var fmt = Marshal.PtrToStructure<WAVEFORMATEX>(pFormat);
        bool isFloat = fmt.wFormatTag == 3 || (fmt.wFormatTag == 0xFFFE && fmt.wBitsPerSample == 32);
        info.AppendLine($"mix_format tag=0x{fmt.wFormatTag:X} channels={fmt.nChannels} rate={fmt.nSamplesPerSec} bits={fmt.wBitsPerSample} float={isFloat}");
        Guid empty = Guid.Empty;
        Check(client.Initialize(0, 0x00020000, 10_000_000, 0, pFormat, ref empty), "Initialize loopback");
        var iidCap = typeof(IAudioCaptureClient).GUID;
        Check(client.GetService(ref iidCap, out var capObj), "GetService capture");
        var cap = (IAudioCaptureClient)capObj;
        int ch = fmt.nChannels, rate = (int)fmt.nSamplesPerSec, bytesPerFrame = fmt.nBlockAlign;
        using var wav = new FileStream(Path.Combine(outDir, "loopback.wav"), FileMode.Create);
        wav.Write(new byte[44]);
        using var csv = new StreamWriter(Path.Combine(outDir, "loopback.csv"), false, new UTF8Encoding(false));
        csv.WriteLine("utc,frames,rms,rms_dbfs,peak,silent_flag_packets");
        long dataBytes = 0, totalFrames = 0;
        double sumSq = 0; float peak = 0; long winFrames = 0; int silentPackets = 0;
        var winStart = DateTime.UtcNow;
        Check(client.Start(), "Start");
        var buf16 = new byte[0];
        while (DateTime.UtcNow < stop)
        {
            Thread.Sleep(10);
            while (true)
            {
                cap.GetNextPacketSize(out uint packet);
                if (packet == 0) break;
                Check(cap.GetBuffer(out IntPtr data, out uint frames, out uint flags, out _, out _), "GetBuffer");
                int n = (int)frames * ch;
                if (buf16.Length < n * 2) buf16 = new byte[n * 2];
                bool silent = (flags & 2) != 0;
                if (silent) silentPackets++;
                unsafe
                {
                    for (int i = 0; i < n; i++)
                    {
                        float v = silent ? 0f : isFloat ? ((float*)data)[i] : ((short*)data)[i] / 32768f;
                        sumSq += v * v;
                        float a = Math.Abs(v);
                        if (a > peak) peak = a;
                        short s = (short)Math.Clamp((int)Math.Round(v * 32767f), -32768, 32767);
                        buf16[2 * i] = (byte)(s & 0xff);
                        buf16[2 * i + 1] = (byte)((s >> 8) & 0xff);
                    }
                }
                wav.Write(buf16, 0, n * 2);
                dataBytes += n * 2;
                totalFrames += frames;
                winFrames += frames;
                cap.ReleaseBuffer(frames);
            }
            if ((DateTime.UtcNow - winStart).TotalMilliseconds >= 100)
            {
                double rms = winFrames > 0 ? Math.Sqrt(sumSq / (winFrames * ch)) : 0;
                string db = rms > 0 ? (20 * Math.Log10(rms)).ToString("0.0", CultureInfo.InvariantCulture) : "-inf";
                csv.WriteLine($"{DateTime.UtcNow:o},{winFrames},{rms.ToString("0.000000", CultureInfo.InvariantCulture)},{db},{peak.ToString("0.000000", CultureInfo.InvariantCulture)},{silentPackets}");
                sumSq = 0; peak = 0; winFrames = 0; silentPackets = 0; winStart = DateTime.UtcNow;
            }
        }
        client.Stop();
        wav.Seek(0, SeekOrigin.Begin);
        using (var bw = new BinaryWriter(wav, Encoding.ASCII, true))
        {
            bw.Write(Encoding.ASCII.GetBytes("RIFF")); bw.Write((int)(36 + dataBytes)); bw.Write(Encoding.ASCII.GetBytes("WAVE"));
            bw.Write(Encoding.ASCII.GetBytes("fmt ")); bw.Write(16); bw.Write((short)1); bw.Write((short)ch); bw.Write(rate);
            bw.Write(rate * ch * 2); bw.Write((short)(ch * 2)); bw.Write((short)16);
            bw.Write(Encoding.ASCII.GetBytes("data")); bw.Write((int)dataBytes);
        }
        info.AppendLine($"loopback_frames={totalFrames} seconds_captured={(double)totalFrames / rate:0.00}");
    }

    static void Check(int hr, string what)
    {
        if (hr < 0) throw new COMException(what + " failed", hr);
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMDeviceEnumerator { }

    [StructLayout(LayoutKind.Sequential)] struct PROPERTYKEY { public Guid fmtid; public int pid; }
    [StructLayout(LayoutKind.Sequential)] struct PROPVARIANT { public ushort vt; ushort r1, r2, r3; public IntPtr p; IntPtr p2; }

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    struct WAVEFORMATEX { public ushort wFormatTag; public ushort nChannels; public uint nSamplesPerSec; public uint nAvgBytesPerSec; public ushort nBlockAlign; public ushort wBitsPerSample; public ushort cbSize; }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
        [PreserveSig] int OpenPropertyStore(int access, out IPropertyStore store);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out int state);
    }

    [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPropertyStore
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int GetAt(int i, out PROPERTYKEY key);
        [PreserveSig] int GetValue(ref PROPERTYKEY key, out PROPVARIANT value);
    }

    [ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioClient
    {
        [PreserveSig] int Initialize(int shareMode, int streamFlags, long bufferDuration, long periodicity, IntPtr format, ref Guid session);
        [PreserveSig] int GetBufferSize(out uint frames);
        [PreserveSig] int GetStreamLatency(out long latency);
        [PreserveSig] int GetCurrentPadding(out uint padding);
        [PreserveSig] int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closest);
        [PreserveSig] int GetMixFormat(out IntPtr format);
        [PreserveSig] int GetDevicePeriod(out long defaultPeriod, out long minPeriod);
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
        [PreserveSig] int Reset();
        [PreserveSig] int SetEventHandle(IntPtr handle);
        [PreserveSig] int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
    }

    [ComImport, Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioCaptureClient
    {
        [PreserveSig] int GetBuffer(out IntPtr data, out uint frames, out uint flags, out ulong devicePosition, out ulong qpcPosition);
        [PreserveSig] int ReleaseBuffer(uint frames);
        [PreserveSig] int GetNextPacketSize(out uint frames);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int GetChannelCount(out uint count);
        [PreserveSig] int SetMasterVolumeLevel(float db, ref Guid ctx);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid ctx);
        [PreserveSig] int GetMasterVolumeLevel(out float db);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint ch, float db, ref Guid ctx);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint ch, float level, ref Guid ctx);
        [PreserveSig] int GetChannelVolumeLevel(uint ch, out float db);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint ch, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid ctx);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }

    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionManager2
    {
        [PreserveSig] int GetAudioSessionControl(IntPtr guid, int flags, out IntPtr ctl);
        [PreserveSig] int GetSimpleAudioVolume(IntPtr guid, int flags, out IntPtr vol);
        [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator en);
    }

    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionEnumerator
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int GetSession(int index, out IAudioSessionControl session);
    }

    [ComImport, Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionControl
    {
        [PreserveSig] int GetState(out int state);
    }

    [ComImport, Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionControl2
    {
        [PreserveSig] int GetState(out int state);
        [PreserveSig] int GetDisplayName(out IntPtr name);
        [PreserveSig] int SetDisplayName(IntPtr name, ref Guid ctx);
        [PreserveSig] int GetIconPath(out IntPtr path);
        [PreserveSig] int SetIconPath(IntPtr path, ref Guid ctx);
        [PreserveSig] int GetGroupingParam(out Guid g);
        [PreserveSig] int SetGroupingParam(ref Guid g, ref Guid ctx);
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr n);
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr n);
        [PreserveSig] int GetSessionIdentifier(out IntPtr id);
        [PreserveSig] int GetSessionInstanceIdentifier(out IntPtr id);
        [PreserveSig] int GetProcessId(out uint pid);
    }

    [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioMeterInformation
    {
        [PreserveSig] int GetPeakValue(out float peak);
    }

    [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] // ISimpleAudioVolume (2026-10-12: 이전 6F0DD6F1A4B0은 QueryInterface 실패)
    interface ISimpleAudioVolume
    {
        [PreserveSig] int SetMasterVolume(float level, ref Guid ctx);
        [PreserveSig] int GetMasterVolume(out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid ctx);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }
}
