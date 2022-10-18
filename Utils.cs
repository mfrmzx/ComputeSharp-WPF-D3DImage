using System.Windows;

namespace YQCool.ComputeSharp.Wpf;

internal class Utils
{
    public static Size WpfSizeToPixels(FrameworkElement element)
    {
        var source = PresentationSource.FromVisual(element);

        if (source?.CompositionTarget == null)
        {
            return Size.Empty;
        }

        var transformToDevice = source.CompositionTarget.TransformToDevice;

        return (Size) transformToDevice.Transform(new Vector(element.ActualWidth, element.ActualHeight));
    }
}