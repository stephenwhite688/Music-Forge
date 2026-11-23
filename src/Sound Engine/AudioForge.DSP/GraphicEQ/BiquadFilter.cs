namespace AudioForge.DSP.GraphicEQ
{
    public class BiquadFilter
    {
        private float x1, x2;
        private float y1, y2;

        private BiquadCoefficients c;

        public void SetCoefficients(BiquadCoefficients coeffs)
        {
            c = coeffs;
        }

        public float Process(float x)
        {
            float y = c.b0 * x + c.b1 * x1 + c.b2 * x2
                      - c.a1 * y1 - c.a2 * y2;

            x2 = x1;
            x1 = x;

            y2 = y1;
            y1 = y;

            return y;
        }
    }
}
