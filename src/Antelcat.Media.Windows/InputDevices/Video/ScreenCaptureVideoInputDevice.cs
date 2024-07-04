using System.Diagnostics;
using Antelcat.Media.Abstractions;
using Antelcat.Media.Abstractions.Enums;
using Antelcat.Media.Windows.Internal;
using EasyPathology.Abstractions.DataTypes;
using SharpDX;
using D3D11 = SharpDX.Direct3D11;
using DXGI = SharpDX.DXGI;

namespace Antelcat.Media.Windows.InputDevices.Video;

public class ScreenCaptureVideoInputDevice : VideoInputDevice
{
    public override bool IsReady => true;

    public override Fraction FrameRate => new(60, 1);

    public override int FrameWidth => frameWidth;

    public override int FrameHeight => frameHeight;

    public override VideoFrameFormat OriginalFormat => VideoFrameFormat.RGBA32;
    
    private double FrameDurationMilliseconds => TimeSpan.FromSeconds(1d / FrameRate.ToDouble()).TotalMilliseconds;

    private readonly SharedD3D11Device sharedD3D11Device = SharedD3D11Device.HardwareVideoEncoder;
    // private D3D11.Texture2D? stagingTexture;
    private DXGI.OutputDuplication? outputDuplicate;
    private int frameWidth, frameHeight;

    protected override void Opening()
    {
        using var output = sharedD3D11Device.Adapter.GetOutput(0);
        using var output1 = output.QueryInterface<DXGI.Output1>();
        outputDuplicate = output1.DuplicateOutput(sharedD3D11Device.Device);
        
        outputDuplicate.TryAcquireNextFrame(int.MaxValue, out _, out var desktopResource).CheckError();
        try
        {
            using var screenTexture2D = desktopResource.QueryInterface<D3D11.Texture2D>();
            (frameWidth, frameHeight) = (screenTexture2D.Description.Width, screenTexture2D.Description.Height);
        }
        finally
        {
            outputDuplicate.ReleaseFrame();
            desktopResource.Dispose();
        }

        base.Opening();
    }

    protected override void RunLoop(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!cancellationToken.IsCancellationRequested && outputDuplicate != null)
        {
            var totalMillisecondsBeforeCapture = stopwatch.Elapsed.TotalMilliseconds;
            double millisecondsToSleep;
            
            outputDuplicate.TryAcquireNextFrame(int.MaxValue, out var information, out var desktopResource).CheckError();
            if (information.LastPresentTime == 0)
            {
                outputDuplicate.ReleaseFrame();
                desktopResource.Dispose();
                millisecondsToSleep = FrameDurationMilliseconds - (stopwatch.Elapsed.TotalMilliseconds - totalMillisecondsBeforeCapture);
                if (millisecondsToSleep >= 1)
                {
                    Thread.Sleep((int)millisecondsToSleep);
                }
                continue;
            }
            
            try
            {
                using var screenTexture2D = desktopResource.QueryInterface<D3D11.Texture2D>();

                // if (stagingTexture == null ||
                //     stagingTexture.Description.Width != screenTexture2D.Description.Width ||
                //     stagingTexture.Description.Height != screenTexture2D.Description.Height)
                // {
                //     frameSize = new Size2I(screenTexture2D.Description.Width, screenTexture2D.Description.Height);
                //
                //     disposeCollector.Replace(ref stagingTexture,
                //         new D3D11.Texture2D(d3dDevice,
                //             new D3D11.Texture2DDescription
                //             {
                //                 Width = frameSize.Width,
                //                 Height = frameSize.Height,
                //                 MipLevels = 1,
                //                 ArraySize = 1,
                //                 Format = DXGI.Format.B8G8R8A8_UNorm,
                //                 SampleDescription = new DXGI.SampleDescription(1, 0),
                //                 Usage = D3D11.ResourceUsage.Staging,
                //                 BindFlags = D3D11.BindFlags.None,
                //                 CpuAccessFlags = D3D11.CpuAccessFlags.Read,
                //                 OptionFlags = D3D11.ResourceOptionFlags.None
                //             }));
                // }
                // var context = d3dDevice.ImmediateContext;
                // context.CopyResource(screenTexture2D, stagingTexture);
                //
                // var dataBox = context.MapSubresource(stagingTexture, 0, D3D11.MapMode.Read, D3D11.MapFlags.None);
                // var frame = new RawVideoFrame(FrameWidth, FrameHeight, dataBox.RowPitch * FrameHeight, dataBox.DataPointer, OriginalFormat)
                // {
                //     Time = TimeSpan.FromTicks(information.LastPresentTime)
                // };
                
                var frame = new D3D11TextureVideoFrame(screenTexture2D)
                {
                    Time = TimeSpan.FromTicks(information.LastPresentTime)
                };
                ProcessFrame(frame, cancellationToken);
            }
            finally
            {
                outputDuplicate.ReleaseFrame();
                desktopResource.Dispose();
            }
            
            millisecondsToSleep = FrameDurationMilliseconds - (stopwatch.Elapsed.TotalMilliseconds - totalMillisecondsBeforeCapture);
            if (millisecondsToSleep >= 1)
            {
                Thread.Sleep((int)millisecondsToSleep);
            }
        }
    }

    protected override void Closing()
    {
        DisposeCollector.DisposeToDefault(ref outputDuplicate);
        frameWidth = frameHeight = default;

        base.Closing();
    }
}