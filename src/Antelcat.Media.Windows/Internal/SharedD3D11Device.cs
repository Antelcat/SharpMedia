using System.Diagnostics;
using SharpDX.Direct3D;
using DXGI = SharpDX.DXGI;
using D3D11 = SharpDX.Direct3D11;

namespace Antelcat.Media.Windows.Internal;

internal class SharedD3D11Device
{
    public static SharedD3D11Device HardwareVideoEncoder { get; } = CreateHardwareVideoEncoder();
    
    public D3D11.Device Device { get; }
    
    public DXGI.Adapter Adapter { get; }

    private static SharedD3D11Device CreateHardwareVideoEncoder()
    {
        D3D11.Device? device = null;
        
        using var factory = new DXGI.Factory1();
        foreach (var adapter in factory.Adapters)
        {
            try
            {
                if (adapter.Description.VendorId == 0 || adapter.Description.DeviceId == 0)
                {
                    // software adapter
                    continue;
                }
                
                device = new D3D11.Device(DriverType.Hardware, D3D11.DeviceCreationFlags.BgraSupport | D3D11.DeviceCreationFlags.VideoSupport | D3D11.DeviceCreationFlags.Debug);
                if (device.QueryInterfaceOrNull<D3D11.VideoDevice>() is { } videoDevice)
                {
                    Debug.WriteLine($"Using {adapter.Description.Description} as {nameof(HardwareVideoEncoder)}");
                    videoDevice.Dispose();
                    break;
                }
                else
                {
                    device = null;
                }
            }
            finally
            {
                adapter.Dispose();
            }
        }

        if (device == null)
        {
            throw new DriveNotFoundException();
        }

        using var multithread = device.QueryInterface<D3D11.Multithread>();
        multithread.SetMultithreadProtected(true);
        return new SharedD3D11Device(device);
    }

    private SharedD3D11Device(D3D11.Device device)
    {
        Device = device;
        using var dxgiDevice = device.QueryInterface<DXGI.Device>();
        Adapter = dxgiDevice.Adapter;
    }
}