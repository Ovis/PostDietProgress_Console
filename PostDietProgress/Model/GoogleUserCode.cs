namespace PostDietProgress.Model
{
    public class GoogleUserCode
    {
        public string DeviceCode { get; set; }

        public string UserCode { get; set; }

        public string VerificationUrl { get; set; }

        public int ExpiresIn { get; set; }

        public int Interval { get; set; }
    }
}
