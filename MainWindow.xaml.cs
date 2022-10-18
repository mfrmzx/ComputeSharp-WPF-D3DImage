using System;
using System.Windows;
using D3D9 = Silk.NET.Direct3D9;
using D3D12 = TerraFX.Interop.DirectX;
using TWindow = TerraFX.Interop.Windows;

namespace YQCool.ComputeSharp.Wpf;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ComputeShaderD3DImage.ShaderRunner = new ShaderRunner<ColorfulInfinity>(static time => new ColorfulInfinity((float) time.TotalSeconds));
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        ComputeShaderD3DImage.Dispose();
    }

    private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        ComputeShaderD3DImage.IsPaused = !ComputeShaderD3DImage.IsPaused;
    }
}