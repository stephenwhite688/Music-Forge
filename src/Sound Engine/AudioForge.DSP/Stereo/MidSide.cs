namespace AudioForge.DSP.Stereo
{
    public static class MidSide
    {
        public static void Encode(float left, float right, out float mid, out float side)
        {
            mid = (left + right) * 0.5f;
            side = (left - right) * 0.5f;
        }

        public static void Decode(float mid, float side, out float left, out float right)
        {
            left = mid + side;
            right = mid - side;
        }
    }
}
