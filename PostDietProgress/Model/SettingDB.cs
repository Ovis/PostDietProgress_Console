namespace PostDietProgress.Model
{
    public class SettingDb
    {
        public string Key { get; set; }
        public string Value { get; set; }
    }

    public enum SettingDbEnum
    {
        PreviousWeight,
        PrevWeekWeight,
        PreviousMeasurementDate,
        RequestToken,
        ExpiresIn,
        RefreshToken,
        ErrorFlag
    }
}
