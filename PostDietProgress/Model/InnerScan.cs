using System.Collections.Generic;

namespace PostDietProgress.Model
{
    public class InnerScan
    {
        public class Health
        {
            public string Date { get; set; }
            public string Keydata { get; set; }
            public string Model { get; set; }
            public string Tag { get; set; }
        }

        public string BirthDate { get; set; }
        public List<Health> Data { get; set; }
        public string Height { get; set; }
        public string Sex { get; set; }
    }
}
