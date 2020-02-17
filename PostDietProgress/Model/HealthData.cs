using System;
using System.Collections.Generic;
using System.Linq;

namespace PostDietProgress.Model
{
    public class HealthData
    {
        public string DateTime { get; set; }

        /* 体重 (kg) */
        public string Weight { get; set; }

        /* 体脂肪率(%) */
        public string BodyFatPerf { get; set; }

        /* 筋肉量(kg) */
        public string MuscleMass { get; set; }

        /* 筋肉スコア */
        public string MuscleScore { get; set; }

        /* 内臓脂肪レベル2(小数点有り、手入力含まず) */
        public string VisceralFatLevel2 { get; set; }

        /* 内臓脂肪レベル(小数点無し、手入力含む) */
        public string VisceralFatLevel { get; set; }

        /* 基礎代謝量(kcal) */
        public string BasalMetabolism { get; set; }

        /* 体内年齢(歳) */
        public string BodyAge { get; set; }

        /* 推定骨量(kg) */
        public string BoneQuantity { get; set; }

        public HealthData() { }

        public HealthData(string dateTime, Dictionary<string, string> dic)
        {
            DateTime = dateTime;

            foreach (var enumVal in dic.Select(item => (HealthTag)Enum.ToObject(typeof(HealthTag), int.Parse(item.Key))))
            {
                switch (enumVal)
                {
                    case HealthTag.WEIGHT:
                        Weight = dic[((int)HealthTag.WEIGHT).ToString()];
                        break;
                    case HealthTag.BODYFATPERF:
                        BodyFatPerf = dic[((int)HealthTag.BODYFATPERF).ToString()];
                        break;
                    case HealthTag.MUSCLEMASS:
                        MuscleMass = dic[((int)HealthTag.MUSCLEMASS).ToString()];
                        break;
                    case HealthTag.MUSCLESCORE:
                        MuscleScore = dic[((int)HealthTag.MUSCLESCORE).ToString()];
                        break;
                    case HealthTag.VISCERALFATLEVEL2:
                        VisceralFatLevel2 = dic[((int)HealthTag.VISCERALFATLEVEL2).ToString()];
                        break;
                    case HealthTag.VISCERALFATLEVEL:
                        VisceralFatLevel = dic[((int)HealthTag.VISCERALFATLEVEL).ToString()];
                        break;
                    case HealthTag.BASALMETABOLISM:
                        BasalMetabolism = dic[((int)HealthTag.BASALMETABOLISM).ToString()];
                        break;
                    case HealthTag.BODYAGE:
                        BodyAge = dic[((int)HealthTag.BODYAGE).ToString()];
                        break;
                    case HealthTag.BONEQUANTITY:
                        BoneQuantity = dic[((int)HealthTag.BONEQUANTITY).ToString()];
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
