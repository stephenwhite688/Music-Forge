using System;

namespace AudioForge.DSP.Stereo
{
    public class StereoImagerProcessor
    {
        public float Width { get; set; } = 1.0f; // 1.0 = normal, >1 wider, <1 narrower

        public void Process(float[] leftIn, float[] rightIn, float[] leftOut, float[] rightOut)
        {
            for (int i = 0; i < leftIn.Length; i++)
            {
                float L = leftIn[i];
                float R = rightIn[i];

                MidSide.Encode(L, R, out float mid, out float side);

                side *= Width;

                MidSide.Decode(mid, side, out float newL, out float newR);

                leftOut[i] = newL;
                rightOut[i] = newR;
            }
        }
    }
}
