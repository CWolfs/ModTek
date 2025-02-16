﻿using System.Collections.Generic;
using ModTek.Features.SoundBanks;

// ReSharper disable once CheckNamespace
namespace ModTek
{
    public static class SoundBanksProcessHelper
    {
        private static Dictionary<string, ProcessParameters> procParams = new Dictionary<string, ProcessParameters>();

        public static void RegisterProcessParams(string soundbank, string param1, string param2)
        {
            if (procParams.ContainsKey(soundbank))
            {
                procParams[soundbank] = new ProcessParameters(param1, param2);
            }
            else
            {
                procParams.Add(soundbank, new ProcessParameters(param1, param2));
            }
        }

        internal static ProcessParameters GetRegisteredProcParams(string soundbank)
        {
            return procParams.TryGetValue(soundbank, out var p) ? p : null;
        }
    }
}
