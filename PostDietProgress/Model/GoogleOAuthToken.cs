namespace PostDietProgress.Model
{
    class GoogleOAuthToken
    {
        public string AccessToken { get; set; }

        public string TokenType { get; set; }

        public int ExpiresIn { get; set; }

        public string RefreshToken { get; set; }

        public string Scope { get; set; }

        public string Issued { get; set; }

        public string IssuedUtc { get; set; }
    }
}
