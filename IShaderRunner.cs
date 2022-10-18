using System;
using ComputeSharp;

namespace YQCool.ComputeSharp.Wpf;

/// <summary>
///     An interface for a shader runner to be used with <see cref="ComputeShaderD3DImage" />
/// </summary>
public interface IShaderRunner
{
    /// <summary>
    ///     Tries to render a single frame to a texture, optionally skipping the frame if needed.
    /// </summary>
    /// <param name="texture">The target texture to render the frame to.</param>
    /// <param name="timespan">The timespan for the current frame.</param>
    /// <param name="parameter">The input parameter for the frame being rendered.</param>
    /// <returns>Whether or not to present the current frame. If <see langword="false" />, the frame will be skipped.</returns>
    /// <remarks>
    ///     Any exceptions thrown by the runner will result in <see cref="ComputeShaderD3DImage.RenderingFailed" />
    /// </remarks>
    bool TryExecute(IReadWriteNormalizedTexture2D<float4> texture, TimeSpan timespan, object? parameter = default);
}