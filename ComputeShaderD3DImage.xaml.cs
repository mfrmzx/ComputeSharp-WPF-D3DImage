using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using ComputeSharp;
using ComputeSharp.Interop;
using Silk.NET.Core.Native;
using D3D9 = Silk.NET.Direct3D9;
using D3D12 = TerraFX.Interop.DirectX;
using TWindow = TerraFX.Interop.Windows;

namespace YQCool.ComputeSharp.Wpf;

public unsafe partial class ComputeShaderD3DImage
{
#region Properties

    public static readonly DependencyProperty ShaderRunnerProperty =
        DependencyProperty.Register(nameof(ShaderRunner), typeof(IShaderRunner), typeof(ComputeShaderD3DImage), new PropertyMetadata(null));

    public IShaderRunner? ShaderRunner
    {
        get => (IShaderRunner) GetValue(ShaderRunnerProperty);
        set => SetValue(ShaderRunnerProperty, value);
    }

    public static readonly DependencyProperty IsShowFpsProperty =
        DependencyProperty.Register(nameof(IsShowFps), typeof(bool), typeof(ComputeShaderD3DImage), new PropertyMetadata(false));

    public bool IsShowFps
    {
        get => (bool) GetValue(IsShowFpsProperty);
        set => SetValue(IsShowFpsProperty, value);
    }

    public static readonly DependencyProperty IsPausedProperty =
        DependencyProperty.Register(nameof(IsPaused), typeof(bool), typeof(ComputeShaderD3DImage), new PropertyMetadata(false));

    public bool IsPaused
    {
        get => (bool) GetValue(IsPausedProperty);
        set => SetValue(IsPausedProperty, value);
    }

    public static readonly DependencyProperty IsFixedFpsProperty =
        DependencyProperty.Register(nameof(IsFixedFps), typeof(bool), typeof(ComputeShaderD3DImage), new PropertyMetadata(false));

    public bool IsFixedFps
    {
        get => (bool) GetValue(IsFixedFpsProperty);
        set => SetValue(IsFixedFpsProperty, value);
    }

    public static readonly DependencyProperty FixedFpsProperty =
        DependencyProperty.Register(nameof(FixedFps), typeof(int), typeof(ComputeShaderD3DImage), new PropertyMetadata(60));

    public int FixedFps
    {
        get => (int) GetValue(FixedFpsProperty);
        set => SetValue(FixedFpsProperty, value);
    }

    public static readonly DependencyProperty ResolutionScaleProperty =
        DependencyProperty.Register(nameof(ResolutionScale), typeof(double), typeof(ComputeShaderD3DImage), new PropertyMetadata(1.0));

    public double ResolutionScale
    {
        get => (double) GetValue(ResolutionScaleProperty);
        set => SetValue(ResolutionScaleProperty, Math.Clamp(value, 0.1, 1.0));
    }

#endregion

    public ComputeShaderD3DImage()
    {
        InitializeComponent();
        Dispose();
    }

    private Size _imageSize;

    private int _fps;
    private int _frameCount;

    private TimeSpan _lastFpsRenderTime = TimeSpan.Zero;
    private TimeSpan _actualRenderTime = TimeSpan.Zero;
    private TimeSpan _fixedFrameRenderTime;
    private readonly Stopwatch _startStopwatch = new();
    private readonly Stopwatch _frameStopwatch = new();

    private bool _isResizePending;

    private ulong _nextD3D12FenceValue = 1;

    private TWindow.ComPtr<D3D12.ID3D12Device> _d3D12Device;
    private TWindow.ComPtr<D3D12.ID3D12CommandAllocator> _d3D12CommandAllocator;
    private TWindow.ComPtr<D3D12.ID3D12GraphicsCommandList> _d3D12GraphicsCommandList;
    private TWindow.ComPtr<D3D12.ID3D12CommandQueue> _d3D12CommandQueue;
    private TWindow.ComPtr<D3D12.ID3D12Fence> _d3D12Fence;

    private ReadWriteTexture2D<Rgba32, float4>? _texture;
    private TWindow.ComPtr<D3D12.ID3D12Resource> _d3D12Resource;
    private TWindow.ComPtr<D3D12.ID3D12Resource> _shardD3D12Resource;

    private ComPtr<D3D9.IDirect3D9Ex> _direct3D9Ex;
    private ComPtr<D3D9.IDirect3DDevice9Ex> _direct3DDevice9Ex;

