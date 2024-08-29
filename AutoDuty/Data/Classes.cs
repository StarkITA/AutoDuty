﻿using System;
using System.Collections.Generic;

namespace AutoDuty.Data
{
    public class Classes
    {
        public class Message
        {
            public string Sender { get; set; } = string.Empty;
            public List<Action> Action { get; set; } = [];
        }
    }
}
