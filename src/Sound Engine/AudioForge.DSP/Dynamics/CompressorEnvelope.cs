namespace AudioForge.DSP.Dynamics
{
    public class CompressorEnvelope
    {
        private float env;

        public float Process(float input, float attack, float release)
        {
            float coeff = input > env ? attack : release;
            env = coeff * env + (1 - coeff) * input;
            return env;
        }
    }
}
