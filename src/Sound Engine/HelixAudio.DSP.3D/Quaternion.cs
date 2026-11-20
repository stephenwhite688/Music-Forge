using System.Runtime.CompilerServices;

namespace HelixAudio.DSP._3D;

public struct Quaternion
{
    public float X, Y, Z, W;

    public Quaternion(float x, float y, float z, float w) { X = x; Y = y; Z = z; W = w; }

    public static Quaternion Identity => new Quaternion(0, 0, 0, 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quaternion FromAxisAngle(Vector3 axis, float angle)
    {
        float halfAngle = angle * 0.5f;
        float sin = MathF.Sin(halfAngle);
        float cos = MathF.Cos(halfAngle);
        axis = axis.Normalized();
        return new Quaternion(axis.X * sin, axis.Y * sin, axis.Z * sin, cos);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3 Rotate(Vector3 vector)
    {
        float qx = X, qy = Y, qz = Z, qw = W;
        float vx = vector.X, vy = vector.Y, vz = vector.Z;
        
        float ix = qw * vx + qy * vz - qz * vy;
        float iy = qw * vy + qz * vx - qx * vz;
        float iz = qw * vz + qx * vy - qy * vx;
        float iw = -qx * vx - qy * vy - qz * vz;
        
        return new Vector3(
            ix * qw + iw * -qx + iy * -qz - iz * -qy,
            iy * qw + iw * -qy + iz * -qx - ix * -qz,
            iz * qw + iw * -qz + ix * -qy - iy * -qx
        );
    }

    public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2}, {W:F2})";
}
