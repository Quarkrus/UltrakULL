using HarmonyLib;
using System;
using UnityEngine.InputSystem;

namespace UltrakULL.Harmony_Patches
{
    [HarmonyPatch]
    internal class InputBindManager
    {
        [HarmonyPatch(typeof(InputBinding), nameof(InputBinding.ToDisplayString),
            new Type[] { typeof(InputBinding.DisplayStringOptions), typeof(InputControl) })]
        [HarmonyPostfix]
        static void ToDisplayString_Postfix(ref string __result)
        {
            if (string.IsNullOrEmpty(__result))
                return;

            __result = CommonFunctions.GetLocalizedInput(__result);
        }
    }
}