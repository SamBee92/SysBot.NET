﻿using System.ComponentModel;
using PKHeX.Core;

namespace SysBot.Pokemon
{
    public class RaidSettings
    {
        private const string FeatureToggle = nameof(FeatureToggle);
        private const string Hosting = nameof(Hosting);
        public override string ToString() => "Raid Bot Settings";

        [Category(FeatureToggle), Description("When set, the bot will assume that ldn_mitm sysmodule is running on your system. Better stability")]
        public bool UseLdnMitm { get; set; } = true;

        [Category(FeatureToggle), Description("When set, the bot will roll species and set date to 2000, resetting it once it reaches 2060.")]
        public bool AutoRoll { get; set; } = true;

        [Category(FeatureToggle), Description("When set, the bot will remove then add 5 friends every 2 raids.")]
        public bool FriendManagement { get; set; } = true;

        [Category(Hosting), Description("Minimum Link Code to host the raid with.")]
        public int MinTradeCode { get; set; } = 8180;

        [Category(Hosting), Description("Maximum Link Code to host the raid with.")]
        public int MaxTradeCode { get; set; } = 8199;

        /// <summary>
        /// Gets a random trade code based on the range settings.
        /// </summary>
        public int GetRandomRaidCode() => Util.Rand.Next(MinTradeCode, MaxTradeCode + 1);
    }
}