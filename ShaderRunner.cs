using System;
using CommunityToolkit.Diagnostics;
using ComputeSharp;

namespace YQCool.ComputeSharp.Wpf;

/// <summary>
///     A simple <see cref="IShaderRunner" /> implementation powered by a supplied shader type.
///     All rendered frames produced by this type will always be presented.
/// </summary>
/// <typeparam name="T">The type of shader to use to render frames.</typeparam>
public sealed class ShaderRunner<T> : IShaderRunner
    where T : struct, IPixelShader<Float4>
{
    /// <summary>
    ///     The <see cref="Func{T1,TResult}" /> instance used to create shaders to run.
    /// </summary>
    private readonly Func<TimeSpan, T>? _shaderFactory;

    /// <summary>
    ///     The <see cref="Func{T1,T2,TResult}" /> instance used to create shaders to run.
    /// </summary>
    private readonly Func<TimeSpan, object?, T>? _statefulShaderFactory;

    /// <summary>
    ///     Creates a new <see cref="ShaderRunner{T}" /> instance that will create shader instances with
    ///     the default constructor. Only use this constructor if the shader doesn't require any additional
    ///     resources or other parameters being initialized before being dispatched on the GPU.
    /// </summary>
    public ShaderRunner() { }

    /// <summary>
    ///     Creates a new <see cref="ShaderRunner{T}" /> instance.
    /// </summary>
    /// <param name="shaderFactory">The <see cref="Func{T1,TResult}" /> instance used to create shaders to run.</param>
    public ShaderRunner(Func<TimeSpan, T> shaderFactory)
    {
        Guard.IsNotNull(shaderFactory);

        _shaderFactory = shaderFactory;
    }

    /// <summary>
    ///     Creates a new <see cref="ShaderRunner{T}" /> instance.
    /// </summary>
    /// <param name="shaderFactory">The <see cref="Func{T1,T2,TResult}" /> instance used to create shaders to run.</param>
    public ShaderRunner(Func<TimeSpan, object?, T> shaderFactory)
    {
        Guard.IsNotNull(shaderFactory);

        _statefulShaderFactory = shaderFactory;
    }

    /// <inheritdoc />
    public bool TryExecute(IReadWriteNormalizedTexture2D<Float4> texture, TimeSpan time, object? parameter = default)
    {
        texture.GraphicsDevice.ForEach(texture, _shaderFactory?.Invoke(time) ?? _statefulShaderFactory!(time, parameter));

        return true;
    }
}