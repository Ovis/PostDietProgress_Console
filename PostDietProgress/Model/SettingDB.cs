﻿using System;
using System.Collections.Generic;
using System.Text;

namespace PostDietProgress
{
    class SettingDB
    {
        public string Key { get; set; }
        public string Value { get; set; }
    }

    public enum SettingDbEnum
    {
        PreviousWeight,
        PreviousMeasurememtDate,
        RequestToken,
        ExpiresIn,
        RefreshToken,
        ErrorFlag
    }
}