    private void Grid_OnLoaded(object sender, RoutedEventArgs e)
    {
        _fixedFrameRenderTime = new TimeSpan(TimeSpan.TicksPerSecond / FixedFps);
        Initialize();
        CompositionTarget.Rendering += CompositionTarget_Rendering;
    }

    private void Grid_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        OnReSize();
    }

    private void D3DImage_OnIsFrontBufferAvailableChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        OnReSize();
    }

    private void CompositionTarget_Rendering(object? sender, EventArgs e)
    {
        Update();
    }

    private void Initialize()
    {
        InitializeD3D9Divice();
        InitializeD3D12Divice();
    }

    private void InitializeD3D9Divice()
    {
        var d3d9 = D3D9.D3D9.GetApi();
        int hr;
        fixed (D3D9.IDirect3D9Ex** direct3D9Ex = _direct3D9Ex)
        {
            hr = d3d9.Direct3DCreate9Ex(32, direct3D9Ex);
            SilkMarshal.ThrowHResult(hr);
        }

        var presentParameters = new D3D9.PresentParameters
        {
            Windowed = 1, // true
            SwapEffect = D3D9.Swapeffect.Discard,
            HDeviceWindow = GetDesktopWindow(),
            PresentationInterval = D3D9.D3D9.PresentIntervalDefault
        };

        // 设置使用多线程方式，这样的性能才足够
        uint createFlags = D3D9.D3D9.CreateHardwareVertexprocessing | D3D9.D3D9.CreateMultithreaded | D3D9.D3D9.CreateFpuPreserve;

        fixed (D3D9.IDirect3DDevice9Ex** direct3DDevice9Ex = _direct3DDevice9Ex)
        {
            hr = _direct3D9Ex.Handle->CreateDeviceEx(0,
                D3D9.Devtype.Hal, // 使用硬件渲染
                IntPtr.Zero,
                createFlags,
                ref presentParameters,
                (D3D9.Displaymodeex*) IntPtr.Zero,
                direct3DDevice9Ex);
            SilkMarshal.ThrowHResult(hr);
        }

    }

    private void InitializeD3D12Divice()
    {
        TWindow.HRESULT hr;
        fixed (D3D12.ID3D12Device** d3D12Device = _d3D12Device)
        {
            hr = InteropServices.TryGetID3D12Device(GraphicsDevice.GetDefault(), TWindow.Windows.__uuidof<D3D12.ID3D12Device>(), (void**) d3D12Device);
            SilkMarshal.ThrowHResult(hr);
        }

        // Create the direct command queue to use
        fixed (D3D12.ID3D12CommandQueue** d3D12CommandQueue = _d3D12CommandQueue)
        {
            D3D12.D3D12_COMMAND_QUEUE_DESC d3D12CommandQueueDesc;
            d3D12CommandQueueDesc.Type = D3D12.D3D12_COMMAND_LIST_TYPE.D3D12_COMMAND_LIST_TYPE_DIRECT;
            d3D12CommandQueueDesc.Priority = (int) D3D12.D3D12_COMMAND_QUEUE_PRIORITY.D3D12_COMMAND_QUEUE_PRIORITY_NORMAL;
            d3D12CommandQueueDesc.Flags = D3D12.D3D12_COMMAND_QUEUE_FLAGS.D3D12_COMMAND_QUEUE_FLAG_NONE;
            d3D12CommandQueueDesc.NodeMask = 0;

            hr = _d3D12Device.Get()->CreateCommandQueue(
                &d3D12CommandQueueDesc,
                TWindow.Windows.__uuidof<D3D12.ID3D12CommandQueue>(),
                (void**) d3D12CommandQueue);
            SilkMarshal.ThrowHResult(hr);
        }

        // Create the direct fence
        fixed (D3D12.ID3D12Fence** d3D12Fence = _d3D12Fence)
        {
            hr = _d3D12Device.Get()->CreateFence(
                0,
                D3D12.D3D12_FENCE_FLAGS.D3D12_FENCE_FLAG_NONE,
                TWindow.Windows.__uuidof<D3D12.ID3D12Fence>(),
                (void**) d3D12Fence);
            SilkMarshal.ThrowHResult(hr);
        }

        // Create the command allocator to use
        fixed (D3D12.ID3D12CommandAllocator** d3D12CommandAllocator = _d3D12CommandAllocator)
        {
            hr = _d3D12Device.Get()->CreateCommandAllocator(
                D3D12.D3D12_COMMAND_LIST_TYPE.D3D12_COMMAND_LIST_TYPE_DIRECT,
                TWindow.Windows.__uuidof<D3D12.ID3D12CommandAllocator>(),
                (void**) d3D12CommandAllocator);
            SilkMarshal.ThrowHResult(hr);
        }

        // Create the reusable command list to copy data to the back buffers
        fixed (D3D12.ID3D12GraphicsCommandList** d3D12GraphicsCommandList = _d3D12GraphicsCommandList)
        {
            hr = _d3D12Device.Get()->CreateCommandList(
                0,
                D3D12.D3D12_COMMAND_LIST_TYPE.D3D12_COMMAND_LIST_TYPE_DIRECT,
                _d3D12CommandAllocator,
                null,
                TWindow.Windows.__uuidof<D3D12.ID3D12GraphicsCommandList>(),
                (void**) d3D12GraphicsCommandList);
            SilkMarshal.ThrowHResult(hr);
        }

        // Close the command list to prepare it for future use
        hr = _d3D12GraphicsCommandList.Get()->Close();
        SilkMarshal.ThrowHResult(hr);
    }

    private void OnReSize()
    {
        _imageSize = Utils.WpfSizeToPixels(ImageGrid);
        _imageSize = new Size(_imageSize.Width * ResolutionScale, _imageSize.Height * ResolutionScale);
        _isResizePending = true;
    }

    private void ApplyReSize()
    {
        CreateShardResource();
        CreateD3D12Resource();
    }

    private void CreateShardResource()
    {
        void* sharedHandle = null;
        D3D9.IDirect3DTexture9* direct3DTexture9 = default;
        var hr = _direct3DDevice9Ex.Handle->CreateTexture((uint) _imageSize.Width, (uint) _imageSize.Height, 1,
            D3D9.D3D9.UsageRendertarget,
            D3D9.Format.A8R8G8B8, // 这是必须要求的颜色，不能使用其他颜色
            D3D9.Pool.Default,
            &direct3DTexture9,
            ref sharedHandle
        );
        SilkMarshal.ThrowHResult(hr);


        D3D9.IDirect3DSurface9* direct3DSurface9 = default;
        hr = direct3DTexture9->GetSurfaceLevel(0, &direct3DSurface9);
        SilkMarshal.ThrowHResult(hr);

        fixed (D3D12.ID3D12Resource** shardD3D12Resource = _shardD3D12Resource)
        {
            hr = _d3D12Device.Get()->OpenSharedHandle(new TWindow.HANDLE(sharedHandle), TWindow.Windows.__uuidof<D3D12.ID3D12Resource>(),
                (void**) shardD3D12Resource);
            SilkMarshal.ThrowHResult(hr);
        }

        try
        {
            InteropD3DImage.Lock();
            InteropD3DImage.SetBackBuffer(D3DResourceType.IDirect3DSurface9, new IntPtr(direct3DSurface9));
        }
        finally
        {
            InteropD3DImage.Unlock();
        }
    }

    private void CreateD3D12Resource()
    {
        _texture?.Dispose();

        _texture = GraphicsDevice.GetDefault().AllocateReadWriteTexture2D<Rgba32, float4>((int) _imageSize.Width, (int) _imageSize.Height);

        fixed (D3D12.ID3D12Resource** d3D12Resource = _d3D12Resource)
        {
            // Get the underlying ID3D12Resource pointer for the texture
            var hr = InteropServices.TryGetID3D12Resource(_texture, TWindow.Windows.__uuidof<D3D12.ID3D12Resource>(), (void**) d3D12Resource);
            SilkMarshal.ThrowHResult(hr);
        }
    }

    private void Update()
    {
        if (IsPaused)
        {
            if (_startStopwatch.IsRunning)
            {
                _startStopwatch.Stop();
            }

            return;
        }

        if (!_startStopwatch.IsRunning)
        {
            _startStopwatch.Start();
        }

        if (!_frameStopwatch.IsRunning)
        {
            _frameStopwatch.Start();
        }

        if (IsFixedFps && _frameStopwatch.ElapsedTicks < _fixedFrameRenderTime.Ticks)
        {
            return;
        }

        _frameStopwatch.Restart();
        try
        {
            InteropD3DImage.Lock();
            RunShader(_startStopwatch.Elapsed);
            InteropD3DImage.AddDirtyRect(new Int32Rect(0, 0, InteropD3DImage.PixelWidth, InteropD3DImage.PixelHeight));
        }
        finally
        {
            InteropD3DImage.Unlock();
        }

        if (IsShowFps)
        {
            CalculateFps();
        }
    }

    private void RunShader(TimeSpan time)
    {
        if (ShaderRunner == null)
        {
            return;
        }

        if (_isResizePending)
        {
            ApplyReSize();
            _isResizePending = false;
        }

        if (_actualRenderTime == time)
        {
            return;
        }

        ShaderRunner.TryExecute(_texture!, time);
        _actualRenderTime = time;

        // Reset the command list and command allocator
        _d3D12CommandAllocator.Get()->Reset();
        _d3D12GraphicsCommandList.Get()->Reset(_d3D12CommandAllocator.Get(), null);

        var d3D12ResourceBarriers = stackalloc D3D12.D3D12_RESOURCE_BARRIER[]
        {
            D3D12.D3D12_RESOURCE_BARRIER.InitTransition(
                _d3D12Resource.Get(),
                D3D12.D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_UNORDERED_ACCESS,
                D3D12.D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_COPY_SOURCE),
            D3D12.D3D12_RESOURCE_BARRIER.InitTransition(
                _shardD3D12Resource.Get(),
                D3D12.D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_COMMON,
                D3D12.D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_COPY_DEST)
        };

        // Transition the resources to COPY_DEST and COPY_SOURCE respectively
        _d3D12GraphicsCommandList.Get()->ResourceBarrier(2, d3D12ResourceBarriers);

        // Copy the generated frame to the target back buffer
        _d3D12GraphicsCommandList.Get()->CopyResource(_shardD3D12Resource.Get(), _d3D12Resource.Get());

        d3D12ResourceBarriers[0] = D3D12.D3D12_RESOURCE_BARRIER.InitTransition(
            _d3D12Resource.Get(),
            D3D12.D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_COPY_SOURCE,
            D3D12.D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_UNORDERED_ACCESS);

        d3D12ResourceBarriers[1] = D3D12.D3D12_RESOURCE_BARRIER.InitTransition(
            _shardD3D12Resource.Get(),
            D3D12.D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_COPY_DEST,
            D3D12.D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_COMMON);

        // Transition the resources back to COMMON and UNORDERED_ACCESS respectively
        _d3D12GraphicsCommandList.Get()->ResourceBarrier(2, d3D12ResourceBarriers);

        _d3D12GraphicsCommandList.Get()->Close();

        // Execute the command list to perform the copy
        _d3D12CommandQueue.Get()->ExecuteCommandLists(1, (D3D12.ID3D12CommandList**) _d3D12GraphicsCommandList.GetAddressOf());
        _d3D12CommandQueue.Get()->Signal(_d3D12Fence.Get(), _nextD3D12FenceValue);

        if (_nextD3D12FenceValue > _d3D12Fence.Get()->GetCompletedValue())
        {
            _d3D12Fence.Get()->SetEventOnCompletion(_nextD3D12FenceValue, default);
        }

        _nextD3D12FenceValue++;

    }

    private void CalculateFps()
    {
        _frameCount++;
        if (_startStopwatch.ElapsedTicks - _lastFpsRenderTime.Ticks < TimeSpan.TicksPerSecond)
        {
            return;
        }

        _lastFpsRenderTime = _startStopwatch.Elapsed;
        _fps = _frameCount;
        FpsLabel.Content = _fps;
        _frameCount = 0;
    }

    public void Dispose()
    {
        CompositionTarget.Rendering -= CompositionTarget_Rendering;
        _d3D12Device.Dispose();
        _d3D12CommandAllocator.Dispose();
        _d3D12GraphicsCommandList.Dispose();
        _d3D12CommandQueue.Dispose();
        _d3D12Fence.Dispose();
        _texture?.Dispose();
        _d3D12Resource.Dispose();
        _shardD3D12Resource.Dispose();

        _direct3D9Ex.Dispose();
        _direct3DDevice9Ex.Dispose();

        ShaderRunner = null;
    }

    [DllImport("user32.dll", SetLastError = false)]
    public static extern IntPtr GetDesktopWindow();
}