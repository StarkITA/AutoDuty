﻿
using System.Collections.Generic;

namespace AutoDuty.Data
{
    public class Classes
    {
        public class Message
        {
            public string Sender { get; set; } = string.Empty;
            public List<(string,string)> Action { get; set; } = [];
        }

        public class Content
        {
            public uint Id { get; set; }
            public string? Name { get; set; }
            public string? EnglishName { get; set; }
            public uint TerritoryType { get; set; }
            public uint ExVersion { get; set; }
            public byte ClassJobLevelRequired { get; set; }
            public uint ItemLevelRequired { get; set; }
            public bool DawnContent { get; set; } = false;
            public int DawnIndex { get; set; } = -1;
            public uint ContentFinderCondition { get; set; }
            public uint ContentType { get; set; }
            public uint ContentMemberType { get; set; }
            public bool TrustContent { get; set; } = false;
            public int TrustIndex { get; set; } = -1;
            public bool VariantContent { get; set; } = false;
            public int VVDIndex { get; set; } = -1;
            public bool GCArmyContent { get; set; } = false;
            public int GCArmyIndex { get; set; } = -1;
            public List<TrustMember> TrustMembers { get; set; } = [];
        }

        public class TrustMember
        {
            public uint Index { get; set; }
            public TrustRole Role { get; set; } // 0 = DPS, 1 = Healer, 2 = Tank, 3 = G'raha All Rounder
            public Lumina.Excel.GeneratedSheets.ClassJob? Job { get; set; } = null;//closest actual job that applies. G'raha gets Blackmage
            public string Name { get; set; } = string.Empty;
            public TrustMemberName MemberName { get; set; }

            public uint Level { get; set; }
            public uint LevelCap { get; set; }
            public uint LevelInit { get; set; }
            public bool LevelIsSet { get; set; }

            public void ResetLevel()
            {
                LevelIsSet = false;
                Level = LevelInit;
            }

            public void SetLevel(uint level)
            {
                if (level >= LevelInit)
                {
                    LevelIsSet = true;
                    Level = level;
                }
            }
        }
    }
}
