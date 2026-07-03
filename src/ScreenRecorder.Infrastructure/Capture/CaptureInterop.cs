using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;

namespace ScreenRecorder.Infrastructure.Capture;

/// <summary>
/// Native bridge between the WinRT Windows Graphics Capture API and the
/// Direct3D11 surface types exposed by Vortice. Isolates every piece of
/// hand-written COM/CsWinRT interop the capture service needs.
/// </summary>
internal static class CaptureInterop
{
    // IID of Windows.Graphics.Capture.IGraphicsCaptureItem.
    private static readonly Guid GraphicsCaptureItemIid = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");

    // IID of ID3D11Texture2D.
    private static readonly Guid Texture2DIid = new("6F15AAF2-D208-4E89-9AB4-489535D34F9C");

    [DllImport("d3d11.dll", EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice", ExactSpelling = true)]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

    [ComImport]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        IntPtr CreateForWindow([In] IntPtr window, [In] ref Guid iid);

        IntPtr CreateForMonitor([In] IntPtr monitor, [In] ref Guid iid);
    }

    [ComImport]
    [Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDirect3DDxgiInterfaceAccess
    {
        IntPtr GetInterface([In] ref Guid iid);
    }

    /// <summary>
    /// Wraps a Vortice D3D11 device as the WinRT <see cref="IDirect3DDevice"/> the
    /// capture frame pool requires.
    /// </summary>
    public static IDirect3DDevice CreateDirect3DDevice(ID3D11Device device)
    {
        using var dxgiDevice = device.QueryInterface<IDXGIDevice>();
        int hr = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out IntPtr inspectable);
        if (hr < 0)
        {
            // Only negative HRESULTs are failures; positive codes (e.g. S_FALSE) are success.
            Marshal.ThrowExceptionForHR(hr);
        }

        try
        {
            return MarshalInspectable<IDirect3DDevice>.FromAbi(inspectable);
        }
        finally
        {
            Marshal.Release(inspectable);
        }
    }

    /// <summary>Creates a capture item for an entire monitor (HMONITOR).</summary>
    public static GraphicsCaptureItem CreateItemForMonitor(IntPtr hMonitor)
    {
        var interop = GetItemInterop();
        var iid = GraphicsCaptureItemIid;
        return FromItemAbi(interop.CreateForMonitor(hMonitor, ref iid));
    }

    /// <summary>Creates a capture item for a single window (HWND).</summary>
    public static GraphicsCaptureItem CreateItemForWindow(IntPtr hWnd)
    {
        var interop = GetItemInterop();
        var iid = GraphicsCaptureItemIid;
        return FromItemAbi(interop.CreateForWindow(hWnd, ref iid));
    }

    /// <summary>
    /// Retrieves the underlying <see cref="ID3D11Texture2D"/> backing a captured
    /// frame's surface. The caller owns (and must dispose) the returned texture.
    /// </summary>
    public static ID3D11Texture2D GetTexture(IDirect3DSurface surface)
    {
        var access = surface.As<IDirect3DDxgiInterfaceAccess>();
        var iid = Texture2DIid;
        return new ID3D11Texture2D(access.GetInterface(ref iid));
    }

    private static IGraphicsCaptureItemInterop GetItemInterop()
    {
        var iid = typeof(IGraphicsCaptureItemInterop).GUID;
        using var factory = ActivationFactory.Get("Windows.Graphics.Capture.GraphicsCaptureItem", iid);
        return (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(factory.ThisPtr);
    }

    private static GraphicsCaptureItem FromItemAbi(IntPtr itemPtr)
    {
        try
        {
            return GraphicsCaptureItem.FromAbi(itemPtr);
        }
        finally
        {
            Marshal.Release(itemPtr);
        }
    }
}
