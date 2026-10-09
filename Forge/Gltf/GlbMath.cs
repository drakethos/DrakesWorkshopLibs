// Copied from Drakes Asset Forge (Forge/Format/Glb/GlbMath.cs) so mods get .glb models from Libs alone; keep in step with it.
using System;

namespace DrakeModsLibs.Forge.Gltf;

/// <summary>
/// A 4x4 transform for the glTF reader: row-major, row vectors (v' = v * M), so a child's matrix is applied before its
/// parent's: child * parent. Written here so Format needs no maths library (it's compiled into net481 runtime too).
/// </summary>
internal readonly struct GlbMat4
{
    private readonly float[] _m;

    private GlbMat4(float[] m) => _m = m;

    public static GlbMat4 Identity { get; } = new GlbMat4(new float[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 });

    /// <summary>From 16 numbers in row-major order.</summary>
    public static GlbMat4 FromRows(float[] values) => new GlbMat4((float[])values.Clone());

    /// <summary>Scale, then rotate, then translate (the glTF TRS order).</summary>
    public static GlbMat4 Trs(float sx, float sy, float sz, float qx, float qy, float qz, float qw, float tx, float ty, float tz)
    {
        // Rotation from a unit quaternion, in row-vector form.
        var xx = qx * qx; var yy = qy * qy; var zz = qz * qz;
        var xy = qx * qy; var xz = qx * qz; var yz = qy * qz;
        var wx = qw * qx; var wy = qw * qy; var wz = qw * qz;
        var rotation = new float[]
        {
            1 - 2 * (yy + zz), 2 * (xy + wz), 2 * (xz - wy), 0,
            2 * (xy - wz), 1 - 2 * (xx + zz), 2 * (yz + wx), 0,
            2 * (xz + wy), 2 * (yz - wx), 1 - 2 * (xx + yy), 0,
            0, 0, 0, 1
        };
        var scale = new float[] { sx, 0, 0, 0, 0, sy, 0, 0, 0, 0, sz, 0, 0, 0, 0, 1 };
        var translate = new float[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, tx, ty, tz, 1 };
        return new GlbMat4(scale).Mul(new GlbMat4(rotation)).Mul(new GlbMat4(translate));
    }

    /// <summary>this * other: applies this first, then other (row vectors).</summary>
    public GlbMat4 Mul(GlbMat4 other)
    {
        var r = new float[16];
        for (var i = 0; i < 4; i++)
            for (var j = 0; j < 4; j++)
            {
                float sum = 0;
                for (var k = 0; k < 4; k++)
                    sum += _m[i * 4 + k] * other._m[k * 4 + j];
                r[i * 4 + j] = sum;
            }
        return new GlbMat4(r);
    }

    public float Determinant()
    {
        var m = _m;
        return m[0] * (m[5] * (m[10] * m[15] - m[11] * m[14]) - m[6] * (m[9] * m[15] - m[11] * m[13]) + m[7] * (m[9] * m[14] - m[10] * m[13]))
             - m[1] * (m[4] * (m[10] * m[15] - m[11] * m[14]) - m[6] * (m[8] * m[15] - m[11] * m[12]) + m[7] * (m[8] * m[14] - m[10] * m[12]))
             + m[2] * (m[4] * (m[9] * m[15] - m[11] * m[13]) - m[5] * (m[8] * m[15] - m[11] * m[12]) + m[7] * (m[8] * m[13] - m[9] * m[12]))
             - m[3] * (m[4] * (m[9] * m[14] - m[10] * m[13]) - m[5] * (m[8] * m[14] - m[10] * m[12]) + m[6] * (m[8] * m[13] - m[9] * m[12]));
    }

    /// <summary>Inverse, or the identity if the matrix is singular (a flattened node can't be inverted).</summary>
    public GlbMat4 Inverse()
    {
        // Gauss-Jordan on [M | I].
        var a = new double[4, 8];
        for (var i = 0; i < 4; i++)
            for (var j = 0; j < 4; j++)
            {
                a[i, j] = _m[i * 4 + j];
                a[i, j + 4] = i == j ? 1 : 0;
            }

        for (var col = 0; col < 4; col++)
        {
            var pivot = col;
            for (var row = col + 1; row < 4; row++)
                if (Math.Abs(a[row, col]) > Math.Abs(a[pivot, col]))
                    pivot = row;
            if (Math.Abs(a[pivot, col]) < 1e-12)
                return Identity;
            for (var j = 0; j < 8; j++)
                (a[col, j], a[pivot, j]) = (a[pivot, j], a[col, j]);

            var div = a[col, col];
            for (var j = 0; j < 8; j++)
                a[col, j] /= div;
            for (var row = 0; row < 4; row++)
            {
                if (row == col)
                    continue;
                var factor = a[row, col];
                for (var j = 0; j < 8; j++)
                    a[row, j] -= factor * a[col, j];
            }
        }

        var r = new float[16];
        for (var i = 0; i < 4; i++)
            for (var j = 0; j < 4; j++)
                r[i * 4 + j] = (float)a[i, j + 4];
        return new GlbMat4(r);
    }

    /// <summary>Position: applies translation as well.</summary>
    public (float x, float y, float z) Point(float x, float y, float z) => (
        x * _m[0] + y * _m[4] + z * _m[8] + _m[12],
        x * _m[1] + y * _m[5] + z * _m[9] + _m[13],
        x * _m[2] + y * _m[6] + z * _m[10] + _m[14]);

    /// <summary>Direction: rotation and scale only. Multiply by the transpose of the inverse for normals.</summary>
    public (float x, float y, float z) Direction(float x, float y, float z) => (
        x * _m[0] + y * _m[4] + z * _m[8],
        x * _m[1] + y * _m[5] + z * _m[9],
        x * _m[2] + y * _m[6] + z * _m[10]);

    /// <summary>The transpose of this matrix's inverse, as a direction transform for normals.</summary>
    public GlbMat4 NormalMatrix()
    {
        var inv = Inverse()._m;
        var r = new float[16];
        for (var i = 0; i < 4; i++)
            for (var j = 0; j < 4; j++)
                r[i * 4 + j] = inv[j * 4 + i];
        return new GlbMat4(r);
    }
}
