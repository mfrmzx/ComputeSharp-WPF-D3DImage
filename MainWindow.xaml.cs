using System;
using System.Windows;
using YQCool.ComputeSharp.Wpf.Shaders;
using D3D9 = Silk.NET.Direct3D9;
using D3D12 = TerraFX.Interop.DirectX;
using TWindow = TerraFX.Interop.Windows;

namespace YQCool.ComputeSharp.Wpf;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// The mapping of available samples to choose from.
    /// </summary>
    private static readonly IShaderRunner[] Samples =
    {
        new ShaderRunner<HelloWorld>(static time => new((float) time.TotalSeconds)),
        new ShaderRunner<FourColorGradient>(static time => new((float) time.TotalSeconds)),
        new ShaderRunner<ColorfulInfinity>(static time => new((float) time.TotalSeconds)),
        new ShaderRunner<FractalTiling>(static time => new((float) time.TotalSeconds)),
        new ShaderRunner<TwoTiledTruchet>(static time => new((float) time.TotalSeconds)),
        new ShaderRunner<MengerJourney>(static time => new((float) time.TotalSeconds)),
        new ShaderRunner<Octagrams>(static time => new((float) time.TotalSeconds)),
        new ShaderRunner<ProteanClouds>(static time => new((float) time.TotalSeconds)),
        new ShaderRunner<ExtrudedTruchetPattern>(static time => new((float) time.TotalSeconds)),
        new ShaderRunner<PyramidPattern>(static time => new((float) time.TotalSeconds)),
        new ShaderRunner<TriangleGridContouring>(static time => new((float) time.TotalSeconds)),
        new ShaderRunner<TerracedHills>(static time => new((float) time.TotalSeconds))
    };

    private int index;

    public MainWindow()
    {
        InitializeComponent();
        ComputeShaderD3DImage.ShaderRunner = Samples[index];
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        ComputeShaderD3DImage.Dispose();
    }

    private void PauseButton_OnClick(object sender, RoutedEventArgs e)
    {
        ComputeShaderD3DImage.IsPaused = !ComputeShaderD3DImage.IsPaused;
    }

    private void LastButton_OnClick(object sender, RoutedEventArgs e)
    {
        index--;
        if (index < 0)
        {
            index = Samples.Length - 1;
        }
        ComputeShaderD3DImage.ShaderRunner = Samples[index];
    }

    private void NextButton_OnClick(object sender, RoutedEventArgs e)
    {
        index++;
        if (index >= Samples.Length)
        {
            index = 0;
        }
        ComputeShaderD3DImage.ShaderRunner = Samples[index];
    }
}