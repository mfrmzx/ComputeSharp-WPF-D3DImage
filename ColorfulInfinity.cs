﻿using ComputeSharp;

namespace YQCool.ComputeSharp.Wpf;

/// <summary>
///     A shader creating an abstract and colorful animation.
///     Ported from <see href="https://www.shadertoy.com/view/WtjyzR" />.
///     <para>Created by Benoit Marini.</para>
///     <para>License Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.</para>
/// </summary>
[AutoConstructor]
[EmbeddedBytecode(DispatchAxis.XY)]
internal partial struct ColorfulInfinity : IPixelShader<float4>
{
    /// <summary>
    ///     The current time since the start of the application.
    /// </summary>
    public float time;

    /// <summary>
    ///     The total number of layers for the final animation.
    /// </summary>
    private const float NumberOfLayers = 16.0f;

    /// <summary>
    ///     The number of iterations to calculate a texel value.
    /// </summary>
    private const int NumberOfIterations = 23;

    /// <summary>
    ///     Makes some magic happen.
    /// </summary>
    private float4 Tex(float3 p)
    {
        var t = time + 78.0f;
        float4 o = new(p.X, p.Y, p.Z, 3.0f * Hlsl.Sin(t * 0.1f));
        var dec =
            new float4(1.0f, 0.9f, 0.1f, 0.15f) +
            new float4(0.06f * Hlsl.Cos(t * 0.1f), 0, 0, 0.14f * Hlsl.Cos(t * 0.23f));

        for (var i = 0; i++ < NumberOfIterations;)
        {
            o.XZYW = Hlsl.Abs(o / Hlsl.Dot(o, o) - dec);
        }

        return o;
    }

    /// <inheritdoc />
    public float4 Execute()
    {
        var uv = (ThreadIds.XY - (float2) DispatchSize.XY * 0.5f) / DispatchSize.Y;
        float3 col = 0;
        var t = time * 0.3f;

        for (var i = 0.0f; i <= 1.0f; i += 1.0f / NumberOfLayers)
        {
            var d = Hlsl.Frac(i + t);
            var s = Hlsl.Lerp(5.0f, 0.5f, d);
            var f = d * Hlsl.SmoothStep(1.0f, 0.9f, d);

            col += Tex(new float3(uv.X * s, uv.Y * s, i * 4.0f)).XYZ * f;
        }

        col /= NumberOfLayers;
        col *= new float3(2, 1.0f, 2.0f);
        col = Hlsl.Pow(col, 0.5f);

        return new Float4(col.X, col.Y, col.Z, 1.0f);
    }
}