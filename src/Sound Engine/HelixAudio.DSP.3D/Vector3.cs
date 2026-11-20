using System.Runtime.CompilerServices;

namespace HelixAudio.DSP._3D;

public struct Vector3
{
    public float X, Y, Z;

    public Vector3(float x, float y, float z) { X = x; Y = y; Z = z; }

    public static Vector3 Zero => new Vector3(0, 0, 0);
    public static Vector3 One => new Vector3(1, 1, 1);
    public static Vector3 Forward => new Vector3(0, 0, 1);
    public static Vector3 Backward => new Vector3(0, 0, -1);
    public static Vector3 Up => new Vector3(0, 1, 0);
    public static Vector3 Down => new Vector3(0, -1, 0);
    public static Vector3 Right => new Vector3(1, 0, 0);
    public static Vector3 Left => new Vector3(-1, 0, 0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 operator *(Vector3 a, float scalar) => new Vector3(a.X * scalar, a.Y * scalar, a.Z * scalar);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 operator /(Vector3 a, float scalar) => new Vector3(a.X / scalar, a.Y / scalar, a.Z / scalar);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Length() => MathF.Sqrt(X * X + Y * Y + Z * Z);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float LengthSquared() => X * X + Y * Y + Z * Z;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3 Normalized()
    {
        float len = Length();
        return len > 0 ? this / len : Zero;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Dot(Vector3 a, Vector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 Cross(Vector3 a, Vector3 b) => new Vector3(
        a.Y * b.Z - a.Z * b.Y,
        a.Z * b.X - a.X * b.Z,
        a.X * b.Y - a.Y * b.X
    );

    public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";
}
