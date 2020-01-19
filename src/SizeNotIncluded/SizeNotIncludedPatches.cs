﻿using Harmony;
using System.Collections.Generic;
using System;
using System.Reflection;
using System.Linq;

namespace SizeNotIncluded
{
    public class SizeNotIncludedPatches
    {
        public static float chunkSize = 16f;
        public static float defaultX = 256f;
        public static float defaultY = 384f;
        public static float xCount = 4f;
        public static float yCount = 6;
        public static float xAmount = xCount * chunkSize;
        public static float yAmount = yCount * chunkSize;
        public static float xscale = defaultX / xAmount;
        public static float yscale = defaultY / yAmount;
        public static float maxDensity = 2.5f;

        [HarmonyPatch(typeof(ProcGen.Worlds), "UpdateWorldCache")]
        public static class Worlds_UpdateWorldCache_Patch
        {
            private static void Postfix(ProcGen.Worlds __instance)
            {
                foreach(ProcGen.World world in __instance.worldCache.Values)
                {
                    Traverse.Create(world).Property("worldsize").SetValue(
                        new Vector2I((int) xAmount, (int) yAmount));
                }
            }
        }

        [HarmonyPatch(typeof(ProcGen.WorldGenSettings), nameof(ProcGen.WorldGenSettings.GetFloatSetting))]
        public static class WorldGenSettings_GetFloatSetting_Patch
        {
            private static bool Prefix(string target, ref float __result)
            {
                var densityModifier = xscale * yscale;
                Console.WriteLine($"Density modifier ended up being {densityModifier}");
                if (densityModifier > maxDensity)
                {
                    densityModifier = maxDensity;
                }
                if (target.Equals("OverworldDensityMin"))
                {
                    __result = 600.0f / densityModifier;
                    return false;
                }
                else if (target.Equals("OverworldDensityMax"))
                {
                    __result = 700.0f / densityModifier;
                    return false;
                }
                else if (target.Equals("OverworldAvoidRadius"))
                {
                    __result = 72.0f / densityModifier;
                    return false;
                }
                return true;
            }
        }

        public static float maxLiquidModifier = 2.0f;

        [HarmonyPatch(typeof(ProcGen.MutatedWorldData), "ApplyTraits")]
        public static class MutatedWorldData_ApplyTraits_Patch
        {
            private static void Postfix(ProcGen.MutatedWorldData __instance)
            {
                var densityModifier = xscale * yscale;
                foreach (KeyValuePair<string, ElementBandConfiguration> bandConfiguration in __instance.biomes.BiomeBackgroundElementBandConfigurations)
                {
                    foreach (ElementGradient elementGradient in bandConfiguration.Value)
                    {
                        ProcGen.WorldTrait.ElementBandModifier modifier = new ProcGen.WorldTrait.ElementBandModifier();
                        Traverse.Create(modifier).Property("element").SetValue(elementGradient.content);
                        var element = ElementLoader.FindElementByName(elementGradient.content);
                        if (element != null && element.id == SimHashes.Magma)
                        {
                            // no modifier for magma. breaks the bottom of the world
                            Traverse.Create(modifier).Property("massMultiplier").SetValue(1f);
                        }
                        else if (element != null && element.IsLiquid)
                        {
                            // give the player at most 2x liquids otherwise they really break out of the pockets they spawn in
                            Traverse.Create(modifier).Property("massMultiplier").SetValue((float) Math.Min(maxLiquidModifier, densityModifier));
                        }
                        else
                        {
                            // everything else - gasses and solid tiles should just use whatever density modifier the game has
                            Traverse.Create(modifier).Property("massMultiplier").SetValue((float) densityModifier);

                        }
                        elementGradient.Mod(modifier);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(ProcGenGame.Border), "ConvertToMap")]
        public static class Border_ConvertToMap_Patch
        {
            private static void Prefix(ProcGenGame.Border __instance)
            {
                // smaller abyssalite border so it doesn't take up most of the map
                __instance.width = 0.5f;
            }
        }

        [HarmonyPatch(typeof(ProcGen.SettingsCache), "LoadWorldTraits")]
        public static class SettingsCache_LoadWorldTraits_Patch
        {
            private static void Postfix()
            {
                var traits = Traverse.Create(typeof(ProcGen.SettingsCache)).Field("traits").GetValue<Dictionary<string, ProcGen.WorldTrait>>();
                var lessTraits = traits.Where(pair => !pair.Value.filePath.Contains("BouldersLarge")
                    && !pair.Value.filePath.Contains("BouldersMedium")
                    && !pair.Value.filePath.Contains("BouldersMixed")
                    && !pair.Value.filePath.Contains("GlaciersLarge")).ToDictionary(pair => pair.Key, pair => pair.Value);
                // remove traits that are too big for small worlds
                Traverse.Create(typeof(ProcGen.SettingsCache)).Field("traits").SetValue(lessTraits);
            }
        }

        [HarmonyPatch(typeof(ProcGen.MobSettings), "GetMob")]
        public static class MobSettings_GetMob_Patch
        {
            // need to keep list of defined in case something is used multiple times
            // otherwise each time it appeared we would up the density which would be ridiculous
            private static HashSet<string> patched = new HashSet<string>();

            private static void Postfix(string id, ref ProcGen.Mob __result)
            {
                if (__result != null)
                {
                    var name = __result.name;
                    var prefabName = __result.prefabName;
                    if (prefabName == null)
                    {
                        prefabName = name;
                    }
                    if (name != null && !patched.Contains(name) )//&& critters.Contains(prefabName))
                    {
                        var densityModifier = xscale * yscale;
                        // surface biome gets mostly deleted. I don't know why. So just give the player lots of voles to compensate.
                        if (name.Equals("Mole") || prefabName.Equals("Mole"))
                        {
                            densityModifier *= 4.0f;
                        }
                        patched.Add(name);
                        typeof(ProcGen.SampleDescriber).GetProperty("density", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                            .SetValue(__result, new ProcGen.MinMax(__result.density.min * densityModifier, __result.density.max * densityModifier), null);
                    }
                }
            }
        }
    }
}
